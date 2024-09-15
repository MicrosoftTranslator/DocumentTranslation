using System;
using System.Diagnostics;
using System.IO;

namespace DocumentTranslationService.Core
{
    public class Logger
    {
        private const string AppName = "Document Translation";
        private const string logFileName = "doctrLog.txt";
        private readonly StreamWriter streamWriter;
        private readonly string filename;

        internal Logger()
        {
            filename = Path.GetTempFileName();
            streamWriter = File.CreateText(filename);
            streamWriter.AutoFlush = true;
        }

        ~Logger()
        {
            Close();
        }

        internal void WriteLine(string line)
        {
            Debug.WriteLine(line);
            streamWriter.WriteLine($"{DateTime.Now}: {line}");
        }
        internal void WriteLine()
        {
            streamWriter.WriteLine();
        }

        internal void Close()
        {
            string finalfilename = GetFinalFilename();
            if (File.Exists(filename))
                try
                {
                    streamWriter.Close();
                    File.Copy(filename, finalfilename, true);
                    File.Delete(filename);
                }
                catch { };
        }

        private static string GetFinalFilename()
        {
            string finalfilename;
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                finalfilename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + logFileName;
            }
            else
            {
                finalfilename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + AppName + "_" + Path.DirectorySeparatorChar + logFileName;
            }
            return finalfilename;
        }
    }
}
