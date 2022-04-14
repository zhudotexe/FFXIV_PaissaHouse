using System;
using System.IO;

namespace AutoSweep.Structures {
    public class LandIdent {
        public short LandId;
        public short TerritoryTypeId;
        public short WardNumber;
        public short WorldId;

        public static LandIdent ReadFromBinaryReader(BinaryReader binaryReader) {
            var landIdent = new LandIdent();
            landIdent.LandId = binaryReader.ReadInt16();
            landIdent.WardNumber = binaryReader.ReadInt16();
            landIdent.TerritoryTypeId = binaryReader.ReadInt16();
            landIdent.WorldId = binaryReader.ReadInt16();
            return landIdent;
        }
    }

    public class HouseInfoEntry {
        public string EstateOwnerName;
        public sbyte[] HouseAppeals;
        public uint HousePrice;
        public HousingFlags InfoFlags;
    }

    [Flags]
    public enum HousingFlags : byte {
        PlotOwned = 1 << 0,
        VisitorsAllowed = 1 << 1,
        HasSearchComment = 1 << 2,
        HouseBuilt = 1 << 3,
        OwnedByFC = 1 << 4
    }

    public enum PurchaseType : byte {
        FCFS = 1,
        Lottery = 2
    }

    [Flags]
    public enum TenantFlags : byte {
        FreeCompany = 1,
        Personal = 2
    }
}
