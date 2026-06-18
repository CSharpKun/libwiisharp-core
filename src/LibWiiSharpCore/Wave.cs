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

namespace LibWiiSharpCore;

public class Wave
{
    private readonly WaveHeader header = new();
    private readonly WaveFmtChunk fmt = new();
    private readonly WaveDataChunk data = new();
    private readonly WaveSmplChunk smpl = new();
    private bool hasSmpl;

    public int SampleRate => (int)(fmt.SampleRate);

    public int BitDepth => fmt.BitsPerSample;

    public int NumChannels => fmt.NumChannels;

    public int NumLoops => !hasSmpl ? 0 : (int)smpl.NumLoops;

    public int LoopStart => NumLoops == 0 ? 0 : (int)smpl.Loops[0].LoopStart;

    public int NumSamples =>
        (int)((data.Data?.Length ?? 8) / (fmt.BitsPerSample / 8) / fmt.NumChannels);

    public int DataFormat => (int)fmt.AudioFormat;

    public byte[]? SampleData => data?.Data;

    public int PlayLength =>
        (int)(
            (data.Data?.Length ?? 8) / fmt.NumChannels / (fmt.BitsPerSample / 8) / fmt.SampleRate
        );

    public Wave(string pathToFile)
    {
        using FileStream fileStream = new(pathToFile, FileMode.Open);
        using BinaryReader reader = new(fileStream);
        ParseWave(reader);
    }

    public Wave(Stream wave)
    {
        ParseWave(new BinaryReader(wave));
    }

    public Wave(byte[] waveFile)
    {
        using MemoryStream memoryStream = new(waveFile);
        using BinaryReader reader = new(memoryStream);
        ParseWave(reader);
    }

    public Wave(int numChannels, int bitsPerSample, int sampleRate, byte[] samples)
    {
        fmt.SampleRate = (uint)sampleRate;
        fmt.NumChannels = (ushort)numChannels;
        fmt.BitsPerSample = (ushort)bitsPerSample;
        data.Data = samples;
    }

    public void Write(Stream writeStream)
    {
        WriteToStream(new BinaryWriter(writeStream));
    }

    public MemoryStream ToMemoryStream()
    {
        MemoryStream memoryStream = new();
        WriteToStream(new BinaryWriter(memoryStream));
        return memoryStream;
    }

    public byte[] ToByteArray()
    {
        return ToMemoryStream().ToArray();
    }

    public void Save(string savePath)
    {
        using FileStream fileStream = new(savePath, FileMode.Create);
        using BinaryWriter writer = new(fileStream);
        WriteToStream(writer);
    }

    public void AddLoop(int loopStartSample)
    {
        smpl.AddLoop(loopStartSample, NumSamples);
        hasSmpl = true;
    }

    public void RemoveLoop()
    {
        hasSmpl = false;
    }

    public void TrimStart(int newStartSample)
    {
        if (data.Data is null)
            throw new NullReferenceException();
        int offset = fmt.NumChannels * (fmt.BitsPerSample / 8) * newStartSample;
        MemoryStream memoryStream = new();
        memoryStream.Write(data.Data, offset, data.Data.Length - offset);
        data.Data = memoryStream.ToArray();
        memoryStream.Dispose();
    }

    private void WriteToStream(BinaryWriter writer)
    {
        header.FileSize = (uint)(
            4
            + (int)fmt.FmtSize
            + 8
            + ((int?)data.Data?.Length ?? 8)
            + 8
            + (hasSmpl ? (int)smpl.SmplSize + 8 : 0)
        );
        header.Write(writer);
        fmt.Write(writer);
        data.Write(writer);
        if (!hasSmpl)
        {
            return;
        }

        smpl.Write(writer);
    }

