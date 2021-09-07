/*
 * Text Translation Service Facade
 */

using System;
using System.Runtime.Serialization;

namespace DocumentTranslationService.Core
{
    /// <summary>
    /// Throws when there is trouble with getting secrets from Azure KeyVault
    /// </summary>
    [Serializable]
    public class KeyVaultAccessException : Exception
    {
        public KeyVaultAccessException()
        {
        }

        public KeyVaultAccessException(string message) : base(message)
        {
        }

        public KeyVaultAccessException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected KeyVaultAccessException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}