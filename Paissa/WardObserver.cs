using System;
using System.Runtime.InteropServices;
using AutoSweep.Structures;
using Dalamud.Logging;
using Lumina.Text;

namespace AutoSweep.Paissa {
    public class WardObserver {
        private Plugin plugin;
        internal readonly SweepState SweepState;

        public WardObserver(Plugin plugin) {
            this.plugin = plugin;
            SweepState = new SweepState(Utils.NumWardsPerDistrict);
        }

        public void OnHousingWardInfo(IntPtr dataPtr) {
            HousingWardInfo wardInfo = HousingWardInfo.Read(dataPtr);
            int serverTimestamp = Marshal.ReadInt32(dataPtr - 0x8);
            PluginLog.LogDebug($"Got HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber} territory: {wardInfo.LandIdent.TerritoryTypeId}");

            // if the current wardinfo is for a different district than the last swept one, print the header
            // or if the last sweep was > 10m ago
            if (SweepState.ShouldStartNewSweep(wardInfo)) {
                // reset last sweep info to the current sweep
                SweepState.StartDistrictSweep(wardInfo);

                SeString districtName = plugin.Territories.GetRow((uint)wardInfo.LandIdent.TerritoryTypeId)?.PlaceName.Value?.Name;
                SeString worldName = plugin.Worlds.GetRow((uint)wardInfo.LandIdent.WorldId)?.Name;
                if (plugin.Configuration.ChatSweepAlert) {
                    plugin.Chat.Print($"Began sweep for {districtName} ({worldName})");
                }
            }

            // if we've seen this ward already, ignore it
            if (SweepState.Contains(wardInfo)) {
                PluginLog.LogDebug($"Skipped processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber} because we have seen it already");
                return;
            }

            // add the ward to this sweep
            SweepState.Add(wardInfo);

            // post wardinfo to PaissaDB
            plugin.PaissaClient.PostWardInfo(wardInfo, serverTimestamp);

            // if that's all the wards, display the district summary and thanks
            if (SweepState.IsComplete) OnFinishedDistrictSweep(wardInfo);

            PluginLog.LogDebug($"Done processing HousingWardInfo for ward: {wardInfo.LandIdent.WardNumber}");
        }

        /// <summary>
        ///     Called each time the user finishes sweeping a full district, with the wardinfo as the last ward swept.
        /// </summary>
        private void OnFinishedDistrictSweep(HousingWardInfo housingWardInfo) {
            if (!plugin.Configuration.ChatSweepAlert) return;

            SeString districtName = plugin.Territories.GetRow((uint)SweepState.DistrictId)?.PlaceName.Value?.Name;

            plugin.Chat.Print($"Swept all {Utils.NumWardsPerDistrict} wards. Thank you for your contribution!");
            plugin.Chat.Print($"Here's a summary of open plots in {districtName}:");
            plugin.Chat.Print($"{districtName}: {SweepState.OpenHouses.Count} open plots.");
            foreach (OpenHouse openHouse in SweepState.OpenHouses)
                plugin.OnFoundOpenHouse((uint)SweepState.WorldId, (uint)SweepState.DistrictId, openHouse.WardNum, openHouse.PlotNum, openHouse.HouseInfoEntry.HousePrice);
        }
    }
}