    private void ParseWave(BinaryReader reader)
    {
        bool[] flagArray = new bool[3];
        while (reader.BaseStream.Position < reader.BaseStream.Length - 4L)
        {
            uint num1 = Shared.Swap(reader.ReadUInt32());
            uint num2 = reader.ReadUInt32();
            long offset = reader.BaseStream.Position + num2;
            switch (num1)
            {
                case 1380533830:
                    try
                    {
                        reader.BaseStream.Seek(-8L, SeekOrigin.Current);
                        header.Read(reader);
                        flagArray[0] = true;
                        break;
                    }
                    catch
                    {
                        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                        break;
                    }
                case 1684108385:
                    try
                    {
                        reader.BaseStream.Seek(-8L, SeekOrigin.Current);
                        data.Read(reader);
                        flagArray[2] = true;
                        break;
                    }
                    catch
                    {
                        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                        break;
                    }
                case 1718449184:
                    try
                    {
                        reader.BaseStream.Seek(-8L, SeekOrigin.Current);
                        fmt.Read(reader);
                        flagArray[1] = true;
                        break;
                    }
                    catch
                    {
                        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                        break;
                    }
                case 1936552044:
                    try
                    {
                        reader.BaseStream.Seek(-8L, SeekOrigin.Current);
                        smpl.Read(reader);
                        hasSmpl = true;
                        break;
                    }
                    catch
                    {
                        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                        break;
                    }
                default:
                    reader.BaseStream.Seek(num2, SeekOrigin.Current);
                    break;
            }
            if (flagArray[0] && flagArray[1] && (flagArray[2] && hasSmpl))
            {
                break;
            }
        }
        if (!flagArray[0] || !flagArray[1] || !flagArray[2])
        {
            throw new Exception("Couldn't parse Wave file...");
        }
    }
}

internal class WaveHeader
{
    private readonly uint headerId = 1380533830;
    private uint fileSize = 12;
    private readonly uint format = 1463899717;

    public uint FileSize
    {
        get => fileSize;
        set => fileSize = value;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Shared.Swap(headerId));
        writer.Write(fileSize);
        writer.Write(Shared.Swap(format));
    }

    public void Read(BinaryReader reader)
    {
        fileSize =
            (int)Shared.Swap(reader.ReadUInt32()) == (int)headerId
                ? reader.ReadUInt32()
                : throw new Exception("Not a valid RIFF Wave file!");
        if ((int)Shared.Swap(reader.ReadUInt32()) != (int)format)
        {
            throw new Exception("Not a valid RIFF Wave file!");
        }
    }
}

internal class WaveFmtChunk
{
    private readonly uint fmtId = 1718449184;
    private uint fmtSize = 16;
    private ushort audioFormat = 1;
    private ushort numChannels = 2;
    private uint sampleRate = 44100;
    private uint byteRate;
    private ushort blockAlign;
    private ushort bitsPerSample = 16;

    public uint FmtSize => fmtSize;

    public ushort NumChannels
    {
        get => numChannels;
        set => numChannels = value;
    }

    public uint SampleRate
    {
        get => sampleRate;
        set => sampleRate = value;
    }

    public ushort BitsPerSample
    {
        get => bitsPerSample;
        set => bitsPerSample = value;
    }

    public uint AudioFormat => audioFormat;

    public void Write(BinaryWriter writer)
    {
        byteRate = sampleRate * numChannels * bitsPerSample / 8U;
        blockAlign = (ushort)(numChannels * bitsPerSample / 8);
        writer.Write(Shared.Swap(fmtId));
        writer.Write(fmtSize);
        writer.Write(audioFormat);
        writer.Write(numChannels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
    }

    public void Read(BinaryReader reader)
    {
        fmtSize =
            (int)Shared.Swap(reader.ReadUInt32()) == (int)fmtId
                ? reader.ReadUInt32()
                : throw new Exception("Wrong chunk ID!");
        audioFormat = reader.ReadUInt16();
        numChannels = reader.ReadUInt16();
        sampleRate = reader.ReadUInt32();
        byteRate = reader.ReadUInt32();
        blockAlign = reader.ReadUInt16();
        bitsPerSample = reader.ReadUInt16();
    }
}

internal class WaveDataChunk
{
    private readonly uint dataId = 1684108385;
    public byte[]? Data { get; set; }

