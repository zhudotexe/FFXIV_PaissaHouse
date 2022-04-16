using System;
using AutoSweep.Structures;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;

namespace AutoSweep.Paissa {
    public unsafe class LotteryObserver {
        private readonly Plugin plugin;

        private delegate void HandlePlacardSaleInfoDelegate(
            void* agentBase,
            byte isUnowned,
            ushort territoryTypeId,
            byte wardId,
            byte plotId,
            short a6,
            IntPtr placardSaleInfoPtr,
            long a8
        );

        [Signature("E8 ?? ?? ?? ?? 48 8B B4 24 ?? ?? ?? ?? 48 8B 6C 24 ?? E9", DetourName = nameof(OnPlacardSaleInfo))]
        private Hook<HandlePlacardSaleInfoDelegate>? PlacardSaleInfoHook { get; init; }

        public LotteryObserver(Plugin plugin) {
            SignatureHelper.Initialise(this);
            this.plugin = plugin;
            PlacardSaleInfoHook?.Enable();
        }

        public void Dispose() {
            PlacardSaleInfoHook?.Dispose();
        }

        public void OnPlacardSaleInfo(
            void* agentBase,
            byte isUnowned,
            ushort territoryTypeId,
            byte wardId,
            byte plotId,
            short a6,
            IntPtr placardSaleInfoPtr,
            long a8
        ) {
            PlacardSaleInfoHook!.Original(agentBase, isUnowned, territoryTypeId, wardId, plotId, a6, placardSaleInfoPtr, a8);
            
            // if the plot is owned, ignore it
            if (isUnowned == 0) return;

            PlacardSaleInfo saleInfo = PlacardSaleInfo.Read(placardSaleInfoPtr);

            PluginLog.LogDebug(
                $"Got PlacardSaleInfo: PurchaseType={saleInfo.PurchaseType}, TenantType={saleInfo.TenantType}, available={saleInfo.AvailabilityType}, until={saleInfo.PhaseEndsAt}, numEntries={saleInfo.EntryCount}");
            PluginLog.LogDebug($"unknown1={saleInfo.Unknown1}, unknown2={saleInfo.Unknown2}, unknown3={BitConverter.ToString(saleInfo.Unknown3)}");
            PluginLog.LogDebug($"isUnowned={isUnowned}, territoryTypeId={territoryTypeId}, wardId={wardId}, plotId={plotId}; a6={a6}, a8={a8}");

            // get information about the world from the clientstate
            World world = plugin.ClientState.LocalPlayer?.CurrentWorld.GameData;
            if (world is null) return;

            SeString place = plugin.Territories.GetRow(territoryTypeId)?.PlaceName.Value?.Name;
            SeString worldName = world.Name;
            PluginLog.LogInformation($"Plot {place} {wardId + 1}-{plotId + 1} ({worldName}) has {saleInfo.EntryCount} lottery entries.");

            plugin.PaissaClient.PostLotteryInfo(world.RowId, territoryTypeId, wardId, plotId, saleInfo);
        }
    }
}
