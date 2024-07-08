using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AutoSweep.Structures;
using Dalamud.Game.ClientState.Objects.SubKinds;
using DebounceThrottle;
using Newtonsoft.Json;
using WebSocketSharp;

namespace AutoSweep.Paissa {
    public class PaissaClient : IDisposable {
        private readonly HttpClient http;
        private WebSocket ws;
        private int wsAttempts = 0;
        private bool disposed = false;
        private string sessionToken;
        internal bool needsHello = true;

        // dalamud
        private Plugin plugin;

        // ingest debounce
        private readonly DebounceDispatcher ingestDebounceDispatcher = new DebounceDispatcher(1200);
        private readonly ArrayList ingestDataQueue = new ArrayList();

#if DEBUG
        private const string apiBase = "http://127.0.0.1:8000";
        private const string wsRoute = "ws://127.0.0.1:8000/ws";
#else
        private const string apiBase = "https://paissadb.zhu.codes";
        private const string wsRoute = "wss://paissadb.zhu.codes/ws";
#endif


        public event EventHandler<PlotOpenedEventArgs> OnPlotOpened;
        public event EventHandler<PlotUpdateEventArgs> OnPlotUpdate;
        public event EventHandler<PlotSoldEventArgs> OnPlotSold;

        public PaissaClient(Plugin plugin) {
            this.plugin = plugin;
            http = new HttpClient();
            ReconnectWS();
        }

        public void Dispose() {
            disposed = true;
            http.Dispose();
            Task.Run(() => ws?.Close(1000));
        }

        // ==== Interface ====
        /// <summary>
        ///     Make a POST request to register the current character's content ID.
        /// </summary>
        public void Hello() {
            IPlayerCharacter player = Plugin.ClientState.LocalPlayer;
            if (player == null) return;
            var homeworld = player.HomeWorld.GameData;
            if (homeworld == null) return;
            var charInfo = new Dictionary<string, object> {
                { "cid", Plugin.ClientState.LocalContentId },
                { "name", player.Name.ToString() },
                { "world", homeworld.Name.ToString() },
                { "worldId", player.HomeWorld.Id }
            };
            string content = JsonConvert.SerializeObject(charInfo);
            Plugin.PluginLog.Debug(content);

            Task.Run(async () => {
                var response = await Post("/hello", content, false);
                if (response.IsSuccessStatusCode) {
                    string respText = await response.Content.ReadAsStringAsync();
                    sessionToken = JsonConvert.DeserializeObject<HelloResponse>(respText).session_token;
                    Plugin.PluginLog.Info("Completed PaissaDB HELLO");
                }
            });
        }

        /// <summary>
        ///     Fire and forget a POST request for a ward info.
        /// </summary>
        public void PostWardInfo(HousingWardInfo wardInfo, int serverTimestamp) {
            var data = new Dictionary<string, object> {
                { "event_type", "HOUSING_WARD_INFO" },
                { "client_timestamp", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() },
                { "server_timestamp", serverTimestamp },
                { "HouseInfoEntries", wardInfo.HouseInfoEntries },
                { "LandIdent", wardInfo.LandIdent },
                { "PurchaseType", wardInfo.PurchaseType },
                { "TenantType", wardInfo.TenantType }
            };
            queueIngest(data);
        }

        /// <summary>
        ///     Fire and forget a POST request for a placard's lottery info.
        /// </summary>
        public void PostLotteryInfo(uint worldId, ushort districtId, ushort wardId, ushort plotId,
            PlacardSaleInfo saleInfo) {
            var data = new Dictionary<string, object> {
                { "event_type", "LOTTERY_INFO" },
                { "client_timestamp", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() },
                { "WorldId", worldId },
                { "DistrictId", districtId },
                { "WardId", wardId },
                { "PlotId", plotId },
                { "PurchaseType", saleInfo.PurchaseType },
                { "TenantType", saleInfo.TenantType },
                { "AvailabilityType", saleInfo.AvailabilityType },
                { "PhaseEndsAt", saleInfo.PhaseEndsAt },
                { "EntryCount", saleInfo.EntryCount }
            };
            queueIngest(data);
        }

        /// <summary>
        ///     Get the district detail for a given district on a given world.
        /// </summary>
        /// <param name="worldId">The ID of the world</param>
        /// <param name="districtId">The ID of the district (339=Mist, 340=LB, 341=Gob, 641=Shiro, 979=Empy)</param>
        /// <returns>The DistrictDetail</returns>
        public async Task<DistrictDetail> GetDistrictDetailAsync(short worldId, short districtId) {
            HttpResponseMessage response = await http.GetAsync($"{apiBase}/worlds/{worldId}/{districtId}");
            Plugin.PluginLog.Debug(
                $"GET {apiBase}/worlds/{worldId}/{districtId} returned {response.StatusCode} ({response.ReasonPhrase})");
            response.EnsureSuccessStatusCode();
            string respText = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DistrictDetail>(respText);
        }

        // ==== HTTP ====
        private void queueIngest(object data) {
            ingestDataQueue.Add(data);
            ingestDebounceDispatcher.Debounce(() => {
                string bulkIngestData = JsonConvert.SerializeObject(ingestDataQueue);
                PostFireAndForget("/ingest", bulkIngestData);
                Plugin.PluginLog.Debug($"Bulk ingesting {ingestDataQueue.Count} entries ({bulkIngestData.Length}B)");
                ingestDataQueue.Clear();
            });
        }

