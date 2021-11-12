using System;
using System.IO;
using System.Text;

namespace AutoSweep.Structures {
    public class HousingWardInfo {
        public HouseInfoEntry[] HouseInfoEntries;
        public LandIdent LandIdent;

        public static unsafe HousingWardInfo Read(IntPtr dataPtr) {
            var wardInfo = new HousingWardInfo();
            using (var unmanagedMemoryStream = new UnmanagedMemoryStream((byte*)dataPtr.ToPointer(), 2656L)) {
                using (var binaryReader = new BinaryReader(unmanagedMemoryStream)) {
                    wardInfo.LandIdent = LandIdent.ReadFromBinaryReader(binaryReader);
                    wardInfo.HouseInfoEntries = new HouseInfoEntry[60];

                    for (var i = 0; i < 60; i++) {
                        var infoEntry = new HouseInfoEntry();
                        infoEntry.HousePrice = binaryReader.ReadUInt32();
                        infoEntry.InfoFlags = (HousingFlags)binaryReader.ReadByte();
                        infoEntry.HouseAppeals = new sbyte[3];
                        for (var j = 0; j < 3; j++) infoEntry.HouseAppeals[j] = binaryReader.ReadSByte();
                        infoEntry.EstateOwnerName = Encoding.UTF8.GetString(binaryReader.ReadBytes(32)).TrimEnd(new char[1]);
                        wardInfo.HouseInfoEntries[i] = infoEntry;

                        // if a house is unowned, the ownerName can be literally anything, so set it to empty string
                        if ((infoEntry.InfoFlags & HousingFlags.PlotOwned) == 0)
                            infoEntry.EstateOwnerName = "";
                    }
                }
            }
            return wardInfo;
        }
    }
}
