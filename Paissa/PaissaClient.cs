using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AutoSweep.Structures;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using JWT.Algorithms;
using JWT.Builder;
using Newtonsoft.Json;

namespace AutoSweep.Paissa
{
    public class PaissaClient : IDisposable
    {
        private HttpClient http;
        private DalamudPluginInterface pi;
        private bool needsHello = true;

        private const string apiBase = "http://127.0.0.1:8000"; // todo use an actual server
        private const string secret = "supersecretsecret"; // todo

        public PaissaClient(DalamudPluginInterface pi)
        {
            http = new HttpClient();
            this.pi = pi;
            this.pi.ClientState.OnLogin += OnLogin;
            this.pi.Framework.OnUpdateEvent += OnUpdateEvent;
        }

        public void Dispose()
        {
            http.Dispose();
            this.pi.ClientState.OnLogin -= OnLogin;
            this.pi.Framework.OnUpdateEvent -= OnUpdateEvent;
        }

        // ==== HTTP ====
        /**
         * Fire and forget a POST request to register the current character's content ID.
         */
        public async void Hello()
        {
            var player = pi.ClientState.LocalPlayer;
            if (player == null)
                return;
            var charInfo = new Dictionary<string, object>()
            {
                {"cid", pi.ClientState.LocalContentId},
                {"name", player.Name},
                {"world", player.HomeWorld.GameData.Name.ToString()},
                {"worldId", player.HomeWorld.Id}
            };
            var content = JsonConvert.SerializeObject(charInfo);
            PluginLog.Debug(content);
            await PostFireAndForget("/hello", content);
        }

        /**
         * Fire and forget a POST request for a HousingWardInfo.
         */
        public async void PostWardInfo(HousingWardInfo wardInfo)
        {
            var content = JsonConvert.SerializeObject(wardInfo);
            await PostFireAndForget("/wardInfo", content);
        }

        // ==== event listeners ====
        private void OnLogin(object _, EventArgs __)
        {
            needsHello = true;
        }

        private void OnUpdateEvent(Framework framework)
        {
            if (needsHello && pi.ClientState.LocalPlayer != null) {
                needsHello = false;
                Hello();
            }
        }

        // ==== helpers ====
        private async Task PostFireAndForget(string route, string content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}{route}")
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
                Headers =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", GenerateJWT())
                }
            };
            try {
                var response = await http.SendAsync(request);
                PluginLog.Debug($"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase})");
            }
            catch (Exception e) {
                PluginLog.Warning(e, $"{request.Method} {request.RequestUri} raised an error:");
            }
        }

        private string GenerateJWT()
        {
            return JwtBuilder.Create()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .WithSecret(secret)
                .AddClaim("cid", pi.ClientState.LocalContentId)
                .AddClaim("iss", "PaissaHouse")
                .AddClaim("aud", "PaissaDB")
                .AddClaim("iat", DateTimeOffset.Now.ToUnixTimeSeconds())
                .Encode();
        }
    }
}
