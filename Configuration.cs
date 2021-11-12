using System;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;

namespace AutoSweep {
    [Serializable]
    public class Configuration : IPluginConfiguration {
        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public bool Enabled { get; set; } = true;
        public string OutputFormatString { get; set; } = "";
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Simple;

        public DistrictNotifConfig Mist { get; set; } = new();
        public DistrictNotifConfig LavenderBeds { get; set; } = new();
        public DistrictNotifConfig Goblet { get; set; } = new();
        public DistrictNotifConfig Shirogane { get; set; } = new();
        public DistrictNotifConfig Empyrean { get; set; } = new();

        public bool HomeworldNotifs { get; set; } = true; // receive alerts for plots on your homeworld
        public bool DatacenterNotifs { get; set; } = false; // receive alerts for plots on all worlds on your data center
        public bool AllNotifs { get; set; } = false; // receive alerts for all worlds

        public XivChatType ChatType { get; set; } = XivChatType.Debug;
        public int Version { get; set; } = 3;

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.pluginInterface = pluginInterface;
        }

        public void Save() {
            pluginInterface.SavePluginConfig(this);
        }
    }

    [Serializable]
    public class DistrictNotifConfig {
        public bool Small { get; set; } = true;
        public bool Medium { get; set; } = true;
        public bool Large { get; set; } = true;
    }

    public enum OutputFormat {
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
        // EnoBot = 3,  // option removed since project abandoned

        Custom = 4
    }
}
