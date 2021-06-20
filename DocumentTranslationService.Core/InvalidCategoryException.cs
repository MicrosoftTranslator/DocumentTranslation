/*
 * Text Translation Service Facade
 */

using System;
using System.Runtime.Serialization;

namespace DocumentTranslationService.Core
{
    /// <summary>
    /// Throws when an invalid categoryID value is encountered
    /// </summary>
    [Serializable]
    public class InvalidCategoryException : Exception
    {
        public InvalidCategoryException()
        {
        }

        public InvalidCategoryException(string message) : base(message)
        {
        }

        public InvalidCategoryException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidCategoryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}