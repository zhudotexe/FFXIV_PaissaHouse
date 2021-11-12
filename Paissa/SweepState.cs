using System;
using System.Collections.Generic;
using AutoSweep.Structures;

namespace AutoSweep.Paissa {
    public class OpenHouse {
        public HouseInfoEntry HouseInfoEntry;
        public ushort PlotNum;

        public ushort WardNum;

        public OpenHouse(ushort wardNum, ushort plotNum, HouseInfoEntry houseInfoEntry) {
            WardNum = wardNum;
            PlotNum = plotNum;
            HouseInfoEntry = houseInfoEntry;
        }
    }

    public class SweepState {
        private readonly int numWardsPerDistrict;

        public SweepState(int numWardsPerDistrict) {
            this.numWardsPerDistrict = numWardsPerDistrict;
        }

        public int DistrictId { get; private set; }
        public int WorldId { get; private set; }
        public DateTime SweepTime { get; private set; }
        public HashSet<int> SeenWardNumbers { get; } = new();
        public List<OpenHouse> OpenHouses { get; } = new();
        public bool IsComplete => SeenWardNumbers.Count == numWardsPerDistrict;

        /// <summary>
        ///     Returns whether or not a received WardInfo should start a new sweep.
        /// </summary>
        public bool ShouldStartNewSweep(HousingWardInfo wardInfo) {
            return wardInfo.LandIdent.WorldId != WorldId
                   || wardInfo.LandIdent.TerritoryTypeId != DistrictId
                   || SweepTime < DateTime.Now - TimeSpan.FromMinutes(10);
        }

        /// <summary>
        ///     Sets the housing state to a sweep of the district of the given WardInfo.
        /// </summary>
        public void StartDistrictSweep(HousingWardInfo wardInfo) {
            WorldId = wardInfo.LandIdent.WorldId;
            DistrictId = wardInfo.LandIdent.TerritoryTypeId;
            SeenWardNumbers.Clear();
            OpenHouses.Clear();
            SweepTime = DateTime.Now;
        }

        /// <summary>
        ///     Returns whether the ward represented by the given wardinfo has been seen in the current sweep.
        /// </summary>
        public bool Contains(HousingWardInfo wardInfo) {
            return SeenWardNumbers.Contains(wardInfo.LandIdent.WardNumber);
        }

        /// <summary>
        ///     Adds sweep information for the given wardinfo to the current sweep.
        /// </summary>
        public void Add(HousingWardInfo wardInfo) {
            if (Contains(wardInfo)) return;
            SeenWardNumbers.Add(wardInfo.LandIdent.WardNumber);

            // add open houses to the internal list
            for (ushort i = 0; i < wardInfo.HouseInfoEntries.Length; i++) {
                HouseInfoEntry houseInfoEntry = wardInfo.HouseInfoEntries[i];
                if ((houseInfoEntry.InfoFlags & HousingFlags.PlotOwned) == 0)
                    OpenHouses.Add(new OpenHouse((ushort)wardInfo.LandIdent.WardNumber, i, houseInfoEntry));
            }
        }

        /// <summary>
        ///     Resets the state such that no wards have been seen.
        /// </summary>
        public void Reset() {
            WorldId = -1;
            DistrictId = -1;
            SeenWardNumbers.Clear();
            OpenHouses.Clear();
        }
    }
}
