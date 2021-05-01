using System;
using System.Collections.Generic;
using AutoSweep.Structures;

namespace AutoSweep.Paissa
{
    public class SweepState
    {
        private int lastSweptDistrictTerritoryTypeId;
        private int lastSweptDistrictWorldId;
        private DateTime lastSweepTime;
        private HashSet<int> lastSweptDistrictSeenWardNumbers = new HashSet<int>();

        public HashSet<int> LastSweptDistrictSeenWardNumbers => lastSweptDistrictSeenWardNumbers;

        /**
         * Resets the state such that no wards have been seen.
         */
        public void Reset()
        {
            lastSweptDistrictWorldId = -1;
            lastSweptDistrictTerritoryTypeId = -1;
            lastSweptDistrictSeenWardNumbers.Clear();
        }

        /**
         * Returns whether or not a received WardInfo should start a new sweep.
         */
        public bool ShouldStartNewSweep(HousingWardInfo wardInfo)
        {
            return wardInfo.LandIdent.WorldId != lastSweptDistrictWorldId
                   || wardInfo.LandIdent.TerritoryTypeId != lastSweptDistrictTerritoryTypeId
                   || lastSweepTime < (DateTime.Now - TimeSpan.FromMinutes(10));
        }

        /**
         * Sets the housing state to a sweep of the district of the given WardInfo.
         */
        public void StartDistrictSweep(HousingWardInfo wardInfo)
        {
            lastSweptDistrictWorldId = wardInfo.LandIdent.WorldId;
            lastSweptDistrictTerritoryTypeId = wardInfo.LandIdent.TerritoryTypeId;
            lastSweptDistrictSeenWardNumbers.Clear();
            lastSweepTime = DateTime.Now;
        }
    }
}
