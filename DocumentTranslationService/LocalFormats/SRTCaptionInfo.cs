using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService.LocalFormats
{
    /// <summary>
    /// Holds the attributes of a single caption for SRT/VTT files.
    /// </summary>
    internal struct SRTCaptionInfo
    {
        /// <summary>
        /// Holds the sequence number of the caption
        /// Sequence numbers in SRT files start at 1
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Holds the time code of the caption
        /// </summary>
        public string TimeCode { get; set; }

        /// <summary>
        /// Denotes the number of lines in the caption and their lengths
        /// </summary>
        public List<int> StringLengths { get; set; }

        /// <summary>
        /// Indicates whether the caption is a continuous sentence with the other lines of this caption
        /// </summary>
        public bool Continuous { get; set; }

        public SRTCaptionInfo()
        {
            SequenceNumber = 0;
            TimeCode = string.Empty;
            StringLengths = [];
            Continuous = true;
        }

        public void Clear()
        {
            SequenceNumber = 0;
            TimeCode = string.Empty;
            StringLengths?.Clear();
            Continuous = true;
        }
    }
}
