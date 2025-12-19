using System;
using AutoSweep.Structures;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Lumina.Excel.Sheets;

namespace AutoSweep.Paissa {
    public unsafe class LotteryObserver {
        private readonly Plugin plugin;
        private Hook<HandlePlacardSaleInfoDelegate>? placardSaleInfoHook;

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

        // Original notes: easy way to find sig: search for scalar 7043
        // New sig method: Find the movups pattern to locate the function
        [Signature("41 0F 10 06 0F 11 43 48 41 0F 10 4E 10 0F 11 4B 58")]
        private IntPtr movupsAddress;

        public LotteryObserver(Plugin plugin) {
            this.plugin = plugin;
            Plugin.InteropProvider.InitializeFromAttributes(this);

            // Manually create hook at the function start
            if (movupsAddress != IntPtr.Zero)
            {
                IntPtr functionStart = movupsAddress - 0xA8;
                placardSaleInfoHook = Plugin.InteropProvider.HookFromAddress<HandlePlacardSaleInfoDelegate>(
                    functionStart,
                    OnPlacardSaleInfo
                );
                placardSaleInfoHook?.Enable();
            }
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
            World? world = Plugin.PlayerState.CurrentWorld.ValueNullable;
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
