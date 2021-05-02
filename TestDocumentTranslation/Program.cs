using System;
using System.IO;
using DocumentTranslationService.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace TranslationService.CLI
{
    partial class Program
    {
        public static async Task<int> Main(string[] args)
        {
            CommandLineApplication app = new();
            app.HelpOption(inherited: true);
            app.Name = "DOCTR";
            app.Description = "DOCTR: Translate a documents in many different formats with the Azure Translator service.";
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });

            app.Command("config", configCmd =>
            {
                configCmd.Description = "Configuration settings.";
                configCmd.Command("set", configSetCmd =>
                {
                    var key = configSetCmd.Option("--key <AzureKey>", "Azure key for the Translator resource.", CommandOptionType.SingleValue);
                    var storage = configSetCmd.Option("--storage <StorageConnectionString>", "Connection string copied from the Azure storage resource.", CommandOptionType.SingleValue);
                    var name = configSetCmd.Option("--name <ResourceName>", "Name of the Translator resource matching the \"key\".", CommandOptionType.SingleValue);
                    var exp = configSetCmd.Option("--experimental <true/false>", "Show experimental languages.", CommandOptionType.SingleValue);
                    configSetCmd.OnExecuteAsync( async (cancellationToken) =>
                    {
                        DocTransAppSettings docTransAppSettings = await AppSettingsSetter.Read(null);
                        Console.WriteLine($"key {key.Value()}, storage {storage.Value()}, name {name.Value()}, experimental {exp.Value()}");
                        if (key.HasValue())
                        {
                            docTransAppSettings.SubscriptionKey = key.Value();
                            Console.WriteLine("Subscription key set.");
                        }
                        if (storage.HasValue())
                        {
                            docTransAppSettings.ConnectionStrings.StorageConnectionString = storage.Value();
                            Console.WriteLine("Storage Connection String set.");
                        }
                        if (name.HasValue())
                        {
                            docTransAppSettings.AzureResourceName = name.Value();
                            Console.WriteLine("Azure resource name set.");
                        }
                        if (exp.HasValue())
                        {
                            switch (exp.Value().ToLowerInvariant()[0])
                            {
                                case 't':
                                case 'y':
                                    docTransAppSettings.ShowExperimental = true;
                                    break;
                                case 'n':
                                case 'f':
                                    docTransAppSettings.ShowExperimental = false;
                                    Console.WriteLine("Experimental languages enabled.");
                                    break;
                                default:
                                    Console.WriteLine("Experimental flag not recognized. Must be yes, no, true, or false.");
                                    break;
                            }
                        }
                        await AppSettingsSetter.Write(null, docTransAppSettings);
                        return 1;
                    });
                    configSetCmd.OnValidationError((i) =>
                    {
                        configSetCmd.ShowHelp();
                    });
                });
                configCmd.Command("list", configListCmd =>
                {
                    configListCmd.Description = "List the configuration settings.";
                    configListCmd.OnExecute(() =>
                    {

                    });
                });
                configCmd.Command("test", configTestCmd =>
                {
                    configTestCmd.OnExecute(() =>
                    {

                    });
                });
                configCmd.OnExecute(() =>
                {
                });
            });

            app.Command("languages", langCmd =>
                {
                    langCmd.AddName("langs");
                    langCmd.AddName("list");
                    langCmd.Description = "List the langauges available for translation.";
                    langCmd.OnExecuteAsync(async (cancellationToken) =>
                    {
                        DocumentTranslationService.Core.DocumentTranslationService translationService = new(null, null, null);
                        await translationService.GetLanguagesAsync();
                        foreach (var language in translationService.Languages.OrderBy(x => x.Key))
                        {
                            Console.WriteLine($"{language.Value.LangCode}\t{language.Value.Name}");
                        }
                    });
                });

            app.Command("formats", langCmd =>
            {
                langCmd.AddName("format");
                langCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    string subscriptionKey = string.Empty;
                    DocumentTranslationService.Core.DocumentTranslationService translationService = new(subscriptionKey, null, null);
                    await translationService.GetFormatsAsync();
                    foreach (var format in translationService.FileFormats.value.OrderBy(x => x.format))
                    {
                        Console.Write($"{format.format}\t");
                        foreach (string ext in format.fileExtensions) Console.Write(ext);
                        Console.WriteLine();
                    }
                });
            });


            int result = 0;
            try
            {
                result = app.Execute(args);
            }
            catch (CommandParsingException e)
            {
                Console.WriteLine(e.Message);
            }

            return result;
        }

        private static int Mainbody()
        {
            throw new NotImplementedException();
        }
    }
}
