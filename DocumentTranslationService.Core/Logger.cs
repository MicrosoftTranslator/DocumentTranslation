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

        public Logger()
        {
            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
            string filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + logFileName;
            streamWriter = File.CreateText(filename);
            streamWriter.AutoFlush = true;
        }

        public void WriteLine(string line)
        {
            Debug.WriteLine(line);
            streamWriter.WriteLine($"{DateTime.Now}: {line}");
        }
        public void WriteLine()
        {
            streamWriter.WriteLine();
        }

    }
}
