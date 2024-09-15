using System.Text.Json;
using System.Collections.Generic;
using System;

namespace DocumentTranslationService.LocalFormats
{
    public static class SRTMarkdownConverter
    {
        /// <summary>
        /// Convert SRT to Markdown. The SRT markup is encoded in comments. The encoding itself is a JSON array of a CaptionInfo object in Base64 format.
        /// The lines of a single caption are combined into a single line (Microsoft does not combine lines of the markdown, as is required.)
        /// </summary>
        /// <param name="srtLines">The content of an SRT file in string array form.</param>
        /// <returns>Markdown document with comments</returns>
        public static string ConvertToMarkdown(string[] srtLines)
        {
            ///TODO: This currently only combines the lines from a single caption. However, often a sentence continues over multiple captions.
            ///It would be better to combine multiple captions and translate as a complete sentence after sent end punc is found, and then split them
            ///out again over multiple captions after translation. 
            string markdownText = string.Empty;
            string currentText = string.Empty;
            SRTCaptionInfo captionInfo = new();
            foreach (string line in srtLines)
            {
                if (IsSequenceNumber(line, out int sequenceNumber))
                {
                    captionInfo.Clear();
                    captionInfo.SequenceNumber = sequenceNumber;
                    continue;
                }
                else
                if (IsTimeCode(line))
                {
                    captionInfo.TimeCode = line;
                    continue;
                }
                else
                if (string.IsNullOrWhiteSpace(line)) //empty line denotes end of caption
                {
                    markdownText += currentText;
                    currentText = string.Empty;
                    markdownText += $"\n<!-- {StringCompression.Compress(JsonSerializer.Serialize(captionInfo))} -->\n";
                    captionInfo.Clear();
                    continue;
                }
                //normal caption text
                currentText += line + " ";
                captionInfo.StringLengths.Add(line.Length);
            }
            if (captionInfo.SequenceNumber > 1) //write out any unwritten caption info
            {
                markdownText += $"\n<!-- {StringCompression.Compress(JsonSerializer.Serialize(captionInfo))} -->\n";
            }
            return markdownText;
        }

        /// <summary>
        /// Convert Markdown to SRT. The SRT markup is encoded in comments. The encoding itself is a JSON array of a CaptionInfo object in Base64 format.
        /// </summary>
        /// <param name="markdownText">Text in Markdown format as a string.</param>
        /// <returns>SRT document</returns>
        public static string ConvertToSRT(string markdownText)
        {
            string srtText = string.Empty;
            List<string> lines = SplitIntoLines(markdownText);
            string currentText = string.Empty;
            foreach (string line in lines)
            {
                if (line.StartsWith("<!--"))
                {
                    SRTCaptionInfo captionInfo = JsonSerializer.Deserialize<SRTCaptionInfo>(StringCompression.Decompress(line[5..^3]));
                    srtText += $"{captionInfo.SequenceNumber}\n{captionInfo.TimeCode}\n";
                    //Write out the caption string split into lines
                    srtText += SplitByLength(currentText, captionInfo.StringLengths);
                    srtText += "\n";
                    currentText = string.Empty;
                    continue;
                }
                else
                {
                    currentText += line + " ";
                }
            }
            return srtText;
        }

        /// <summary>
        /// Split markdown text into lines. Split on newline or comment start. This is designed to solve the problem that after
        /// translation the comment may not be on its separate line anymore.
        /// </summary>
        /// <param name="markdownText">Markdown text to be processed</param>
        /// <returns>List of strings containing the intended lines of the Markdown text</returns>
        private static List<string> SplitIntoLines(string markdownText)
        {
            List<string> lines = [];
            int index = 0;
            while (index < markdownText.Length)
            {
                int nextIndex = Math.Min(markdownText.IndexOf(@"<!--", index), markdownText.IndexOf(@"-->", index) + 3);
                if (index >= 1) index--;
                if (nextIndex == -1)
                {
                    lines.Add(markdownText[index..].Trim());
                    break;
                }
                lines.Add(markdownText[index..nextIndex].Trim());
                index = nextIndex + 1;
            }
            return lines;
        }

        /// <summary>
        /// Receive a string and a list of lengths. Split the string into lines of the given lengths, by using the nearest word break to the length.
        /// Search forward and backward from the length to find the nearest word break. Consider punctuation as a word break opportunity.
        /// </summary>
        /// <param name="currentText">The text to split</param>
        /// <param name="stringLengths">A list of desired string lengths</param>
        /// <returns></returns>
        private static string SplitByLength(string currentText, List<int> stringLengths)
        {
            if (string.IsNullOrEmpty(currentText)) return string.Empty;
            if (stringLengths.Count <= 1) return currentText + "\n";
            string result = string.Empty;
            for (int i = 0; i < stringLengths.Count - 1; i++)
            {
                int length = stringLengths[i];
                int forwardIndex = Math.Min(length, currentText.Length);
                int backwardIndex = Math.Max(0, currentText.Length - length);
                while (forwardIndex < currentText.Length && !char.IsWhiteSpace(currentText[forwardIndex]) && !char.IsPunctuation(currentText[forwardIndex]))
                {
                    forwardIndex++;
                }
                while (backwardIndex > 0 && !char.IsWhiteSpace(currentText[backwardIndex]) && !char.IsPunctuation(currentText[backwardIndex]))
                {
                    backwardIndex--;
                }
                int index = Math.Abs(length - forwardIndex) < Math.Abs(length - backwardIndex) ? forwardIndex : backwardIndex;
                if (index >= currentText.Length) index = currentText.Length - 1;
                result += string.Concat(currentText.AsSpan(0, index), "\n");
                currentText = currentText[index..];
                if (currentText.Length == 0) break;
                if (currentText[0] == ' ') currentText = currentText[1..];
                if (currentText.Length <= 2)
                {
                    result += currentText;
                    break;
                }
            }
            return string.Concat(result, currentText, "\n");
        }



        /// <summary>
        /// Determine if a line is a SRT style time code. Time codes are in the format hh:mm:ss,fff --> hh:mm:ss,fff
        /// </summary>
        /// <param name="line">A line of the SRT file.</param>
        /// <returns>Whether the line contains an SRT style time code.</returns>
        private static bool IsTimeCode(string line)
        {
            if (!line.Contains("-->")) return false;
            ///This pattern is meant to match the time code format of SRT files and of VTT files.
            ///VTT uses the US decimal separator and allows hours to be optional
            string timeCodePattern = @"(\d{2}:){1,2}\d{2}[,.]\d{3}\s-->\s(\d{2}:){1,2}\d{2}[,.]\d{3}";
            return System.Text.RegularExpressions.Regex.IsMatch(line, timeCodePattern, System.Text.RegularExpressions.RegexOptions.Compiled);
        }

        /// <summary>
        /// Determine if a line is a sequence number. SRT style sequence numbers are one integer on a line by itself.
        /// </summary>
        /// <param name="line">A line of the SRT file</param>
        /// <param name="sequenceNumber">If there is a single number on the line, return it.</param>
        /// <returns></returns>
        private static bool IsSequenceNumber(string line, out int sequenceNumber)
        {
            return int.TryParse(line, out sequenceNumber);
        }

    }
}
