using System;
using System.Net.Http;
using System.Text;
using AutoSweep.Structures;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace AutoSweep.Paissa
{
    public class PaissaClient : IDisposable
    {
        private HttpClient http;

        public PaissaClient()
        {
            http = new HttpClient();
        }

        public void Dispose()
        {
            http.Dispose();
        }

        /**
         * Fire and forget a POST request for a HousingWardInfo.
         */
        public async void PostWardInfo(HousingWardInfo wardInfo)
        {
            var content = JsonConvert.SerializeObject(wardInfo);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://hookb.in/yDRrEdD6rrFJNNPaRKVJ") // todo use an actual server
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };

            try
            {
                var response = await http.SendAsync(request);
                PluginLog.Debug($"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase})");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"{request.Method} {request.RequestUri} raised an error:");
            }
        }
    }
}
