using BundleHelper;
using System;
using System.IO;

namespace AssetImporter
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
                    if (!Directory.Exists($"{fileName}-content"))
                    {
                        Console.WriteLine($"Skipped: directory “{fileName}-content” does not exist.");
                        continue;
                    }
                    EndianBinaryReader reader = new EndianBinaryReader(File.OpenRead(arg));
                    Bundle bundleData = new Bundle(reader);

                    reader.Close();
                    Console.WriteLine(bundleData.ToString());

                    foreach (var file in bundleData.FileList)
                    {
                        if (File.Exists($"{fileName}-content/{file.fileName}"))
                        {
                            file.stream = File.OpenRead($"{fileName}-content/{file.fileName}");
                        }
                    }

                    EndianBinaryWriter writer = new EndianBinaryWriter(File.Create($"{arg}-replaced"));
                    bundleData.DumpRaw(writer);

                    writer.Close();
                    foreach (var file in bundleData.FileList)
                    {
                        file.stream.Close();
                    }
                }
            }
            else
            {
                Console.WriteLine("Drop your file on it. Put your files in the directory with the name of file name with a “-content” postfix. New files are renamed with a “-replaced” postfix.");
                Console.ReadKey();
            }
        }
    }
}