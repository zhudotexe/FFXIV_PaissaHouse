
using Lumina.Excel.Sheets;

namespace AutoSweep.Paissa {
    public class Utils {
        // configuration constants
        public const string CommandName = "/psweep";
        public const string HouseCommandName = "/phouse";
        public const int NumWardsPerDistrict = 30;

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

        public static bool ConfigEnabledForPlot(Plugin plugin, ushort worldId, ushort districtId, ushort size,
            PurchaseSystem purchaseSystem) {
            if (!plugin.Configuration.Enabled) return false;
            // does the config want notifs for this world?
            World eventWorld = plugin.Worlds.GetRow(worldId);
            if (!(plugin.Configuration.AllNotifs
                  || plugin.Configuration.HomeworldNotifs && worldId == Plugin.ClientState.LocalPlayer?.HomeWorld.RowId
                  || plugin.Configuration.DatacenterNotifs && eventWorld.DataCenter.RowId ==
                  Plugin.ClientState.LocalPlayer?.HomeWorld.Value.DataCenter.RowId))
                return false;
            // get the district config
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
                    return false;
            }

            // what about house sizes in this district?
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
                    return false;
            }

            // and FC/individual purchase?
            PurchaseSystem purchaseSystemMask = (districtNotifs.FreeCompany ? PurchaseSystem.FreeCompany : 0) |
                                                (districtNotifs.Individual ? PurchaseSystem.Individual : 0);
            notifEnabled = notifEnabled && (purchaseSystem & purchaseSystemMask) != 0;
            return notifEnabled;
        }
    }
}