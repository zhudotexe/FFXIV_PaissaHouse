namespace AutoSweep.Paissa {
    public class Opcodes {
        // last updated 6.1
        // server => client
        public const ushort PlacardSaleInfo = 0x022D;

        // client => server
        public const ushort HousingRequest = 0x028E; // may be a broader event handler?
    }

    public class SubOpcodes {
        public const ushort HousingRequest_GetUnownedHousePlacard = 1105;
        public const ushort HousingRequest_GetOwnedHousePlacard = 1106;
    }
}
