using BundleHelper;
using System;
using System.IO;

namespace BundleCompress
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    EndianBinaryReader reader = new EndianBinaryReader(File.OpenRead(arg));
                    Bundle bundleData = new Bundle(reader);

                    reader.Close();
                    Console.WriteLine($"Compressing: {arg}");

                    EndianBinaryWriter writer = new EndianBinaryWriter(File.Create($"{arg}-compressed"));
                    bundleData.DumpRaw(writer);

                    writer.Close();
                }
            }
            else
            {
                Console.WriteLine("Drop your file on it. New files are renamed with a “-compressed” postfix.");
                Console.ReadKey();
            }
        }
    }
}
