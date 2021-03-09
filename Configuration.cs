using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace AutoSweep
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public bool Enabled { get; set; } = true;
        public bool PostInfo { get; set; } = true;
        public string OutputFormatString { get; set; } = "";
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Simple;

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
