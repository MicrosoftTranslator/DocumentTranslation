using Azure.AI.Translation.Document;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    public partial class DocumentTranslationService
    {
        /// <summary>
        /// Holds the list of file formats after initial retrieval from Service
        /// </summary>
        public List<LocalFormats.LocalDocumentTranslationFileFormat> FileFormats { get; private set; } = [];

        public HashSet<string> Extensions { get; private set; } = [];

        public HashSet<string> GlossaryExtensions { get; private set; } = [];

        public event EventHandler OnFileFormatsUpdate;

        public List<LocalFormats.LocalDocumentTranslationFileFormat> GlossaryFormats { get; private set; } = [];

        public async Task<IReadOnlyList<LocalFormats.LocalDocumentTranslationFileFormat>> GetDocumentFormatsAsync()
        {
            if (FileFormats?.Count > 0) return FileFormats;
            else return await GetFormatsInternal();
        }

        private async Task<IReadOnlyList<LocalFormats.LocalDocumentTranslationFileFormat>> GetFormatsInternal()
        {
            JsonSerializerOptions options = new() { IncludeFields = true };
            if (String.IsNullOrEmpty(AzureResourceName)) throw new CredentialsException("name");
            for (int i = 0; i < 3; i++)
            {
                Azure.Response<SupportedFileFormats> result = null;
                try
                {
                    result = await documentTranslationClient.GetSupportedDocumentFormatsAsync();
                }
                catch (Azure.RequestFailedException ex)
                {
                    if (ex.Status == 401 || ex.Status == 403) throw new CredentialsException(ex.Message, ex);
                }
                catch (System.AggregateException ex)
                {
                    throw new Exception("Unknown host: " + ex.Message, ex);
                }

                if (result?.Value?.Value.Count > 0)
                {
                    Debug.WriteLine($"GetFormats: Response: {JsonSerializer.Serialize(result, options)}");
                    foreach (var item in result.Value.Value)
                    {
                        if (item.Type == FileFormatType.Document)
                        { //Add the file formats and extensions from the service
                            FileFormats.Add(new LocalFormats.LocalDocumentTranslationFileFormat(item.Format, [.. (List<string>)item.FileExtensions]));
                            foreach (string ext in item.FileExtensions)
                            {
                                Extensions.Add(ext.ToLowerInvariant());
                            }
                        }
                        else if (item.Type == FileFormatType.Glossary)
                        {
                            GlossaryFormats.Add(new LocalFormats.LocalDocumentTranslationFileFormat(item.Format, [.. (List<string>)item.FileExtensions]));

                            foreach (string ext in item.FileExtensions)
                            {
                                GlossaryExtensions.Add(ext.ToLowerInvariant());
                            }
                        }
                    }
                    //Add the formats and extensions for the locally provided formats
                    foreach (var localFormat in LocalFormats.LocalFormats.Formats)
                    {
                        LocalFormats.LocalDocumentTranslationFileFormat localDocumentTranslationFileFormat = new(
                            localFormat.Format,
                            localFormat.FileExtensions,
                            localFormat.ConvertToMarkdown,
                            localFormat.ConvertFromMarkdown
                        );
                        FileFormats.Add(localDocumentTranslationFileFormat);
                        foreach (string ext in localFormat.FileExtensions)
                        {
                            Extensions.Add(ext.ToLowerInvariant());
                        }
                    }
                    OnFileFormatsUpdate?.Invoke(this, EventArgs.Empty);
                    return FileFormats;
                }
                else
                {
                    Debug.WriteLine("GetFormatsInternal: Get file formats failed.");
                    await Task.Delay(1000);
                }
            }
            return null;
        }

    }
}
