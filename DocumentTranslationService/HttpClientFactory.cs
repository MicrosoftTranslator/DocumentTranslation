using DocumentTranslationService.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService
{
    public class HttpClientFactory
    {
        public static HttpClient GetHttpClient()
        {
            var settings = AppSettingsSetter.Read();

            if (settings.UsingProxy == false)
            {
                return new HttpClient();
            }

            var proxy = new WebProxy 
            { 
                UseDefaultCredentials = settings.ProxyUseDefaultCredentials, 
            };

            if (!string.IsNullOrEmpty(settings.ProxyAddress))
            {
                proxy.Address = new Uri(settings.ProxyAddress);
            }

            var httpClientHandler = new HttpClientHandler { Proxy = proxy };

            return new HttpClient(handler: httpClientHandler, disposeHandler: true);

        }
    }
}
