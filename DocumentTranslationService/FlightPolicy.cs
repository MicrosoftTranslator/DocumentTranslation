using Azure.Core;
using Azure.Core.Pipeline;

namespace DocumentTranslationService
{
    internal class FlightPolicy : HttpPipelineSynchronousPolicy
    {
        private readonly string[] flightStrings;
        /// <summary>
        /// Sets the string to be used in a flight
        /// </summary>
        /// <param name="flightString">the string to be used in a flight</param>
        public FlightPolicy(string flightString)
        {
            char[] splitchars = { ',', ';', ' ' };
            this.flightStrings = flightString.Split(splitchars);
        }

        public override void OnSendingRequest(HttpMessage message)
        {
            if (message.Request.Method.Method == "POST")
            {
                foreach (string s in flightStrings)
                {
                    string st = s.Trim();
                    if (!string.IsNullOrEmpty(st))
                        message.Request.Uri.AppendQuery("flight", st);
                }
            }
        }
    }
}
