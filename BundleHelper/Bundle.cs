using System;
using System.IO;

namespace BundleHelper
{
    public partial class Bundle
    {
        public string Signature;
        public uint Version;
        public string UnityVersion;
        public string UnityRevision;

        public long Size;
        public uint CompressedBlocksInfoSize;
        public uint UncompressedBlocksInfoSize;
        public ArchiveFlags Flags;

        private byte[] BlocksUncompressedDataHash;

        private StorageBlock[] BlocksInfo;
        private Node[] DirectoryInfo;
        public StreamFile[] FileList;

        public string outputPath;

        public class StorageBlock
        {
            public uint compressedSize;
            public uint uncompressedSize;
            public StorageBlockFlags flags;
        }

        public class Node
        {
            public long offset;
            public long size;
            public uint flags;
            public string path;
        }

        public class StreamFile
        {
            public string path;
            public string fileName;
            public Stream stream;
        }

        [Flags]
        public enum ArchiveFlags
        {
            CompressionTypeMask = 0x3f,
            BlocksAndDirectoryInfoCombined = 0x40,
            BlocksInfoAtTheEnd = 0x80,
            OldWebPluginCompatibility = 0x100,
            BlockInfoNeedPaddingAtStart = 0x200
        }

        [Flags]
        public enum StorageBlockFlags
        {
            CompressionTypeMask = 0x3f,
            Streamed = 0x40
        }

        public enum CompressionType
        {
            None,
            Lzma,
            Lz4,
            Lz4HC,
            Lzham
        }
    }
}
