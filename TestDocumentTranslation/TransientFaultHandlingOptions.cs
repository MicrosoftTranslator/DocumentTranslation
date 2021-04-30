using System;

namespace TestDocumentTranslation
{
    partial class Program
    {
        class TransientFaultHandlingOptions
        {
            public bool Enabled { get; set; }
            public TimeSpan AutoRetryDelay { get; set; }
        }
    }
}
