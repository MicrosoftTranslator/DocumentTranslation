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
            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
            string finalfilename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + logFileName;
            try
            {
                streamWriter.Close();
                File.Copy(filename, finalfilename, true);
                File.Delete(filename);
            }
            catch { };
        }
    }
}
