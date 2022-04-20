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
            HousingType housingType,
            ushort territoryTypeId,
            byte wardId,
            byte plotId,
            short apartmentNumber,
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
            HousingType housingType,
            ushort territoryTypeId,
            byte wardId,
            byte plotId,
            short apartmentNumber,
            IntPtr placardSaleInfoPtr,
            long a8
        ) {
            PlacardSaleInfoHook!.Original(agentBase, housingType, territoryTypeId, wardId, plotId, apartmentNumber, placardSaleInfoPtr, a8);

            // if the plot is owned, ignore it
            if (housingType != HousingType.UnownedHouse) return;
            if (placardSaleInfoPtr == IntPtr.Zero) return;

            PlacardSaleInfo saleInfo = PlacardSaleInfo.Read(placardSaleInfoPtr);

            PluginLog.LogDebug(
                $"Got PlacardSaleInfo: PurchaseType={saleInfo.PurchaseType}, TenantType={saleInfo.TenantType}, available={saleInfo.AvailabilityType}, until={saleInfo.PhaseEndsAt}, numEntries={saleInfo.EntryCount}");
            PluginLog.LogDebug($"unknown1={saleInfo.Unknown1}, unknown2={saleInfo.Unknown2}, unknown3={BitConverter.ToString(saleInfo.Unknown3)}");
            PluginLog.LogDebug(
                $"housingType={housingType}, territoryTypeId={territoryTypeId}, wardId={wardId}, plotId={plotId}, apartmentNumber={apartmentNumber}, placardSaleInfoPtr={placardSaleInfoPtr}, a8={a8}");

            // get information about the world from the clientstate
            World world = plugin.ClientState.LocalPlayer?.CurrentWorld.GameData;
            if (world is null) return;

            SeString place = plugin.Territories.GetRow(territoryTypeId)?.PlaceName.Value?.Name;
            SeString worldName = world.Name;
            PluginLog.LogInformation($"Plot {place} {wardId + 1}-{plotId + 1} ({worldName}) has {saleInfo.EntryCount} lottery entries.");

            plugin.PaissaClient.PostLotteryInfo(world.RowId, territoryTypeId, wardId, plotId, saleInfo);
        }
    }

    public enum HousingType : byte {
        OwnedHouse = 0,
        UnownedHouse = 1,
        FreeCompanyApartment = 2,
        Apartment = 3
    }
}
