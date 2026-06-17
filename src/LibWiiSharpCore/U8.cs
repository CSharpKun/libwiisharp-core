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
using System.Security.Cryptography;
using System.Text;

namespace LibWiiSharpCore
{
    public enum U8_NodeType : ushort
    {
        File = 0,
        Directory = 256, // 0x0100
    }

    public class U8
    {
        //private const int dataPadding = 32;
        private U8_Header u8Header = new();
        private List<string> stringTable = [];
        private List<byte[]> data = [];

        public Headers.HeaderType HeaderType { get; private set; }

        public object? Header { get; private set; }

        public U8_Node RootNode { get; private set; } = new U8_Node();

        public List<U8_Node> Nodes { get; private set; } = [];

        public string[] StringTable => [.. stringTable];

        public byte[][] Data => [.. data];

        public int NumOfNodes => (int)RootNode.SizeOfData - 1;

        public int IconSize { get; private set; } = -1;

        public int BannerSize { get; private set; } = -1;

        public int SoundSize { get; private set; } = -1;

        public bool Lz77Compress { get; set; }

        public event EventHandler<ProgressChangedEventArgs>? Progress;

        public event EventHandler<MessageEventArgs>? Warning;

        public event EventHandler<MessageEventArgs>? Debug;

        public U8()
        {
            RootNode.Type = U8_NodeType.Directory;
        }

        public static bool IsU8(string pathToFile)
        {
            return IsU8(File.ReadAllBytes(pathToFile));
        }

        public static bool IsU8(byte[] file)
        {
            if (Lz77.IsLz77Compressed(file))
            {
                byte[] file1 = new byte[file.Length > 2000 ? 2000 : file.Length];
                for (int index = 0; index < file1.Length; ++index)
                {
                    file1[index] = file[index];
                }

                return IsU8(new Lz77().Decompress(file1));
            }
            Headers.HeaderType headerType = Headers.DetectHeader(file);
            return Shared.Swap(BitConverter.ToUInt32(file, (int)headerType)) == 1437218861U;
        }

        public static U8 Load(string pathToU8)
        {
            return Load(File.ReadAllBytes(pathToU8));
        }

