using BundleHelper;
using System;
using System.IO;

namespace AssetExtractor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                foreach (string arg in args)
                {
                    var fileName = Path.GetFileName(arg);
                    Directory.CreateDirectory($"{fileName}-content");
                    EndianBinaryReader reader = new EndianBinaryReader(File.OpenRead(arg));
                    Bundle bundleData = new Bundle(reader);

                    reader.Close();
                    Console.WriteLine($"Extracting: {arg}");

                    foreach (var file in bundleData.FileList)
                    {
                        Console.WriteLine($"File: {fileName}");
                        var fw = File.Create($"{fileName}-content/{file.fileName}");
                        file.stream.Position = 0;
                        file.stream.CopyTo(fw);
                        fw.Close();
                    }
                }
            }
            else
            {
                Console.WriteLine("Drop your file on it. New files are saved to the directory with the name of file name with a “-content” postfix.");
                Console.ReadKey();
            }
        }
    }
}