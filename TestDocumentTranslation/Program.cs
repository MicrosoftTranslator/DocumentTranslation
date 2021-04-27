using System;
using System.IO;
using DocumentTranslationServices.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TestDocumentTranslation
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            string storageConnectionString = configuration.GetConnectionString("StorageConnectionString");
            if (String.IsNullOrEmpty(storageConnectionString))
            {
                Console.WriteLine("ERROR: StorageConnectionString is missing in appsettings.json.");
                return -1;
            }
            string azureResourceName = configuration["AzureResourceName"];
            if (String.IsNullOrEmpty(azureResourceName))
            {
                Console.WriteLine("ERROR: AzureResourceName is missing in appsettings.json.");
                return -1;
            }
            string subscriptionKey = configuration["SubscriptionKey"];
            if (String.IsNullOrEmpty(subscriptionKey))
            {
                Console.WriteLine("ERROR: SubscriptionKey is missing in appsettings.json.");
                return -1;
            }

            if (args.Length != 2)
            {
                Console.WriteLine("Syntax: DocumentTranslation.exe <file or Directory> <language to translate to>");
                return -2;
            }
            string givenPath = args[0];

            //Read argument 0 as file or file list from directory.
            string[] files = new string[1];
            if (File.GetAttributes(givenPath) == FileAttributes.Directory)
            {
                try
                {
                    files = Directory.GetFiles(givenPath);
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine("Directory {0} not found.", givenPath);
                    return -2;
                }
            }
            else
            {
                files[0] = givenPath;
            }
            if (files.Length < 1)
            {
                Console.WriteLine("Nothing to translate.");
                return -3;
            }

            string toLanguage = args[1];

            DocumentTranslationService documentTranslation = new(subscriptionKey, azureResourceName, storageConnectionString);
            if (documentTranslation is null)
            {
                Console.WriteLine("ERROR: Unable to initialize.");
                return 0;
            }

            DocumentTranslationBusiness translationBusiness = new(documentTranslation);
            translationBusiness.StatusUpdate += DocumentTranslation_OnStatusUpdate;
            Task task = translationBusiness.Run(files, toLanguage);
            Console.WriteLine("Translation starting...");
            await task;
            Console.WriteLine("Translation is complete.");
            translationBusiness.StatusUpdate -= DocumentTranslation_OnStatusUpdate;
            return 0;
        }

        private static void DocumentTranslation_OnStatusUpdate(object sender, StatusResponse e)
        {
            Console.WriteLine("Time: {0}\tStatus: {1}\tTotal: {2}\tTranslatedCharacters: {3}", e.lastActionDateTimeUtc, e.status, e.summary.total, e.summary.totalCharacterCharged);
        }
    }
}