        public static U8 Load(byte[] u8File)
        {
            U8 u8 = new();
            MemoryStream memoryStream = new(u8File);
            try
            {
                u8.ParseU8(memoryStream);
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
            memoryStream.Dispose();
            return u8;
        }

        public static U8 Load(Stream u8File)
        {
            U8 u8 = new();
            u8.ParseU8(u8File);
            return u8;
        }

        public static U8 FromDirectory(string pathToDirectory)
        {
            U8 u8 = new();
            u8.CreateFromDir(pathToDirectory);
            return u8;
        }

        public void LoadFile(string pathToU8)
        {
            LoadFile(File.ReadAllBytes(pathToU8));
        }

        public void LoadFile(byte[] u8File)
        {
            MemoryStream memoryStream = new(u8File);
            try
            {
                ParseU8(memoryStream);
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
            memoryStream.Dispose();
        }

        public void LoadFile(Stream u8File)
        {
            ParseU8(u8File);
        }

        public void CreateFromDirectory(string pathToDirectory)
        {
            CreateFromDir(pathToDirectory);
        }

        public void Save(string savePath)
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            using FileStream fileStream = new(savePath, FileMode.Create);
            WriteToStream(fileStream);
        }

        public MemoryStream ToMemoryStream()
        {
            MemoryStream memoryStream = new();
            try
            {
                WriteToStream(memoryStream);
                return memoryStream;
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
        }

        public byte[] ToByteArray()
        {
            return ToMemoryStream().ToArray();
        }

        public void Unpack(string saveDir)
        {
            UnpackToDir(saveDir);
        }

        public void Extract(string saveDir)
        {
            UnpackToDir(saveDir);
        }

        public void AddHeaderImet(bool shortImet, params string[] titles)
        {
            if (IconSize == -1)
            {
                throw new Exception("icon.bin wasn't found!");
            }

            if (BannerSize == -1)
            {
                throw new Exception("banner.bin wasn't found!");
            }

            if (SoundSize == -1)
            {
                throw new Exception("sound.bin wasn't found!");
            }

            Header = Headers.IMET.Create(shortImet, IconSize, BannerSize, SoundSize, titles);
            HeaderType = shortImet ? Headers.HeaderType.ShortIMET : Headers.HeaderType.IMET;
        }

        public void AddHeaderImd5()
        {
            HeaderType = Headers.HeaderType.IMD5;
        }

        public void ReplaceFile(int fileIndex, string pathToNewFile, bool changeFileName = false)
        {
            if (Nodes[fileIndex].Type == U8_NodeType.Directory)
            {
                throw new Exception("You can't replace a directory with a file!");
            }

            data[fileIndex] = File.ReadAllBytes(pathToNewFile);
            if (changeFileName)
            {
                stringTable[fileIndex] = Path.GetFileName(pathToNewFile);
            }

            if (stringTable[fileIndex].Equals("icon.bin", StringComparison.OrdinalIgnoreCase))
            {
                IconSize = GetRealSize(File.ReadAllBytes(pathToNewFile));
            }
            else if (
                stringTable[fileIndex].Equals("banner.bin", StringComparison.OrdinalIgnoreCase)
            )
            {
                BannerSize = GetRealSize(File.ReadAllBytes(pathToNewFile));
            }
            else
            {
                if (
                    !(
                        stringTable[fileIndex]
                            .Equals("sound.bin", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    return;
                }

                SoundSize = GetRealSize(File.ReadAllBytes(pathToNewFile));
            }
        }

        public void ReplaceFile(int fileIndex, byte[] newData)
        {
            if (Nodes[fileIndex].Type == U8_NodeType.Directory)
            {
                throw new Exception("You can't replace a directory with a file!");
            }

            data[fileIndex] = newData;
            if (stringTable[fileIndex].Equals("icon.bin", StringComparison.OrdinalIgnoreCase))
            {
                IconSize = GetRealSize(newData);
            }
            else if (
                stringTable[fileIndex].Equals("banner.bin", StringComparison.OrdinalIgnoreCase)
            )
            {
                BannerSize = GetRealSize(newData);
            }
            else
            {
                if (
                    !(
                        stringTable[fileIndex]
                            .Equals("sound.bin", StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    return;
                }

                SoundSize = GetRealSize(newData);
            }
        }

        public int GetNodeIndex(string fileOrDirName)
        {
            for (int index = 0; index < Nodes.Count; ++index)
            {
                if (stringTable[index].Equals(fileOrDirName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        public void RenameNode(int index, string newName)
        {
            stringTable[index] = newName;
        }

        public void RenameNode(string oldName, string newName)
        {
            stringTable[GetNodeIndex(oldName)] = newName;
        }

        public void AddDirectory(string path)
        {
            AddEntry(path, []);
        }

        public void AddFile(string path, byte[] data)
        {
            AddEntry(path, data);
        }

        public void RemoveDirectory(string path)
        {
            RemoveEntry(path);
        }

        public void RemoveFile(string path)
        {
            RemoveEntry(path);
        }

        private void WriteToStream(Stream writeStream)
        {
            FireDebug("Writing U8 File...");
            FireDebug("   Updating Rootnode...");
            RootNode.SizeOfData = (uint)(Nodes.Count + 1);
            MemoryStream memoryStream = new();
            memoryStream.Seek(u8Header.OffsetToRootNode + (Nodes.Count + 1) * 12, SeekOrigin.Begin);
            FireDebug(
                "   Writing String Table... (Offset: 0x{0})",
                (object)memoryStream.Position.ToString("x8").ToUpper()
            );
            memoryStream.WriteByte(0);
            int num = (int)memoryStream.Position - 1;
            long position;
            for (int index = 0; index < Nodes.Count; ++index)
            {
                object[] objArray = new object[4];
                position = memoryStream.Position;
                objArray[0] = position.ToString("x8").ToUpper();
                objArray[1] = index + 1;
                objArray[2] = Nodes.Count;
                objArray[3] = stringTable[index];
                FireDebug("    -> Entry #{1} of {2}: \"{3}\"... (Offset: 0x{0})", objArray);
                Nodes[index].OffsetToName = (ushort)((ulong)memoryStream.Position - (ulong)num);
                byte[] bytes = Encoding.ASCII.GetBytes(stringTable[index]);
                memoryStream.Write(bytes, 0, bytes.Length);
                memoryStream.WriteByte(0);
            }
            u8Header.HeaderSize = (uint)((ulong)memoryStream.Position - u8Header.OffsetToRootNode);
            u8Header.OffsetToData = 0U;
            for (int index = 0; index < Nodes.Count; ++index)
            {
                FireProgress((index + 1) * 100 / Nodes.Count);
                if (Nodes[index].Type == U8_NodeType.File)
                {
                    memoryStream.Seek(
                        Shared.AddPadding((int)memoryStream.Position, 32),
                        SeekOrigin.Begin
                    );
                    object[] objArray = new object[3];
                    position = memoryStream.Position;
                    objArray[0] = position.ToString("x8").ToUpper();
                    objArray[1] = index + 1;
                    objArray[2] = Nodes.Count;
                    FireDebug("   Writing Data #{1} of {2}... (Offset: 0x{0})", objArray);
                    if (u8Header.OffsetToData == 0U)
                    {
                        u8Header.OffsetToData = (uint)memoryStream.Position;
                    }

                    Nodes[index].OffsetToData = (uint)memoryStream.Position;
                    Nodes[index].SizeOfData = (uint)data[index].Length;
                    memoryStream.Write(data[index], 0, data[index].Length);
                }
                else
                {
                    FireDebug("   Node #{0} of {1} is a Directory...", index + 1, Nodes.Count);
                }
            }
            while (memoryStream.Position % 16L != 0L)
            {
                memoryStream.WriteByte(0);
            }

            memoryStream.Seek(0L, SeekOrigin.Begin);
            object[] objArray1 = new object[1];
            position = memoryStream.Position;
            objArray1[0] = position.ToString("x8").ToUpper();
            FireDebug("   Writing Header... (Offset: 0x{0})", objArray1);
            u8Header.Write(memoryStream);
            object[] objArray2 = new object[1];
            position = memoryStream.Position;
            objArray2[0] = position.ToString("x8").ToUpper();
            FireDebug("   Writing Rootnode... (Offset: 0x{0})", objArray2);
            RootNode.Write(memoryStream);
            for (int index = 0; index < Nodes.Count; ++index)
            {
                object[] objArray3 = new object[3];
                position = memoryStream.Position;
                objArray3[0] = position.ToString("x8").ToUpper();
                objArray3[1] = index + 1;
                objArray3[2] = Nodes.Count;
                FireDebug("   Writing Node Entry #{1} of {2}... (Offset: 0x{0})", objArray3);
                Nodes[index].Write(memoryStream);
            }
            byte[] numArray = memoryStream.ToArray();
            memoryStream.Dispose();
            if (Lz77Compress)
            {
                FireDebug("   Lz77 Compressing U8 File...");
                numArray = new Lz77().Compress(numArray);
            }
            if (HeaderType == Headers.HeaderType.IMD5)
            {
                FireDebug("   Adding IMD5 Header...");
                writeStream.Seek(0L, SeekOrigin.Begin);
                Headers.IMD5.Create(numArray).Write(writeStream);
            }
            else if (
                (
                    HeaderType == Headers.HeaderType.IMET
                    || HeaderType == Headers.HeaderType.ShortIMET
                ) && Header is not null
            )
            {
                FireDebug("   Adding IMET Header...");
                ((Headers.IMET)Header).IconSize = (uint)IconSize;
                ((Headers.IMET)Header).BannerSize = (uint)BannerSize;
                ((Headers.IMET)Header).SoundSize = (uint)SoundSize;
                writeStream.Seek(0L, SeekOrigin.Begin);
                ((Headers.IMET)Header).Write(writeStream);
            }
            writeStream.Write(numArray, 0, numArray.Length);
            FireDebug("Writing U8 File Finished...");
        }

        private void UnpackToDir(string saveDir)
        {
            FireDebug("Unpacking U8 File to: {0}", (object)saveDir);
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            string[] strArray = new string[Nodes.Count];
            strArray[0] = saveDir;
            int[] numArray = new int[Nodes.Count];
            int index1 = 0;
            for (int index2 = 0; index2 < Nodes.Count; ++index2)
            {
                FireDebug("   Unpacking Entry #{0} of {1}", index2 + 1, Nodes.Count);
                FireProgress((index2 + 1) * 100 / Nodes.Count);
                if (Nodes[index2].Type == U8_NodeType.Directory)
                {
                    FireDebug("    -> Directory: \"{0}\"", (object)stringTable[index2]);
                    if (strArray[index1][^1] != Path.DirectorySeparatorChar)
                    {
                        // ISSUE: explicit reference operation
                        strArray[index1] += Path.DirectorySeparatorChar.ToString();
                    }
                    Directory.CreateDirectory(strArray[index1] + stringTable[index2]);
                    strArray[index1 + 1] = strArray[index1] + stringTable[index2];
                    ++index1;
                    numArray[index1] = (int)Nodes[index2].SizeOfData;
                }
                else
                {
                    FireDebug("    -> File: \"{0}\"", (object)stringTable[index2]);
                    FireDebug("    -> Size: {0} bytes", (object)data[index2].Length);
                    using FileStream fileStream = new(
                        strArray[index1]
                            + Path.DirectorySeparatorChar.ToString()
                            + stringTable[index2],
                        FileMode.Create
                    );
                    fileStream.Write(data[index2], 0, data[index2].Length);
                }
                while (index1 > 0 && numArray[index1] == index2 + 2)
                {
                    --index1;
                }
            }
            FireDebug("Unpacking U8 File Finished");
        }

        private void ParseU8(Stream u8File)
        {
            FireDebug("Pasing U8 File...");
            u8Header = new U8_Header();
            RootNode = new U8_Node();
            Nodes = [];
            stringTable = [];
            data = [];
            FireDebug("   Detecting Header...");
            HeaderType = Headers.DetectHeader(u8File);
            Headers.HeaderType headerType = HeaderType;
            FireDebug("    -> {0}", (object)HeaderType.ToString());
            if (HeaderType == Headers.HeaderType.IMD5)
            {
                FireDebug("   Reading IMD5 Header...");
                Header = Headers.IMD5.Load(u8File);
                byte[] buffer = new byte[u8File.Length];
                u8File.ReadExactly(buffer);
                MD5 md5 = MD5.Create();
                byte[] hash1 = md5.ComputeHash(
                    buffer,
                    (int)HeaderType,
                    (int)((int)u8File.Length - HeaderType)
                );
                md5.Clear();
                byte[] hash2 = ((Headers.IMD5)Header).Hash;
                if (!Shared.CompareByteArrays(hash1, hash2))
                {
                    FireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                    FireWarning(
                        "Hashes of IMD5 header and file do not match! The content might be corrupted!"
                    );
                }
            }
            else if (
                HeaderType == Headers.HeaderType.IMET
                || HeaderType == Headers.HeaderType.ShortIMET
            )
            {
                FireDebug("   Reading IMET Header...");
                Header = Headers.IMET.Load(u8File);
                if (!((Headers.IMET)Header).HashesMatch)
                {
                    FireDebug("/!\\ /!\\ /!\\ Hashes do not match /!\\ /!\\ /!\\");
                    FireWarning(
                        "The hash stored in the IMET header doesn't match the headers hash! The header and/or file might be corrupted!"
                    );
                }
            }
            FireDebug("   Checking for Lz77 Compression...");
            if (Lz77.IsLz77Compressed(u8File))
            {
                FireDebug("    -> Lz77 Compression Found...");
                FireDebug("   Decompressing U8 Data...");
                Stream file = new Lz77().Decompress(u8File);
                headerType = Headers.DetectHeader(file);
                u8File = file;
                Lz77Compress = true;
            }
            u8File.Seek((long)headerType, SeekOrigin.Begin);
            byte[] buffer1 = new byte[4];
            FireDebug(
                "   Reading U8 Header: Magic... (Offset: 0x{0})",
                (object)u8File.Position.ToString("x8").ToUpper()
            );
            u8File.ReadExactly(buffer1);
            if ((int)Shared.Swap(BitConverter.ToUInt32(buffer1, 0)) != (int)u8Header.U8Magic)
            {
                FireDebug("    -> Invalid Magic!");
                throw new Exception("U8 Header: Invalid Magic!");
            }
            FireDebug(
                "   Reading U8 Header: Offset to Rootnode... (Offset: 0x{0})",
                (object)u8File.Position.ToString("x8").ToUpper()
            );
            u8File.ReadExactly(buffer1);
            if (
                (int)Shared.Swap(BitConverter.ToUInt32(buffer1, 0))
                != (int)u8Header.OffsetToRootNode
            )
            {
                FireDebug("    -> Invalid Offset to Rootnode");
                throw new Exception("U8 Header: Invalid Offset to Rootnode!");
            }
            FireDebug(
                "   Reading U8 Header: Header Size... (Offset: 0x{0})",
                (object)u8File.Position.ToString("x8").ToUpper()
            );
            u8File.ReadExactly(buffer1);
            u8Header.HeaderSize = Shared.Swap(BitConverter.ToUInt32(buffer1, 0));
            FireDebug(
                "   Reading U8 Header: Offset to Data... (Offset: 0x{0})",
                (object)u8File.Position.ToString("x8").ToUpper()
            );
            u8File.ReadExactly(buffer1);
            u8Header.OffsetToData = Shared.Swap(BitConverter.ToUInt32(buffer1, 0));
            u8File.Seek(16L, SeekOrigin.Current);
            object[] objArray1 = new object[1];
            long position1 = u8File.Position;
            objArray1[0] = position1.ToString("x8").ToUpper();
            FireDebug("   Reading Rootnode... (Offset: 0x{0})", objArray1);
            u8File.ReadExactly(buffer1);
            RootNode.Type = (U8_NodeType)Shared.Swap(BitConverter.ToUInt16(buffer1, 0));
            RootNode.OffsetToName = Shared.Swap(BitConverter.ToUInt16(buffer1, 2));
            u8File.ReadExactly(buffer1);
            RootNode.OffsetToData = Shared.Swap(BitConverter.ToUInt32(buffer1, 0));
            u8File.ReadExactly(buffer1);
            RootNode.SizeOfData = Shared.Swap(BitConverter.ToUInt32(buffer1, 0));
            int num = (int)(
                (long)headerType + u8Header.OffsetToRootNode + RootNode.SizeOfData * 12U
            );
            int position2 = (int)u8File.Position;
            for (int index = 0; index < RootNode.SizeOfData - 1U; ++index)
            {
                object[] objArray2 = new object[3];
                position1 = u8File.Position;
                objArray2[0] = position1.ToString("x8").ToUpper();
                objArray2[1] = index + 1;
                objArray2[2] = (uint)((int)RootNode.SizeOfData - 1);
                FireDebug("   Reading Node #{1} of {2}... (Offset: 0x{0})", objArray2);
                FireProgress((int)((index + 1) * 100 / (RootNode.SizeOfData - 1U)));
                U8_Node u8Node = new();
                string empty = string.Empty;
                byte[] numArray = [];
                u8File.Seek(position2, SeekOrigin.Begin);
                object[] objArray3 = new object[1];
                position1 = u8File.Position;
                objArray3[0] = position1.ToString("x8").ToUpper();
                FireDebug("    -> Reading Node Entry... (Offset: 0x{0})", objArray3);
                u8File.ReadExactly(buffer1);
                u8Node.Type = (U8_NodeType)Shared.Swap(BitConverter.ToUInt16(buffer1, 0));
                u8Node.OffsetToName = Shared.Swap(BitConverter.ToUInt16(buffer1, 2));
                u8File.ReadExactly(buffer1);
                u8Node.OffsetToData = Shared.Swap(BitConverter.ToUInt32(buffer1, 0));
                u8File.ReadExactly(buffer1);
                u8Node.SizeOfData = Shared.Swap(BitConverter.ToUInt32(buffer1, 0));
                position2 = (int)u8File.Position;
                FireDebug("        -> {0}", (object)u8Node.Type.ToString());
                u8File.Seek(num + u8Node.OffsetToName, SeekOrigin.Begin);
                object[] objArray4 = new object[1];
                position1 = u8File.Position;
                objArray4[0] = position1.ToString("x8").ToUpper();
                FireDebug("    -> Reading Node Name... (Offset: 0x{0})", objArray4);
                do
                {
                    char ch = (char)u8File.ReadByte();
                    if (ch != char.MinValue)
                    {
                        empty += ch.ToString();
                    }
                    else
                    {
                        break;
                    }
                } while (empty.Length <= byte.MaxValue);
                FireDebug("        -> {0}", (object)empty);
                if (u8Node.Type == U8_NodeType.File)
                {
                    u8File.Seek((long)headerType + u8Node.OffsetToData, SeekOrigin.Begin);
                    object[] objArray5 = new object[1];
                    position1 = u8File.Position;
                    objArray5[0] = position1.ToString("x8").ToUpper();
                    FireDebug("    -> Reading Node Data (Offset: 0x{0})", objArray5);
                    numArray = new byte[(int)u8Node.SizeOfData];
                    u8File.ReadExactly(numArray);
                }
                if (empty.Equals("icon.bin", StringComparison.OrdinalIgnoreCase))
                {
                    IconSize = GetRealSize(numArray);
                }
                else if (empty.Equals("banner.bin", StringComparison.OrdinalIgnoreCase))
                {
                    BannerSize = GetRealSize(numArray);
                }
                else if (empty.Equals("sound.bin", StringComparison.OrdinalIgnoreCase))
                {
                    SoundSize = GetRealSize(numArray);
                }

                Nodes.Add(u8Node);
                stringTable.Add(empty);
                data.Add(numArray);
            }
            FireDebug("Pasing U8 File Finished...");
        }

        private void CreateFromDir(string path)
        {
            FireDebug("Creating U8 File from: {0}", (object)path);
            if (path[^1] != Path.DirectorySeparatorChar)
            {
                path += Path.DirectorySeparatorChar.ToString();
            }

            FireDebug("   Collecting Content...");
            string[] dirContent = GetDirContent(path, true);
            int num1 = 1;
            int num2 = 0;
            FireDebug("   Creating U8 Header...");
            u8Header = new U8_Header();
            RootNode = new U8_Node();
            Nodes = [];
            stringTable = [];
            data = [];
            FireDebug("   Creating Rootnode...");
            RootNode.Type = U8_NodeType.Directory;
            RootNode.OffsetToName = 0;
            RootNode.OffsetToData = 0U;
            RootNode.SizeOfData = (uint)(dirContent.Length + 1);
            for (int index1 = 0; index1 < dirContent.Length; ++index1)
            {
                FireDebug("   Creating Node #{0} of {1}", index1 + 1, dirContent.Length);
                FireProgress((index1 + 1) * 100 / dirContent.Length);
                U8_Node u8Node = new();
                byte[] data = [];
                string theString = dirContent[index1][(path.Length - 1)..];
                if (Directory.Exists(dirContent[index1]))
                {
                    FireDebug("    -> Directory");
                    u8Node.Type = U8_NodeType.Directory;
                    u8Node.OffsetToData = (uint)
                        Shared.CountCharsInString(theString, Path.DirectorySeparatorChar);
                    int num3 = Nodes.Count + 2;
                    for (int index2 = 0; index2 < dirContent.Length; ++index2)
                    {
                        if (
                            dirContent[index2]
                                .Contains(dirContent[index1] + Path.DirectorySeparatorChar)
                        )
                        {
                            ++num3;
                        }
                    }
                    u8Node.SizeOfData = (uint)num3;
                }
                else
                {
                    FireDebug("    -> File");
                    FireDebug("    -> Reading File Data...");
                    data = File.ReadAllBytes(dirContent[index1]);
                    u8Node.Type = U8_NodeType.File;
                    u8Node.OffsetToData = (uint)num2;
                    u8Node.SizeOfData = (uint)data.Length;
                    num2 += Shared.AddPadding(num2 + data.Length, 32);
                }
                u8Node.OffsetToName = (ushort)num1;
                num1 += Path.GetFileName(dirContent[index1]).Length + 1;
                FireDebug("    -> Reading Name...");
                string fileName = Path.GetFileName(dirContent[index1]);
                if (fileName.Equals("icon.bin", StringComparison.OrdinalIgnoreCase))
                {
                    IconSize = GetRealSize(data);
                }
                else if (fileName.Equals("banner.bin", StringComparison.OrdinalIgnoreCase))
                {
                    BannerSize = GetRealSize(data);
                }
                else if (fileName.Equals("sound.bin", StringComparison.OrdinalIgnoreCase))
                {
                    SoundSize = GetRealSize(data);
                }

                Nodes.Add(u8Node);
                stringTable.Add(fileName);
                this.data.Add(data);
            }
            FireDebug("   Updating U8 Header...");
            u8Header.HeaderSize = (uint)((Nodes.Count + 1) * 12 + num1);
            u8Header.OffsetToData = (uint)
                Shared.AddPadding((int)u8Header.OffsetToRootNode + (int)u8Header.HeaderSize, 32);
            FireDebug("   Calculating Data Offsets...");
            for (int index = 0; index < Nodes.Count; ++index)
            {
                FireDebug("    -> Node #{0} of {1}...", index + 1, Nodes.Count);
                int offsetToData = (int)Nodes[index].OffsetToData;
                Nodes[index].OffsetToData = (uint)(u8Header.OffsetToData + (ulong)offsetToData);
            }
            FireDebug("Creating U8 File Finished...");
        }

        private static string[] GetDirContent(string dir, bool root)
        {
            string[] files = Directory.GetFiles(dir);
            string[] directories = Directory.GetDirectories(dir);
            string str1 = "";
            if (!root)
            {
                str1 = str1 + dir + "\n";
            }

            for (int index = 0; index < files.Length; ++index)
            {
                str1 = str1 + files[index] + "\n";
            }

            foreach (string dir1 in directories)
            {
                foreach (string str2 in GetDirContent(dir1, false))
                {
                    str1 = str1 + str2 + "\n";
                }
            }
            return str1.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
        }

        private static int GetRealSize(byte[] data)
        {
            if (data[0] != 73 || data[1] != 77 || (data[2] != 68 || data[3] != 53))
            {
                return data.Length;
            }

            return data[32] == 76 && data[33] == 90 && (data[34] == 55 && data[35] == 55)
                ? BitConverter.ToInt32(data, 36) >> 8
                : data.Length - 32;
        }

        private void AddEntry(string nodePath, byte[] fileData)
        {
            if (nodePath.StartsWith('/'))
            {
                nodePath = nodePath[1..];
            }

            string[] strArray = nodePath.Split('/');
            int index1 = -1;
            int num1 = Nodes.Count > 0 ? Nodes.Count - 1 : 0;
            int num2 = 0;
            List<int> intList = [];
            for (int index2 = 0; index2 < strArray.Length - 1; ++index2)
            {
                for (int index3 = num2; index3 <= num1; ++index3)
                {
                    if (
                        !(
                            stringTable[index3]
                                .Equals(strArray[index2], StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        if (index3 == num1 - 1)
                        {
                            throw new Exception("Path wasn't found!");
                        }
                    }
                    else
                    {
                        if (index2 == strArray.Length - 2)
                        {
                            index1 = index3;
                        }

                        num1 = (int)Nodes[index3].SizeOfData - 1;
                        num2 = index3 + 1;
                        intList.Add(index3);
                        break;
                    }
                }
            }
            int num3 =
                index1 > -1
                    ? (int)Nodes[index1].SizeOfData - 2
                    : (RootNode.SizeOfData > 1U ? (int)RootNode.SizeOfData - 2 : -1);
            U8_Node u8Node = new()
            {
                Type = fileData.Length == 0 ? U8_NodeType.Directory : U8_NodeType.File,
                SizeOfData = fileData.Length == 0 ? (uint)(num3 + 2) : (uint)fileData.Length,
                OffsetToData =
                    fileData.Length == 0 ? (uint)Shared.CountCharsInString(nodePath, '/') : 0U,
            };
            stringTable.Insert(num3 + 1, strArray[^1]);
            Nodes.Insert(num3 + 1, u8Node);
            data.Insert(num3 + 1, fileData);
            ++RootNode.SizeOfData;
            foreach (int index2 in intList)
            {
                if (Nodes[index2].Type == U8_NodeType.Directory)
                {
                    ++Nodes[index2].SizeOfData;
                }
            }
            for (int index2 = num3 + 1; index2 < Nodes.Count; ++index2)
            {
                if (Nodes[index2].Type == U8_NodeType.Directory)
                {
                    ++Nodes[index2].SizeOfData;
                }
            }
        }

        private void RemoveEntry(string nodePath)
        {
            if (nodePath.StartsWith('/'))
            {
                nodePath = nodePath[1..];
            }

            string[] strArray = nodePath.Split('/');
            int index1 = -1;
            int num1 = Nodes.Count - 1;
            int num2 = 0;
            List<int> intList = [];
            for (int index2 = 0; index2 < strArray.Length; ++index2)
            {
                for (int index3 = num2; index3 < num1; ++index3)
                {
                    if (
                        !(
                            stringTable[index3]
                                .Equals(strArray[index2], StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        if (index3 == num1 - 1)
                        {
                            throw new Exception("Path wasn't found!");
                        }
                    }
                    else
                    {
                        if (index2 == strArray.Length - 1)
                        {
                            index1 = index3;
                        }
                        else
                        {
                            intList.Add(index3);
                        }

                        num1 = (int)Nodes[index3].SizeOfData - 1;
                        num2 = index3 + 1;
                        break;
                    }
                }
            }
            int num3 = 0;
            if (Nodes[index1].Type == U8_NodeType.Directory)
            {
                for (int index2 = (int)Nodes[index1].SizeOfData - 2; index2 >= index1; --index2)
                {
                    stringTable.RemoveAt(index2);
                    Nodes.RemoveAt(index2);
                    data.RemoveAt(index2);
                    ++num3;
                }
            }
            else
            {
                stringTable.RemoveAt(index1);
                Nodes.RemoveAt(index1);
                data.RemoveAt(index1);
                ++num3;
            }
            RootNode.SizeOfData -= (uint)num3;
            foreach (int index2 in intList)
            {
                if (Nodes[index2].Type == U8_NodeType.Directory)
                {
                    Nodes[index2].SizeOfData -= (uint)num3;
                }
            }
            for (int index2 = index1 + 1; index2 < Nodes.Count; ++index2)
            {
                if (Nodes[index2].Type == U8_NodeType.Directory)
                {
                    Nodes[index2].SizeOfData -= (uint)num3;
                }
            }
        }

        private void FireWarning(string warningMessage)
        {
            EventHandler<MessageEventArgs>? warning = Warning;
            if (warning == null)
            {
                return;
            }

            warning(new object(), new MessageEventArgs(warningMessage));
        }

        private void FireDebug(string debugMessage, params object[] args)
        {
            EventHandler<MessageEventArgs>? debug = Debug;
            if (debug == null)
            {
                return;
            }

            debug(new object(), new MessageEventArgs(string.Format(debugMessage, args)));
        }

        private void FireProgress(int progressPercentage)
        {
            EventHandler<ProgressChangedEventArgs>? progress = Progress;
            if (progress == null)
            {
                return;
            }

            progress(new object(), new ProgressChangedEventArgs(progressPercentage, string.Empty));
        }
    }

    public class U8_Header
    {
        private readonly uint u8Magic = 1437218861;
        private readonly uint offsetToRootNode = 32;
        private uint headerSize;
        private uint offsetToData;
        private readonly byte[] padding = new byte[16];

        public uint U8Magic => u8Magic;

        public uint OffsetToRootNode => offsetToRootNode;

        public uint HeaderSize
        {
            get => headerSize;
            set => headerSize = value;
        }

        public uint OffsetToData
        {
            get => offsetToData;
            set => offsetToData = value;
        }

        public byte[] Padding => padding;

        public void Write(Stream writeStream)
        {
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(u8Magic)), 0, 4);
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(offsetToRootNode)), 0, 4);
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(headerSize)), 0, 4);
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(offsetToData)), 0, 4);
            writeStream.Write(padding, 0, 16);
        }
    }

    public class U8_Node
    {
        private ushort type;
        private ushort offsetToName;
        private uint offsetToData;
        private uint sizeOfData;

        public U8_NodeType Type
        {
            get => (U8_NodeType)type;
            set => type = (ushort)value;
        }

        public ushort OffsetToName
        {
            get => offsetToName;
            set => offsetToName = value;
        }

        public uint OffsetToData
        {
            get => offsetToData;
            set => offsetToData = value;
        }

        public uint SizeOfData
        {
            get => sizeOfData;
            set => sizeOfData = value;
        }

        public void Write(Stream writeStream)
        {
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(type)), 0, 2);
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(offsetToName)), 0, 2);
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(offsetToData)), 0, 4);
            writeStream.Write(BitConverter.GetBytes(Shared.Swap(sizeOfData)), 0, 4);
        }
    }
}
