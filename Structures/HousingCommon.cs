using System;
using System.IO;

namespace AutoSweep.Structures
{
    public class LandIdent
    {
        public short LandId;
        public short WardNumber;
        public short TerritoryTypeId;
        public short WorldId;

        public static LandIdent ReadFromBinaryReader(BinaryReader binaryReader)
        {
            LandIdent landIdent = new LandIdent();
            landIdent.LandId = binaryReader.ReadInt16();
            landIdent.WardNumber = binaryReader.ReadInt16();
            landIdent.TerritoryTypeId = binaryReader.ReadInt16();
            landIdent.WorldId = binaryReader.ReadInt16();
            return landIdent;
        }
    }

    public class HouseInfoEntry
    {
        public uint HousePrice;
        public HousingFlags InfoFlags;
        public sbyte[] HouseAppeals;
        public string EstateOwnerName;
    }

    [Flags]
    public enum HousingFlags : byte
    {
        PlotOwned = 1 << 0,
        VisitorsAllowed = 1 << 1,
        HasSearchComment = 1 << 2,
        HouseBuilt = 1 << 3,
        OwnedByFC = 1 << 4,
    }
}
