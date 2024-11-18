using System;
using AutoSweep.Structures;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Lumina.Excel.Sheets;
using Lumina.Text;

namespace AutoSweep.Paissa {
    public unsafe class LotteryObserver {
        private readonly Plugin plugin;

        private delegate void HandlePlacardSaleInfoDelegate(
            void* agentBase,
            HousingType housingType,
            ushort territoryTypeId,
            byte wardId,
            byte plotId,
            short apartmentNumber,
            IntPtr placardSaleInfoPtr,
            long a8
        );

        // easy way to find sig: search for scalar 7043
        [Signature("E8 ?? ?? ?? ?? 48 8B 74 24 ?? 48 8B 6C 24 ?? E9", DetourName = nameof(OnPlacardSaleInfo))]
        private Hook<HandlePlacardSaleInfoDelegate>? placardSaleInfoHook;

        public LotteryObserver(Plugin plugin) {
            this.plugin = plugin;
            Plugin.InteropProvider.InitializeFromAttributes(this);
            placardSaleInfoHook?.Enable();
        }

        public void Dispose() {
            placardSaleInfoHook?.Dispose();
        }

        public void OnPlacardSaleInfo(
            void* agentBase,
            HousingType housingType,
            ushort territoryTypeId,
            byte wardId,
            byte plotId,
            short apartmentNumber,
            IntPtr placardSaleInfoPtr,
            long a8
        ) {
            placardSaleInfoHook!.Original(agentBase, housingType, territoryTypeId, wardId, plotId, apartmentNumber, placardSaleInfoPtr, a8);

            // if the plot is owned, ignore it
            if (housingType != HousingType.UnownedHouse) return;
            if (placardSaleInfoPtr == IntPtr.Zero) return;

            PlacardSaleInfo saleInfo = PlacardSaleInfo.Read(placardSaleInfoPtr);

            Plugin.PluginLog.Debug(
                $"Got PlacardSaleInfo: PurchaseType={saleInfo.PurchaseType}, TenantType={saleInfo.TenantType}, available={saleInfo.AvailabilityType}, until={saleInfo.PhaseEndsAt}, numEntries={saleInfo.EntryCount}");
            Plugin.PluginLog.Debug(
                $"unknown1={saleInfo.Unknown1}, unknown2={saleInfo.Unknown2}, unknown3={saleInfo.Unknown3}, unknown4={BitConverter.ToString(saleInfo.Unknown4)}");
            Plugin.PluginLog.Debug(
                $"housingType={housingType}, territoryTypeId={territoryTypeId}, wardId={wardId}, plotId={plotId}, apartmentNumber={apartmentNumber}, placardSaleInfoPtr={placardSaleInfoPtr}, a8={a8}");

            // get information about the world from the clientstate
            World? world = Plugin.ClientState.LocalPlayer?.CurrentWorld.ValueNullable;
            if (world is null) return;

            var place = plugin.Territories.GetRow(territoryTypeId).PlaceName.Value.Name;
            var worldName = world.Value.Name;
            Plugin.PluginLog.Info($"Plot {place} {wardId + 1}-{plotId + 1} ({worldName}) has {saleInfo.EntryCount} lottery entries.");

            plugin.PaissaClient.PostLotteryInfo(world.Value.RowId, territoryTypeId, wardId, plotId, saleInfo);
        }
    }

    public enum HousingType : byte {
        OwnedHouse = 0,
        UnownedHouse = 1,
        FreeCompanyApartment = 2,
        Apartment = 3
    }
}
