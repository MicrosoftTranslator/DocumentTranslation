using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentTranslation.GUI
{
    public class Categories
    {
        public BindingList<MyCategory> MyCategoryList { get; set; } = new();

        const string AppName = "Document Translation";
        const string AppSettingsFileName = "CustomCategories.json";

        public Categories()
        {
            Read();
        }

        private async void Read(string filename = null)
        {
            string categoriesJson;
            try
            {
                if (string.IsNullOrEmpty(filename)) filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
                categoriesJson = await File.ReadAllTextAsync(filename);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    MyCategoryList.Add(new MyCategory("Category 1", ""));
                    MyCategoryList.Add(new MyCategory("Category 2", ""));
                    return;
                }
                throw;
            }
            MyCategoryList = JsonSerializer.Deserialize<BindingList<MyCategory>>(categoriesJson, new JsonSerializerOptions { IncludeFields = true });
        }

        public async Task WriteAsync(string filename = null)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName);
                filename = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + AppName + Path.DirectorySeparatorChar + AppSettingsFileName;
            }
            await File.WriteAllTextAsync(filename, JsonSerializer.Serialize(this.MyCategoryList, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        }
    }

    public class MyCategory
    {
        public string Name { get; set; }
        public string ID { get; set; }

        public MyCategory(string name, string iD)
        {
            Name = name;
            ID = iD;
        }
    }
}
