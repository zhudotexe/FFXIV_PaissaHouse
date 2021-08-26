using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AutoSweep.Structures;
using Dalamud.Game.Internal;
using Dalamud.Plugin;
using Jose;
using Newtonsoft.Json;
using WebSocketSharp;

namespace AutoSweep.Paissa
{
    public class PaissaClient : IDisposable
    {
        private readonly HttpClient http;
        private readonly WebSocket ws;
        private readonly DalamudPluginInterface pi;
        private bool needsHello = true;
        private bool disposed = false;

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

        public PaissaClient(DalamudPluginInterface pi)
        {
            http = new HttpClient();
            ws = new WebSocket(GetWSRouteWithAuth());
            ws.OnOpen += OnWSOpen;
            ws.OnMessage += OnWSMessage;
            ws.OnClose += OnWSClose;
            ws.OnError += OnWSError;
            ws.ConnectAsync();
            this.pi = pi;
            this.pi.ClientState.OnLogin += OnLogin;
            this.pi.Framework.OnUpdateEvent += OnUpdateEvent;
        }

        public void Dispose()
        {
            disposed = true;
            http.Dispose();
            ws.CloseAsync(1000);
            this.pi.ClientState.OnLogin -= OnLogin;
            this.pi.Framework.OnUpdateEvent -= OnUpdateEvent;
        }

        // ==== HTTP ====
        /// <summary>
        /// Fire and forget a POST request to register the current character's content ID.
        /// </summary>
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

        /// <summary>
        /// Fire and forget a POST request for a ward info.
        /// </summary>
        public async void PostWardInfo(HousingWardInfo wardInfo)
        {
            var content = JsonConvert.SerializeObject(wardInfo);
            await PostFireAndForget("/wardInfo", content);
        }

        /// <summary>
        /// Get the district detail for a given district on a given world.
        /// </summary>
        /// <param name="worldId">The ID of the world</param>
        /// <param name="districtId">The ID of the district (339=Mist, 340=LB, 341=Gob, 641=Shiro)</param>
        /// <returns>The DistrictDetail</returns>
        public async Task<DistrictDetail> GetDistrictDetailAsync(short worldId, short districtId)
        {
            var response = await http.GetAsync($"{apiBase}/worlds/{worldId}/{districtId}");
            PluginLog.Debug($"GET {apiBase}/worlds/{worldId}/{districtId} returned {response.StatusCode} ({response.ReasonPhrase})");
            response.EnsureSuccessStatusCode();
            var respText = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DistrictDetail>(respText);
        }

        private async Task PostFireAndForget(string route, string content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}{route}")
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
                Headers =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", GenerateJwt())
                }
            };
            try {
                var response = await http.SendAsync(request);
                PluginLog.Debug($"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase})");
                if (!response.IsSuccessStatusCode) {
                    var respText = await response.Content.ReadAsStringAsync();
                    PluginLog.Warning($"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase}):\n{respText}");
                    pi.Framework.Gui.Chat.PrintError($"There was an error connecting to PaissaDB: {response.ReasonPhrase}");
                }
            }
            catch (Exception e) {
                PluginLog.Warning(e, $"{request.Method} {request.RequestUri} raised an error:");
                pi.Framework.Gui.Chat.PrintError("There was an error connecting to PaissaDB.");
            }
        }

        // ==== WebSocket ====
        private void OnWSOpen(object sender, EventArgs e)
        {
            PluginLog.Information("WebSocket connected");
        }

        private void OnWSMessage(object sender, MessageEventArgs e)
        {
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

        private void OnWSClose(object sender, CloseEventArgs e)
        {
            PluginLog.Information($"WebSocket closed ({e.Code}: {e.Reason})");
            // reconnect if unexpected close or server restarting
            if ((!e.WasClean || e.Code == 1012) && !disposed)
                WSReconnectSoon();
        }

        private void OnWSError(object sender, ErrorEventArgs e)
        {
            PluginLog.LogWarning(e.Exception, $"WebSocket error: {e.Message}");
            if (!disposed)
                WSReconnectSoon();
        }

        private void WSReconnectSoon()
        {
            if (ws.IsAlive) return;
            var t = new Random().Next(5_000, 15_000);
            PluginLog.Warning($"WebSocket closed unexpectedly: will reconnect to socket in {t / 1000f:F3} seconds");
            Task.Run(async () => await Task.Delay(t)).ContinueWith(_ =>
            {
                if (!disposed) ws.ConnectAsync();
            });
        }

        // ==== Dalamud listeners ====
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
        private string GenerateJwt()
        {
            var payload = new Dictionary<string, object>()
            {
                {"cid", pi.ClientState.LocalContentId},
                {"aud", "PaissaHouse"},
                {"iss", "PaissaDB"},
                {"iat", DateTimeOffset.Now.ToUnixTimeSeconds()}
            };
            return JWT.Encode(payload, secret, JwsAlgorithm.HS256);
        }

        private string GetWSRouteWithAuth()
        {
            return $"{wsRoute}?jwt={GenerateJwt()}";
        }
    }
}
