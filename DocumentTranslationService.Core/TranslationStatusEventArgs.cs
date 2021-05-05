using System;

namespace DocumentTranslationService.Core
{
    public class TranslationStatusEventArgs : EventArgs
    {
        public TranslationStatusEventArgs(StatusResponse statusResponse)
        {
            Status = statusResponse;
        }

        public StatusResponse Status { get; set; }
    }
}

