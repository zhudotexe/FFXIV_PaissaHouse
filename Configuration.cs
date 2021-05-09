using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AutoSweep
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 2;

        public bool Enabled { get; set; } = true;
        public string OutputFormatString { get; set; } = "";
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Simple;

        public DistrictNotifConfig Mist { get; set; } = new DistrictNotifConfig();
        public DistrictNotifConfig LavenderBeds { get; set; } = new DistrictNotifConfig();
        public DistrictNotifConfig Goblet { get; set; } = new DistrictNotifConfig();
        public DistrictNotifConfig Shirogane { get; set; } = new DistrictNotifConfig();
        public DistrictNotifConfig Firmament { get; set; } = new DistrictNotifConfig(); // futureproofing :)

        public bool HomeworldNotifs { get; set; } = true; // receive alerts for plots on your homeworld
        public bool DatacenterNotifs { get; set; } = false; // receive alerts for plots on all worlds on your data center
        public bool AllNotifs { get; set; } = false; // receive alerts for all worlds

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }

    [Serializable]
    public class DistrictNotifConfig
    {
        public bool Small { get; set; } = true;
        public bool Medium { get; set; } = true;
        public bool Large { get; set; } = true;
    }

    public enum OutputFormat
    {
        // Sweep for Shirogane (Diabolos)
        // Shirogane 5-25 (3.187m)
        // ...
        Simple = 1,

        // Sweep for Shirogane (Diabolos)
        // @SmallShirogane 5-25 (3.187m)
        // @LargeShirogane 1-1 (50.000m)
        Pings = 2,

        // Sweep for Shirogane (Diabolos)
        // ##forsale shirogane w5 p25
        // ...
        EnoBot = 3,

        Custom = 4,
    }
}
