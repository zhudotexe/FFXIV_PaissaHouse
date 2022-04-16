using System;
using AutoSweep.Paissa;
using AutoSweep.Structures;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.Game.Text;
using Dalamud.Logging;
using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;

namespace AutoSweep {
    public class Plugin : IDalamudPlugin {
        public string Name => "PaissaHouse";

        // frameworks/data
        internal readonly ChatGui Chat;
        internal readonly ClientState ClientState;
        internal readonly CommandManager Commands;
        internal readonly Configuration Configuration;
        internal readonly DataManager Data;
        internal readonly Framework Framework;
        internal readonly GameNetwork Network;
        internal readonly GameGui GameGui;
        internal readonly PaissaClient PaissaClient;

        internal readonly ExcelSheet<HousingLandSet> HousingLandSets;
        internal readonly ExcelSheet<TerritoryType> Territories;
        internal readonly ExcelSheet<World> Worlds;

        // state
        private readonly WardObserver wardObserver;
        private readonly LotteryObserver lotteryObserver;
        private readonly PluginUI ui;
        private bool clientNeedsHello = true;

        public Plugin(
            DalamudPluginInterface pi,
            ChatGui chat,
            GameNetwork network,
            DataManager data,
            CommandManager commands,
            ClientState clientState,
            Framework framework,
            GameGui gameGui
        ) {
            Chat = chat;
            Network = network;
            Data = data;
            Commands = commands;
            ClientState = clientState;
            Framework = framework;
            GameGui = gameGui;

            // setup
            Configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pi);
            ui = new PluginUI(Configuration);
            Territories = data.GetExcelSheet<TerritoryType>();
            Worlds = data.GetExcelSheet<World>();
            HousingLandSets = data.GetExcelSheet<HousingLandSet>();

            commands.AddHandler(Utils.CommandName, new CommandInfo(OnCommand) {
                HelpMessage = $"Configure PaissaHouse settings.\n\"{Utils.CommandName} reset\" to reset a sweep if sweeping the same district multiple times in a row."
            });

            // event hooks
            network.NetworkMessage += OnNetworkEvent;
            pi.UiBuilder.Draw += DrawUI;
            pi.UiBuilder.OpenConfigUi += DrawConfigUI;
            framework.Update += OnUpdateEvent;
            clientState.Login += OnLogin;

            // paissa setup
            wardObserver = new WardObserver(this);
            lotteryObserver = new LotteryObserver(this);
            PaissaClient = new PaissaClient(clientState, chat);
            PaissaClient.OnPlotOpened += OnPlotOpened;
            PaissaClient.OnPlotUpdate += OnPlotUpdate;

            PluginLog.LogDebug($"Initialization complete: configVersion={Configuration.Version}");
        }

        public void Dispose() {
            ui.Dispose();
            Network.NetworkMessage -= OnNetworkEvent;
            Framework.Update -= OnUpdateEvent;
            ClientState.Login -= OnLogin;
            Commands.RemoveHandler(Utils.CommandName);
            PaissaClient?.Dispose();
            lotteryObserver.Dispose();
        }

        // ==== dalamud events ====
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

        private void OnLogin(object _, EventArgs __) {
            clientNeedsHello = true;
        }

