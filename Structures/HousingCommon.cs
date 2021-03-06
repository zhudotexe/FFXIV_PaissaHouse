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
        public HousingAppeal[] HouseAppeals;
        public string EstateOwnerName;
    }

    public enum HousingAppeal : byte
    {
        None = 0,
        Emporium = 1,
        Boutique = 2,
        DesignerHome = 3,
        MessageBook = 4,
        Tavern = 5,
        Eatery = 6,
        ImmersiveExperience = 7,
        Cafe = 8,
        Aquarium = 9,
        Sanctum = 10,
        Venue = 11,
        Florist = 12,
        Library = 14,
        PhotoStudio = 15,
        HauntedHouse = 16,
        Atelier = 17,
        Bathhouse = 18,
        Garden = 19,
        FarEastern = 20,
    }

    [Flags]
    public enum HousingFlags : byte
    {
        PlotOwned = 1 << 0,
        VisitorsAllowed = 1 << 1,
        HasSearchComment = 1 << 2,
        HouseBuilt = 1 << 3,
        OwnedByFC = 1 << 4,
        // 00001 1: small owned by personal, no visitors, no search comment, no house built
        // 01001 9: small owned by personal, no visitors, no search comment
        // 01011 11: small owned by personal, visitors, no search comment
        // 01101 13: small owned by personal, no visitors, search comment
        // 01111 15: small owned by personal, visitors, search comment
        // 10000 16: small open
        // 11001 25: small owned by fc, no visitors, no search comment
        // 11011 27: medium owned by fc, visitors
        // 11101 29: small owned by fc, no visitors, search comment
        // 11111 31: large or medium owned by fc, visitors, search comment
    }
}
