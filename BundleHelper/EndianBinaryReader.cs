using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BundleHelper
{
    public enum EndianType
    {
        LittleEndian,
        BigEndian
    }

    public class EndianBinaryReader : BinaryReader
    {
        public EndianType Endian;
        public EndianBinaryReader(Stream input, EndianType endian = EndianType.LittleEndian) : base(input)
        {
            Endian = endian;
        }

        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public short ReadInt16BE()
        {
            var data = ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }

        public override short ReadInt16()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadInt16BE();
            }
            return base.ReadInt16();
        }

        public int ReadInt32BE()
        {
            var data = ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        public override int ReadInt32()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadInt32BE();
            }
            return base.ReadInt32();
        }

        public long ReadInt64BE()
        {
            var data = ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }

        public override long ReadInt64()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadInt64BE();
            }
            return base.ReadInt64();
        }

        public ushort ReadUInt16BE()
        {
            var data = ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        public override ushort ReadUInt16()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadUInt16BE();
            }
            return base.ReadUInt16();
        }

        public uint ReadUInt32BE()
        {
            var data = ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        public override uint ReadUInt32()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadUInt32BE();
            }
            return base.ReadUInt32();
        }

        public ulong ReadUInt64BE()
        {
            var data = ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        public override ulong ReadUInt64()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadUInt64BE();
            }
            return base.ReadUInt64();
        }

        public float ReadSingleBE()
        {
            var data = ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToSingle(data, 0);
        }

        public override float ReadSingle()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadSingleBE();
            }
            return base.ReadSingle();
        }

        public double ReadDoubleBE()
        {
            var data = ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToDouble(data, 0);
        }

        public override double ReadDouble()
        {
            if (Endian == EndianType.BigEndian)
            {
                return ReadDoubleBE();
            }
            return base.ReadDouble();
        }

        public string ReadAlignedString()
        {
            var length = ReadInt32();
            if (length > 0 && length <= BaseStream.Length - BaseStream.Position)
            {
                var stringData = ReadBytes(length);
                var result = Encoding.UTF8.GetString(stringData);
                AlignStream(4);
                return result;
            }
            return "";
        }

        public string ReadStringToNull(int maxLength = 32767)
        {
            var bytes = new List<byte>();
            int count = 0;
            while (BaseStream.Position != BaseStream.Length && count < maxLength)
            {
                var b = ReadByte();
                if (b == 0)
                {
                    break;
                }
                bytes.Add(b);
                count++;
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public void AlignStream()
        {
            AlignStream(4);
        }

        public void AlignStream(int alignment)
        {
            var pos = BaseStream.Position;
            var mod = pos % alignment;
            if (mod != 0)
            {
                BaseStream.Position += alignment - mod;
            }
        }
    }


    public static class BinaryReaderExtensions
    {
        private static T[] ReadArray<T>(Func<T> del, int length)
        {
            var array = new T[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = del();
            }
            return array;
        }

        public static int[] ReadInt32Array(this EndianBinaryReader reader)
        {
            return ReadArray(reader.ReadInt32, reader.ReadInt32());
        }
    }
}