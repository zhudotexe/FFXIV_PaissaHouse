using System;
using System.IO;

namespace AutoSweep.Structures {
    public class PlacardSaleInfo {
        public PurchaseType PurchaseType; // 0x20
        public TenantType TenantType; // 0x21
        public AvailabilityType AvailabilityType; // 0x22
        public byte Unknown1; // 0x23
        public uint Unknown2; // 0x24 - 0x27
        public uint PhaseEndsAt; // 0x28 - 0x2B
        public uint Unknown3; // 0x2C - 0x2F
        public uint EntryCount; // 0x30 - 0x33
        public byte[] Unknown4; // 0x34 - 0x4B

        public static unsafe PlacardSaleInfo Read(IntPtr dataPtr) {
            var saleInfo = new PlacardSaleInfo();
            using var unmanagedMemoryStream = new UnmanagedMemoryStream((byte*)dataPtr.ToPointer(), 32);
            using var binaryReader = new BinaryReader(unmanagedMemoryStream);
            saleInfo.PurchaseType = (PurchaseType)binaryReader.ReadByte();
            saleInfo.TenantType = (TenantType)binaryReader.ReadByte();
            saleInfo.AvailabilityType = (AvailabilityType)binaryReader.ReadByte();
            saleInfo.Unknown1 = binaryReader.ReadByte();
            saleInfo.Unknown2 = binaryReader.ReadUInt32();
            saleInfo.PhaseEndsAt = binaryReader.ReadUInt32();
            saleInfo.Unknown3 = binaryReader.ReadUInt32();
            saleInfo.EntryCount = binaryReader.ReadUInt32();
            saleInfo.Unknown4 = binaryReader.ReadBytes(16);
            return saleInfo;
        }
    }

    public enum AvailabilityType : byte {
        Available = 1,
        InResultsPeriod = 2,
        Unavailable = 3
    }
}
