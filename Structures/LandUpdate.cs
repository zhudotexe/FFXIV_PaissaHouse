using System;
using System.IO;

namespace AutoSweep.Structures
{
    public class LandUpdate
    {
        public LandIdent LandIdent;
        public LandStruct LandStruct;

        public static unsafe LandUpdate Read(IntPtr dataPtr)
        {
            LandUpdate landUpdate = new LandUpdate();
            using (UnmanagedMemoryStream unmanagedMemoryStream = new UnmanagedMemoryStream((byte*)dataPtr.ToPointer(), 384L)) {
                using (BinaryReader binaryReader = new BinaryReader(unmanagedMemoryStream)) {
                    landUpdate.LandIdent = LandIdent.ReadFromBinaryReader(binaryReader);
                    landUpdate.LandStruct = LandStruct.ReadFromBinaryReader(binaryReader);
                }
            }
            return landUpdate;
        }
    }
}
