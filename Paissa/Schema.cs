using System;
using AutoSweep.Structures;
using Newtonsoft.Json.Linq;

// ReSharper disable InconsistentNaming

namespace AutoSweep.Paissa {
    public class WSMessage {
        public string Type { get; set; }
        public JObject Data { get; set; }
    }

    public class DistrictDetail {
        public ushort district_id { get; set; }
        public string name { get; set; }
        public ushort num_open_plots { get; set; }
        public OpenPlotDetail[] open_plots { get; set; }
    }

    public class OpenPlotDetail {
        public ushort world_id { get; set; }
        public ushort district_id { get; set; }
        public ushort ward_number { get; set; }
        public ushort plot_number { get; set; }
        public ushort size { get; set; }
        public uint price { get; set; }
        public float last_updated_time { get; set; }
        public float est_time_open_min { get; set; }
        public float est_time_open_max { get; set; }
        public PurchaseSystem purchase_system { get; set; }
        public uint? lotto_entries { get; set; }
        public AvailabilityType? lotto_phase { get; set; }
        public uint? lotto_phase_until { get; set; }
    }

    public class PlotUpdate {
        public ushort world_id { get; set; }
        public ushort district_id { get; set; }
        public ushort ward_number { get; set; }
        public ushort plot_number { get; set; }
        public ushort size { get; set; }
        public uint price { get; set; }
        public float last_updated_time { get; set; }
        public PurchaseSystem purchase_system { get; set; }
        public uint lotto_entries { get; set; }
        public AvailabilityType lotto_phase { get; set; }
        public AvailabilityType? previous_lotto_phase { get; set; }
        public uint lotto_phase_until { get; set; }
    }

    public class SoldPlotDetail {
        public ushort world_id { get; set; }
        public ushort district_id { get; set; }
        public ushort ward_number { get; set; }
        public ushort plot_number { get; set; }
        public ushort size { get; set; }
        public float last_updated_time { get; set; }
        public float est_time_sold_min { get; set; }
        public float est_time_sold_max { get; set; }
    }

    [Flags]
    public enum PurchaseSystem : byte {
        Lottery = 1,
        FreeCompany = 2,
        Individual = 4
    }
}
