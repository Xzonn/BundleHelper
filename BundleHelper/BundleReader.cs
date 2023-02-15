using K4os.Compression.LZ4;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;

namespace BundleHelper
{
    public partial class Bundle
    {
        public Bundle(EndianBinaryReader reader)
        {
            reader.Position = 0;

            ReadHeader(reader);
            ReadBlocksInfoAndDirectory(reader);
            using (var blocksStream = CreateBlocksStream())
            {
                ReadBlocks(reader, blocksStream);
                ReadFiles(blocksStream);
            }
        }

        private void ReadHeader(EndianBinaryReader reader)
        {
            Signature = reader.ReadStringToNull();
            Debug.Assert(Signature == "UnityFS");
            Version = reader.ReadUInt32BE();
            Debug.Assert(Version >= 6);
            UnityVersion = reader.ReadStringToNull();
            UnityRevision = reader.ReadStringToNull();

            Size = reader.ReadInt64BE();
            CompressedBlocksInfoSize = reader.ReadUInt32BE();
            UncompressedBlocksInfoSize = reader.ReadUInt32BE();
            Flags = (ArchiveFlags)reader.ReadUInt32BE();
        }

        private void ReadBlocksInfoAndDirectory(EndianBinaryReader reader)
        {
            byte[] blocksInfoBytes;
            if (Version >= 7)
            {
                reader.AlignStream(16);
            }
            if ((Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0)
            {
                var position = reader.Position;
                reader.Position = reader.BaseStream.Length - CompressedBlocksInfoSize;
                blocksInfoBytes = reader.ReadBytes((int)CompressedBlocksInfoSize);
                reader.Position = position;
            }
            else //0x40 BlocksAndDirectoryInfoCombined
            {
                blocksInfoBytes = reader.ReadBytes((int)CompressedBlocksInfoSize);
            }
            MemoryStream blocksInfoUncompresseddStream;
            var uncompressedSize = UncompressedBlocksInfoSize;
            var compressionType = (CompressionType)(Flags & ArchiveFlags.CompressionTypeMask);
            switch (compressionType)
            {
                case CompressionType.None:
                    blocksInfoUncompresseddStream = new MemoryStream(blocksInfoBytes);
                    break;
                case CompressionType.Lzma:
                    goto default;
                case CompressionType.Lz4:
                case CompressionType.Lz4HC:
                    var uncompressedBytes = new byte[uncompressedSize];
                    var numWrite = LZ4Codec.Decode(blocksInfoBytes, 0, (int)CompressedBlocksInfoSize, uncompressedBytes, 0, (int)uncompressedSize);
                    if (numWrite != uncompressedSize)
                    {
                        throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                    }
                    blocksInfoUncompresseddStream = new MemoryStream(uncompressedBytes);
                    break;
                default:
                    throw new IOException($"Unsupported compression type {compressionType}");
            }
            using (var blocksInfoReader = new EndianBinaryReader(blocksInfoUncompresseddStream))
            {
                BlocksUncompressedDataHash = blocksInfoReader.ReadBytes(16);
                var blocksInfoCount = blocksInfoReader.ReadInt32BE();
                BlocksInfo = new StorageBlock[blocksInfoCount];
                for (int i = 0; i < blocksInfoCount; i++)
                {
                    BlocksInfo[i] = new StorageBlock
                    {
                        uncompressedSize = blocksInfoReader.ReadUInt32BE(),
                        compressedSize = blocksInfoReader.ReadUInt32BE(),
                        flags = (StorageBlockFlags)blocksInfoReader.ReadUInt16BE()
                    };
                }

                var nodesCount = blocksInfoReader.ReadInt32BE();
                DirectoryInfo = new Node[nodesCount];
                for (int i = 0; i < nodesCount; i++)
                {
                    DirectoryInfo[i] = new Node
                    {
                        offset = blocksInfoReader.ReadInt64BE(),
                        size = blocksInfoReader.ReadInt64BE(),
                        flags = blocksInfoReader.ReadUInt32BE(),
                        path = blocksInfoReader.ReadStringToNull(),
                    };
                }
            }
            if ((Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            {
                reader.AlignStream(16);
            }
        }

        private void ReadBlocks(EndianBinaryReader reader, Stream blocksStream)
        {
            foreach (var blockInfo in BlocksInfo)
            {
                var compressionType = (CompressionType)(blockInfo.flags & StorageBlockFlags.CompressionTypeMask);
                switch (compressionType)
                {
                    case CompressionType.None:
                        StreamCopy(reader.BaseStream, blocksStream, (int)blockInfo.compressedSize);
                        break;
                    case CompressionType.Lzma:
                        goto default;
                    case CompressionType.Lz4:
                    case CompressionType.Lz4HC:
                        var compressedSize = (int)blockInfo.compressedSize;
                        var compressedBytes = new byte[compressedSize];
                        reader.Read(compressedBytes, 0, compressedSize);
                        var uncompressedSize = (int)blockInfo.uncompressedSize;
                        var uncompressedBytes = new byte[uncompressedSize];
                        var numWrite = LZ4Codec.Decode(compressedBytes, 0, compressedSize, uncompressedBytes, 0, uncompressedSize);
                        if (numWrite != uncompressedSize)
                        {
                            throw new IOException($"Lz4 decompression error, write {numWrite} bytes but expected {uncompressedSize} bytes");
                        }
                        blocksStream.Write(uncompressedBytes, 0, uncompressedSize);
                        break;

                    default:
                        throw new IOException($"Unsupported compression type {compressionType}");
                }
            }
            blocksStream.Position = 0;
        }

        public void ReadFiles(Stream blocksStream)
        {
            FileList = new StreamFile[DirectoryInfo.Length];
            for (int i = 0; i < DirectoryInfo.Length; i++)
            {
                var node = DirectoryInfo[i];
                var file = new StreamFile();
                FileList[i] = file;
                file.path = node.path;
                file.fileName = Path.GetFileName(node.path);
                file.stream = new MemoryStream((int)node.size);
                blocksStream.Position = node.offset;
                StreamCopy(blocksStream, file.stream, (int)node.size);
                file.stream.Position = 0;
            }
            blocksStream.Close();
        }

        private Stream CreateBlocksStream()
        {
            var uncompressedSizeSum = BlocksInfo.Sum(x => x.uncompressedSize);
            Stream blocksStream = new MemoryStream((int)uncompressedSizeSum);
            return blocksStream;
        }

        public static void StreamCopy(Stream source, Stream destination, long size)
        {
            int BufferSize = 81920;
            var buffer = new byte[BufferSize];
            for (var left = size; left > 0; left -= BufferSize)
            {
                int toRead = BufferSize < left ? BufferSize : (int)left;
                int read = source.Read(buffer, 0, toRead);
                destination.Write(buffer, 0, read);
                if (read != toRead)
                {
                    return;
                }
            }
        }
    }
}
