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

using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;

namespace LibWiiSharpCore
{
    public enum StoreType
    {
        EncryptedContent,
        DecryptedContent,
        WAD,
        All,
    }

    public sealed class NusClient(HttpClient nusClient)
    {
        private volatile bool isStopRequired;

        public void IsStopRequired(bool isStopRequired) => this.isStopRequired = isStopRequired;

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
            string titleId,
            string titleVersion,
            string outputDir,
            params StoreType[] storeTypes
        )
        {
            if (titleId.Length != 16)
            {
                if (isStopRequired)
                    return;
                throw new Exception("Title ID must be 16 characters long!");
            }

            await PrivDownloadTitle(titleId, titleVersion, outputDir, storeTypes);
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
        public void DownloadTitle(
            string titleId,
            string titleVersion,
            string outputDir,
            string nusUrl,
            params StoreType[] storeTypes
        )
        {
            if (titleId.Length != 16)
            {
                if (isStopRequired)
                    return;
                throw new Exception("Title ID must be 16 characters long!");
            }

            PrivDownloadTitle(titleId, titleVersion, outputDir, nusUrl, storeTypes);
        }

        /// <summary>
        /// Grabs a TMD from NUS.
        /// Leave the title version empty for the latest.
        /// </summary>
        /// <param name="titleId"></param>
        /// <param name="titleVersion"></param>
        /// <returns></returns>
        public async Task<TMD?> DownloadTMD(string titleId, string titleVersion)
        {
            if (isStopRequired)
                return null;
            return titleId.Length == 16
                ? await PrivDownloadTmd(titleId, titleVersion)
                : throw new Exception("Title ID must be 16 characters long!");
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
        public async Task<TMD?> DownloadTMD(string titleId, string titleVersion, string nusUrl)
        {
            if (isStopRequired)
                return null;
            return titleId.Length == 16
                ? await PrivDownloadTmd(titleId, titleVersion, nusUrl)
                : throw new Exception("Title ID must be 16 characters long!");
        }

        /// <summary>
        /// Grabs a Ticket from NUS.
        /// </summary>
        /// <param name="titleId"></param>
        /// <returns></returns>
        public async Task<Ticket?> DownloadTicket(string titleId)
        {
            if (isStopRequired)
                return null;
            return titleId.Length == 16
                ? await PrivDownloadTicket(titleId)
                : throw new Exception("Title ID must be 16 characters long!");
        }

        /// <summary>
        /// Grabs a Ticket from NUS.
        /// nusUrl should be formatted as "http://x.y.z.host.com/ccs/download/"
        /// </summary>
        /// <param name="titleId"></param>
        /// <param name="nusUrl"></param>
        /// <returns></returns>
        public async Task<Ticket?> DownloadTicket(string titleId, string nusUrl)
        {
            if (isStopRequired)
                return null;
            return titleId.Length == 16
                ? await PrivDownloadTicket(titleId, nusUrl)
                : throw new Exception("Title ID must be 16 characters long!");
        }

        /// <summary>
        /// Grabs a single content file and decrypts it.
        /// Leave the title version empty for the latest.
        /// </summary>
        /// <param name="titleId"></param>
        /// <param name="titleVersion"></param>
        /// <param name="contentId"></param>
        /// <returns></returns>
        public async Task<byte[]?> DownloadSingleContent(
            string titleId,
            string titleVersion,
            string contentId
        )
        {
            if (isStopRequired)
                return null;
            if (titleId.Length != 16)
            {
                throw new Exception("Title ID must be 16 characters long!");
            }

            return await PrivDownloadSingleContent(titleId, titleVersion, contentId);
        }

        /// <summary>
        /// Grabs a single content file and decrypts it.
        /// Leave the title version empty for the latest.
        /// </summary>
        /// <param name="titleId"></param>
        /// <param name="titleVersion"></param>
        /// <param name="contentId"></param>
        /// <param name="savePath"></param>
        public async Task DownloadSingleContent(
            string titleId,
            string titleVersion,
            string contentId,
            string savePath
        )
        {
            if (titleId.Length != 16)
            {
                throw new Exception("Title ID must be 16 characters long!");
            }

            if (!Directory.Exists(Path.GetDirectoryName(savePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            }

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            byte[] bytes =
                await PrivDownloadSingleContent(titleId, titleVersion, contentId)
                ?? throw new NullReferenceException();
            File.WriteAllBytes(savePath, bytes);
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
        public async Task DownloadSingleContent(
            string titleId,
            string titleVersion,
            string contentId,
            string savePath,
            string nusUrl
        )
        {
            if (titleId.Length != 16)
            {
                throw new Exception("Title ID must be 16 characters long!");
            }

            if (!Directory.Exists(Path.GetDirectoryName(savePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            }

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            byte[] bytes =
                await PrivDownloadSingleContent(titleId, titleVersion, contentId, nusUrl)
                ?? throw new NullReferenceException();
            File.WriteAllBytes(savePath, bytes);
        }
        #endregion

        #region Private Functions
        private async Task<byte[]?> PrivDownloadSingleContent(
            string titleId,
            string titleVersion,
            string contentId
        )
        {
            uint num = uint.Parse(contentId, NumberStyles.HexNumber);
            contentId = num.ToString("x8");
            FireDebug(
                "Downloading Content (Content ID: {0}) of Title {1} v{2}...",
                contentId,
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            FireDebug("   Checking for Internet connection...");
            string nusUrl = await PrivNUSUp();
            FireProgress(0);
            string str1 =
                "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion);
            string str2 = string.Format("{0}{1}/", nusUrl, titleId);
            string empty = string.Empty;
            int contentIndex = 0;
            if (isStopRequired)
            {
                FireDebug("    Stopping...");
                return null;
            }
            FireDebug("   Downloading TMD...");
            byte[] tmdFile = await nusClient.GetByteArrayAsync(str2 + str1);
            FireDebug("   Parsing TMD...");
            TMD tmd = TMD.Load(tmdFile);
            FireProgress(20);
            FireDebug("   Looking for Content ID {0} in TMD...", (object)contentId);
            bool flag = false;
            for (int index = 0; index < tmd.Contents.Length; ++index)
            {
                if ((int)tmd.Contents[index].ContentID == (int)num)
                {
                    FireDebug("   Content ID {0} found in TMD...", (object)contentId);
                    flag = true;
                    empty = tmd.Contents[index].ContentID.ToString("x8");
                    contentIndex = index;
                    break;
                }
            }
            if (!flag)
            {
                FireDebug("   Content ID {0} wasn't found in TMD...", (object)contentId);
                throw new Exception("Content ID wasn't found in the TMD!");
            }
            if (!File.Exists("cetk") && !ContinueWithoutTicket)
            {
                if (isStopRequired)
                {
                    FireDebug("    Stopping...");
                    return null;
                }
                FireDebug("   Downloading Ticket...");
                try
                {
                    byte[] tikArray = await nusClient.GetByteArrayAsync(str2 + "cetk");
                }
                catch (Exception ex)
                {
                    FireDebug("   Downloading Ticket Failed...");
                    throw new Exception(
                        "CETK Doesn't Exist and Downloading Ticket Failed:\n" + ex.Message
                    );
                }
            }
            FireDebug("Parsing Ticket...");
            Ticket tik = Ticket.Load("cetk");
            FireProgress(40);
            if (isStopRequired)
            {
                FireDebug("    Stopping...");
                return null;
            }
            FireDebug(
                "   Downloading Content... ({0} bytes)",
                (object)tmd.Contents[contentIndex].Size
            );
            byte[] content = await nusClient.GetByteArrayAsync(str2 + empty);
            FireProgress(80);
            FireDebug("   Decrypting Content...");
            byte[] array = PrivDecryptContent(content, contentIndex, tik, tmd);
            Array.Resize<byte>(ref array, (int)tmd.Contents[contentIndex].Size);
            if (!Shared.CompareByteArrays(SHA1.HashData(array), tmd.Contents[contentIndex].Hash))
            {
                FireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                throw new Exception("Hashes do not match!");
            }
            FireProgress(100);
            FireDebug(
                "Downloading Content (Content ID: {0}) of Title {1} v{2} Finished...",
                contentId,
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            return array;
        }

        private async Task<byte[]?> PrivDownloadSingleContent(
            string titleId,
            string titleVersion,
            string contentId,
            string nusUrl
        )
        {
            uint num = uint.Parse(contentId, NumberStyles.HexNumber);
            contentId = num.ToString("x8");
            FireDebug(
                "Downloading Content (Content ID: {0}) of Title {1} v{2}...",
                contentId,
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            FireProgress(0);
            string str1 =
                "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion);
            string str2 = string.Format("{0}{1}/", nusUrl, titleId);
            string empty = string.Empty;
            int contentIndex = 0;
            if (isStopRequired)
            {
                FireDebug("    Stopping...");
                return null;
            }
            FireDebug("   Downloading TMD...");
            byte[] tmdFile = await nusClient.GetByteArrayAsync(str2 + str1);
            FireDebug("   Parsing TMD...");
            TMD tmd = TMD.Load(tmdFile);
            FireProgress(20);
            FireDebug("   Looking for Content ID {0} in TMD...", (object)contentId);
            bool flag = false;
            for (int index = 0; index < tmd.Contents.Length; ++index)
            {
                if ((int)tmd.Contents[index].ContentID == (int)num)
                {
                    FireDebug("   Content ID {0} found in TMD...", (object)contentId);
                    flag = true;
                    empty = tmd.Contents[index].ContentID.ToString("x8");
                    contentIndex = index;
                    break;
                }
            }
            if (!flag)
            {
                FireDebug("   Content ID {0} wasn't found in TMD...", (object)contentId);
                throw new Exception("Content ID wasn't found in the TMD!");
            }
            if (!File.Exists("cetk") && !ContinueWithoutTicket)
            {
                if (isStopRequired)
                {
                    FireDebug("    Stopping...");
                    return null;
                }
                FireDebug("   Downloading Ticket...");
                try
                {
                    byte[] tikArray = await nusClient.GetByteArrayAsync(str2 + "cetk");
                }
                catch (Exception ex)
                {
                    FireDebug("   Downloading Ticket Failed...");
                    throw new Exception(
                        "CETK Doesn't Exist and Downloading Ticket Failed:\n" + ex.Message
                    );
                }
            }
            FireDebug("Parsing Ticket...");
            Ticket tik = Ticket.Load("cetk");
            FireProgress(40);
            if (isStopRequired)
            {
                FireDebug("    Stopping...");
                return null;
            }
            FireDebug(
                "   Downloading Content... ({0} bytes)",
                (object)tmd.Contents[contentIndex].Size
            );
            byte[] content = await nusClient.GetByteArrayAsync(str2 + empty);
            FireProgress(80);
            FireDebug("   Decrypting Content...");
            byte[] array = PrivDecryptContent(content, contentIndex, tik, tmd);
            Array.Resize<byte>(ref array, (int)tmd.Contents[contentIndex].Size);
            if (!Shared.CompareByteArrays(SHA1.HashData(array), tmd.Contents[contentIndex].Hash))
            {
                FireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                throw new Exception("Hashes do not match!");
            }
            FireProgress(100);
            FireDebug(
                "Downloading Content (Content ID: {0}) of Title {1} v{2} Finished...",
                contentId,
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            return array;
        }

        private async Task<Ticket> PrivDownloadTicket(string titleId)
        {
            string nusUrl = await PrivNUSUp();

            string titleUrl = string.Format("{0}{1}/", nusUrl, titleId);
            byte[] tikArray = await nusClient.GetByteArrayAsync(titleUrl + "cetk");

            return Ticket.Load(tikArray);
        }

        private async Task<Ticket> PrivDownloadTicket(string titleId, string nusUrl)
        {
            string titleUrl = string.Format("{0}{1}/", nusUrl, titleId);
            byte[] tikArray = await nusClient.GetByteArrayAsync(titleUrl + "cetk");

            return Ticket.Load(tikArray);
        }

        private async Task<TMD> PrivDownloadTmd(string titleId, string titleVersion)
        {
            string nusUrl = await PrivNUSUp();

            return TMD.Load(
                await nusClient.GetByteArrayAsync(
                    string.Format("{0}{1}/", nusUrl, titleId)
                        + (
                            "tmd"
                            + (
                                string.IsNullOrEmpty(titleVersion)
                                    ? string.Empty
                                    : "." + titleVersion
                            )
                        )
                )
            );
        }

        private async Task<TMD> PrivDownloadTmd(string titleId, string titleVersion, string nusUrl)
        {
            return TMD.Load(
                await nusClient.GetByteArrayAsync(
                    string.Format("{0}{1}/", nusUrl, titleId)
                        + (
                            "tmd"
                            + (
                                string.IsNullOrEmpty(titleVersion)
                                    ? string.Empty
                                    : "." + titleVersion
                            )
                        )
                )
            );
        }

        private async Task PrivDownloadTitle(
            string titleId,
            string titleVersion,
            string outputDir,
            StoreType[] storeTypes
        )
        {
            FireDebug(
                "Downloading Title {0} v{1}...",
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            if (storeTypes.Length < 1)
            {
                FireDebug("  No store types were defined...");
                throw new Exception("You must at least define one store type!");
            }
            FireDebug("   Checking for Internet connection...");
            string nusUrl = await PrivNUSUp();
            string str1 = string.Format("{0}{1}/", nusUrl, titleId);
            bool flag1 = false;
            bool flag2 = false;
            bool flag3 = false;
            FireProgress(0);
            for (int index = 0; index < storeTypes.Length; ++index)
            {
                switch (storeTypes[index])
                {
                    case StoreType.EncryptedContent:
                        FireDebug("    -> Storing Encrypted Content...");
                        flag1 = true;
                        break;
                    case StoreType.DecryptedContent:
                        FireDebug("    -> Storing Decrypted Content...");
                        flag2 = true;
                        break;
                    case StoreType.WAD:
                        FireDebug("    -> Storing WAD...");
                        flag3 = true;
                        break;
                    case StoreType.All:
                        FireDebug("    -> Storing Decrypted Content...");
                        FireDebug("    -> Storing Encrypted Content...");
                        FireDebug("    -> Storing WAD...");
                        flag2 = true;
                        flag1 = true;
                        flag3 = true;
                        break;
                }
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

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string str2 =
                "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion);
            FireDebug("   Downloading TMD...");

            string filePath = Path.Combine(outputDir, str2);

            using var response = await nusClient.GetAsync(str1 + str2);
            response.EnsureSuccessStatusCode();

            Directory.CreateDirectory(outputDir);

            await using var fileStream = File.Create(filePath);
            await response.Content.CopyToAsync(fileStream);

            if (!File.Exists(outputDir + "cetk"))
            {
                //Download cetk
                FireDebug("   Downloading Ticket...");
                try
                {
                    wcNus.DownloadFile(
                        string.Format("{0}{1}/", nusUrl, titleId) + "cetk",
                        outputDir + "cetk"
                    );
                }
                catch (Exception ex)
                {
                    if (!ContinueWithoutTicket || !flag1)
                    {
                        FireDebug("   Downloading Ticket Failed...");
                        throw new Exception(
                            "CETK Doesn't Exist and Downloading Ticket Failed:\n" + ex.Message
                        );
                    }

                    flag2 = false;
                    flag3 = false;
                }
            }

            FireProgress(10);
            FireDebug("   Parsing TMD...");
            TMD tmd = TMD.Load(outputDir + str2);
            if (string.IsNullOrEmpty(titleVersion))
            {
                FireDebug("    -> Title Version: {0}", (object)tmd.TitleVersion);
            }

            FireDebug("    -> {0} Contents", (object)tmd.NumOfContents);
            FireDebug("   Parsing Ticket...");
            Ticket? tik = null;
            if (!ContinueWithoutTicket)
            {
                tik = Ticket.Load(outputDir + "cetk");
            }
            string[] strArray1 = new string[tmd.NumOfContents];
            uint contentId;
            for (int index1 = 0; index1 < tmd.NumOfContents; ++index1)
            {
                FireDebug(
                    "   Downloading Content #{0} of {1}... ({2} bytes)",
                    index1 + 1,
                    tmd.NumOfContents,
                    tmd.Contents[index1].Size
                );
                FireProgress((index1 + 1) * 60 / tmd.NumOfContents + 10);
                if (UseLocalFiles)
                {
                    string str8 = outputDir;
                    contentId = tmd.Contents[index1].ContentID;
                    string str9 = contentId.ToString("x8");
                    if (File.Exists(str8 + str9))
                    {
                        FireDebug("   Using Local File, Skipping...");
                        continue;
                    }
                }

                string str3 = str1;
                contentId = tmd.Contents[index1].ContentID;
                string str4 = contentId.ToString("x8");
                string address = str3 + str4;
                string str5 = outputDir;
                contentId = tmd.Contents[index1].ContentID;
                string str6 = contentId.ToString("x8");
                string fileName = str5 + str6;

                using var responseMessage = await nusClient.GetAsync(address);
                response.EnsureSuccessStatusCode();

                Directory.CreateDirectory(outputDir);

                await using var file = File.Create(fileName);
                await response.Content.CopyToAsync(file);

                string[] strArray2 = strArray1;
                int index2 = index1;
                contentId = tmd.Contents[index1].ContentID;
                string str7 = contentId.ToString("x8");
                strArray2[index2] = str7;
            }
            if (flag2 | flag3)
            {
                SHA1 shA1 = SHA1.Create();
                for (int contentIndex = 0; contentIndex < tmd.NumOfContents; ++contentIndex)
                {
                    FireDebug(
                        "   Decrypting Content #{0} of {1}...",
                        contentIndex + 1,
                        tmd.NumOfContents
                    );
                    FireProgress((contentIndex + 1) * 20 / tmd.NumOfContents + 75);
                    string str3 = outputDir;
                    contentId = tmd.Contents[contentIndex].ContentID;
                    string str4 = contentId.ToString("x8");
                    byte[] array = PrivDecryptContent(
                        File.ReadAllBytes(str3 + str4),
                        contentIndex,
                        tik,
                        tmd
                    );
                    Array.Resize<byte>(ref array, (int)tmd.Contents[contentIndex].Size);
                    if (
                        !Shared.CompareByteArrays(
                            shA1.ComputeHash(array),
                            tmd.Contents[contentIndex].Hash
                        )
                    )
                    {
                        FireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                        throw new Exception(
                            string.Format("Content #{0}: Hashes do not match!", contentIndex)
                        );
                    }
                    string str5 = outputDir;
                    contentId = tmd.Contents[contentIndex].ContentID;
                    string str6 = contentId.ToString("x8");
                    File.WriteAllBytes(str5 + str6 + ".app", array);
                }
                shA1.Clear();
            }
            if (flag3)
            {
                FireDebug("   Building Certificate Chain...");
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
                FireDebug("   Creating WAD...");
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
                FireDebug("   Deleting Encrypted Contents...");
                for (int index = 0; index < strArray1.Length; ++index)
                {
                    if (File.Exists(outputDir + strArray1[index]))
                    {
                        File.Delete(outputDir + strArray1[index]);
                    }
                }
            }
            if (flag3 && !flag2)
            {
                FireDebug("   Deleting Decrypted Contents...");
                for (int index = 0; index < strArray1.Length; ++index)
                {
                    if (File.Exists(outputDir + strArray1[index] + ".app"))
                    {
                        File.Delete(outputDir + strArray1[index] + ".app");
                    }
                }
            }
            if (!flag2 && !flag1)
            {
                FireDebug("   Deleting TMD and Ticket...");
                File.Delete(outputDir + str2);
                if (ContinueWithoutTicket == false)
                {
                    File.Delete(outputDir + "cetk");
                }
            }
            FireDebug(
                "Downloading Title {0} v{1} Finished...",
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            FireProgress(100);
        }

        private void PrivDownloadTitle(
            string titleId,
            string titleVersion,
            string outputDir,
            string nusUrl,
            StoreType[] storeTypes
        )
        {
            FireDebug(
                "Downloading Title {0} v{1}...",
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            if (storeTypes.Length < 1)
            {
                FireDebug("  No store types were defined...");
                throw new Exception("You must at least define one store type!");
            }
            string str1 = string.Format("{0}{1}/", nusUrl, titleId);
            bool flag1 = false;
            bool flag2 = false;
            bool flag3 = false;
            FireProgress(0);
            for (int index = 0; index < storeTypes.Length; ++index)
            {
                switch (storeTypes[index])
                {
                    case StoreType.EncryptedContent:
                        FireDebug("    -> Storing Encrypted Content...");
                        flag1 = true;
                        break;
                    case StoreType.DecryptedContent:
                        FireDebug("    -> Storing Decrypted Content...");
                        flag2 = true;
                        break;
                    case StoreType.WAD:
                        FireDebug("    -> Storing WAD...");
                        flag3 = true;
                        break;
                    case StoreType.All:
                        FireDebug("    -> Storing Decrypted Content...");
                        FireDebug("    -> Storing Encrypted Content...");
                        FireDebug("    -> Storing WAD...");
                        flag2 = true;
                        flag1 = true;
                        flag3 = true;
                        break;
                }
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

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string str2 =
                "tmd" + (string.IsNullOrEmpty(titleVersion) ? string.Empty : "." + titleVersion);
            FireDebug("   Downloading TMD...");
            try
            {
                wcNus.DownloadFile(str1 + str2, outputDir + str2);
            }
            catch (Exception ex)
            {
                FireDebug("   Downloading TMD Failed...");
                throw new Exception("Downloading TMD Failed:\n" + ex.Message);
            }

            if (!File.Exists(outputDir + "cetk"))
            {
                //Download cetk
                FireDebug("   Downloading Ticket...");
                try
                {
                    wcNus.DownloadFile(
                        string.Format("{0}{1}/", nusUrl, titleId) + "cetk",
                        outputDir + "cetk"
                    );
                }
                catch (Exception ex)
                {
                    if (!ContinueWithoutTicket || !flag1)
                    {
                        FireDebug("   Downloading Ticket Failed...");
                        throw new Exception(
                            "CETK Doesn't Exist and Downloading Ticket Failed:\n" + ex.Message
                        );
                    }

                    flag2 = false;
                    flag3 = false;
                }
            }

            FireProgress(10);
            FireDebug("   Parsing TMD...");
            TMD tmd = TMD.Load(outputDir + str2);
            if (string.IsNullOrEmpty(titleVersion))
            {
                FireDebug("    -> Title Version: {0}", (object)tmd.TitleVersion);
            }

            FireDebug("    -> {0} Contents", (object)tmd.NumOfContents);
            FireDebug("   Parsing Ticket...");
            Ticket tik = null;
            if (!ContinueWithoutTicket)
            {
                tik = Ticket.Load(outputDir + "cetk");
            }
            string[] strArray1 = new string[tmd.NumOfContents];
            uint contentId;
            for (int index1 = 0; index1 < tmd.NumOfContents; ++index1)
            {
                FireDebug(
                    "   Downloading Content #{0} of {1}... ({2} bytes)",
                    index1 + 1,
                    tmd.NumOfContents,
                    tmd.Contents[index1].Size
                );
                FireProgress((index1 + 1) * 60 / tmd.NumOfContents + 10);
                if (UseLocalFiles)
                {
                    string str3 = outputDir;
                    contentId = tmd.Contents[index1].ContentID;
                    string str4 = contentId.ToString("x8");
                    if (File.Exists(str3 + str4))
                    {
                        FireDebug("   Using Local File, Skipping...");
                        continue;
                    }
                }
                try
                {
                    string str3 = str1;
                    contentId = tmd.Contents[index1].ContentID;
                    string str4 = contentId.ToString("x8");
                    string address = str3 + str4;
                    string str5 = outputDir;
                    contentId = tmd.Contents[index1].ContentID;
                    string str6 = contentId.ToString("x8");
                    string fileName = str5 + str6;
                    wcNus.DownloadFile(address, fileName);
                    string[] strArray2 = strArray1;
                    int index2 = index1;
                    contentId = tmd.Contents[index1].ContentID;
                    string str7 = contentId.ToString("x8");
                    strArray2[index2] = str7;
                }
                catch (Exception ex)
                {
                    FireDebug(
                        "   Downloading Content #{0} of {1} failed...",
                        index1 + 1,
                        tmd.NumOfContents
                    );
                    throw new Exception("Downloading Content Failed:\n" + ex.Message);
                }
            }
            if (flag2 | flag3)
            {
                SHA1 shA1 = SHA1.Create();
                for (int contentIndex = 0; contentIndex < tmd.NumOfContents; ++contentIndex)
                {
                    FireDebug(
                        "   Decrypting Content #{0} of {1}...",
                        contentIndex + 1,
                        tmd.NumOfContents
                    );
                    FireProgress((contentIndex + 1) * 20 / tmd.NumOfContents + 75);
                    string str3 = outputDir;
                    contentId = tmd.Contents[contentIndex].ContentID;
                    string str4 = contentId.ToString("x8");
                    byte[] array = PrivDecryptContent(
                        File.ReadAllBytes(str3 + str4),
                        contentIndex,
                        tik,
                        tmd
                    );
                    Array.Resize<byte>(ref array, (int)tmd.Contents[contentIndex].Size);
                    if (
                        !Shared.CompareByteArrays(
                            shA1.ComputeHash(array),
                            tmd.Contents[contentIndex].Hash
                        )
                    )
                    {
                        FireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                        throw new Exception(
                            string.Format("Content #{0}: Hashes do not match!", contentIndex)
                        );
                    }
                    string str5 = outputDir;
                    contentId = tmd.Contents[contentIndex].ContentID;
                    string str6 = contentId.ToString("x8");
                    File.WriteAllBytes(str5 + str6 + ".app", array);
                }
                shA1.Clear();
            }
            if (flag3)
            {
                FireDebug("   Building Certificate Chain...");
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
                FireDebug("   Creating WAD...");
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
                FireDebug("   Deleting Encrypted Contents...");
                for (int index = 0; index < strArray1.Length; ++index)
                {
                    if (File.Exists(outputDir + strArray1[index]))
                    {
                        File.Delete(outputDir + strArray1[index]);
                    }
                }
            }
            if (flag3 && !flag2)
            {
                FireDebug("   Deleting Decrypted Contents...");
                for (int index = 0; index < strArray1.Length; ++index)
                {
                    if (File.Exists(outputDir + strArray1[index] + ".app"))
                    {
                        File.Delete(outputDir + strArray1[index] + ".app");
                    }
                }
            }
            if (!flag2 && !flag1)
            {
                FireDebug("   Deleting TMD and Ticket...");
                File.Delete(outputDir + str2);
                if (ContinueWithoutTicket == false)
                {
                    File.Delete(outputDir + "cetk");
                }
            }
            FireDebug(
                "Downloading Title {0} v{1} Finished...",
                titleId,
                string.IsNullOrEmpty(titleVersion) ? "[Latest]" : titleVersion
            );
            FireProgress(100);
        }

        private static byte[] PrivDecryptContent(
            byte[] content,
            int contentIndex,
            Ticket tik,
            TMD tmd
        )
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
            MemoryStream memoryStream = new(content);
            CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);
            byte[] buffer = new byte[content.Length];
            cryptoStream.ReadExactly(buffer);
            cryptoStream.Dispose();
            memoryStream.Dispose();
            return buffer;
        }

        private async Task<string> PrivNUSUp()
        {
            const string WiiEndpoint = "http://nus.cdn.shop.wii.com/ccs/download/";
            const string WiiUEndpoint = "http://ccs.cdn.wup.shop.nintendo.net/ccs/download/";
            const string DSiEndpoint = "http://nus.cdn.t.shop.nintendowifi.net/ccs/download/";
            const string RC24Endpoint = "http://ccs.cdn.sho.rc24.xyz/ccs/download/";

            // Wii Endpoint

            var response = await nusClient.GetAsync(WiiEndpoint);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return WiiEndpoint;

            // WiiU Endpoint

            response = await nusClient.GetAsync(WiiUEndpoint);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return WiiUEndpoint;

            // DSi Endpoint
            response = await nusClient.GetAsync(DSiEndpoint);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return DSiEndpoint;

            // RC24 Endpoint
            response = await nusClient.GetAsync(RC24Endpoint);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return RC24Endpoint;

            throw new Exception("Unable to verify any online NUS server!");
        }
        #endregion

        #region Events
        /// <summary>
        /// Fires the Progress of various operations
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> Progress;

        /// <summary>
        /// Fires debugging messages. You may write them into a log file or log textbox.
        /// </summary>
        public event EventHandler<MessageEventArgs> Debug;

        private void FireProgress(int progressPercentage)
        {
            EventHandler<ProgressChangedEventArgs> progress = Progress;
            if (progress == null)
            {
                return;
            }

            progress(new object(), new ProgressChangedEventArgs(progressPercentage, string.Empty));
        }

        private void FireDebug(string debugMessage, params object[] args)
        {
            EventHandler<MessageEventArgs> debug = Debug;
            if (debug == null)
            {
                return;
            }

            debug(new object(), new MessageEventArgs(string.Format(debugMessage, args)));
        }

        #endregion
    }
}
