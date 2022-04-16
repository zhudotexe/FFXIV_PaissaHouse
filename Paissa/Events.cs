using System;

namespace AutoSweep.Paissa {
    public class PlotOpenedEventArgs : EventArgs {
        public PlotOpenedEventArgs(OpenPlotDetail plotDetail) {
            PlotDetail = plotDetail;
        }

        public OpenPlotDetail PlotDetail { get; set; }
    }

    public class PlotUpdateEventArgs : EventArgs {
        public PlotUpdateEventArgs(PlotUpdate plotUpdate) {
            PlotUpdate = plotUpdate;
        }

        public PlotUpdate PlotUpdate { get; set; }
    }

    public class PlotSoldEventArgs : EventArgs {
        public PlotSoldEventArgs(SoldPlotDetail plotDetail) {
            PlotDetail = plotDetail;
        }

        public SoldPlotDetail PlotDetail { get; set; }
    }
}
