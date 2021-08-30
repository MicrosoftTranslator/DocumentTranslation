using System;
using System.Collections.Generic;
using System.IO;

namespace DocumentTranslationService.Core
{
    public static class AzureRegionsList
    {
        public static List<AzureRegion> ReadAzureRegions()
        {
            List<AzureRegion> azureRegions = new();
            string regionsText = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "AzureRegionsList.tsv");
            azureRegions.Clear();
            azureRegions.Add(new AzureRegion("Global", "global"));
            string[] lines = regionsText.Split("\r\n");
            foreach (string line in lines)
            {
                if (!line.StartsWith("/"))
                {
                    string[] elements = line.Split("\t");
                    if ((string.IsNullOrEmpty(elements[0])) || (string.IsNullOrEmpty(elements[1]))) continue;
                    azureRegions.Add(new AzureRegion(elements[0], elements[1]));
                }
            }
            return azureRegions;
        }
    }

    public class AzureRegion
    {
        public string Name { get; set; }
        public string ID { get; set; }

        public AzureRegion(string name, string iD)
        {
            Name = name;
            ID = iD;
        }
    }
}
