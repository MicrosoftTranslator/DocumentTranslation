using DocumentTranslationService.Core;
using McMaster.Extensions.CommandLineUtils;
using System;
using System.Linq;
using System.Timers;

namespace DocumentTranslation.CLI
{
    partial class Program
    {
        private static DocumentTranslationService.Core.DocumentTranslationService TranslationService;


        public static int Main(string[] args)
        {
            CommandLineApplication app = new();
            app.HelpOption(inherited: true);
            app.Name = "DOCTR";
            app.Description = "DOCTR: Translate documents with the Azure Translator service.";
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });
            app.Command("clear", clearCmd =>
            {
                clearCmd.Description = "Delete containers potentially left over from previous failed runs, in the given storage account, which are older than a week.";
                clearCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    DocTransAppSettings settings = AppSettingsSetter.Read();
                    DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
                    DocumentTranslationBusiness translationBusiness = new(documentTranslationService);
                    try
                    {
                        int deletedCount = await translationBusiness.ClearOldContainersAsync();
                        Console.WriteLine($"Number of old containers deleted: {deletedCount}.");
                    }
                    catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException ex)
                    {
                        Console.WriteLine($"{Properties.Resources.msg_MissingCredentials}: {ex.Message} {ex.InnerException?.Message}");
                        return;
                    }
                });
            }
            );
            app.Command("translate", translateCmd =>
            {
                translateCmd.AddName("trans");
                translateCmd.AddName("x");
                translateCmd.Description = "Translate a document or all documents in a folder.";
                var sourceFiles = translateCmd.Argument("source", "Translate this document or folder")
                                              .IsRequired(true, "A source document or folder is required.");
                var targetFolder = translateCmd.Argument("target", "Translate to this folder. If omitted, target folder is <sourcefolder>.<language>.");
                var toLang = translateCmd.Option("-t|--to <LanguageCode>", "The language code of the language to translate to. Use 'doctr languages' to see the available languages.", CommandOptionType.MultipleValue)
                                         .IsRequired(true, "Specification of a language to translate to is required.");
                var fromLang = translateCmd.Option("-f|--from <LanguageCode>",
                                                   "Optional: The language code of the language to translate from. Use 'doctr languages' to see the available languages. If omitted, the language will be auto-detected.",
                                                   CommandOptionType.SingleOrNoValue);
                var key = translateCmd.Option("-k|--key <SubscriptionKey>",
                                              "Optional: The resource key to use for this translation. Will not be saved in config settings. If omitted, will use the key of the configuration.",
                                              CommandOptionType.SingleValue);
                var cat = translateCmd.Option("-c|--category",
                                              "The Custom Translator category to use. Set to 'none' or 'n' to force use of no category.",
                                              CommandOptionType.SingleValue);
                var gls = translateCmd.Option("-g|--glossary",
                                              "Glossary file, files, or folder to use as glossary. Cannot be same as source.",
                                              CommandOptionType.MultipleValue);
                var nodelete = translateCmd.Option("--nodelete",
                                                   "Do not delete the container in the storage account. For debugging purposes only.",
                                                   CommandOptionType.NoValue);
                translateCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    DocTransAppSettings settings = AppSettingsSetter.Read();
                    if (key.HasValue()) settings.SubscriptionKey = key.Value();
                    try { AppSettingsSetter.CheckSettings(settings); }
                    catch (ArgumentException e)
                    {
                        var savedcolor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Missing credential: {e.Message}");
                        Console.ForegroundColor = savedcolor;
                        Console.WriteLine("Use 'DOCTR config set' to set the credentials.");
                        return;
                    };
                    DocumentTranslationService.Core.DocumentTranslationService documentTranslationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
                    TranslationService = documentTranslationService;
                    DocumentTranslationBusiness translationBusiness = new(documentTranslationService);
                    if (nodelete.HasValue()) translationBusiness.Nodelete = true;
                    if (cat.HasValue()) documentTranslationService.Category = cat.Value();
                    translationBusiness.OnStatusUpdate += TranslationBusiness_OnStatusUpdate;
                    translationBusiness.OnDownloadComplete += TranslationBusiness_OnDownloadComplete;
                    translationBusiness.OnFilesDiscarded += TranslationBusiness_OnFilesDiscarded;
                    translationBusiness.OnUploadComplete += TranslationBusiness_OnUploadComplete;
                    translationBusiness.OnFinalResults += TranslationBusiness_OnFinalResults;
                    Timer timer = new(500) { AutoReset = true, Enabled = true };
                    timer.Elapsed += Timer_Elapsed;
                    Console.WriteLine($"Starting translation of {sourceFiles.Value} to {toLang.Value()}. Press Esc to cancel.");
                    string target = null;
                    if (!string.IsNullOrEmpty(targetFolder.Value)) target = targetFolder.Value;
                    try
                    {
                        string[] langs = new string[1];
                        langs[0] = toLang.Value();
                        await translationBusiness.RunAsync(filestotranslate: sourceFiles.Values, fromlanguage: fromLang.Value(), tolanguages: langs, glossaryfiles: gls.Values, targetFolder: target);
                    }
                    catch (System.IO.FileNotFoundException e)
                    {
                        Console.WriteLine(e.Message);
                        return;
                    }
                    catch (System.ArgumentNullException e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                        return;
                    }
                    catch (System.ArgumentException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    catch (Azure.RequestFailedException e)
                    {
                        Console.WriteLine(e.Message);
                        return;
                    }
                    Console.WriteLine($"Target folder: {translationBusiness.TargetFolder}");
                    timer.Stop();
                    timer.Dispose();
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
                        DocTransAppSettings docTransAppSettings = AppSettingsSetter.Read(null);
                        DocumentTranslationService.Core.DocumentTranslationService translationService = new(docTransAppSettings.SubscriptionKey, docTransAppSettings.AzureResourceName, docTransAppSettings.ConnectionStrings.StorageConnectionString);
                        try { await translationService.TryCredentials(); }
                        catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException ex)
                        {
                            switch (ex.Message.ToLowerInvariant())
                            {
                                case "key":
                                    Console.WriteLine("FAIL: Invalid or missing resource key.");
                                    break;
                                case "storage":
                                    Console.WriteLine("FAIL: Storage account not present or invalid storage connection string.");
                                    break;
                                case "name":
                                    Console.WriteLine("FAIL: Resource name not found.");
                                    break;
                                default:
                                    Console.WriteLine($"Exception: {ex.Message} {ex.InnerException?.Message}");
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
                    var key = configSetCmd.Option("--key <AzureKey>", "Azure key for the Translator resource. 'clear' to remove.", CommandOptionType.SingleValue);
                    var storage = configSetCmd.Option("--storage <StorageConnectionString>", "Connection string copied from the Azure storage resource. 'clear' to remove.", CommandOptionType.SingleValue);
                    var endpoint = configSetCmd.Option("--endpoint <Endpoint>", "URL of the Translator endpoint matching the \"key\".", CommandOptionType.SingleValue);
                    var region = configSetCmd.Option("--region <Region>", "Region where the Translator resource is located.", CommandOptionType.SingleValue);
                    var cat = configSetCmd.Option("--category", "Set the Custom Translator category to use for translations. 'clear' to remove.", CommandOptionType.SingleValue);
                    configSetCmd.Description = "Set the values of configuration parameters. Required before using Document Translation.";
                    configSetCmd.OnExecute(() =>
                    {
                        if (!(key.HasValue() || storage.HasValue() || endpoint.HasValue() || region.HasValue() || cat.HasValue())) configSetCmd.ShowHelp();
                        DocTransAppSettings docTransAppSettings = AppSettingsSetter.Read(null);
                        if (key.HasValue())
                        {
                            if (key.Value().ToLowerInvariant() == "clear") docTransAppSettings.SubscriptionKey = string.Empty;
                            else docTransAppSettings.SubscriptionKey = key.Value();
                            Console.WriteLine($"{app.Name}: Resource key set.");
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
                        if (endpoint.HasValue())
                        {
                            if (endpoint.Value().ToLowerInvariant() == "clear") docTransAppSettings.AzureResourceName = string.Empty;
                            else docTransAppSettings.AzureResourceName = endpoint.Value();
                            Console.WriteLine($"{app.Name}: Azure resource endpoint set.");
                        }
                        if (cat.HasValue())
                        {
                            if (cat.Value().ToLowerInvariant() == "clear") docTransAppSettings.Category = string.Empty;
                            else docTransAppSettings.Category = cat.Value();
                            Console.WriteLine($"{app.Name}: Custom Translator Category set.");
                        }
                        if (region.HasValue())
                        {
                            if (cat.Value().ToLowerInvariant() == "clear") docTransAppSettings.AzureRegion = string.Empty;
                            else docTransAppSettings.AzureRegion = region.Value();
                            Console.WriteLine($"{app.Name}: Azure region set.");
                        }
                        AppSettingsSetter.Write(null, docTransAppSettings);
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
                    configListCmd.OnExecute(() =>
                    {
                        DocTransAppSettings docTransAppSettings = new();
                        docTransAppSettings = AppSettingsSetter.Read();
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
            app.Command("formats", formatsCmd =>
            {
                formatsCmd.AddName("format");
                formatsCmd.Description = "List the translatable document formats.";
                formatsCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    DocTransAppSettings settings = new();
                    settings = AppSettingsSetter.Read();
                    DocumentTranslationService.Core.DocumentTranslationService translationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
                    await translationService.InitializeAsync();
                    try
                    {
                        var result = await translationService.GetDocumentFormatsAsync();
                        if (result is null)
                        {
                            Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                            return;
                        }
                    }
                    catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException)
                    {
                        Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                        return;
                    }
                    catch (System.UriFormatException)
                    {
                        Console.WriteLine(Properties.Resources.msg_WrongResourceName);
                        return;
                    }
                    if (translationService.FileFormats is null || translationService.FileFormats is null || translationService.FileFormats.Count < 2)
                    {
                        Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                        return;
                    }
                    foreach (var format in translationService.FileFormats.OrderBy(x => x.Format))
                    {
                        Console.Write($"{format.Format}");
                        foreach (string ext in format.FileExtensions) Console.Write($"\t{ext}");
                        Console.WriteLine();
                    }
                });
            });
            app.Command("glossary", glosCmd =>
            {
                glosCmd.AddName("glossaryformats");
                glosCmd.Description = "List the usable glossary formats.";
                glosCmd.OnExecuteAsync(async (cancellationToken) =>
                {
                    DocTransAppSettings settings = new();
                    settings = AppSettingsSetter.Read();
                    DocumentTranslationService.Core.DocumentTranslationService translationService = new(settings.SubscriptionKey, settings.AzureResourceName, settings.ConnectionStrings.StorageConnectionString);
                    await translationService.InitializeAsync();
                    try
                    {
                        var result = await translationService.GetGlossaryFormatsAsync();
                        if (result is null)
                        {
                            Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                            return;
                        }
                    }
                    catch (DocumentTranslationService.Core.DocumentTranslationService.CredentialsException)
                    {
                        Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                        return;
                    }
                    catch (System.UriFormatException)
                    {
                        Console.WriteLine(Properties.Resources.msg_WrongResourceName);
                        return;
                    }
                    if (translationService.GlossaryFormats is null || translationService.GlossaryFormats is null || translationService.GlossaryFormats.Count < 2)
                    {
                        Console.WriteLine(Properties.Resources.msg_MissingCredentials);
                        return;
                    }
                    foreach (var format in translationService.GlossaryFormats.OrderBy(x => x.Format))
                    {
                        Console.Write($"{format.Format}");
                        foreach (string ext in format.FileExtensions) Console.Write($"\t{ext}");
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

        private static void TranslationBusiness_OnFinalResults(object sender, long e)
        {
            Console.WriteLine($"Characters charged: {e}");
        }

        private static void TranslationBusiness_OnUploadComplete(object sender, (int count, long sizeInBytes) e)
        {
            Console.WriteLine($"Submitted: {e.count} documents, {e.sizeInBytes} bytes.");
        }

        private static async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //check whether user pressed Escape.
            if (Console.KeyAvailable)
            {
                if (Console.ReadKey().Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    Console.WriteLine("Canceling...");
                    await TranslationService.CancelRunAsync();
                }
            }
        }

        private static void TranslationBusiness_OnFilesDiscarded(object sender, System.Collections.Generic.List<string> discardedFilenames)
        {
            Console.WriteLine("Following files were excluded due to a file type mismatch:");
            foreach (string filename in discardedFilenames) Console.WriteLine(filename);
        }

        private static void TranslationBusiness_OnDownloadComplete(object Sender, (int count, long sizeInBytes) e)
        {
            Console.WriteLine($"Translation complete: {e.count} documents, {e.sizeInBytes} bytes.");
        }

        private static void TranslationBusiness_OnStatusUpdate(object sender, StatusResponse e)
        {
            if (e.Status?.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.Failed
                || e.Status?.Status == Azure.AI.Translation.Document.DocumentTranslationStatus.ValidationFailed
                || !String.IsNullOrEmpty(e.Message))
            {
                Console.WriteLine($"{Properties.Resources.msg_ServerMessage}{e.Status?.Status}");
                Console.WriteLine(e.Message);
            }
            else
            {
                var time = e.Status.LastModified;           //lastActionDateTimeUtc);
                Console.WriteLine($"{time.TimeOfDay}\tStatus: {e.Status.Status}\tIn progress: {e.Status.DocumentsInProgress}\tSuccess: {e.Status.DocumentsSucceeded}\tFail: {e.Status.DocumentsFailed}");
            }
        }

    }
}
