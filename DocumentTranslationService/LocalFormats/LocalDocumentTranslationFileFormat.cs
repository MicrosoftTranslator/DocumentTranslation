using Azure.AI.Translation.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService.LocalFormats
{
    public delegate string ConvertToMarkdown(string[] input);
    public delegate string ConvertFromMarkdown(string input);

    /// <summary>
    /// Hold the information about a file format, the associated extensions, and  how it can be converted to and from Markdown.
    /// </summary>
    public struct LocalDocumentTranslationFileFormat
    {
        public LocalDocumentTranslationFileFormat(string name, List<string> extensions) : this()
        {
            Format = name;
            FileExtensions = extensions;
            ConvertToMarkdown = null;
            ConvertFromMarkdown = null;
        }

        public LocalDocumentTranslationFileFormat(string name, List<string> extensions, ConvertToMarkdown convertToMarkdown, ConvertFromMarkdown convertFromMarkdown) : this(name, extensions)
        {
            ConvertToMarkdown = convertToMarkdown;
            ConvertFromMarkdown = convertFromMarkdown;
        }

        public string Format { get; set; }
        public List<string> FileExtensions { get; set; }
        public ConvertToMarkdown ConvertToMarkdown { get; set; }
        public ConvertFromMarkdown ConvertFromMarkdown { get; set; }
        public readonly bool IsLocal => ConvertToMarkdown != null && ConvertFromMarkdown != null;
    }
}
