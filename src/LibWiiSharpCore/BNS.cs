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

using System.Buffers.Binary;

namespace LibWiiSharpCore;

public class BNS
{
    private readonly BNS_Header bnsHeader = new();
    private readonly BNS_Info bnsInfo = new();
    private readonly BNS_Data bnsData = new();

    // Unused
    //private int[,] lSamples = new int[2, 2];
    private readonly int[,] rlSamples = new int[2, 2];
    private readonly int[] tlSamples = new int[2];

    // Unused
    /*
    private int[] hbcDefTbl = new int[16]
    {
        674,
        1040,
        3598,
        -1738,
        2270,
        -583,
        3967,
        -1969,
        1516,
        381,
        3453,
        -1468,
        2606,
        -617,
        3795,
        -1759
    };
    */
    private readonly int[] defTbl =
    [
        1820,
        -856,
        3238,
        -1514,
        2333,
        -550,
        3336,
        -1376,
        2444,
        -949,
        3666,
        -1764,
        2654,
        -701,
        3420,
        -1398,
    ];
    private readonly int[] pHist1 = new int[2];
    private readonly int[] pHist2 = new int[2];
    private int tempSampleCount;
    private readonly byte[]? waveFile;
    private readonly bool loopFromWave;
    private bool converted;

    /// <summary>
    /// 0x00 (0) = No Loop, 0x01 (1) = Loop
    /// </summary>
    public bool HasLoop
    {
        get => bnsInfo.HasLoop == 1;
        set => bnsInfo.HasLoop = (byte)(value ? 1 : 0);
    }

    /// <summary>
    /// The start sample of the Loop
    /// </summary>
    public uint LoopStart
    {
        get => bnsInfo.LoopStart;
        set => bnsInfo.LoopStart = value;
    }

    /// <summary>
    /// The total number of samples in this file
    /// </summary>
    public uint TotalSampleCount
    {
        get => bnsInfo.LoopEnd;
        set => bnsInfo.LoopEnd = value;
    }

    /// <summary>
    /// If true and the input Wave file is stereo, the BNS will be converted to Mono.
    /// Be sure to set this before you call Convert()!
    /// </summary>
    public bool StereoToMono { get; set; }

    protected BNS() { }

    public BNS(string waveFile)
    {
        this.waveFile = File.ReadAllBytes(waveFile);
    }

    public BNS(string waveFile, bool loopFromWave)
    {
        this.waveFile = File.ReadAllBytes(waveFile);
        this.loopFromWave = loopFromWave;
    }

    public BNS(byte[] waveFile)
    {
        this.waveFile = waveFile;
    }

    public BNS(byte[] waveFile, bool loopFromWave)
    {
        this.waveFile = waveFile;
        this.loopFromWave = loopFromWave;
    }

    #region Public Functions

    /// <summary>
    /// Returns the length of the BNS audio file in seconds
    /// </summary>
    /// <param name="bnsFile"></param>
    /// <returns></returns>
    public static int GetBnsLength(byte[] bnsFile)
    {
        uint sampleRate = Shared.Swap(BitConverter.ToUInt16(bnsFile, 44));
        uint sampleCount = Shared.Swap(BitConverter.ToUInt32(bnsFile, 52));

        return (int)(sampleCount / sampleRate);
    }

    /// <summary>
    /// Converts the Wave file to BNS
    /// </summary>
    public void Convert()
    {
        if (waveFile is null)
            throw new NullReferenceException("Wave file is null");
        Convert(waveFile, loopFromWave);
    }

    /// <summary>
    /// Returns the BNS file as a Byte Array. If not already converted, it will be done first.
    /// </summary>
    /// <returns></returns>
    public byte[] ToByteArray()
    {
        return ToMemoryStream().ToArray();
    }

