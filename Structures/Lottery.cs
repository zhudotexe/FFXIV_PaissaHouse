using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AutoSweep.Structures {
    public class PlacardSaleInfo {
        public int ServerTimestamp; // 0x18 - 0x1B
        public PurchaseType PurchaseType; // 0x20
        public TenantFlags TenantFlags; // 0x21
        public AvailabilityType AvailabilityType; // 0x22
        public byte Unknown1; // 0x23
        public uint AcceptingEntriesUntil; // 0x24 - 0x27
        public uint Unknown2; // 0x28 - 0x2B
        public ushort EntryCount; // 0x2C - 0x2D
        public byte[] Unknown3; // 0x2E - 0x3F

        public static unsafe PlacardSaleInfo Read(IntPtr dataPtr) {
            var saleInfo = new PlacardSaleInfo();
            saleInfo.ServerTimestamp = Marshal.ReadInt32(dataPtr - 0x8);
            using (var unmanagedMemoryStream = new UnmanagedMemoryStream((byte*)dataPtr.ToPointer(), 32)) {
                using (var binaryReader = new BinaryReader(unmanagedMemoryStream)) {
                    saleInfo.PurchaseType = (PurchaseType)binaryReader.ReadByte();
                    saleInfo.TenantFlags = (TenantFlags)binaryReader.ReadByte();
                    saleInfo.AvailabilityType = (AvailabilityType)binaryReader.ReadByte();
                    saleInfo.Unknown1 = binaryReader.ReadByte();
                    saleInfo.AcceptingEntriesUntil = binaryReader.ReadUInt32();
                    saleInfo.Unknown2 = binaryReader.ReadUInt32();
                    saleInfo.EntryCount = binaryReader.ReadUInt16();
                    saleInfo.Unknown3 = binaryReader.ReadBytes(16);
                }
            }
            return saleInfo;
        }
    }

    public class HousingRequest {
        public int ServerTimestamp; // 0x18 - 0x1B
        public uint SubOpcode; // 0x20 - 0x23
        public ushort TerritoryTypeId; // 0x24 - 0x25
        public ushort Unknown1; // 0x26 - 0x27
        public byte PlotId; // 0x28
        public byte WardId; // 0x29
        public byte[] Unknown2; // 0x2A - 0x3F

        public static unsafe HousingRequest Read(IntPtr dataPtr) {
            var housingRequest = new HousingRequest();
            housingRequest.ServerTimestamp = Marshal.ReadInt32(dataPtr - 0x8);
            using (var unmanagedMemoryStream = new UnmanagedMemoryStream((byte*)dataPtr.ToPointer(), 32)) {
                using (var binaryReader = new BinaryReader(unmanagedMemoryStream)) {
                    housingRequest.SubOpcode = binaryReader.ReadUInt32();
                    housingRequest.TerritoryTypeId = binaryReader.ReadUInt16();
                    housingRequest.Unknown1 = binaryReader.ReadUInt16();
                    housingRequest.PlotId = binaryReader.ReadByte();
                    housingRequest.WardId = binaryReader.ReadByte();
                    housingRequest.Unknown2 = binaryReader.ReadBytes(20);
                }
            }
            return housingRequest;
        }
    }

    public enum AvailabilityType : byte {
        Available = 1,
        Unavailable = 3
    }
}
