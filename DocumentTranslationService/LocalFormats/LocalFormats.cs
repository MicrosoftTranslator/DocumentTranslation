using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DocumentTranslationService.LocalFormats
{
    /// <summary>
    /// Extensible list of locally converted formats. Add any new formats here
    /// Underlying assumption is that the format translated by the service is Markdown.
    /// You supply the functions to convert the local format to Markdown and from Markdown.
    /// </summary>
    public static class LocalFormats
    {
        /// <summary>
        /// List of formats that can be converted to and from Markdown
        /// Add to this list to add additional local file formats. 
        /// Extension must start with a period and be all lowercase.
        /// </summary>
        public static readonly List<LocalDocumentTranslationFileFormat> Formats = new() { new LocalDocumentTranslationFileFormat()
        {
                Format = "SubRip",
                FileExtensions = new() { ".srt" },
                ConvertToMarkdown = SRTMarkdownConverter.ConvertToMarkdown,
                ConvertFromMarkdown = SRTMarkdownConverter.ConvertToSRT
        } };

        /// <summary>
        /// Creates a new list of files to process, replacing the original filename where a local converter exists, with a temporary file in MarkDown format
        /// </summary>
        /// <param name="filenames">Original set of file names</param>
        /// <returns>List of service-translatable file names</returns>
        /// <exception cref="Exception">No converter found for this file's format</exception>
        public static List<string> PreprocessSourceFiles(List<string> filenames)
        {
            List<string> result = new();
            foreach (string filename in filenames)
            {
                if (IsLocalFormat(filename))
                {
                    var format = Formats.Find(f => f.FileExtensions.Contains(Path.GetExtension(filename.ToLowerInvariant())));
                    if (format.ConvertToMarkdown != null)
                    {
                        Debug.WriteLine($"Converting {filename} to Markdown");
                        string markdown = format.ConvertToMarkdown(File.ReadAllLines(filename));
                        string newFilename = Path.Combine(Path.GetTempPath(), Path.GetFileName(filename) + ".md");
                        File.WriteAllText(newFilename, markdown);
                        result.Add(newFilename);
                    }
                    else throw new Exception($"No conversion function found for {filename}, but listed as local format.");
                }
                else result.Add(filename);
            }
            return result;
        }

        /// <summary>
        /// After translation, convert from MarkDown back to original format
        /// </summary>
        /// <param name="filenames">List of filenames, whether they need conversion or not</param>
        /// <exception cref="Exception">Exception if there is noi conversion function found for this filename extension</exception>
        public static void PostprocessTargetFiles(List<string> filenames)
        {
            foreach (string filename in filenames)
            {
                if (IsLocalFormat(filename, out string originalextension))
                {
                    var format = Formats.Find(f => f.FileExtensions.Contains(originalextension));
                    if (format.ConvertFromMarkdown != null)
                    {
                        Debug.WriteLine($"Converting {filename} from Markdown to {originalextension}");
                        string markdown = File.ReadAllText(filename);
                        string newFilename = filename[..filename.LastIndexOf('.')];
                        File.WriteAllText(newFilename, format.ConvertFromMarkdown(markdown));
                        //#if !DEBUG
                        //Delete the temporary files
                        File.Delete(filename);
                        File.Delete(Path.Combine(Path.GetTempPath(), Path.GetFileName(filename)));
                        //#endif
                    }
                    else throw new Exception($"No conversion function found for {filename}, but listed as local format.");
                }
            }
        }

        /// <summary>
        /// Determine if the file is a locally processed format
        /// </summary>
        /// <param name="filename">Single file name</param>
        /// <returns>TRUE if this is a locally converted file</returns>
        private static bool IsLocalFormat(string filename)
        {
            foreach (var format in Formats)
            {
                if (format.IsLocal)
                {
                    if (format.FileExtensions.Contains(Path.GetExtension(filename.ToLowerInvariant())))
                        return true;
                }
            }
            return false;
        }



        /// <summary>
        /// In post-processing: Determine if the file is a local format, and if so, return the original extension.
        /// </summary>
        /// <param name="filename">Post-processed file name</param>
        /// <param name="originalextension">Return the original extension</param>
        /// <returns>True of the original extension indicates local processing was necessary</returns>
        private static bool IsLocalFormat(string filename, out string originalextension)
        {
            string _originalextension = GetSubstringBetweenLastTwoPeriods(filename)?.ToLower();
            if (string.IsNullOrEmpty(_originalextension))
            {
                originalextension = null;
                return false;
            }
            foreach (var format in Formats)
            {
                if (format.IsLocal)
                {
                    if (
                        (Path.GetExtension(filename.ToLowerInvariant()) == ".md") &&
                        format.FileExtensions.Contains("." + _originalextension)
                        )
                    {
                        originalextension = "." + _originalextension;
                        return true;
                    }
                    else
                        originalextension = Path.GetExtension(filename.ToLowerInvariant());
                    return false;
                }
            }
            originalextension = Path.GetExtension(filename.ToLowerInvariant());
            return false;
        }


        private static string GetSubstringBetweenLastTwoPeriods(string input)
        {
            int lastPeriodIndex = input.LastIndexOf('.');
            int secondToLastPeriodIndex = input.LastIndexOf('.', lastPeriodIndex - 1);

            if (lastPeriodIndex == -1)
            {
                // If there is no period, return null.
                return null;
            }

            if (secondToLastPeriodIndex == -1)
            {
                // If there is only one period or none, return null.
                return null;
            }

            int startIndex = secondToLastPeriodIndex + 1;
            int length = lastPeriodIndex - startIndex;

            string substring = input.Substring(startIndex, length);
            return substring;
        }
    }
}
