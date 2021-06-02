using System;
using System.Runtime.Serialization;

namespace DocumentTranslationService.Core
{
    public class ServiceErrorException : Exception
    {
        public ServiceErrorException()
        {
        }

        public ServiceErrorException(string message) : base(message)
        {
        }

        public ServiceErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ServiceErrorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