        private async void PostFireAndForget(string route, string content, bool auth = true, ushort retries = 5) {
            await Post(route, content, auth, retries);
        }

        private async Task<HttpResponseMessage>
            Post(string route, string content, bool auth = true, ushort retries = 5) {
            HttpResponseMessage response = null;
            Plugin.PluginLog.Verbose(content);

            for (var i = 0; i < retries; i++) {
                HttpRequestMessage request;
                if (auth) {
                    if (sessionToken == null) {
                        Plugin.PluginLog.Warning("Trying to send authed request but no session token!");
                        needsHello = true;
                        return null;
                    }

                    request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}{route}") {
                        Content = new StringContent(content, Encoding.UTF8, "application/json"),
                        Headers = {
                            Authorization = new AuthenticationHeaderValue("Bearer", sessionToken)
                        }
                    };
                }
                else {
                    request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}{route}") {
                        Content = new StringContent(content, Encoding.UTF8, "application/json"),
                    };
                }

                try {
                    response = await http.SendAsync(request);
                    Plugin.PluginLog.Debug(
                        $"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase})");
                    if (!response.IsSuccessStatusCode) {
                        string respText = await response.Content.ReadAsStringAsync();
                        Plugin.PluginLog.Warning(
                            $"{request.Method} {request.RequestUri} returned {response.StatusCode} ({response.ReasonPhrase}):\n{respText}");
                    }
                    else {
                        break;
                    }
                }
                catch (Exception e) {
                    Plugin.PluginLog.Warning(e, $"{request.Method} {request.RequestUri} raised an error:");
                }

                // if our request failed, exponential backoff for 2 * (i + 1) seconds
                if (i + 1 < retries) {
                    int toDelay = 2000 * (i + 1) + new Random().Next(500, 1_500);
                    Plugin.PluginLog.Warning($"Request {i} failed, waiting for {toDelay}ms before retry...");
                    await Task.Delay(toDelay);
                }
            }

            // todo better error handling
            if (response == null) {
                Plugin.Chat.PrintError("There was an error connecting to PaissaDB.");
            }
            else if (!response.IsSuccessStatusCode) {
                Plugin.Chat.PrintError($"There was an error connecting to PaissaDB: {response.ReasonPhrase}");
            }

            return response;
        }


        // ==== WebSocket ====
        private void ReconnectWS() {
            Task.Run(() => {
                ws?.Close(1000);
                ws = new WebSocket(wsRoute);
                ws.OnOpen += OnWSOpen;
                ws.OnMessage += OnWSMessage;
                ws.OnClose += OnWSClose;
                ws.OnError += OnWSError;
                try {
                    ws.Connect();
                }
                catch (PlatformNotSupportedException) {
                    // idk why this happens but it doesn't seem to affect the ws
                    // silence for now to avoid polluting logs
                    // todo what is happening here?
                    // https://github.com/zhudotexe/FFXIV_PaissaHouse/issues/14
                }

                Plugin.PluginLog.Debug("ReconnectWS complete");
            });
        }

        private void OnWSOpen(object sender, EventArgs e) {
            Plugin.PluginLog.Information("WebSocket connected");
            wsAttempts = 0;
        }

        private void OnWSMessage(object sender, MessageEventArgs e) {
            if (!e.IsText) return;
            Plugin.PluginLog.Verbose($">>>> R: {e.Data}");
            var message = JsonConvert.DeserializeObject<WSMessage>(e.Data);
            switch (message.Type) {
                case "plot_open":
                    OnPlotOpened?.Invoke(this, new PlotOpenedEventArgs(message.Data.ToObject<OpenPlotDetail>()));
                    break;
                case "plot_update":
                    OnPlotUpdate?.Invoke(this, new PlotUpdateEventArgs(message.Data.ToObject<PlotUpdate>()));
                    break;
                case "plot_sold":
                    OnPlotSold?.Invoke(this, new PlotSoldEventArgs(message.Data.ToObject<SoldPlotDetail>()));
                    break;
                case "ping":
                    break;
                default:
                    Plugin.PluginLog.Warning($"Got unknown WS message: {e.Data}");
                    break;
            }
        }

        private void OnWSClose(object sender, CloseEventArgs e) {
            Plugin.PluginLog.Information($"WebSocket closed ({e.Code}: {e.Reason})");
            // reconnect if unexpected close or server restarting
            if ((!e.WasClean || e.Code == 1012) && !disposed)
                WSReconnectSoon();
        }

        private void OnWSError(object sender, ErrorEventArgs e) {
            Plugin.PluginLog.Warning(e.Exception, $"WebSocket error: {e.Message}");
            if (!disposed)
                WSReconnectSoon();
        }

        private void WSReconnectSoon() {
            if (ws.IsAlive) return;
            if (++wsAttempts > 5) {
                Plugin.PluginLog.Warning($"Could not connect to websocket after {wsAttempts} attempts; giving up");
                return;
            }

            int t = new Random().Next(5_000, 15_000);
            t *= wsAttempts;
            Plugin.PluginLog.Warning(
                $"WebSocket closed unexpectedly: will reconnect to socket in {t / 1000f:F3} seconds");
            Task.Run(async () => await Task.Delay(t)).ContinueWith(_ => {
                if (!disposed) ReconnectWS();
            });
        }
    }
}