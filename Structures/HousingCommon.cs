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

    public class LandStruct
    {
        public byte PlotSize;
        public byte HouseState; // ?
        public byte Flags; // ?
        public byte IconAddIcon; // related to sharing?
        public uint FcId;
        public uint FcIcon;
        public uint FcIconColor;
        public ushort[] HousePart;
        public ushort[] HouseColor;

        public static LandStruct ReadFromBinaryReader(BinaryReader binaryReader)
        {
            LandStruct landStruct = new LandStruct();
            landStruct.PlotSize = binaryReader.ReadByte();
            landStruct.HouseState = binaryReader.ReadByte();
            landStruct.Flags = binaryReader.ReadByte();
            landStruct.IconAddIcon = binaryReader.ReadByte();
            landStruct.FcId = binaryReader.ReadUInt32();
            landStruct.FcIcon = binaryReader.ReadUInt32();
            landStruct.FcIconColor = binaryReader.ReadUInt32();
            landStruct.HousePart = new ushort[8];
            landStruct.HouseColor = new ushort[8];
            for (int i = 0; i < 8; i++) {
                landStruct.HousePart[i] = binaryReader.ReadUInt16();
            }
            for (int i = 0; i < 8; i++) {
                landStruct.HouseColor[i] = binaryReader.ReadByte();
            }
            return landStruct;
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
