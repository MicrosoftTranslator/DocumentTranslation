using System;

namespace DocumentTranslationServices.Core
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

