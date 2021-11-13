using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AutoSweep.Structures;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;
using Newtonsoft.Json;
using WebSocketSharp;

namespace AutoSweep.Paissa {
    public class PaissaClient : IDisposable {
        private readonly HttpClient http;
        private WebSocket ws;
        private readonly JwtEncoder encoder = new(new HMACSHA256Algorithm(), new JsonNetSerializer(), new JwtBase64UrlEncoder());
        private bool disposed = false;

        // dalamud
        private readonly ClientState clientState;
        private readonly ChatGui chat;

#if DEBUG
        private const string apiBase = "http://127.0.0.1:8000";
        private const string wsRoute = "ws://127.0.0.1:8000/ws";
#else
        private const string apiBase = "https://paissadb.zhu.codes";
        private const string wsRoute = "wss://paissadb.zhu.codes/ws";
#endif

        private readonly byte[] secret = Encoding.UTF8.GetBytes(Secrets.JwtSecret);

        public event EventHandler<PlotOpenedEventArgs> OnPlotOpened;
        public event EventHandler<PlotSoldEventArgs> OnPlotSold;

        public PaissaClient(ClientState clientState, ChatGui chatGui) {
            this.clientState = clientState;
            chat = chatGui;
            http = new HttpClient();
            ReconnectWS();
        }

        public void Dispose() {
            disposed = true;
            http.Dispose();
            Task.Run(() => ws?.Close(1000));
        }

        // ==== HTTP ====
        /// <summary>
        ///     Fire and forget a POST request to register the current character's content ID.
        /// </summary>
        public async void Hello() {
            PlayerCharacter player = clientState.LocalPlayer;
            if (player == null)
                return;
            var charInfo = new Dictionary<string, object> {
                { "cid", clientState.LocalContentId },
                { "name", player.Name.ToString() },
                { "world", player.HomeWorld.GameData.Name.ToString() },
                { "worldId", player.HomeWorld.Id }
            };
            string content = JsonConvert.SerializeObject(charInfo);
            PluginLog.Debug(content);
            await PostFireAndForget("/hello", content);
        }

        /// <summary>
        ///     Fire and forget a POST request for a ward info.
        /// </summary>
        public async void PostWardInfo(HousingWardInfo wardInfo, int serverTimestamp) {
            var data = new Dictionary<string, object> {
                { "HouseInfoEntries", wardInfo.HouseInfoEntries },
                { "LandIdent", wardInfo.LandIdent },
                { "ClientTimestamp", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() },
                { "ServerTimestamp", serverTimestamp }
            };
            string content = JsonConvert.SerializeObject(data);
            await PostFireAndForget("/wardInfo", content);
        }

        /// <summary>
        ///     Get the district detail for a given district on a given world.
        /// </summary>
        /// <param name="worldId">The ID of the world</param>
        /// <param name="districtId">The ID of the district (339=Mist, 340=LB, 341=Gob, 641=Shiro)</param>
        /// <returns>The DistrictDetail</returns>
        public async Task<DistrictDetail> GetDistrictDetailAsync(short worldId, short districtId) {
            HttpResponseMessage response = await http.GetAsync($"{apiBase}/worlds/{worldId}/{districtId}");
            PluginLog.Debug($"GET {apiBase}/worlds/{worldId}/{districtId} returned {response.StatusCode} ({response.ReasonPhrase})");
            response.EnsureSuccessStatusCode();
            string respText = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DistrictDetail>(respText);
        }

        private async Task PostFireAndForget(string route, string content) {
            await PostFireAndForget(route, content, 5);
        }

        private async Task PostFireAndForget(string route, string content, ushort retries) {
            HttpResponseMessage response = null;

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}{route}") {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };


            for (var i = 0; i < retries; i++) {
                // refresh request headers to update jwt generation time
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwt());
                try {
                    response = await http.SendAsync(request);
                    PluginLog.Debug($"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase})");
                    if (!response.IsSuccessStatusCode) {
                        string respText = await response.Content.ReadAsStringAsync();
                        PluginLog.Warning($"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase}):\n{respText}");
                    } else {
                        break;
                    }
                } catch (Exception e) {
                    PluginLog.Warning(e, $"{request.Method} {request.RequestUri} raised an error:");
                }
                // if our request failed, exponential backoff for 2 * (i + 1) seconds
                if (i + 1 < retries) {
                    int toDelay = 2000 * (i + 1) + new Random().Next(500, 1_500);
                    PluginLog.Warning($"Request {i} failed, waiting for {toDelay}ms before retry...");
                    await Task.Delay(toDelay);
                }
            }

            if (response == null)
                chat.PrintError("There was an error connecting to PaissaDB.");
            else if (!response.IsSuccessStatusCode)
                chat.PrintError($"There was an error connecting to PaissaDB: {response.ReasonPhrase}");
        }


        // ==== WebSocket ====
        private void ReconnectWS() {
            Task.Run(() => {
                ws?.Close(1000);
                ws = new WebSocket(GetWSRouteWithAuth());
                ws.OnOpen += OnWSOpen;
                ws.OnMessage += OnWSMessage;
                ws.OnClose += OnWSClose;
                ws.OnError += OnWSError;
                ws.Connect();
                PluginLog.Debug("ReconnectWS complete");
            });
        }

        private void OnWSOpen(object sender, EventArgs e) {
            PluginLog.Information("WebSocket connected");
        }

        private void OnWSMessage(object sender, MessageEventArgs e) {
            if (!e.IsText) return;
            PluginLog.Verbose($">>>> R: {e.Data}");
            var message = JsonConvert.DeserializeObject<WSMessage>(e.Data);
            switch (message.Type) {
                case "plot_open":
                    OnPlotOpened?.Invoke(this, new PlotOpenedEventArgs(message.Data.ToObject<OpenPlotDetail>()));
                    break;
                case "plot_sold":
                    OnPlotSold?.Invoke(this, new PlotSoldEventArgs(message.Data.ToObject<SoldPlotDetail>()));
                    break;
                case "ping":
                    break;
                default:
                    PluginLog.Warning($"Got unknown WS message: {e.Data}");
                    break;
            }
        }

        private void OnWSClose(object sender, CloseEventArgs e) {
            PluginLog.Information($"WebSocket closed ({e.Code}: {e.Reason})");
            // reconnect if unexpected close or server restarting
            if ((!e.WasClean || e.Code == 1012) && !disposed)
                WSReconnectSoon();
        }

        private void OnWSError(object sender, ErrorEventArgs e) {
            PluginLog.LogWarning(e.Exception, $"WebSocket error: {e.Message}");
            if (!disposed)
                WSReconnectSoon();
        }

        private void WSReconnectSoon() {
            if (ws.IsAlive) return;
            int t = new Random().Next(5_000, 15_000);
            PluginLog.Warning($"WebSocket closed unexpectedly: will reconnect to socket in {t / 1000f:F3} seconds");
            Task.Run(async () => await Task.Delay(t)).ContinueWith(_ => {
                if (!disposed) ReconnectWS();
            });
        }


        // ==== helpers ====
        private string GenerateJwt() {
            var payload = new Dictionary<string, object> {
                { "cid", clientState.LocalContentId },
                { "aud", "PaissaHouse" },
                { "iss", "PaissaDB" },
                { "iat", DateTimeOffset.Now.ToUnixTimeSeconds() }
            };
            return encoder.Encode(payload, secret);
        }

        private string GetWSRouteWithAuth() {
            return $"{wsRoute}?jwt={GenerateJwt()}";
        }
    }
}
