using System;
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
        public string Name => "Auto Sweeper";

        private const string commandName = "/pautosweep";
        private const int housingWardInfoOpcode = 0x015E; // https://github.com/SapphireServer/Sapphire/blob/master/src/common/Network/PacketDef/Ipcs.h#L257

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;
        private ExcelSheet<TerritoryType> territories;
        private ExcelSheet<World> worlds;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;

            // setup
            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);
            this.ui = new PluginUI(this.configuration);
            this.territories = pi.Data.GetExcelSheet<TerritoryType>();
            this.worlds = pi.Data.GetExcelSheet<World>();

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Configure autosweep settings."
            });

            // event hooks
            this.pi.Framework.Network.OnNetworkMessage += OnNetworkEvent;
            this.pi.UiBuilder.OnBuildUi += DrawUI;
            this.pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();

            PluginLog.LogDebug("Initialization complete");
        }

        public void Dispose()
        {
            this.ui.Dispose();
            this.pi.Framework.Network.OnNetworkMessage -= OnNetworkEvent;
            this.pi.CommandManager.RemoveHandler(commandName);
            this.pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            // command just opens settings
            this.ui.SettingsVisible = true;
        }

        private void OnNetworkEvent(IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (!this.configuration.Enabled) return;
            if (direction != NetworkMessageDirection.ZoneDown) return;
            if (!this.pi.Data.IsDataReady) return;
            if (opCode == housingWardInfoOpcode)
            {
                this.OnHousingWardInfo(dataPtr);
            }
        }

        private void OnHousingWardInfo(IntPtr dataPtr)
        {
            HousingWardInfo wardInfo = HousingWardInfo.Read(dataPtr);
            PluginLog.LogDebug($"Got HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber}");

            for (int i = 0; i < wardInfo.HouseInfoEntries.Length; i++)
            {
                HouseInfoEntry houseInfoEntry = wardInfo.HouseInfoEntries[i];
                PluginLog.LogVerbose(
                    $"Got {wardInfo.LandIdent.WardNumber + 1}-{i + 1}: owned by {houseInfoEntry.EstateOwnerName}, flags {houseInfoEntry.InfoFlags}, price {houseInfoEntry.HousePrice}");
                if ((houseInfoEntry.InfoFlags & HousingFlags.PlotOwned) == 0)
                    this.OnFoundOpenHouse(wardInfo, houseInfoEntry, i);
            }
            PluginLog.LogDebug($"Done processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber}");
        }

        private void OnFoundOpenHouse(HousingWardInfo wardInfo, HouseInfoEntry houseInfoEntry, int plotNumber)
        {
            var districtName = this.territories.GetRow((uint)wardInfo.LandIdent.TerritoryTypeId).PlaceName.Value.Name;
            var worldName = this.worlds.GetRow((uint)wardInfo.LandIdent.WorldId).Name;
            // todo output with correct format
            this.pi.Framework.Gui.Chat.Print(
                $"Open plot found at {districtName} {wardInfo.LandIdent.WardNumber + 1}-{plotNumber + 1} ({houseInfoEntry.HousePrice} gil, {worldName})");
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
