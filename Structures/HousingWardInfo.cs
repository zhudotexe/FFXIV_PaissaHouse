using System;
using System.IO;
using System.Text;

namespace AutoSweep.Structures
{
    public class HousingWardInfo
    {
        public LandIdent LandIdent;
        public HouseInfoEntry[] HouseInfoEntries;

        public static unsafe HousingWardInfo Read(IntPtr dataPtr)
        {
            HousingWardInfo wardInfo = new HousingWardInfo();
            using (UnmanagedMemoryStream unmanagedMemoryStream = new UnmanagedMemoryStream((byte*)dataPtr.ToPointer(), 2656L))
            {
                using (BinaryReader binaryReader = new BinaryReader(unmanagedMemoryStream))
                {
                    wardInfo.LandIdent = LandIdent.ReadFromBinaryReader(binaryReader);
                    wardInfo.HouseInfoEntries = new HouseInfoEntry[60];

                    for (int i = 0; i < 60; i++)
                    {
                        HouseInfoEntry infoEntry = new HouseInfoEntry();
                        infoEntry.HousePrice = binaryReader.ReadUInt32();
                        infoEntry.InfoFlags = (HousingFlags)binaryReader.ReadByte();
                        infoEntry.HouseAppeals = new sbyte[3];
                        for (int j = 0; j < 3; j++)
                        {
                            infoEntry.HouseAppeals[j] = binaryReader.ReadSByte();
                        }
                        infoEntry.EstateOwnerName = Encoding.UTF8.GetString(binaryReader.ReadBytes(32)).TrimEnd(new char[1]);
                        wardInfo.HouseInfoEntries[i] = infoEntry;
                    }
                }
            }
            return wardInfo;
        }
    }
}