    /// <summary>
    /// Returns the BNS file as a Memory Stream. If not already converted, it will be done first.
    /// </summary>
    /// <returns></returns>
    public MemoryStream ToMemoryStream()
    {
        if (!converted)
        {
            if (waveFile is null)
                throw new NullReferenceException("Wave file is null");
            Convert(waveFile, loopFromWave);
        }

        MemoryStream memoryStream = new();
        try
        {
            bnsHeader.Write(memoryStream);
            bnsInfo.Write(memoryStream);
            bnsData.Write(memoryStream);
            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Saves the BNS file to the given path. If not already converted, it will be done first.
    /// </summary>
    /// <param name="destinationFile"></param>
    public void Save(string destinationFile)
    {
        if (File.Exists(destinationFile))
        {
            File.Delete(destinationFile);
        }

        using FileStream fileStream = new(destinationFile, FileMode.Create);
        byte[] array = ToMemoryStream().ToArray();
        fileStream.Write(array, 0, array.Length);
    }

    /// <summary>
    /// Sets the Loop to the given Start Sample. Be sure that you call Convert() first!
    /// </summary>
    /// <param name="loopStartSample"></param>
    public void SetLoop(int loopStartSample)
    {
        bnsInfo.HasLoop = 1;
        bnsInfo.LoopStart = (uint)loopStartSample;
    }

    #endregion

    #region Private Functions

    private void Convert(byte[] waveFile, bool loopFromWave)
    {
        Wave wave = new(waveFile);
        int numLoops = wave.NumLoops;
        int loopStart = wave.LoopStart;
        bnsInfo.SampleRate = (ushort)wave.SampleRate;
        if (wave.NumChannels > 2 || wave.NumChannels < 1)
        {
            throw new Exception("Unsupported Amount of Channels!");
        }

        if (wave.BitDepth != 16)
        {
            throw new Exception("Only 16bit Wave files are supported!");
        }

        if (wave.DataFormat != 1)
            throw new Exception("The format of this Wave file is not supported!");

        bnsData.Data = Encode(
            wave.SampleData ?? throw new NullReferenceException("Wave sample data is null.")
        );

        if (wave.NumChannels == 1)
        {
            bnsHeader.InfoLength = 96U;
            bnsHeader.DataOffset = 128U;
            bnsInfo.Size = 96U;
            bnsInfo.Channels[0].StartOffset = 28U;
            bnsInfo.Channels[0].Start = 40U;
            bnsInfo.Coefficients1Offset = 0U;
        }
        bnsData.Size = (uint)(bnsData.Data.Length + 8);
        bnsHeader.DataLength = bnsData.Size;
        bnsHeader.FileSize = bnsHeader.Size + bnsInfo.Size + bnsData.Size;
        if (loopFromWave && numLoops == 1 && loopStart != -1)
        {
            bnsInfo.LoopStart = (uint)loopStart;
            bnsInfo.HasLoop = 1;
        }
        bnsInfo.LoopEnd = (uint)tempSampleCount;
        for (int index = 0; index < 16; ++index)
        {
            bnsInfo.Channels[0].Coefficients[index] = defTbl[index];
            if (wave.NumChannels == 2)
            {
                bnsInfo.Channels[2].Coefficients[index] = defTbl[index];
            }
        }
        converted = true;
    }

    private byte[] Encode(byte[] inputFrames)
    {
        int[] inputBuffer = new int[14];
        tempSampleCount = inputFrames.Length / (bnsInfo.Channels.Length == 2 ? 4 : 2);
        int num1 = inputFrames.Length / (bnsInfo.Channels.Length == 2 ? 4 : 2) % 14;
        Array.Resize<byte>(
            ref inputFrames,
            inputFrames.Length + (14 - num1) * (bnsInfo.Channels.Length == 2 ? 4 : 2)
        );
        int num2 = inputFrames.Length / (bnsInfo.Channels.Length == 2 ? 4 : 2);
        int num3 = (num2 + 13) / 14;
        List<int> intList1 = [];
        List<int> intList2 = [];
        int startIndex = 0;
        if (StereoToMono && bnsInfo.Channels.Length == 2)
        {
            bnsInfo.Channels = bnsInfo.Channels[..1];
        }
        else if (StereoToMono)
        {
            StereoToMono = false;
        }

        for (int index = 0; index < num2; ++index)
        {
            intList1.Add(BitConverter.ToInt16(inputFrames, startIndex));
            startIndex += 2;
            if (bnsInfo.Channels.Length == 2 || StereoToMono)
            {
                intList2.Add(BitConverter.ToInt16(inputFrames, startIndex));
                startIndex += 2;
            }
        }
        byte[] numArray1 = new byte[bnsInfo.Channels.Length == 2 ? num3 * 16 : num3 * 8];
        int num4 = 0;
        int num5 = num3 * 8;
        bnsInfo.Channels[1].Start = bnsInfo.Channels.Length == 2 ? (uint)num5 : 0U;
        int[] array1 = [.. intList1];
        int[] array2 = [.. intList2];
        for (int index1 = 0; index1 < num3; ++index1)
        {
            try
            {
                if (index1 % (num3 / 100) != 0)
                {
                    if (index1 + 1 != num3)
                    {
                        goto label_14;
                    }
                }
            }
            catch { }
            label_14:
            for (int index2 = 0; index2 < 14; ++index2)
            {
                inputBuffer[index2] = array1[index1 * 14 + index2];
            }

            byte[] numArray2 = RepackAdpcm(0, defTbl, inputBuffer);
            for (int index2 = 0; index2 < 8; ++index2)
            {
                numArray1[num4 + index2] = numArray2[index2];
            }

            num4 += 8;
            if (bnsInfo.Channels.Length == 2)
            {
                for (int index2 = 0; index2 < 14; ++index2)
                {
                    inputBuffer[index2] = array2[index1 * 14 + index2];
                }

                byte[] numArray3 = RepackAdpcm(1, defTbl, inputBuffer);
                for (int index2 = 0; index2 < 8; ++index2)
                {
                    numArray1[num5 + index2] = numArray3[index2];
                }

                num5 += 8;
            }
        }
        bnsInfo.LoopEnd = (uint)(num3 * 7);
        return numArray1;
    }

    private byte[] RepackAdpcm(int index, int[] table, int[] inputBuffer)
    {
        byte[] numArray1 = new byte[8];
        int[] numArray2 = new int[2];
        double num1 = 999999999.0;
        for (int tableIndex = 0; tableIndex < 8; ++tableIndex)
        {
            byte[] numArray3 = CompressAdpcm(
                index,
                table,
                tableIndex,
                inputBuffer,
                out double outError
            );
            if (outError < num1)
            {
                num1 = outError;
                for (int index1 = 0; index1 < 8; ++index1)
                {
                    numArray1[index1] = numArray3[index1];
                }

                for (int index1 = 0; index1 < 2; ++index1)
                {
                    numArray2[index1] = tlSamples[index1];
                }
            }
        }
        for (int index1 = 0; index1 < 2; ++index1)
        {
            int[,] rlSamples = this.rlSamples;
            int num2 = index1;
            int index2 = index;
            int index3 = num2;
            int num3 = numArray2[index1];
            rlSamples[index2, index3] = num3;
        }
        return numArray1;
    }

    private byte[] CompressAdpcm(
        int index,
        int[] table,
        int tableIndex,
        int[] inputBuffer,
        out double outError
    )
    {
        byte[] numArray = new byte[8];
        int num1 = 0;
        int num2 = table[2 * tableIndex];
        int num3 = table[2 * tableIndex + 1];
        int stdExponent = DetermineStdExponent(index, table, tableIndex, inputBuffer);
        while (stdExponent <= 15)
        {
            bool flag = false;
            num1 = 0;
            numArray[0] = (byte)(stdExponent | tableIndex << 4);
            for (int index1 = 0; index1 < 2; ++index1)
            {
                tlSamples[index1] = rlSamples[index, index1];
            }

            int num4 = 0;
            for (int index1 = 0; index1 < 14; ++index1)
            {
                int num5 = tlSamples[1] * num2 + tlSamples[0] * num3 >> 11;
                int input1 = inputBuffer[index1] - num5 >> stdExponent;
                if (input1 <= 7 && input1 >= -8)
                {
                    int num6 = Math.Clamp(input1, -8, 7);
                    numArray[index1 / 2 + 1] =
                        (index1 & 1) == 0
                            ? (byte)(num6 << 4)
                            : (byte)(numArray[index1 / 2 + 1] | (uint)(num6 & 15));
                    int input2 = num5 + (num6 << stdExponent);
                    tlSamples[0] = tlSamples[1];
                    tlSamples[1] = Math.Clamp(input2, short.MinValue, short.MaxValue);
                    num1 += (int)Math.Pow(tlSamples[1] - inputBuffer[index1], 2.0);
                }
                else
                {
                    ++stdExponent;
                    flag = true;
                    break;
                }
            }
            if (!flag)
            {
                num4 = 14;
            }

            if (num4 == 14)
            {
                break;
            }
        }
        outError = num1;
        return numArray;
    }

    private int DetermineStdExponent(int index, int[] table, int tableIndex, int[] inputBuffer)
    {
        int[] numArray = new int[2];
        int num1 = 0;
        int num2 = table[2 * tableIndex];
        int num3 = table[2 * tableIndex + 1];
        for (int index1 = 0; index1 < 2; ++index1)
        {
            numArray[index1] = rlSamples[index, index1];
        }

        for (int index1 = 0; index1 < 14; ++index1)
        {
            int num4 = numArray[1] * num2 + numArray[0] * num3 >> 11;
            int num5 = inputBuffer[index1] - num4;
            if (num5 > num1)
            {
                num1 = num5;
            }

            numArray[0] = numArray[1];
            numArray[1] = inputBuffer[index1];
        }
        return FindExponent(num1);
    }

    private static int FindExponent(double residual)
    {
        int num = 0;
        for (; residual > 7.5 || residual < -8.5; residual /= 2.0)
        {
            ++num;
        }

        return num;
    }

    #endregion

    #region BNS to Wave

    #region Public Functions

    /// <summary>
    /// Converts a BNS audio file to Wave format.
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="outputFile"></param>
    /// <returns></returns>
    public static Wave BnsToWave(Stream inputFile)
    {
        BNS bns = new();
        byte[] samples = bns.Read(inputFile);
        Wave wave = new(bns.bnsInfo.Channels.Length, 16, bns.bnsInfo.SampleRate, samples);
        if (bns.bnsInfo.HasLoop == 1)
        {
            wave.AddLoop((int)bns.bnsInfo.LoopStart);
        }

        return wave;
    }

    public static Wave BnsToWave(string pathToFile)
    {
        BNS bns = new();
        byte[] samples;
        using (FileStream fileStream = new(pathToFile, FileMode.Open))
        {
            samples = bns.Read(fileStream);
        }

        Wave wave = new(bns.bnsInfo.Channels.Length, 16, bns.bnsInfo.SampleRate, samples);
        if (bns.bnsInfo.HasLoop == 1)
        {
            wave.AddLoop((int)bns.bnsInfo.LoopStart);
        }

        return wave;
    }

    public static Wave BnsToWave(byte[] bnsFile)
    {
        BNS bns = new();
        using MemoryStream memoryStream = new(bnsFile);
        var samples = bns.Read(memoryStream);
        Wave wave = new(bns.bnsInfo.Channels.Length, 16, bns.bnsInfo.SampleRate, samples);
        if (bns.bnsInfo.HasLoop == 1)
        {
            wave.AddLoop((int)bns.bnsInfo.LoopStart);
        }

        return wave;
    }

    #endregion

    #region Private Functions

    private byte[] Read(Stream input)
    {
        input.Seek(0L, SeekOrigin.Begin);
        bnsHeader.Read(input);
        bnsInfo.Read(input);
        bnsData.Read(input);
        return Decode();
    }

    private byte[] Decode()
    {
        List<byte> byteList = [];
        int num = bnsData.Data.Length / (bnsInfo.Channels.Length == 2 ? 16 : 8);
        int dataOffset1 = 0;
        int dataOffset2 = num * 8;
        //byte[] numArray1 = new byte[0];
        byte[] numArray2 = [];
        for (int index1 = 0; index1 < num; ++index1)
        {
            byte[] numArray3 = DecodeAdpcm(0, dataOffset1);
            if (bnsInfo.Channels.Length == 2)
            {
                numArray2 = DecodeAdpcm(1, dataOffset2);
            }

            for (int index2 = 0; index2 < 14; ++index2)
            {
                byteList.Add(numArray3[index2 * 2]);
                byteList.Add(numArray3[index2 * 2 + 1]);
                if (bnsInfo.Channels.Length == 2)
                {
                    byteList.Add(numArray2[index2 * 2]);
                    byteList.Add(numArray2[index2 * 2 + 1]);
                }
            }
            dataOffset1 += 8;
            if (bnsInfo.Channels.Length == 2)
            {
                dataOffset2 += 8;
            }
        }
        return [.. byteList];
    }

    private byte[] DecodeAdpcm(int channel, int dataOffset)
    {
        byte[] numArray = new byte[28];
        int num1 = bnsData.Data[dataOffset] >> 4 & 15;
        int num2 = 1 << (bnsData.Data[dataOffset] & 15);
        int num3 = pHist1[channel];
        int num4 = pHist2[channel];
        int num5 =
            channel == 0
                ? bnsInfo.Channels[0].Coefficients[num1 * 2]
                : bnsInfo.Channels[1].Coefficients[num1 * 2];
        int num6 =
            channel == 0
                ? bnsInfo.Channels[0].Coefficients[num1 * 2 + 1]
                : bnsInfo.Channels[1].Coefficients[num1 * 2 + 1];
        for (int index = 0; index < 14; ++index)
        {
            short num7 = bnsData.Data[dataOffset + (index / 2 + 1)];
            int num8 = (index & 1) != 0 ? num7 & 15 : num7 >> 4;
            if (num8 >= 8)
            {
                num8 -= 16;
            }

            int num9 = Math.Clamp(
                (num2 * num8 << 11) + (num5 * num3 + num6 * num4) + 1024 >> 11,
                short.MinValue,
                short.MaxValue
            );
            numArray[index * 2] = (byte)((uint)(short)num9 & byte.MaxValue);
            numArray[index * 2 + 1] = (byte)((uint)(short)num9 >> 8);
            num4 = num3;
            num3 = num9;
        }
        pHist1[channel] = num3;
        pHist2[channel] = num4;
        return numArray;
    }
    #endregion
    #endregion
}

internal class BNS_Data
{
    private readonly byte[] magic = "DATA"u8.ToArray();

    public uint Size { get; set; } = 315392;

    public byte[]? Data { get; set; }

    public void Write(Stream outStream)
    {
        byte[] bytes = BitConverter.GetBytes(Shared.Swap(Size));
        outStream.Write(magic);
        outStream.Write(bytes);
        outStream.Write(Data);
    }

    public void Read(Stream input)
    {
        BinaryReader binaryReader = new(input);
        Size = Shared.CompareByteArrays(magic, binaryReader.ReadBytes(4))
            ? Shared.Swap(binaryReader.ReadUInt32())
            : throw new Exception("This is not a valid BNS audfo file!");
        Data = binaryReader.ReadBytes((int)Size - 8);
    }
}

internal class BNS_Header
{
    //Private Variables
    private readonly byte[] magic = "BNS "u8.ToArray();
    private uint flags = 4278124800;
    private ushort chunkCount = 2;
    private uint infoOffset = 32;

    //Public Variables
    public uint DataOffset { get; set; } = 192;
    public uint InfoLength { get; set; } = 160;
    public ushort Size { get; set; } = 32;
    public uint DataLength { get; set; } = 315392;
    public uint FileSize { get; set; } = 315584;

    public void Write(Stream outStream)
    {
        outStream.Write(magic);
        BigEndianHelper.WriteBigEndian(outStream, flags);
        BigEndianHelper.WriteBigEndian(outStream, FileSize);
        BigEndianHelper.WriteBigEndian(outStream, Size);
        BigEndianHelper.WriteBigEndian(outStream, chunkCount);
        BigEndianHelper.WriteBigEndian(outStream, infoOffset);
        BigEndianHelper.WriteBigEndian(outStream, InfoLength);
        BigEndianHelper.WriteBigEndian(outStream, DataOffset);
        BigEndianHelper.WriteBigEndian(outStream, DataLength);
    }

    public void Read(Stream input)
    {
        BinaryReader binaryReader = new(input);
        if (!Shared.CompareByteArrays(magic, binaryReader.ReadBytes(4)))
        {
            binaryReader.BaseStream.Seek(28L, SeekOrigin.Current);
            if (!Shared.CompareByteArrays(magic, binaryReader.ReadBytes(4)))
            {
                throw new Exception("This is not a valid BNS audio file!");
            }
        }
        flags = Shared.Swap(binaryReader.ReadUInt32());
        FileSize = Shared.Swap(binaryReader.ReadUInt32());
        Size = Shared.Swap(binaryReader.ReadUInt16());
        chunkCount = Shared.Swap(binaryReader.ReadUInt16());
        infoOffset = Shared.Swap(binaryReader.ReadUInt32());
        InfoLength = Shared.Swap(binaryReader.ReadUInt32());
        DataOffset = Shared.Swap(binaryReader.ReadUInt32());
        DataLength = Shared.Swap(binaryReader.ReadUInt32());
    }
}

internal static class BigEndianHelper
{
    public static void WriteBigEndian(Stream outStream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        outStream.Write(bytes);
    }

    public static void WriteBigEndian(Stream outStream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        outStream.Write(bytes);
    }

    public static uint ReadUInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4).AsSpan();
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    public static ushort ReadUInt16(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(2).AsSpan();
        return BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }
}

internal class BNS_Info
{
    //Private Variables
    private readonly byte[] magic = "INFO"u8.ToArray();
    private byte codec;
    private byte zero;
    private ushort pad0;
    private uint offsetToChannelStart = 24;
    private uint pad1;
    private uint pad2;
    private uint coefficients2Offset = 104;
    private uint pad3;

