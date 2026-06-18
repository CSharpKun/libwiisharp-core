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

using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLibDotNet;

namespace LibWiiSharpCore;

public enum Protocol
{
    /// <summary>
    /// Will preconfigure all settings for HBC to 1.0.5 (HAXX).
    /// </summary>
    HAXX,

    /// <summary>
    /// Will preconfigure all settings for HBC from 1.0.5 (JODI).
    /// </summary>
    JODI,

    /// <summary>
    /// Remember to define your custom settings.
    /// </summary>
    Custom,
}

/// <summary>
/// The HbcTransmitter can easily transmit files to the Homebrew Channel.
/// </summary>
public class HbcTransmitter
{
    private readonly ILogger<HbcTransmitter> _logger;

    private bool compress;
    private readonly Protocol protocol;

    /// <summary>
    /// The size of the buffer that is used to transmit the data.
    /// Default is 4 * 1024. If you're facing problems (freezes while transmitting), try a higher size.
    /// </summary>
    public int Blocksize { get; set; } = 4096;

    /// <summary>
    /// The mayor version of wiiload. You might need to change it for upcoming releases of the HBC.
    /// </summary>
    public int WiiloadVersionMayor { get; set; }

    /// <summary>
    /// The minor version of wiiload. You might need to change it for upcoming releases of the HBC.
    /// </summary>
    public int WiiloadVersionMinor { get; set; } = 5;

    /// <summary>
    /// If true, the data will be compressed before being transmitted. NOT available for Protocol.HAXX!
    /// </summary>
    public bool Compress
    {
        get => compress;
        set
        {
            if (protocol == Protocol.HAXX)
            {
                throw new NotSupportedException();
            }

            compress = value;
        }
    }

    /// <summary>
    /// The IP address of the Wii.
    /// </summary>
    public string IpAddress { get; set; }

    /// The port used for the transmission.
    /// You don't need to touch this unless the port changes in future releases of the HBC.
    /// </summary>
    public int Port { get; set; } = 4299;

    /// <summary>
    /// After a successfully completed transmission, this value holds the number of transmitted bytes.
    /// </summary>
    public int TransmittedLength { get; private set; }

    /// <summary>
    /// After a successfully completed transmission, this value holds the compression ratio.
    /// Will be 0 if the data wasn't compressed.
    /// </summary>
    public int CompressionRatio { get; private set; }

    public HbcTransmitter(
        Protocol protocol,
        string ipAddress,
        ILogger<HbcTransmitter>? logger = null
    )
    {
        _logger = logger ?? NullLogger<HbcTransmitter>.Instance;
        this.protocol = protocol;
        IpAddress = ipAddress;
        WiiloadVersionMinor = protocol == Protocol.HAXX ? 4 : 5;
        compress = protocol == Protocol.JODI;
    }

    public void TransmitFile(string pathToFile)
    {
        TransmitFile(Path.GetFileName(pathToFile), File.ReadAllBytes(pathToFile));
    }

    public void TransmitFile(string fileName, byte[] fileData)
    {
        _logger.LogDebug("Transmitting {File} to {IP}:{Port}...", fileName, IpAddress, Port);

        using var tcpClient = new TcpClient();
        byte[] buffer1 = new byte[4];
        _logger.LogDebug("Connecting...");
        tcpClient.Connect(IpAddress, 4299);
        using var nwStream = tcpClient.GetStream();
        _logger.LogDebug("   Sending Magic...");
        buffer1[0] = 72;
        buffer1[1] = 65;
        buffer1[2] = 88;
        buffer1[3] = 88;
        nwStream.Write(buffer1, 0, 4);
        _logger.LogDebug("   Sending Version Info...");
        buffer1[0] = (byte)WiiloadVersionMayor;
        buffer1[1] = (byte)WiiloadVersionMinor;
        buffer1[2] = (byte)(fileName.Length + 2 >> 8 & byte.MaxValue);
        buffer1[3] = (byte)(fileName.Length + 2 & byte.MaxValue);
        nwStream.Write(buffer1, 0, 4);
        byte[] buffer2;
        if (compress)
        {
            _logger.LogInformation("Compressing File...");
            buffer2 = ZlibWrapper.Compress(fileData);
        }
        else
        {
            buffer2 = fileData;
            fileData = new byte[0];
        }
        _logger.LogDebug("   Sending Filesize...");
        buffer1[0] = (byte)(buffer2.Length >> 24 & byte.MaxValue);
        buffer1[1] = (byte)(buffer2.Length >> 16 & byte.MaxValue);
        buffer1[2] = (byte)(buffer2.Length >> 8 & byte.MaxValue);
        buffer1[3] = (byte)(buffer2.Length & byte.MaxValue);
        nwStream.Write(buffer1, 0, 4);
        if (protocol != Protocol.HAXX)
        {
            buffer1[0] = (byte)(fileData.Length >> 24 & byte.MaxValue);
            buffer1[1] = (byte)(fileData.Length >> 16 & byte.MaxValue);
            buffer1[2] = (byte)(fileData.Length >> 8 & byte.MaxValue);
            buffer1[3] = (byte)(fileData.Length & byte.MaxValue);
            nwStream.Write(buffer1, 0, 4);
        }
        _logger.LogDebug("Sending File...");
        int offset = 0;
        int num1 = 0;
        int num2 = buffer2.Length / Blocksize;
        int num3 = buffer2.Length % Blocksize;

        do
        {
            nwStream.Write(buffer2, offset, Blocksize);
            offset += Blocksize;
        } while (num1 < num2);
        if (num3 > 0)
        {
            nwStream.Write(buffer2, offset, buffer2.Length - offset);
        }

        _logger.LogDebug("Sending Arguments...");
        byte[] buffer3 = new byte[fileName.Length + 2];
        for (int index = 0; index < fileName.Length; ++index)
        {
            buffer3[index] = (byte)fileName.ToCharArray()[index];
        }
        nwStream.Write(buffer3, 0, buffer3.Length);
        TransmittedLength = buffer2.Length;
        CompressionRatio =
            !compress || fileData.Length == 0 ? 0 : buffer2.Length * 100 / fileData.Length;
        _logger.LogDebug("Transmitting {File} to {IP}:{Port} Finished.", fileName, IpAddress, Port);
    }
}

internal class ZlibWrapper
{
    private static readonly ZLib zLib = new();

    public static byte[] Compress(byte[] inFile)
    {
        byte[] array = new byte[inFile.Length + 64];
        var zlibError = zLib.Compress(
            array,
            out var destLength,
            inFile,
            inFile.Length,
            ZLib.Z_DEFAULT_COMPRESSION
        );

        if (zlibError != ZLib.Z_OK || destLength <= 0)
        {
            throw new Exception($"An error occured while compressing! Code: {zlibError}");
        }

        Array.Resize<byte>(ref array, destLength);
        return array;
    }
}