        private void OnUpdateEvent(Framework f) {
            if (clientNeedsHello && ClientState?.LocalPlayer != null && PaissaClient != null) {
                clientNeedsHello = false;
                PaissaClient.Hello();
            }
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction) {
            if (!Configuration.Enabled) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!Data.IsDataReady) return;
            if (opCode == Data.ServerOpCodes["HousingWardInfo"]) wardObserver.OnHousingWardInfo(dataPtr);
        }


        // ==== paissa events ====
        /// <summary>
        ///     Hook to call when a new plot open event is received over the websocket.
        /// </summary>
        private void OnPlotOpened(object sender, PlotOpenedEventArgs e) {
            if (e.PlotDetail == null) return;
            bool notifEnabled = Utils.ConfigEnabledForPlot(this, e.PlotDetail.world_id, e.PlotDetail.district_id, e.PlotDetail.size);
            if (!notifEnabled) return;
            // we only notify on PlotOpen if the purchase type is FCFS or we know it is available
            if (!((e.PlotDetail.purchase_system & PurchaseSystem.Lottery) == 0 || e.PlotDetail.lotto_phase == AvailabilityType.Available)) return;
            World eventWorld = Worlds.GetRow(e.PlotDetail.world_id);
            OnFoundOpenHouse(e.PlotDetail.world_id, e.PlotDetail.district_id, e.PlotDetail.ward_number, e.PlotDetail.plot_number, e.PlotDetail.price,
                $"New plot available for purchase on {eventWorld?.Name}: ");
        }

        private void OnPlotUpdate(object sender, PlotUpdateEventArgs e) {
            if (e.PlotUpdate == null) return;
            bool notifEnabled = Utils.ConfigEnabledForPlot(this, e.PlotUpdate.world_id, e.PlotUpdate.district_id, e.PlotUpdate.size);
            if (!notifEnabled) return;
            // we only notify on PlotUpdate if the purchase type is lottery and it is available now and was not before
            if (!((e.PlotUpdate.purchase_system & PurchaseSystem.Lottery) == PurchaseSystem.Lottery
                  && e.PlotUpdate.previous_lotto_phase != AvailabilityType.Available
                  && e.PlotUpdate.lotto_phase == AvailabilityType.Available)) return;
            World eventWorld = Worlds.GetRow(e.PlotUpdate.world_id);
            OnFoundOpenHouse(e.PlotUpdate.world_id, e.PlotUpdate.district_id, e.PlotUpdate.ward_number, e.PlotUpdate.plot_number, e.PlotUpdate.price,
                $"New plot available for purchase on {eventWorld?.Name}: ");
        }


        /// <summary>
        ///     Display the details of an open plot in the user's preferred format.
        /// </summary>
        internal void OnFoundOpenHouse(uint worldId, uint territoryTypeId, int wardNumber, int plotNumber, uint? price, string messagePrefix = "") {
            PlaceName place = Territories.GetRow(territoryTypeId)?.PlaceName.Value;
            SeString districtName = place?.NameNoArticle.RawString.Length > 0 ? place.NameNoArticle : place?.Name; // languages like German do not use NameNoArticle (#2)
            SeString worldName = Worlds.GetRow(worldId)?.Name;

            HousingLandSet landSet = HousingLandSets.GetRow(Utils.TerritoryTypeIdToLandSetId(territoryTypeId));
            byte? houseSize = landSet?.PlotSize[plotNumber];
            uint realPrice = price.GetValueOrDefault(landSet?.InitialPrice[plotNumber] ?? 0); // if price is null, it's probably the default price (landupdate)

            string districtNameNoSpaces = districtName?.ToString().Replace(" ", "");
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
                    output = $"{messagePrefix}@{houseSizeName}{districtNameNoSpaces} {wardNum}-{plotNum} ({housePriceMillions:F3}m)";
                    break;
                case OutputFormat.Custom:
                    var template = $"{messagePrefix}{Configuration.OutputFormatString}";
                    output = Utils.FormatCustomOutputString(template, districtName?.ToString(), districtNameNoSpaces, worldName, wardNum.ToString(),
                        plotNum.ToString(), realPrice.ToString(), housePriceMillions.ToString("F3"), houseSizeName);
                    break;
                default:
                    output = $"{messagePrefix}{districtName} {wardNum}-{plotNum} ({houseSizeName}, {housePriceMillions:F3}m)";
                    break;
            }
            SendChatToConfiguredChannel(output);
        }

        // ==== helpers ====
        private void SendChatToConfiguredChannel(string message) {
            Chat.PrintChat(new XivChatEntry {
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