    public BNSChannelInfo[] Channels { get; set; } = [];

    //Public Variables
    public byte HasLoop { get; set; }
    public uint Coefficients1Offset { get; set; } = 56;
    public uint Size { get; set; } = 160;
    public ushort SampleRate { get; set; } = 44100;
    public uint LoopStart { get; set; }
    public uint LoopEnd { get; set; }

    public void Write(Stream outStream)
    {
        outStream.Write(magic);
        BigEndianHelper.WriteBigEndian(outStream, Size);
        outStream.WriteByte(codec);
        outStream.WriteByte(HasLoop);
        outStream.WriteByte((byte)Channels.Length);
        outStream.WriteByte(zero);
        BigEndianHelper.WriteBigEndian(outStream, SampleRate);
        BigEndianHelper.WriteBigEndian(outStream, pad0);
        BigEndianHelper.WriteBigEndian(outStream, LoopStart);
        BigEndianHelper.WriteBigEndian(outStream, LoopEnd);
        BigEndianHelper.WriteBigEndian(outStream, offsetToChannelStart);
        BigEndianHelper.WriteBigEndian(outStream, pad1);
        BigEndianHelper.WriteBigEndian(outStream, Channels[0].StartOffset);
        BigEndianHelper.WriteBigEndian(
            outStream,
            Channels.Length > 1 ? Channels[1].StartOffset : 44
        );
        BigEndianHelper.WriteBigEndian(outStream, Channels[0].Start);
        BigEndianHelper.WriteBigEndian(outStream, Coefficients1Offset);

        for (int i = 0; i < Channels.Length; i++)
        {
            if (i == 1)
            {
                BigEndianHelper.WriteBigEndian(outStream, pad2);
                BigEndianHelper.WriteBigEndian(outStream, Channels[i].StartOffset);
                BigEndianHelper.WriteBigEndian(outStream, coefficients2Offset);
                BigEndianHelper.WriteBigEndian(outStream, pad3);
            }

            WriteChannel(outStream, Channels[i]);
        }
    }