    public void Write(BinaryWriter writer)
    {
        if (Data is null)
            throw new NullReferenceException();
        writer.Write(Shared.Swap(dataId));
        writer.Write(Data.Length);
        writer.Write(Data);
    }

    public void Read(BinaryReader reader)
    {
        var dataSize =
            Shared.Swap(reader.ReadUInt32()) == dataId
                ? reader.ReadUInt32()
                : throw new Exception("Wrong chunk ID!");
        Data = reader.ReadBytes((int)dataSize);
    }
}

internal class WaveSmplChunk
{
    private readonly uint smplId = 1936552044;
    private uint smplSize = 36;
    private uint manufacturer;
    private uint product;
    private uint samplePeriod;
    private uint unityNote = 60;
    private uint pitchFraction;
    private uint smpteFormat;
    private uint smpteOffset;
    private uint numLoops;
    private uint samplerData;
    private readonly List<WaveSmplLoop> smplLoops = [];

    public uint SmplSize => smplSize;

    public uint NumLoops => numLoops;

    public WaveSmplLoop[] Loops => [.. smplLoops];

    public void AddLoop(int loopStartSample, int loopEndSample)
    {
        RemoveAllLoops();
        ++numLoops;
        smplLoops.Add(
            new WaveSmplLoop() { LoopStart = (uint)loopStartSample, LoopEnd = (uint)loopEndSample }
        );
    }

    public void RemoveAllLoops()
    {
        smplLoops.Clear();
        numLoops = 0U;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Shared.Swap(smplId));
        writer.Write(smplSize);
        writer.Write(manufacturer);
        writer.Write(product);
        writer.Write(samplePeriod);
        writer.Write(unityNote);
        writer.Write(pitchFraction);
        writer.Write(smpteFormat);
        writer.Write(smpteOffset);
        writer.Write(numLoops);
        writer.Write(samplerData);
        for (int index = 0; index < numLoops; ++index)
        {
            smplLoops[index].Write(writer);
        }
    }

    public void Read(BinaryReader reader)
    {
        smplSize =
            (int)Shared.Swap(reader.ReadUInt32()) == (int)smplId
                ? reader.ReadUInt32()
                : throw new Exception("Wrong chunk ID!");
        manufacturer = reader.ReadUInt32();
        product = reader.ReadUInt32();
        samplePeriod = reader.ReadUInt32();
        unityNote = reader.ReadUInt32();
        pitchFraction = reader.ReadUInt32();
        smpteFormat = reader.ReadUInt32();
        smpteOffset = reader.ReadUInt32();
        numLoops = reader.ReadUInt32();
        samplerData = reader.ReadUInt32();
        for (int index = 0; index < numLoops; ++index)
        {
            WaveSmplLoop waveSmplLoop = new();
            waveSmplLoop.Read(reader);
            smplLoops.Add(waveSmplLoop);
        }
    }
}

internal class WaveSmplLoop
{
    private uint cuePointId;
    private uint type;
    private uint start;
    private uint end;
    private uint fraction;
    private uint playCount;

    public uint LoopStart
    {
        get => start;
        set => start = value;
    }

    public uint LoopEnd
    {
        get => end;
        set => end = value;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(cuePointId);
        writer.Write(type);
        writer.Write(start);
        writer.Write(end);
        writer.Write(fraction);
        writer.Write(playCount);
    }

    public void Read(BinaryReader reader)
    {
        cuePointId = reader.ReadUInt32();
        type = reader.ReadUInt32();
        start = reader.ReadUInt32();
        end = reader.ReadUInt32();
        fraction = reader.ReadUInt32();
        playCount = reader.ReadUInt32();
    }
}
