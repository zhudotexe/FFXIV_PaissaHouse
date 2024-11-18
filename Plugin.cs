using System.Diagnostics;
using AutoSweep.Paissa;
using AutoSweep.Structures;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AutoSweep {
    public sealed class Plugin : IDalamudPlugin {
        public string Name => "PaissaHouse";

        // frameworks/data
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IChatGui Chat { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService] internal static IGameInteropProvider InteropProvider { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        internal readonly Configuration Configuration;
        internal readonly PaissaClient PaissaClient;

        internal readonly ExcelSheet<HousingLandSet> HousingLandSets;
        internal readonly ExcelSheet<TerritoryType> Territories;
        internal readonly ExcelSheet<World> Worlds;
        private readonly DalamudLinkPayload chatLinkPayload;

        // state
        private readonly WardObserver wardObserver;
        private readonly LotteryObserver lotteryObserver;
        private readonly PluginUI ui;

        public Plugin() {
            // setup
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ui = new PluginUI(Configuration);
            Territories = DataManager.GetExcelSheet<TerritoryType>();
            Worlds = DataManager.GetExcelSheet<World>();
            HousingLandSets = DataManager.GetExcelSheet<HousingLandSet>();

            Commands.AddHandler(Utils.HouseCommandName, new CommandInfo(OnHouseCommand) {
                HelpMessage = "View all houses available for sale."
            });
            Commands.AddHandler(Utils.CommandName, new CommandInfo(OnCommand) {
                HelpMessage =
                    $"Configure PaissaHouse settings.\n\"{Utils.CommandName} reset\" to reset a sweep if sweeping the same district multiple times in a row."
            });

            chatLinkPayload = PluginInterface.AddChatLinkHandler(0, OnChatLinkClick);

            // event hooks
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Framework.Update += OnUpdateEvent;
            ClientState.Login += OnLogin;

            // paissa setup
            wardObserver = new WardObserver(this);
            lotteryObserver = new LotteryObserver(this);
            PaissaClient = new PaissaClient(this);
            PaissaClient.OnPlotOpened += OnPlotOpened;
            PaissaClient.OnPlotUpdate += OnPlotUpdate;

            PluginLog.Debug($"Initialization complete: configVersion={Configuration.Version}");
        }

        public void Dispose() {
            ui.Dispose();
            Framework.Update -= OnUpdateEvent;
            ClientState.Login -= OnLogin;
            Commands.RemoveHandler(Utils.CommandName);
            Commands.RemoveHandler(Utils.HouseCommandName);
            PluginInterface.RemoveChatLinkHandler();
            PaissaClient?.Dispose();
            lotteryObserver.Dispose();
            wardObserver.Dispose();
        }

        // ==== dalamud events ====
        private void OnHouseCommand(string command, string args) {
            Chat.Print(new SeString(
                new TextPayload("Thanks for using PaissaHouse! You can view all of the houses available for sale at "),
                new UIForegroundPayload(710),
                chatLinkPayload,
                new TextPayload("https://paissadb.zhu.codes/"),
                RawPayload.LinkTerminator,
                new UIForegroundPayload(0),
                new TextPayload(" (opens in browser)."))
            );
        }

        private void OnCommand(string command, string args) {
            switch (args) {
                case "reset":
                    wardObserver.SweepState.Reset();
                    Chat.Print("The sweep state has been reset.");
                    break;
                default:
                    ui.SettingsVisible = true;
                    break;
            }
        }

        private void OnChatLinkClick(uint cmdId, SeString seString) {
            Process.Start(new ProcessStartInfo {
                FileName = "https://paissadb.zhu.codes/",
                UseShellExecute = true
            });
        }

        private void OnLogin() {
            PaissaClient.needsHello = true;
        }

        private void OnUpdateEvent(IFramework f) {
            if (ClientState?.LocalPlayer != null && PaissaClient != null && PaissaClient.needsHello) {
                PaissaClient.needsHello = false;
                PaissaClient.Hello();
            }
        }

        // ==== paissa events ====
        /// <summary>
        ///     Hook to call when a new plot open event is received over the websocket.
        /// </summary>
        private void OnPlotOpened(object sender, PlotOpenedEventArgs e) {
            if (e.PlotDetail == null) return;
            bool notifEnabled = Utils.ConfigEnabledForPlot(this, e.PlotDetail.world_id, e.PlotDetail.district_id,
                e.PlotDetail.size, e.PlotDetail.purchase_system);
            if (!notifEnabled) return;
            // we only notify on PlotOpen if the purchase type is FCFS or we know it is available
            if (!((e.PlotDetail.purchase_system & PurchaseSystem.Lottery) == 0 ||
                  e.PlotDetail.lotto_phase == AvailabilityType.Available)) return;
            World eventWorld = Worlds.GetRow(e.PlotDetail.world_id);
            OnFoundOpenHouse(e.PlotDetail.world_id, e.PlotDetail.district_id, e.PlotDetail.ward_number,
                e.PlotDetail.plot_number, e.PlotDetail.price,
                $"New plot available for purchase on {eventWorld.Name}: ");
        }

        private void OnPlotUpdate(object sender, PlotUpdateEventArgs e) {
            if (e.PlotUpdate == null) return;
            bool notifEnabled = Utils.ConfigEnabledForPlot(this, e.PlotUpdate.world_id, e.PlotUpdate.district_id,
                e.PlotUpdate.size, e.PlotUpdate.purchase_system);
            if (!notifEnabled) return;
            // we only notify on PlotUpdate if the purchase type is lottery and it is available now and was not before
            if (!((e.PlotUpdate.purchase_system & PurchaseSystem.Lottery) == PurchaseSystem.Lottery
                  && e.PlotUpdate.previous_lotto_phase != AvailabilityType.Available
                  && e.PlotUpdate.lotto_phase == AvailabilityType.Available)) return;
            World eventWorld = Worlds.GetRow(e.PlotUpdate.world_id);
            OnFoundOpenHouse(e.PlotUpdate.world_id, e.PlotUpdate.district_id, e.PlotUpdate.ward_number,
                e.PlotUpdate.plot_number, e.PlotUpdate.price,
                $"New plot available for purchase on {eventWorld.Name}: ");
        }


        /// <summary>
        ///     Display the details of an open plot in the user's preferred format.
        /// </summary>
        internal void OnFoundOpenHouse(uint worldId, uint territoryTypeId, int wardNumber, int plotNumber, uint? price,
            string messagePrefix = "") {
            PlaceName place = Territories.GetRow(territoryTypeId).PlaceName.Value;
            // languages like German do not use NameNoArticle (#2)
            var districtName =
                !place.NameNoArticle.IsEmpty ? place.NameNoArticle : place.Name;
            var worldName = Worlds.GetRow(worldId).Name;

            HousingLandSet landSet = HousingLandSets.GetRow(Utils.TerritoryTypeIdToLandSetId(territoryTypeId));
            byte? houseSize = landSet.LandSet[plotNumber].PlotSize;
            uint realPrice =
                price.GetValueOrDefault(landSet.LandSet[plotNumber].InitialPrice); // if price is null, it's probably the default price (landupdate)

            string districtNameNoSpaces = districtName.ToString().Replace(" ", "");
            int wardNum = wardNumber + 1;
            int plotNum = plotNumber + 1;
            float housePriceMillions = realPrice / 1000000f;
            string houseSizeName = houseSize switch {
                0 => "Small",
                1 => "Medium",
                _ => "Large"
            };

            string output;
            switch (Configuration.OutputFormat) {
                case OutputFormat.Pings:
                    output =
                        $"{messagePrefix}@{houseSizeName}{districtNameNoSpaces} {wardNum}-{plotNum} ({housePriceMillions:F3}m)";
                    break;
                case OutputFormat.Custom:
                    var template = $"{messagePrefix}{Configuration.OutputFormatString}";
                    output = Utils.FormatCustomOutputString(template, districtName.ToString(), districtNameNoSpaces,
                        worldName.ToString(), wardNum.ToString(),
                        plotNum.ToString(), realPrice.ToString(), housePriceMillions.ToString("F3"), houseSizeName);
                    break;
                default:
                    output =
                        $"{messagePrefix}{districtName} {wardNum}-{plotNum} ({houseSizeName}, {housePriceMillions:F3}m)";
                    break;
            }

            SendChatToConfiguredChannel(output);
        }

        // ==== helpers ====
        private void SendChatToConfiguredChannel(string message) {
            Chat.Print(new XivChatEntry {
                Name = "[PaissaHouse]",
                Message = message,
                Type = Configuration.ChatType
            });
        }

        // ==== UI ====
        private void DrawUI() {
            ui.Draw();
        }

        private void DrawConfigUI() {
            ui.SettingsVisible = true;
        }
    }
}