    private static void WriteChannel(Stream outStream, BNSChannelInfo channel)
    {
        foreach (int coeff in channel.Coefficients)
        {
            BigEndianHelper.WriteBigEndian(outStream, (ushort)coeff);
        }

        BigEndianHelper.WriteBigEndian(outStream, channel.Gain);
        BigEndianHelper.WriteBigEndian(outStream, channel.PredictiveScale);
        BigEndianHelper.WriteBigEndian(outStream, channel.PreviousValue);
        BigEndianHelper.WriteBigEndian(outStream, channel.NextPreviousValue);
        BigEndianHelper.WriteBigEndian(outStream, channel.LoopPredictiveScale);
        BigEndianHelper.WriteBigEndian(outStream, channel.LoopPreviousValue);
        BigEndianHelper.WriteBigEndian(outStream, channel.LoopNextPreviousValue);
        BigEndianHelper.WriteBigEndian(outStream, channel.LoopPadding);
    }

    public void Read(Stream input)
    {
        using var reader = new BinaryReader(input);

        // Магия
        if (!Shared.CompareByteArrays(magic, reader.ReadBytes(4)))
            throw new Exception("This is not a valid BNS audio file!");

        // Заголовок
        Size = BigEndianHelper.ReadUInt32(reader);
        codec = reader.ReadByte();
        HasLoop = reader.ReadByte();
        byte channelCount = reader.ReadByte();
        zero = reader.ReadByte();
        SampleRate = BigEndianHelper.ReadUInt16(reader);
        pad0 = BigEndianHelper.ReadUInt16(reader);
        LoopStart = BigEndianHelper.ReadUInt32(reader);
        LoopEnd = BigEndianHelper.ReadUInt32(reader);
        offsetToChannelStart = BigEndianHelper.ReadUInt32(reader);
        pad1 = BigEndianHelper.ReadUInt32(reader);

        uint channel1StartOffset = BigEndianHelper.ReadUInt32(reader);
        uint channel2StartOffset = BigEndianHelper.ReadUInt32(reader);
        uint channel1Start = BigEndianHelper.ReadUInt32(reader);
        Coefficients1Offset = BigEndianHelper.ReadUInt32(reader);

        // Читаем каналы
        Channels = new BNSChannelInfo[channelCount];

        for (int i = 0; i < channelCount; i++)
        {
            if (i == 1)
            {
                pad2 = BigEndianHelper.ReadUInt32(reader);
                channel2StartOffset = BigEndianHelper.ReadUInt32(reader);
                coefficients2Offset = BigEndianHelper.ReadUInt32(reader);
                pad3 = BigEndianHelper.ReadUInt32(reader);
            }

            Channels[i] = ReadChannel(reader, i == 1 ? channel2StartOffset : channel1StartOffset);
            Channels[i].StartOffset = i == 0 ? channel1StartOffset : channel2StartOffset;
            Channels[i].Start = i == 0 ? channel1Start : 0u;
        }
    }

