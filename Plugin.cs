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

namespace AutoSweep
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "PaissaHouse";

        // configuration constants
        private const string commandName = "/psweep";
        private const int numWardsPerDistrict = 24;

        // frameworks/data
        private readonly DalamudPluginInterface pi;
        private readonly ChatGui chat;
        private readonly GameNetwork network;
        private readonly DataManager data;
        private readonly CommandManager commands;
        private readonly ClientState clientState;
        private readonly Framework framework;
        private readonly Configuration configuration;
        private readonly PluginUI ui;
        private readonly ExcelSheet<TerritoryType> territories;
        private readonly ExcelSheet<World> worlds;
        private readonly ExcelSheet<HousingLandSet> housingLandSets;

        // state
        private readonly SweepState sweepState;
        private readonly PaissaClient paissaClient;
        private bool clientNeedsHello = true;

        public Plugin(
            DalamudPluginInterface pi,
            ChatGui chat,
            GameNetwork network,
            DataManager data,
            CommandManager commands,
            ClientState clientState,
            Framework framework)
        {
            this.pi = pi;
            this.chat = chat;
            this.network = network;
            this.data = data;
            this.commands = commands;
            this.clientState = clientState;
            this.framework = framework;

            // setup
            configuration = pi.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pi);
            ui = new PluginUI(configuration);
            territories = data.GetExcelSheet<TerritoryType>();
            worlds = data.GetExcelSheet<World>();
            housingLandSets = data.GetExcelSheet<HousingLandSet>();

            commands.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = $"Configure PaissaHouse settings.\n\"{commandName} reset\" to reset a sweep if sweeping the same district multiple times in a row."
            });

            // event hooks
            network.NetworkMessage += OnNetworkEvent;
            pi.UiBuilder.Draw += DrawUI;
            pi.UiBuilder.OpenConfigUi += DrawConfigUI;
            framework.Update += OnUpdateEvent;
            clientState.Login += OnLogin;

            // paissa setup
            sweepState = new SweepState(numWardsPerDistrict);
            paissaClient = new PaissaClient(clientState, chat);
            paissaClient.OnPlotOpened += OnPlotOpened;

            PluginLog.LogDebug($"Initialization complete: configVersion={configuration.Version}");
        }

        public void Dispose()
        {
            ui.Dispose();
            network.NetworkMessage -= OnNetworkEvent;
            framework.Update -= OnUpdateEvent;
            clientState.Login -= OnLogin;
            commands.RemoveHandler(commandName);
            paissaClient?.Dispose();
            pi.Dispose();
        }

        // ==== dalamud events ====
        private void OnCommand(string command, string args)
        {
            switch (args) {
                case "reset":
                    sweepState.Reset();
                    chat.Print("The sweep state has been reset.");
                    break;
                default:
                    ui.SettingsVisible = true;
                    break;
            }
        }

        private void OnLogin(object _, EventArgs __)
        {
            clientNeedsHello = true;
        }

        private void OnUpdateEvent(Framework f)
        {
            if (clientNeedsHello && clientState?.LocalPlayer != null && paissaClient != null) {
                clientNeedsHello = false;
                paissaClient.Hello();
            }
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (!configuration.Enabled) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!data.IsDataReady) return;
            if (opCode == data.ServerOpCodes["HousingWardInfo"]) {
                OnHousingWardInfo(dataPtr);
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

                var districtName = territories.GetRow((uint)wardInfo.LandIdent.TerritoryTypeId)?.PlaceName.Value?.Name;
                var worldName = worlds.GetRow((uint)wardInfo.LandIdent.WorldId)?.Name;
                chat.Print($"Began sweep for {districtName} ({worldName})");
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
            if (!configuration.Enabled) return;
            if (e.PlotDetail == null) return;
            // does the config want notifs for this world?
            var eventWorld = worlds.GetRow(e.PlotDetail.world_id);
            if (!(configuration.AllNotifs
                  || (configuration.HomeworldNotifs && (e.PlotDetail.world_id == clientState.LocalPlayer?.HomeWorld.Id))
                  || (configuration.DatacenterNotifs && (eventWorld?.DataCenter.Row == clientState.LocalPlayer?.HomeWorld.GameData.DataCenter.Row))))
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
                    districtNotifs = configuration.Empyrean;
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
                $"New plot up for sale on {eventWorld?.Name}: ");
        }

        /// <summary>
        /// Called each time the user finishes sweeping a full district, with the wardinfo as the last ward swept. 
        /// </summary>
        private void OnFinishedDistrictSweep(HousingWardInfo housingWardInfo)
        {
            var districtName = territories.GetRow((uint)sweepState.DistrictId)?.PlaceName.Value?.Name;

            chat.Print($"Swept all {numWardsPerDistrict} wards. Thank you for your contribution!");
            chat.Print($"Here's a summary of open plots in {districtName}:");
            chat.Print($"{districtName}: {sweepState.OpenHouses.Count} open plots.");
            foreach (var openHouse in sweepState.OpenHouses) {
                OnFoundOpenHouse((uint)sweepState.WorldId, (uint)sweepState.DistrictId, openHouse.WardNum, openHouse.PlotNum, openHouse.HouseInfoEntry.HousePrice);
            }
        }

        /// <summary>
        /// Display the details of an open plot in the user's preferred format.
        /// </summary>
        private void OnFoundOpenHouse(uint worldId, uint territoryTypeId, int wardNumber, int plotNumber, uint? price, string messagePrefix = "")
        {
            var place = territories.GetRow(territoryTypeId)?.PlaceName.Value;
            var districtName = place?.NameNoArticle.RawString.Length > 0 ? place.NameNoArticle : place?.Name; // languages like German do not use NameNoArticle (#2)
            var worldName = worlds.GetRow(worldId)?.Name;

            var landSet = housingLandSets.GetRow(TerritoryTypeIdToLandSetId(territoryTypeId));
            var houseSize = landSet?.PlotSize[plotNumber];
            var realPrice = price.GetValueOrDefault(landSet?.InitialPrice[plotNumber] ?? 0); // if price is null, it's probably the default price (landupdate)

            var districtNameNoSpaces = districtName?.ToString().Replace(" ", "");
            var wardNum = wardNumber + 1;
            var plotNum = plotNumber + 1;
            var housePriceMillions = realPrice / 1000000f;
            var houseSizeName = houseSize switch
            {
                0 => "Small",
                1 => "Medium",
                _ => "Large"
            };

            string output;
            switch (configuration.OutputFormat) {
                case OutputFormat.Pings:
                    output = $"{messagePrefix}@{houseSizeName}{districtNameNoSpaces} {wardNum}-{plotNum} ({housePriceMillions:F3}m)";
                    break;
                case OutputFormat.Custom:
                    var template = $"{messagePrefix}{configuration.OutputFormatString}";
                    output = FormatCustomOutputString(template, districtName?.ToString(), districtNameNoSpaces, worldName, wardNum.ToString(),
                        plotNum.ToString(), realPrice.ToString(), housePriceMillions.ToString("F3"), houseSizeName);
                    break;
                default:
                    output = $"{messagePrefix}{districtName} {wardNum}-{plotNum} ({houseSizeName}, {housePriceMillions:F3}m)";
                    break;
            }
            SendChatToConfiguredChannel(output);
        }

        // ==== helpers ====
        private static uint TerritoryTypeIdToLandSetId(uint territoryTypeId)
        {
            return territoryTypeId switch
            {
                641 => 3, // shirogane
                886 => 4, // empyreum
                _ => territoryTypeId - 339 // mist, lb, gob are 339-341
            };
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

        private void SendChatToConfiguredChannel(string message)
        {
            chat.PrintChat(new XivChatEntry
            {
                Name = "[PaissaHouse]",
                Message = message,
                Type = configuration.ChatType
            });
        }

        // ==== UI ====
        private void DrawUI()
        {
            ui.Draw();
        }

        private void DrawConfigUI()
        {
            ui.SettingsVisible = true;
        }
    }
}
