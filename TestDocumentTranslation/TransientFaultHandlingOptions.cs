using System;

namespace TranslationService.CLI
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
