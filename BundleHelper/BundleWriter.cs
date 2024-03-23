using K4os.Compression.LZ4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BundleHelper
{
    public partial class Bundle
    {
        private long SizePosition;
        private byte[] BlocksInfoBytes;
        public void DumpRaw(EndianBinaryWriter writer)
        {
            writer.Position = 0;

            var blocksStream = CompressBlocksStream();
            WriteHeader(writer);
            WriteBlocksInfoAndDirectory(writer);
            WriteBlocks(writer, blocksStream);
            if ((Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0)
            {
                writer.Write(BlocksInfoBytes);
            }
            writer.Flush();
            writer.Position = SizePosition;
            writer.WriteBE(writer.BaseStream.Length);
        }

        private void WriteHeader(EndianBinaryWriter writer)
        {
            writer.WriteStringToNull(Signature);
            writer.WriteBE(Version);
            writer.WriteStringToNull(UnityVersion);
            writer.WriteStringToNull(UnityRevision);

            SizePosition = writer.Position;
            writer.WriteBE((long)0);
            writer.WriteBE((uint)0);
            writer.WriteBE((uint)0);
            writer.WriteBE((uint)Flags);
        }

        private void WriteBlocksInfoAndDirectory(EndianBinaryWriter writer)
        {
            if (Version >= 7)
            {
                writer.AlignStream(16);
            }
            MemoryStream blocksInfoUncompresseddStream = new MemoryStream();
            using (var blocksInfoWriter = new EndianBinaryWriter(blocksInfoUncompresseddStream))
            {
                blocksInfoWriter.Write(BlocksUncompressedDataHash, 0, 16);
                blocksInfoWriter.WriteBE(BlocksInfo.Length);
                foreach (var blockInfo in BlocksInfo)
                {
                    blocksInfoWriter.WriteBE(blockInfo.uncompressedSize);
                    blocksInfoWriter.WriteBE(blockInfo.compressedSize);
                    blocksInfoWriter.WriteBE((ushort)blockInfo.flags);
                }

                blocksInfoWriter.WriteBE(DirectoryInfo.Length);
                for (int i = 0; i < DirectoryInfo.Length; i++)
                {
                    var directoryInfo = DirectoryInfo[i];

                    blocksInfoWriter.WriteBE(directoryInfo.offset);
                    blocksInfoWriter.WriteBE(directoryInfo.size);
                    blocksInfoWriter.WriteBE(directoryInfo.flags);
                    blocksInfoWriter.WriteStringToNull(directoryInfo.path);
                }

                int uncompressedSize = (int)(blocksInfoUncompresseddStream.Length);
                var compressionType = (CompressionType)(Flags & ArchiveFlags.CompressionTypeMask);
                int compressedSize = uncompressedSize;
                switch (compressionType)
                {
                    case CompressionType.None:
                        BlocksInfoBytes = blocksInfoUncompresseddStream.ToArray();
                        break;
                    case CompressionType.Lzma:
                        goto default;
                    case CompressionType.Lz4:
                    case CompressionType.Lz4HC:
                        var uncompressedBytes = blocksInfoUncompresseddStream.ToArray();
                        var compressedBytes = new byte[uncompressedSize];
                        compressedSize = LZ4Codec.Encode(uncompressedBytes, 0, uncompressedSize, compressedBytes, 0, uncompressedSize, LZ4Level.L12_MAX);
                        BlocksInfoBytes = compressedBytes.Take(compressedSize).ToArray();
                        break;
                    default:
                        throw new IOException($"Unsupported compression type {compressionType}");
                }
                CompressedBlocksInfoSize = (uint)compressedSize;
                UncompressedBlocksInfoSize = (uint)uncompressedSize;

                if ((Flags & ArchiveFlags.BlocksInfoAtTheEnd) != 0)
                {
                    // 最后再写
                }
                else //0x40 BlocksAndDirectoryInfoCombined
                {
                    writer.Write(BlocksInfoBytes);
                }
                if ((Flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
                {
                    writer.AlignStream(16);
                }
            }
            if (true)
            {
                var position = writer.Position;
                writer.Position = SizePosition + 8;
                writer.WriteBE(CompressedBlocksInfoSize);
                writer.WriteBE(UncompressedBlocksInfoSize);
                writer.Position = position;
            }
        }

        private void WriteBlocks(BinaryWriter writer, MemoryStream blocksStream)
        {
            blocksStream.Position = 0;
            StreamCopy(blocksStream, writer.BaseStream, blocksStream.Length);
        }

        private MemoryStream CompressBlocksStream()
        {
            StorageBlock baseBlock = BlocksInfo[0];
            var uncompressedStream = new MemoryStream();
            WriteFiles(uncompressedStream);
            uint blockSize = baseBlock.uncompressedSize;
            if (blockSize < 0x20000)
            {
                blockSize = 0x20000;
            }
            uncompressedStream.Position = 0;
            var compressedStream = new MemoryStream();
            var newBlocksInfo = new List<StorageBlock>();
            while (uncompressedStream.Position < uncompressedStream.Length)
            {
                var flags = baseBlock.flags;
                if (flags == StorageBlockFlags.Streamed)
                {
                    flags = (StorageBlockFlags)0x03;
                }
                int uncompressedSize = (int)Math.Min(blockSize, uncompressedStream.Length - uncompressedStream.Position);
                int compressedSize = uncompressedSize;
                var compressionType = (CompressionType)(flags & StorageBlockFlags.CompressionTypeMask);

                byte[] uncompressedBlock = new byte[uncompressedSize];
                uncompressedStream.Read(uncompressedBlock, 0, uncompressedBlock.Length);

                switch (compressionType)
                {
                    case CompressionType.None:
                        compressedStream.Write(uncompressedBlock, 0, uncompressedSize);
                        break;
                    case CompressionType.Lzma:
                        goto default;
                    case CompressionType.Lz4:
                    case CompressionType.Lz4HC:
                        byte[] compressedBlock = new byte[uncompressedBlock.Length];
                        compressedSize = LZ4Codec.Encode(uncompressedBlock, 0, uncompressedSize, compressedBlock, 0, uncompressedSize, LZ4Level.L07_HC);
                        if (compressedSize > 0 && compressedSize < uncompressedSize)
                        {
                            compressedStream.Write(compressedBlock, 0, compressedSize);
                        }
                        else
                        {
                            flags = (StorageBlockFlags)((int)flags & (0xff ^ (int)StorageBlockFlags.CompressionTypeMask) & (int)CompressionType.None);
                            compressedSize = uncompressedSize;
                            compressedStream.Write(uncompressedBlock, 0, uncompressedSize);
                        }
                        break;
                    default:
                        throw new IOException($"Unsupported compression type {compressionType}");
                }
                StorageBlock block = new StorageBlock
                {
                    uncompressedSize = (uint)uncompressedSize,
                    compressedSize = (uint)compressedSize,
                    flags = flags
                };
                newBlocksInfo.Add(block);
            }
            BlocksInfo = newBlocksInfo.ToArray();
            return compressedStream;
        }

        private class DirectoryOrder
        {
            public Node directoryInfo;
            public int orignalOrder;
            public long offset => directoryInfo.offset;
        }

        private void WriteFiles(MemoryStream blocksStream)
        {
            blocksStream.Position = 0;
            List<DirectoryOrder> directoryOrders = new List<DirectoryOrder>();
            for (int i = 0; i < DirectoryInfo.Length; i++)
            {
                directoryOrders.Add(new DirectoryOrder
                {
                    directoryInfo = DirectoryInfo[i],
                    orignalOrder = i
                });
            }
            directoryOrders.Sort((x, y) => x.offset.CompareTo(y.offset));
            foreach (var directoryOrder in directoryOrders)
            {
                var file = FileList[directoryOrder.orignalOrder];
                file.stream.Position = 0;
                directoryOrder.directoryInfo.offset = blocksStream.Position;
                StreamCopy(file.stream, blocksStream, file.stream.Length);
                directoryOrder.directoryInfo.size = file.stream.Length;
                file.stream.Close();
            }
        }
    }
}