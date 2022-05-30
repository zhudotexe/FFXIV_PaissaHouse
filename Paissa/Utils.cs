using Dalamud.Game.ClientState;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

namespace AutoSweep.Paissa {
    public class Utils {
        // configuration constants
        public const string CommandName = "/psweep";
        public const string HouseCommandName = "/phouse";
        public const int NumWardsPerDistrict = 24;

        public static uint TerritoryTypeIdToLandSetId(uint territoryTypeId) {
            return territoryTypeId switch {
                641 => 3, // shirogane
                979 => 4, // empyreum
                _ => territoryTypeId - 339 // mist, lb, gob are 339-341
            };
        }

        public static string FormatCustomOutputString(
            string template,
            string districtName,
            string districtNameNoSpaces,
            string worldName,
            string wardNum,
            string plotNum,
            string housePrice,
            string housePriceMillions,
            string houseSizeName
        ) {
            // mildly disgusting
            // why can't we have nice things like python :(
            return template.Replace("{districtName}", districtName)
                .Replace("{districtNameNoSpaces}", districtNameNoSpaces)
                .Replace("{worldName}", worldName)
                .Replace("{wardNum}", wardNum)
                .Replace("{plotNum}", plotNum)
                .Replace("{housePrice}", housePrice)
                .Replace("{housePriceMillions}", housePriceMillions)
                .Replace("{houseSizeName}", houseSizeName);
        }

        public static bool ConfigEnabledForPlot(Plugin plugin, ushort worldId, ushort districtId, ushort size) {
            if (!plugin.Configuration.Enabled) return false;
            // does the config want notifs for this world?
            World eventWorld = plugin.Worlds.GetRow(worldId);
            if (!(plugin.Configuration.AllNotifs
                  || plugin.Configuration.HomeworldNotifs && worldId == plugin.ClientState.LocalPlayer?.HomeWorld.Id
                  || plugin.Configuration.DatacenterNotifs && eventWorld?.DataCenter.Row == plugin.ClientState.LocalPlayer?.HomeWorld.GameData.DataCenter.Row))
                return false;
            // what about house sizes in this district?
            DistrictNotifConfig districtNotifs;
            switch (districtId) {
                case 339:
                    districtNotifs = plugin.Configuration.Mist;
                    break;
                case 340:
                    districtNotifs = plugin.Configuration.LavenderBeds;
                    break;
                case 341:
                    districtNotifs = plugin.Configuration.Goblet;
                    break;
                case 641:
                    districtNotifs = plugin.Configuration.Shirogane;
                    break;
                case 979:
                    districtNotifs = plugin.Configuration.Empyrean;
                    break;
                default:
                    PluginLog.Warning($"Unknown district in plot open event: {districtId}");
                    return false;
            }
            bool notifEnabled;
            switch (size) {
                case 0:
                    notifEnabled = districtNotifs.Small;
                    break;
                case 1:
                    notifEnabled = districtNotifs.Medium;
                    break;
                case 2:
                    notifEnabled = districtNotifs.Large;
                    break;
                default:
                    PluginLog.Warning($"Unknown plot size in plot open event: {size}");
                    return false;
            }
            return notifEnabled;
        }
    }
}
