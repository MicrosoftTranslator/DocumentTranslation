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
            app.Description = "DOCTR: Translate documents in many different formats with the Azure Translator service.";
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });
            app.Command("translate", translateCmd =>
            {
                translateCmd.AddName("trans");
                translateCmd.AddName("x");
                translateCmd.Description = "Translate a file or the content of a folder.";
                var sourceFiles = translateCmd.Argument("source", "Translate from this file or folder").IsRequired(true);
                var targetFolder = translateCmd.Argument("target", "Translate to this folder. If ommitted, target folder is <sourcefolder>.<language>.").IsRequired(false);
                var toLang = translateCmd.Option("--to <LanguageCode>", "The language code of the language to translate to. Use 'doctr languages' to see the available languages.", CommandOptionType.MultipleValue).IsRequired(true, "specification of a language to translate to is required.");
                var fromLang = translateCmd.Option("--from <LanguageCode>", "The language code of the language to translate from. Use 'doctr languages' to see the available languages.", CommandOptionType.SingleOrNoValue).IsRequired(false);
                var key = translateCmd.Option("--key <SubscriptionKey>", "The subscription key to use for this translation. Will not be saved in config settings.", CommandOptionType.SingleValue).IsRequired(false);
                translateCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    DocTransAppSettings settings = await AppSettingsSetter.Read();
                    if (key.HasValue()) settings.SubscriptionKey = key.Value();
                    DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
                    DocumentTranslationBusiness translationBusiness = new(documentTranslationService);

                    await translationBusiness.RunAsync(sourceFiles.Values, toLang.Value());
                });
            });
            app.Command("config", configCmd =>
            {
                configCmd.Description = "Configuration settings.";
                configCmd.Command("test", configTestCmd =>
                {
                    configTestCmd.Description = "Provide test result whether the configuration settings are functional. PASS if no problem found.";
                    configTestCmd.OnExecuteAsync(async (cancellationToken) =>
                    {
                        DocTransAppSettings docTransAppSettings = await AppSettingsSetter.Read(null);
                        DocumentTranslationService.Core.DocumentTranslationService translationService = new(docTransAppSettings.SubscriptionKey, docTransAppSettings.AzureResourceName, docTransAppSettings.ConnectionStrings.StorageConnectionString);
                        try { await translationService.TryCredentials(); }
                        catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException ex)
                        {
                            switch (ex.Message.ToLowerInvariant())
                            {
                                case "key":
                                    Console.WriteLine("FAIL: Invalid or missing subscription key.");
                                    break;
                                case "storage":
                                    Console.WriteLine("FAIL: Storage account not present or invalid storage connection string.");
                                    break;
                                case "name":
                                    Console.WriteLine("FAIL: Resource name not found.");
                                    break;
                                default:
                                    Console.WriteLine($"Exception: {ex.Message}");
                                    break;
                            }
                            return;
                        };
                        Console.WriteLine("PASS");
                        return;
                    });
                });
                configCmd.Command("set", configSetCmd =>
                {
                    var key = configSetCmd.Option("--key <AzureKey>", "Azure key for the Translator resource.", CommandOptionType.SingleValue);
                    var storage = configSetCmd.Option("--storage <StorageConnectionString>", "Connection string copied from the Azure storage resource.", CommandOptionType.SingleValue);
                    var name = configSetCmd.Option("--name <ResourceName>", "Name of the Translator resource matching the \"key\".", CommandOptionType.SingleValue);
                    var exp = configSetCmd.Option("--experimental <true/false>", "Show experimental languages.", CommandOptionType.SingleValue);
                    configSetCmd.Description = "Set the values of configuration parameters. Required before using Document Translation.";
                    configSetCmd.OnExecuteAsync(async (cancellationToken) =>
                    {
                        if (!(key.HasValue() || storage.HasValue() || name.HasValue() || exp.HasValue())) configSetCmd.ShowHelp();
                        DocTransAppSettings docTransAppSettings = await AppSettingsSetter.Read(null);
                        if (key.HasValue())
                        {
                            docTransAppSettings.SubscriptionKey = key.Value();
                            Console.WriteLine($"{app.Name}: Subscription key set.");
                        }
                        if (storage.HasValue())
                        {
                            if (docTransAppSettings.ConnectionStrings is null)
                            {
                                Connectionstrings connectionstrings = new();
                                docTransAppSettings.ConnectionStrings = connectionstrings;
                            }
                            docTransAppSettings.ConnectionStrings.StorageConnectionString = storage.Value();
                            Console.WriteLine($"{app.Name}: Storage Connection String set.");
                        }
                        if (name.HasValue())
                        {
                            docTransAppSettings.AzureResourceName = name.Value();
                            Console.WriteLine($"{app.Name}: Azure resource name set.");
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
                                    Console.WriteLine($"{app.Name}: Experimental languages enabled.");
                                    break;
                                default:
                                    Console.WriteLine($"{app.Name}: Experimental flag not recognized. Must be yes, no, true, or false.");
                                    break;
                            }
                        }
                        await AppSettingsSetter.Write(null, docTransAppSettings);
                        return 0;
                    });
                    configSetCmd.OnValidationError((i) =>
                    {
                        configSetCmd.ShowHelp();
                        return 1;
                    });
                });
                configCmd.Command("list", configListCmd =>
                {
                    configListCmd.Description = "List the configuration settings.";
                    configListCmd.OnExecuteAsync(async (cancellationToken) =>
                    {
                        DocTransAppSettings docTransAppSettings = new();
                        docTransAppSettings = await AppSettingsSetter.Read();
                        Console.WriteLine(AppSettingsSetter.GetJson(docTransAppSettings));
                        return 0;
                    });
                });
                configCmd.OnExecute(() =>
                {
                    configCmd.ShowHelp();
                    return 1;
                });
            });
            app.Command("languages", langCmd =>
                {
                    langCmd.AddName("langs");
                    langCmd.AddName("list");
                    langCmd.Description = "List the languages available for translation.";
                    langCmd.OnExecuteAsync(async (cancellationToken) =>
                    {
                        DocumentTranslationService.Core.DocumentTranslationService translationService = new(null, null, null);
                        await translationService.GetLanguagesAsync();
                        foreach (var language in translationService.Languages.OrderBy(x => x.Key))
                        {
                            Console.WriteLine($"{language.Value.LangCode}\t{language.Value.Name}");
                        }
                        return 0;
                    });
                });
            app.Command("formats", langCmd =>
            {
                langCmd.AddName("format");
                langCmd.Description = "List the translatable document formats.";
                langCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    DocTransAppSettings settings = new();
                    settings = await AppSettingsSetter.Read();
                    DocumentTranslationService.Core.DocumentTranslationService translationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
                    await translationService.GetFormatsAsync();
                    foreach (var format in translationService.FileFormats.value.OrderBy(x => x.format))
                    {
                        Console.Write($"{format.format}");
                        foreach (string ext in format.fileExtensions) Console.Write($"\t{ext}");
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

    }
}
