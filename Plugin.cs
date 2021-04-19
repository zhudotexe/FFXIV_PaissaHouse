using System;
using AutoSweep.Paissa;
using AutoSweep.Structures;
using Dalamud.Game.Command;
using Dalamud.Game.Internal.Network;
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
        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;
        private ExcelSheet<TerritoryType> territories;
        private ExcelSheet<World> worlds;
        private ExcelSheet<HousingLandSet> housingLandSets;

        // state
        private HousingState housingState;
        private PaissaClient paissaClient;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;

            // setup
            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);
            this.ui = new PluginUI(this.configuration);
            this.territories = pi.Data.GetExcelSheet<TerritoryType>();
            this.worlds = pi.Data.GetExcelSheet<World>();
            this.housingLandSets = pi.Data.GetExcelSheet<HousingLandSet>();

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = $"Configure PaissaHouse settings.\n\"{commandName} reset\" to reset a sweep if sweeping the same district multiple times in a row."
            });

            // event hooks
            this.pi.Framework.Network.OnNetworkMessage += OnNetworkEvent;
            this.pi.UiBuilder.OnBuildUi += DrawUI;
            this.pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();

            // paissa setup
            this.housingState = new HousingState();
            this.paissaClient = new PaissaClient(this.pi);

            PluginLog.LogDebug($"Initialization complete: configVersion={this.configuration.Version}");
        }

        public void Dispose()
        {
            this.ui.Dispose();
            this.pi.Framework.Network.OnNetworkMessage -= OnNetworkEvent;
            this.pi.CommandManager.RemoveHandler(commandName);
            this.paissaClient?.Dispose();
            this.pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            switch (args) {
                case "reset":
                    housingState.Reset();
                    break;
                default:
                    this.ui.SettingsVisible = true;
                    break;
            }
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (!this.configuration.Enabled) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!this.pi.Data.IsDataReady) return;
            if (opCode == this.pi.Data.ServerOpCodes["HousingWardInfo"]) {
                this.OnHousingWardInfo(dataPtr);
            }
            else if (opCode == this.pi.Data.ServerOpCodes["LandUpdate"]) {
                this.OnLandUpdate(dataPtr);
            }
        }

        private void OnHousingWardInfo(IntPtr dataPtr)
        {
            HousingWardInfo wardInfo = HousingWardInfo.Read(dataPtr);
            PluginLog.LogDebug($"Got HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber} territory: {wardInfo.LandIdent.TerritoryTypeId}");

            // if the current wardinfo is for a different district than the last swept one, print the header
            // or if the last sweep was > 10m ago
            if (housingState.ShouldStartNewSweep(wardInfo)) {
                // reset last sweep info to the current sweep
                housingState.StartDistrictSweep(wardInfo);

                var districtName = this.territories.GetRow((uint)wardInfo.LandIdent.TerritoryTypeId).PlaceName.Value.Name;
                var worldName = this.worlds.GetRow((uint)wardInfo.LandIdent.WorldId).Name;
                this.pi.Framework.Gui.Chat.Print($"Began sweep for {districtName} ({worldName})");
            }

            // if we've seen this ward already, ignore it
            if (housingState.LastSweptDistrictSeenWardNumbers.Contains(wardInfo.LandIdent.WardNumber)) {
                PluginLog.LogDebug($"Skipped processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber} because we have seen it already");
                return;
            }

            // add the ward number to this sweep's seen numbers
            housingState.LastSweptDistrictSeenWardNumbers.Add(wardInfo.LandIdent.WardNumber);
            // if that's all the wards, give the user a cookie
            if (housingState.LastSweptDistrictSeenWardNumbers.Count == numWardsPerDistrict)
                this.pi.Framework.Gui.Chat.Print($"Swept all {numWardsPerDistrict} wards. Thank you!");

            // iterate over houses to find open houses
            for (int i = 0; i < wardInfo.HouseInfoEntries.Length; i++) {
                HouseInfoEntry houseInfoEntry = wardInfo.HouseInfoEntries[i];
                PluginLog.LogVerbose(
                    $"Got {wardInfo.LandIdent.WardNumber + 1}-{i + 1}: owned by {houseInfoEntry.EstateOwnerName}, flags {houseInfoEntry.InfoFlags}, price {houseInfoEntry.HousePrice}");
                if ((houseInfoEntry.InfoFlags & HousingFlags.PlotOwned) == 0)
                    this.OnFoundOpenHouse(wardInfo, houseInfoEntry, i);
            }

            // post wardinfo to PaissaDB
            if (this.configuration.PostInfo)
                paissaClient?.PostWardInfo(wardInfo);

            PluginLog.LogDebug($"Done processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber}");
        }

        private void OnLandUpdate(IntPtr dataPtr)
        {
            LandUpdate landUpdate = LandUpdate.Read(dataPtr);
            PluginLog.LogDebug($"Got LandUpdate for ward: {landUpdate.LandIdent.WardNumber} plot: {landUpdate.LandIdent.LandId} territory: {landUpdate.LandIdent.TerritoryTypeId}");
            PluginLog.LogDebug($"PlotSize: {landUpdate.LandStruct.PlotSize} HouseState: {landUpdate.LandStruct.HouseState} Flags: {landUpdate.LandStruct.Flags} IconAddIcon: {landUpdate.LandStruct.IconAddIcon}");
        }

        private void OnFoundOpenHouse(HousingWardInfo wardInfo, HouseInfoEntry houseInfoEntry, int plotNumber)
        {
            var place = this.territories.GetRow((uint)wardInfo.LandIdent.TerritoryTypeId).PlaceName.Value;
            var districtName = place.NameNoArticle.RawString.Length > 0 ? place.NameNoArticle : place.Name; // languages like German do not use NameNoArticle (#2)
            var worldName = this.worlds.GetRow((uint)wardInfo.LandIdent.WorldId).Name;

            // gross way of getting the landset from the territorytype but the game does not send the correct landsetid
            uint landSetIndex = (uint)wardInfo.LandIdent.TerritoryTypeId - 339;
            landSetIndex = landSetIndex > 3 ? 3 : landSetIndex;
            var houseSize = this.housingLandSets.GetRow(landSetIndex).PlotSize[plotNumber];

            var districtNameNoSpaces = districtName.ToString().Replace(" ", "");
            int wardNum = wardInfo.LandIdent.WardNumber + 1;
            int plotNum = plotNumber + 1;
            float housePriceMillions = houseInfoEntry.HousePrice / 1000000f;
            string houseSizeName = houseSize == 0 ? "Small" : houseSize == 1 ? "Medium" : "Large";

            string output;
            switch (configuration.OutputFormat) {
                case OutputFormat.Pings:
                    output = $"@{houseSizeName}{districtNameNoSpaces} {wardNum}-{plotNum} ({housePriceMillions:F3}m)";
                    break;
                case OutputFormat.EnoBot:
                    output = $"##forsale {districtNameNoSpaces} w{wardNum} p{plotNum}";
                    break;
                case OutputFormat.Custom:
                    output = FormatCustomOutputString(this.configuration.OutputFormatString, districtName.ToString(), districtNameNoSpaces, worldName, wardNum.ToString(),
                        plotNum.ToString(), houseInfoEntry.HousePrice.ToString(), housePriceMillions.ToString("F3"), houseSizeName);
                    break;
                default:
                    output = $"{districtName} {wardNum}-{plotNum} ({housePriceMillions:F3}m)";
                    break;
            }
            this.pi.Framework.Gui.Chat.Print(output);
        }

        private string FormatCustomOutputString(string template, string districtName, string districtNameNoSpaces, string worldName, string wardNum, string plotNum,
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