    private static BNSChannelInfo ReadChannel(BinaryReader reader, uint startOffset)
    {
        int[] coefficients = new int[16];
        for (int i = 0; i < 16; i++)
            coefficients[i] = BigEndianHelper.ReadUInt16(reader);

        return new BNSChannelInfo
        {
            Coefficients = coefficients,
            Gain = BigEndianHelper.ReadUInt16(reader),
            PredictiveScale = BigEndianHelper.ReadUInt16(reader),
            PreviousValue = BigEndianHelper.ReadUInt16(reader),
            NextPreviousValue = BigEndianHelper.ReadUInt16(reader),
            LoopPredictiveScale = BigEndianHelper.ReadUInt16(reader),
            LoopPreviousValue = BigEndianHelper.ReadUInt16(reader),
            LoopNextPreviousValue = BigEndianHelper.ReadUInt16(reader),
            LoopPadding = BigEndianHelper.ReadUInt16(reader),
            StartOffset = startOffset,
        };
    }

    public record BNSChannelInfo
    {
        internal ushort Gain { get; init; }
        internal ushort PredictiveScale { get; init; }
        internal ushort PreviousValue { get; init; }
        internal ushort NextPreviousValue { get; init; }
        internal ushort LoopPredictiveScale { get; init; }
        internal ushort LoopPreviousValue { get; init; }
        internal ushort LoopNextPreviousValue { get; init; }
        internal ushort LoopPadding { get; init; }
        public uint StartOffset { get; set; } = 32; // 44 for channel 2
        public int[] Coefficients { get; set; } = new int[16];
        public uint Start { get; set; }
    }
}
