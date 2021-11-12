using System;
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
        public uint known_price { get; set; }
        public DateTime last_updated_time { get; set; }
        public DateTime est_time_open_min { get; set; }
        public DateTime est_time_open_max { get; set; }
        public ushort est_num_devals { get; set; }
    }

    public class SoldPlotDetail {
        public ushort world_id { get; set; }
        public ushort district_id { get; set; }
        public ushort ward_number { get; set; }
        public ushort plot_number { get; set; }
        public ushort size { get; set; }
        public DateTime last_updated_time { get; set; }
        public DateTime est_time_sold_min { get; set; }
        public DateTime est_time_sold_max { get; set; }
    }
}
