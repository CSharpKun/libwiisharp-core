/* This file is part of LibWiiSharpCore
 * Copyright (C) 2009 Leathl
 * Copyright (C) 2020 - 2022 TheShadowEevee, Github Contributors
 * Copyright (C) 2026 CSharpKun
 *
 * LibWiiSharpCore is free software: you can redistribute it and/or
 * modify it under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * LibWiiSharpCore is distributed in the hope that it will be
 * useful, but WITHOUT ANY WARRANTY; without even the implied warranty
 * of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibWiiSharpCore;

[Flags]
public enum StoreType
{
    EncryptedContent = 1 << 0,
    DecryptedContent = 1 << 1,
    WAD = 1 << 2,
    All = EncryptedContent | DecryptedContent | WAD,
}

public sealed class NusClient(HttpClient nusClient, ILogger<NusClient>? logger = null)
{
    private readonly ILogger<NusClient> _logger = logger ?? NullLogger<NusClient>.Instance;

    /// <summary>
    /// If true, existing local files will be used.
    /// </summary>
    public bool UseLocalFiles { get; set; }

    /// <summary>
    /// If true, the download will be continued even if no ticket for the title is avaiable (WAD packaging and decryption are disabled).
    /// </summary>
    public bool ContinueWithoutTicket { get; set; }

    #region Public Functions
    /// <summary>
    /// Grabs a title from NUS, you can define several store types.
    /// Leave the title version empty for the latest.
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="outputDir"></param>
    /// <param name="storeTypes"></param>
    public async Task DownloadTitle(
        string outputDir,
        string titleId,
        string? titleVersion = null,
        StoreType storeType = StoreType.All,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));
        var nusUrl = await PrivNUSUp(token);
        await DownloadTitleViaUrl(outputDir, nusUrl, titleId, titleVersion, storeType, token);
    }

    /// <summary>
    /// Grabs a title from NUS, you can define several store types.
    /// Leave the title version empty for the latest.
    /// nusUrl should be formatted as "http://x.y.z.host.com/ccs/download/"
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="outputDir"></param>
    /// <param name="nusUrl"></param>
    /// <param name="storeTypes"></param>
    public async Task DownloadTitleViaUrl(
        string outputDir,
        string nusUrl,
        string titleId,
        string? titleVersion = null,
        StoreType storeType = StoreType.All,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));

        _logger.LogDebug(
            "Downloading Title {Title} v{Version}...",
            titleId,
            string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
        );
        string str1 = string.Format("{0}{1}/", nusUrl, titleId);

        bool flag1 = false,
            flag2 = false,
            flag3 = false;

        if (storeType.HasFlag(StoreType.EncryptedContent))
        {
            _logger.LogDebug("    -> Storing Encrypted Content...");
            flag1 = true;
        }

        if (storeType.HasFlag(StoreType.DecryptedContent))
        {
            _logger.LogDebug("    -> Storing Decrypted Content...");
            flag2 = true;
        }

        if (storeType.HasFlag(StoreType.WAD))
        {
            _logger.LogDebug("    -> Storing WAD...");
            flag3 = true;
        }

        if (ContinueWithoutTicket == true)
        {
            flag2 = false;
            flag1 = true;
            flag3 = false;
        }
        if (outputDir[^1] != Path.DirectorySeparatorChar)
        {
            outputDir += Path.DirectorySeparatorChar.ToString();
        }

        Directory.CreateDirectory(outputDir);

        string str2 =
            "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion);
        _logger.LogDebug("   Downloading TMD...");
        try
        {
            await DownloadFile(str1 + str2, outputDir + str2, token);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("   Downloading TMD Failed...");
            throw new Exception("Downloading TMD Failed:\n" + ex.Message);
        }

        if (!File.Exists(outputDir + "cetk"))
        {
            //Download cetk
            _logger.LogDebug("   Downloading Ticket...");
            try
            {
                await DownloadFile(
                    string.Format("{0}{1}/", nusUrl, titleId) + "cetk",
                    outputDir + "cetk",
                    token
                );
            }
            catch (Exception ex)
            {
                if (!ContinueWithoutTicket || !flag1)
                {
                    _logger.LogDebug("   Downloading Ticket Failed...");
                    throw new Exception(
                        "CETK Doesn't Exist and Downloading Ticket Failed:\n" + ex.Message
                    );
                }

                flag2 = false;
                flag3 = false;
            }
        }

        _logger.LogDebug("   Parsing TMD...");
        TMD tmd = TMD.Load(outputDir + str2);
        if (string.IsNullOrEmpty(titleVersion))
        {
            _logger.LogDebug("    -> Title Version: {Version}", tmd.TitleVersion);
        }

        _logger.LogDebug("    -> {Count} Contents", tmd.NumOfContents);
        _logger.LogDebug("   Parsing Ticket...");
        Ticket? tik = null;
        if (!ContinueWithoutTicket)
        {
            tik = Ticket.Load(outputDir + "cetk");
        }
        string[] strArray1 = new string[tmd.NumOfContents];
        uint contentId;
        for (int index1 = 0; index1 < tmd.NumOfContents; ++index1)
        {
            _logger.LogDebug(
                "   Downloading Content #{Number} of {All}... ({Size} bytes)",
                index1 + 1,
                tmd.NumOfContents,
                tmd.Contents[index1].Size
            );

            var x8ContentId = tmd.Contents[index1].ContentID.ToString("x8");

            if (UseLocalFiles)
            {
                if (File.Exists(outputDir + x8ContentId))
                {
                    _logger.LogDebug("   Using Local File, Skipping...");
                    continue;
                }
            }

            string address = str1 + x8ContentId;
            string fileName = outputDir + x8ContentId;
            await DownloadFile(address, fileName, token);
            string[] strArray2 = strArray1;
            int index2 = index1;
            contentId = tmd.Contents[index1].ContentID;
            string str7 = contentId.ToString("x8");
            strArray2[index2] = str7;
        }
        if (flag2 | flag3)
        {
            for (int contentIndex = 0; contentIndex < tmd.NumOfContents; ++contentIndex)
            {
                _logger.LogDebug(
                    "   Decrypting Content #{Number} of {All}...",
                    contentIndex + 1,
                    tmd.NumOfContents
                );

                string str3 = outputDir;
                contentId = tmd.Contents[contentIndex].ContentID;
                string str4 = contentId.ToString("x8");

                if (tik is null)
                    throw new NullReferenceException();

                byte[] array = PrivDecryptContent(
                    File.ReadAllBytes(str3 + str4),
                    contentIndex,
                    tik,
                    tmd
                );
                Array.Resize<byte>(ref array, (int)tmd.Contents[contentIndex].Size);
                if (
                    !Shared.CompareByteArrays(SHA1.HashData(array), tmd.Contents[contentIndex].Hash)
                )
                {
                    _logger.LogDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                    throw new Exception(
                        string.Format("Content #{0}: Hashes do not match!", contentIndex)
                    );
                }
                string str5 = outputDir;
                contentId = tmd.Contents[contentIndex].ContentID;
                string str6 = contentId.ToString("x8");
                File.WriteAllBytes(str5 + str6 + ".app", array);
            }
        }
        if (flag3)
        {
            _logger.LogDebug("   Building Certificate Chain...");
            CertificateChain cert = CertificateChain.FromTikTmd(
                outputDir + "cetk",
                outputDir + str2
            );
            byte[][] contents = new byte[tmd.NumOfContents][];
            for (int index1 = 0; index1 < tmd.NumOfContents; ++index1)
            {
                byte[][] numArray1 = contents;
                int index2 = index1;
                string str3 = outputDir;
                contentId = tmd.Contents[index1].ContentID;
                string str4 = contentId.ToString("x8");
                byte[] numArray2 = File.ReadAllBytes(str3 + str4 + ".app");
                numArray1[index2] = numArray2;
            }
            _logger.LogDebug("   Creating WAD...");

            if (tik is null)
                throw new NullReferenceException();

            WAD.Create(cert, tik, tmd, contents)
                .Save(
                    outputDir
                        + tmd.TitleID.ToString("x16")
                        + "v"
                        + tmd.TitleVersion.ToString()
                        + ".wad"
                );
        }
        if (!flag1)
        {
            _logger.LogDebug("   Deleting Encrypted Contents...");
            for (int index = 0; index < strArray1.Length; ++index)
            {
                File.Delete(outputDir + strArray1[index]);
            }
        }
        if (flag3 && !flag2)
        {
            _logger.LogDebug("   Deleting Decrypted Contents...");
            for (int index = 0; index < strArray1.Length; ++index)
            {
                File.Delete(outputDir + strArray1[index] + ".app");
            }
        }
        if (!flag2 && !flag1)
        {
            _logger.LogDebug("   Deleting TMD and Ticket...");
            File.Delete(outputDir + str2);
            if (ContinueWithoutTicket == false)
            {
                File.Delete(outputDir + "cetk");
            }
        }
        _logger.LogDebug(
            "Downloading Title {Title} v{Version} Finished...",
            titleId,
            string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
        );
    }

    /// <summary>
    /// Grabs a TMD from NUS.
    /// Leave the title version empty for the latest.
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <returns></returns>
    public async Task<TMD?> DownloadTMD(
        string titleId,
        string titleVersion,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));
        var nusUrl = await PrivNUSUp(token);
        return await DownloadTMDViaUrl(titleId, titleVersion, nusUrl, token);
    }

    /// <summary>
    /// Grabs a TMD from NUS.
    /// Leave the title version empty for the latest.
    /// nusUrl should be formatted as "http://x.y.z.host.com/ccs/download/"
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="nusUrl"></param>
    /// <returns></returns>
    public async Task<TMD?> DownloadTMDViaUrl(
        string titleId,
        string titleVersion,
        string nusUrl,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));

        return TMD.Load(
            await nusClient.GetByteArrayAsync(
                string.Format("{0}{1}/", nusUrl, titleId)
                    + (
                        "tmd"
                        + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion)
                    ),
                token
            )
        );
    }

    /// <summary>
    /// Grabs a Ticket from NUS.
    /// </summary>
    /// <param name="titleId"></param>
    /// <returns></returns>
    public async Task<Ticket?> DownloadTicket(string titleId, CancellationToken token = default)
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));
        var nusUrl = await PrivNUSUp(token);
        return await DownloadTicketViaUrl(titleId, nusUrl, token);
    }

    /// <summary>
    /// Grabs a Ticket from NUS.
    /// nusUrl should be formatted as "http://x.y.z.host.com/ccs/download/"
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="nusUrl"></param>
    /// <returns></returns>
    public async Task<Ticket?> DownloadTicketViaUrl(
        string titleId,
        string nusUrl,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));

        string titleUrl = string.Format("{0}{1}/", nusUrl, titleId);
        byte[] tikArray = await nusClient.GetByteArrayAsync(titleUrl + "cetk", token);

        return Ticket.Load(tikArray);
    }

    /// <summary>
    /// Grabs a single content file and decrypts it.
    /// Leave the title version empty for the latest.
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="contentId"></param>
    /// <param name="savePath"></param>
    public async Task DownloadAndSaveSingleContent(
        string contentId,
        string savePath,
        string titleId,
        string? titleVersion = null,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));
        var nusUrl = await PrivNUSUp(token);
        await DownloadAndSaveSingleContentViaUrl(
            contentId,
            savePath,
            nusUrl,
            titleId,
            titleVersion,
            token
        );
    }

    /// <summary>
    /// Grabs a single content file and decrypts it.
    /// Leave the title version empty for the latest.
    /// nusUrl should be formatted as "http://x.y.z.host.com/ccs/download/"
    /// </summary>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="contentId"></param>
    /// <param name="savePath"></param>
    /// <param name="nusUrl"></param>
    public async Task DownloadAndSaveSingleContentViaUrl(
        string contentId,
        string savePath,
        string nusUrl,
        string titleId,
        string? titleVersion = null,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));

        var directory = Path.GetDirectoryName(savePath) ?? throw new DirectoryNotFoundException();
        Directory.CreateDirectory(directory);
        File.Delete(savePath);

        byte[] bytes =
            await DownloadSingleContentViaUrl(contentId, nusUrl, titleId, titleVersion, token)
            ?? throw new NullReferenceException();
        File.WriteAllBytes(savePath, bytes);
    }

    /// <summary>
    /// Grabs a single content file and decrypts it.
    /// Leave the title version empty for the latest.
    /// </summary>
    /// <param name="contentId"></param>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<byte[]?> DownloadSingleContent(
        string contentId,
        string titleId,
        string? titleVersion = null,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));
        var nusUrl = await PrivNUSUp(token);
        return await DownloadSingleContentViaUrl(contentId, nusUrl, titleId, titleVersion, token);
    }

    /// <summary>
    /// Grabs a single content file and decrypts it.
    /// Leave the title version empty for the latest.
    /// </summary>
    /// <param name="contentId"></param>
    /// <param name="nusUrl"></param>
    /// <param name="titleId"></param>
    /// <param name="titleVersion"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    /// <exception cref="Exception"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<byte[]?> DownloadSingleContentViaUrl(
        string contentId,
        string nusUrl,
        string titleId,
        string? titleVersion = null,
        CancellationToken token = default
    )
    {
        if (titleId.Length != 16)
            throw new ArgumentException("Title ID must be 16 characters long!", nameof(titleId));

        uint num = uint.Parse(contentId, NumberStyles.HexNumber);
        contentId = num.ToString("x8");
        _logger.LogDebug(
            "Downloading Content (Content ID: {ID}) of Title {Title} v{Version}...",
            contentId,
            titleId,
            string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
        );

        string str1 =
            "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion);
        string str2 = string.Format("{0}{1}/", nusUrl, titleId);
        string empty = string.Empty;
        int contentIndex = 0;
        token.ThrowIfCancellationRequested();
        _logger.LogDebug("Downloading TMD...");
        byte[] tmdFile = await nusClient.GetByteArrayAsync(str2 + str1, token);
        _logger.LogDebug("Parsing TMD...");
        TMD tmd = TMD.Load(tmdFile);

        _logger.LogDebug("Looking for Content ID {ID} in TMD...", contentId);
        bool flag = false;
        for (int index = 0; index < tmd.Contents.Length; ++index)
        {
            if ((int)tmd.Contents[index].ContentID == (int)num)
            {
                _logger.LogDebug("   Content ID {ID} found in TMD...", contentId);
                flag = true;
                empty = tmd.Contents[index].ContentID.ToString("x8");
                contentIndex = index;
                break;
            }
        }
        if (!flag)
        {
            _logger.LogDebug("   Content ID {ID} wasn't found in TMD...", contentId);
            throw new Exception("Content ID wasn't found in the TMD!");
        }
        if (!File.Exists("cetk") && !ContinueWithoutTicket)
        {
            token.ThrowIfCancellationRequested();
            _logger.LogDebug("   Downloading Ticket...");
            try
            {
                byte[] tikArray = await nusClient.GetByteArrayAsync(str2 + "cetk", token);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("   Downloading Ticket Failed...");
                throw new Exception(
                    "CETK Doesn't Exist and Downloading Ticket Failed:\n" + ex.Message
                );
            }
        }
        _logger.LogDebug("Parsing Ticket...");
        Ticket tik = Ticket.Load("cetk");

        token.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "   Downloading Content... ({Size} bytes)",
            tmd.Contents[contentIndex].Size
        );
        byte[] content = await nusClient.GetByteArrayAsync(str2 + empty, token);

        _logger.LogDebug("   Decrypting Content...");
        byte[] array = PrivDecryptContent(content, contentIndex, tik, tmd);
        Array.Resize<byte>(ref array, (int)tmd.Contents[contentIndex].Size);
        if (!Shared.CompareByteArrays(SHA1.HashData(array), tmd.Contents[contentIndex].Hash))
        {
            _logger.LogDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
            throw new Exception("Hashes do not match!");
        }

        _logger.LogDebug(
            "Downloading Content (Content ID: {ID}) of Title {Title} v{Version} Finished...",
            contentId,
            titleId,
            string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
        );
        return array;
    }

    #endregion

    #region Private Functions

    private static byte[] PrivDecryptContent(byte[] content, int contentIndex, Ticket tik, TMD tmd)
    {
        Array.Resize<byte>(ref content, Shared.AddPadding(content.Length, 16));
        byte[] titleKey = tik.TitleKey;
        byte[] numArray = new byte[16];
        byte[] bytes = BitConverter.GetBytes(tmd.Contents[contentIndex].Index);
        numArray[0] = bytes[1];
        numArray[1] = bytes[0];
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Key = titleKey;
        aes.IV = numArray;
        ICryptoTransform decryptor = aes.CreateDecryptor();
        using MemoryStream memoryStream = new(content);
        using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);
        byte[] buffer = new byte[content.Length];
        cryptoStream.ReadExactly(buffer);
        return buffer;
    }

    private async Task DownloadFile(string url, string path, CancellationToken token = default)
    {
        var directory =
            Path.GetDirectoryName(path)
            ?? throw new ArgumentException(
                "File path doesn't contain a valid directory",
                nameof(path)
            );
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(path, await nusClient.GetByteArrayAsync(url, token), token);
    }

    private async Task<string> PrivNUSUp(CancellationToken token = default)
    {
        const string WiiEndpoint = "http://nus.cdn.shop.wii.com/ccs/download/";
        const string WiiUEndpoint = "http://ccs.cdn.wup.shop.nintendo.net/ccs/download/";
        const string DSiEndpoint = "http://nus.cdn.t.shop.nintendowifi.net/ccs/download/";

        // Wii Endpoint

        var response = await nusClient.GetAsync(WiiEndpoint, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized)
            return WiiEndpoint;

        // WiiU Endpoint

        response = await nusClient.GetAsync(WiiUEndpoint, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized)
            return WiiUEndpoint;

        // DSi Endpoint
        response = await nusClient.GetAsync(DSiEndpoint, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized)
            return DSiEndpoint;

        throw new Exception("Unable to verify any online NUS server!");
    }
    #endregion
}
