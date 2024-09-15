using System;

namespace DocumentTranslationService.Core
{
    public partial class DocumentTranslationService
    {
        [Serializable]
        public class CredentialsException : Exception
        {
            public CredentialsException() : base() { }
            public CredentialsException(string reason) : base(reason) { }
            public CredentialsException(string reason, Exception innerException) : base(reason, innerException) { }
        }
    }
}

