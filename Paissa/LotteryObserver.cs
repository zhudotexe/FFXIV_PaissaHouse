using System;
using AutoSweep.Structures;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;

namespace AutoSweep.Paissa {
    public class LotteryObserver {
        private Plugin plugin;
        private PlacardSaleInfo lastSeenSaleInfo = null;

        public LotteryObserver(Plugin plugin) {
            this.plugin = plugin;
        }

        public void OnPlacardSaleInfo(IntPtr dataPtr) {
            PlacardSaleInfo saleInfo = PlacardSaleInfo.Read(dataPtr);
            PluginLog.LogDebug(
                $"Got PlacardSaleInfo: PurchaseType={saleInfo.PurchaseType}, TenantFlags={saleInfo.TenantFlags}, available={saleInfo.AvailabilityType}, until={saleInfo.AcceptingEntriesUntil}, numEntries={saleInfo.EntryCount}");
            PluginLog.LogDebug($"unknown1={saleInfo.Unknown1}, unknown2={saleInfo.Unknown2}, unknown3={BitConverter.ToString(saleInfo.Unknown3)}");
            lastSeenSaleInfo = saleInfo;
        }

        public void OnHousingRequest(IntPtr dataPtr) {
            HousingRequest housingRequest = HousingRequest.Read(dataPtr);
            if (housingRequest.SubOpcode != SubOpcodes.HousingRequest_GetUnownedHousePlacard) return;
            PluginLog.LogDebug($"Got RequestUnownedPlacard for {housingRequest.WardId}-{housingRequest.PlotId} (district {housingRequest.TerritoryTypeId})");
            PluginLog.LogDebug($"unknown1={housingRequest.Unknown1}, unknown2={BitConverter.ToString(housingRequest.Unknown2)}");
            if (lastSeenSaleInfo is null) return;
            if (housingRequest.ServerTimestamp - lastSeenSaleInfo.ServerTimestamp > 3) {
                PluginLog.LogWarning($"High time delta between last seen PlacardSaleInfo and valid HousingRequest, ignoring!");
                return;
            }
            World world = plugin.ClientState.LocalPlayer?.CurrentWorld.GameData;
            if (world is null) return;
            SeString place = plugin.Territories.GetRow(housingRequest.TerritoryTypeId)?.PlaceName.Value?.Name;
            SeString worldName = world.Name;
            PluginLog.LogInformation(
                $"Plot {place} {housingRequest.WardId + 1}-{housingRequest.PlotId + 1} ({worldName}) has {lastSeenSaleInfo.EntryCount} lottery entries as of {lastSeenSaleInfo.ServerTimestamp}.");
            plugin.PaissaClient.PostLotteryInfo(world.RowId, housingRequest, lastSeenSaleInfo);
        }
    }
}
