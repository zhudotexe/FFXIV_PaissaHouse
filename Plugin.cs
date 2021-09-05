using System;
using AutoSweep.Paissa;
using AutoSweep.Structures;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Network;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace AutoSweep
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "PaissaHouse";

        // configuration constants
        private const string commandName = "/psweep";
        private const int numWardsPerDistrict = 24;

        // frameworks/data
        [PluginService] public static DalamudPluginInterface Pi { get; private set; }
        [PluginService] public static ChatGui Chat { get; private set; }
        [PluginService] public static GameNetwork Network { get; private set; }
        [PluginService] public static DataManager Data { get; private set; }
        [PluginService] public static CommandManager Commands { get; private set; }
        [PluginService] public static ClientState ClientState { get; private set; }
        [PluginService] public static Framework Framework { get; private set; }
        private Configuration configuration;
        private PluginUI ui;
        private ExcelSheet<TerritoryType> territories;
        private ExcelSheet<World> worlds;
        private ExcelSheet<HousingLandSet> housingLandSets;

        // state
        private SweepState sweepState;
        private PaissaClient paissaClient;
        private bool clientNeedsHello = true;

        public Plugin()
        {
            // setup
            this.configuration = Pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(Pi);
            this.ui = new PluginUI(this.configuration);
            this.territories = Data.GetExcelSheet<TerritoryType>();
            this.worlds = Data.GetExcelSheet<World>();
            this.housingLandSets = Data.GetExcelSheet<HousingLandSet>();

            Commands.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = $"Configure PaissaHouse settings.\n\"{commandName} reset\" to reset a sweep if sweeping the same district multiple times in a row."
            });

            // event hooks
            Network.NetworkMessage += OnNetworkEvent;
            Pi.UiBuilder.Draw += DrawUI;
            Pi.UiBuilder.OpenConfigUi += DrawConfigUI;
            Framework.Update += OnUpdateEvent;
            ClientState.Login += OnLogin;

            // paissa setup
            this.sweepState = new SweepState(numWardsPerDistrict);
            this.paissaClient = Pi.Create<PaissaClient>();
            this.paissaClient.OnPlotOpened += OnPlotOpened;

            PluginLog.LogDebug($"Initialization complete: configVersion={this.configuration.Version}");
        }

        public void Dispose()
        {
            ui.Dispose();
            Network.NetworkMessage -= OnNetworkEvent;
            Framework.Update -= OnUpdateEvent;
            ClientState.Login -= OnLogin;
            Commands.RemoveHandler(commandName);
            paissaClient?.Dispose();
            Pi.Dispose();
        }

        // ==== dalamud events ====
        private void OnCommand(string command, string args)
        {
            switch (args) {
                case "reset":
                    sweepState.Reset();
                    break;
                default:
                    this.ui.SettingsVisible = true;
                    break;
            }
        }

        private void OnLogin(object _, EventArgs __)
        {
            clientNeedsHello = true;
        }

        private void OnUpdateEvent(Framework framework)
        {
            if (clientNeedsHello && ClientState?.LocalPlayer != null && paissaClient != null) {
                clientNeedsHello = false;
                paissaClient.Hello();
            }
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (!this.configuration.Enabled) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!Data.IsDataReady) return;
            if (opCode == Data.ServerOpCodes["HousingWardInfo"]) {
                this.OnHousingWardInfo(dataPtr);
            }
        }

        private void OnHousingWardInfo(IntPtr dataPtr)
        {
            HousingWardInfo wardInfo = HousingWardInfo.Read(dataPtr);
            PluginLog.LogDebug($"Got HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber} territory: {wardInfo.LandIdent.TerritoryTypeId}");

            // if the current wardinfo is for a different district than the last swept one, print the header
            // or if the last sweep was > 10m ago
            if (sweepState.ShouldStartNewSweep(wardInfo)) {
                // reset last sweep info to the current sweep
                sweepState.StartDistrictSweep(wardInfo);

                var districtName = this.territories.GetRow((uint)wardInfo.LandIdent.TerritoryTypeId).PlaceName.Value.Name;
                var worldName = this.worlds.GetRow((uint)wardInfo.LandIdent.WorldId).Name;
                Chat.Print($"Began sweep for {districtName} ({worldName})");
            }

            // if we've seen this ward already, ignore it
            if (sweepState.Contains(wardInfo)) {
                PluginLog.LogDebug($"Skipped processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber} because we have seen it already");
                return;
            }

            // add the ward to this sweep
            sweepState.Add(wardInfo);

            // post wardinfo to PaissaDB
            paissaClient.PostWardInfo(wardInfo);

            // if that's all the wards, display the district summary and thanks
            if (sweepState.IsComplete) {
                OnFinishedDistrictSweep(wardInfo);
            }

            PluginLog.LogDebug($"Done processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber}");
        }

        // ==== paissa events ====
        /// <summary>
        /// Hook to call when a new plot open event is received over the websocket.
        /// </summary>
        private void OnPlotOpened(object sender, PlotOpenedEventArgs e)
        {
            if (!this.configuration.Enabled) return;
            if (e.PlotDetail == null) return;
            // does the config want notifs for this world?
            var eventWorld = worlds.GetRow(e.PlotDetail.world_id);
            if (!(configuration.AllNotifs
                  || (configuration.HomeworldNotifs && (e.PlotDetail.world_id == ClientState.LocalPlayer?.HomeWorld.Id))
                  || (configuration.DatacenterNotifs && (eventWorld.DataCenter.Row == ClientState.LocalPlayer?.HomeWorld.GameData.DataCenter.Row))))
                return;
            // what about house sizes in this district?
            DistrictNotifConfig districtNotifs;
            switch (e.PlotDetail.district_id) {
                case 339:
                    districtNotifs = configuration.Mist;
                    break;
                case 340:
                    districtNotifs = configuration.LavenderBeds;
                    break;
                case 341:
                    districtNotifs = configuration.Goblet;
                    break;
                case 641:
                    districtNotifs = configuration.Shirogane;
                    break;
                case 886:
                    districtNotifs = configuration.Firmament;
                    break;
                default:
                    PluginLog.Warning($"Unknown district in plot open event: {e.PlotDetail.district_id}");
                    return;
            }
            bool notifEnabled;
            switch (e.PlotDetail.size) {
                case 0:
                    notifEnabled = districtNotifs.Small;
                    break;
                case 1:
                    notifEnabled = districtNotifs.Medium;
                    break;
                case 2:
                    notifEnabled = districtNotifs.Large;
                    break;
                default:
                    PluginLog.Warning($"Unknown plot size in plot open event: {e.PlotDetail.size}");
                    return;
            }
            // do the notification
            if (!notifEnabled) return;
            OnFoundOpenHouse(e.PlotDetail.world_id, e.PlotDetail.district_id, e.PlotDetail.ward_number, e.PlotDetail.plot_number, e.PlotDetail.known_price,
                $"New plot up for sale on {eventWorld.Name}: ");
        }

        /// <summary>
        /// Called each time the user finishes sweeping a full district, with the wardinfo as the last ward swept. 
        /// </summary>
        private void OnFinishedDistrictSweep(HousingWardInfo wardInfo)
        {
            var districtName = territories.GetRow((uint)sweepState.DistrictId).PlaceName.Value.Name;

            Chat.Print($"Swept all {numWardsPerDistrict} wards. Thank you for your contribution!");
            Chat.Print($"Here's a summary of open plots in {districtName}:");
            Chat.Print($"{districtName}: {sweepState.OpenHouses.Count} open plots.");
            foreach (var openHouse in sweepState.OpenHouses) {
                OnFoundOpenHouse((uint)sweepState.WorldId, (uint)sweepState.DistrictId, openHouse.WardNum, openHouse.PlotNum, openHouse.HouseInfoEntry.HousePrice);
            }

            // Task.Run(async () =>
            // {
            //     DistrictDetail districtDetail;
            //     try {
            //         districtDetail = await paissaClient.GetDistrictDetailAsync(wardInfo.LandIdent.WorldId, wardInfo.LandIdent.TerritoryTypeId);
            //     }
            //     catch (HttpRequestException) {
            //         pi.Framework.Gui.Chat.PrintError("There was an error getting the district summary.");
            //         return;
            //     }
            //     pi.Framework.Gui.Chat.Print($"Here's a summary of open plots in {districtDetail.name}:");
            //     pi.Framework.Gui.Chat.Print($"{districtDetail.name}: {districtDetail.num_open_plots} open plots.");
            //     foreach (OpenPlotDetail plotDetail in districtDetail.open_plots) {
            //         OnFoundOpenHouse(plotDetail.world_id, plotDetail.district_id, plotDetail.ward_number, plotDetail.plot_number, plotDetail.known_price);
            //     }
            // });
        }

        /// <summary>
        /// Display the details of an open plot in the user's preferred format.
        /// </summary>
        private void OnFoundOpenHouse(uint worldId, uint territoryTypeId, int wardNumber, int plotNumber, uint? price, string messagePrefix = "")
        {
            var place = territories.GetRow(territoryTypeId).PlaceName.Value;
            var districtName = place.NameNoArticle.RawString.Length > 0 ? place.NameNoArticle : place.Name; // languages like German do not use NameNoArticle (#2)
            var worldName = worlds.GetRow(worldId).Name;

            var landSet = housingLandSets.GetRow(TerritoryTypeIdToLandSetId(territoryTypeId));
            var houseSize = landSet.PlotSize[plotNumber];
            var realPrice = price.GetValueOrDefault(landSet.InitialPrice[plotNumber]); // if price is null, it's probably the default price (landupdate)

            var districtNameNoSpaces = districtName.ToString().Replace(" ", "");
            int wardNum = wardNumber + 1;
            int plotNum = plotNumber + 1;
            float housePriceMillions = realPrice / 1000000f;
            string houseSizeName = houseSize == 0 ? "Small" : houseSize == 1 ? "Medium" : "Large";

            string output;
            switch (configuration.OutputFormat) {
                case OutputFormat.Pings:
                    output = $"{messagePrefix}@{houseSizeName}{districtNameNoSpaces} {wardNum}-{plotNum} ({housePriceMillions:F3}m)";
                    break;
                case OutputFormat.EnoBot:
                    output = $"{messagePrefix}##forsale {districtNameNoSpaces} w{wardNum} p{plotNum}";
                    break;
                case OutputFormat.Custom:
                    var template = $"{messagePrefix}{configuration.OutputFormatString}";
                    output = FormatCustomOutputString(template, districtName.ToString(), districtNameNoSpaces, worldName, wardNum.ToString(),
                        plotNum.ToString(), realPrice.ToString(), housePriceMillions.ToString("F3"), houseSizeName);
                    break;
                default:
                    output = $"{messagePrefix}{districtName} {wardNum}-{plotNum} ({houseSizeName}, {housePriceMillions:F3}m)";
                    break;
            }
            Chat.Print(output);
        }

        // ==== helpers ====
        private static uint TerritoryTypeIdToLandSetId(uint territoryTypeId)
        {
            switch (territoryTypeId) {
                case 641: // shirogane
                    return 3;
                case 886: // firmament?
                    return 4;
                default: // mist, lb, gob are 339-341
                    return territoryTypeId - 339;
            }
        }

        private static string FormatCustomOutputString(string template, string districtName, string districtNameNoSpaces, string worldName, string wardNum, string plotNum,
            string housePrice, string housePriceMillions, string houseSizeName)
        {
            // mildly disgusting
            // why can't we have nice things like python :(
            return template.Replace("{districtName}", districtName)
                .Replace("{districtNameNoSpaces}", districtNameNoSpaces)
                .Replace("{worldName}", worldName)
                .Replace("{wardNum}", wardNum)
                .Replace("{plotNum}", plotNum)
                .Replace("{housePrice}", housePrice)
                .Replace("{housePriceMillions}", housePriceMillions)
                .Replace("{houseSizeName}", houseSizeName);
        }

        // ==== UI ====
        private void DrawUI()
        {
            this.ui.Draw();
        }

        private void DrawConfigUI()
        {
            this.ui.SettingsVisible = true;
        }
    }
}
