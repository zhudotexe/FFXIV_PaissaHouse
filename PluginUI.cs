using System;
using System.Numerics;
using ImGuiNET;

namespace AutoSweep
{
    class PluginUI : IDisposable
    {
        private readonly Configuration configuration;

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public void Draw()
        {
            DrawSettingsWindow();
        }

        private void DrawSettingsWindow()
        {
            if (!SettingsVisible) {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(450, 210), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("PaissaHouse Configuration", ref settingsVisible,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
                // tab bar
                if (ImGui.BeginTabBar("paissatabs")) {
                    // tab: settings
                    if (ImGui.BeginTabItem("Settings")) {
                        // enabled
                        var enabled = configuration.Enabled;
                        if (ImGui.Checkbox("Enabled", ref enabled)) {
                            configuration.Enabled = enabled;
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Enable or disable PaissaHouse. If disabled, it will not look for houses, post ward information to PaissaDB, or send notifications.");
                        }

                        // output format
                        var outputFormat = configuration.OutputFormat;
                        if (ImGui.BeginCombo("Output Format", outputFormat.ToString())) {
                            foreach (OutputFormat format in Enum.GetValues(typeof(OutputFormat))) {
                                var selected = format == outputFormat;
                                if (ImGui.Selectable(format.ToString(), selected))
                                    configuration.OutputFormat = format;
                                if (selected)
                                    ImGui.SetItemDefaultFocus();
                            }
                            ImGui.EndCombo();
                        }

                        // custom output format
                        if (configuration.OutputFormat == OutputFormat.Custom) {
                            var customOutputFormat = configuration.OutputFormatString;
                            if (ImGui.InputText("Custom Output Format", ref customOutputFormat, 2000)) {
                                configuration.OutputFormatString = customOutputFormat;
                            }
                        }

                        // homeworld alerts
                        var homeworldNotifs = configuration.HomeworldNotifs;
                        if (ImGui.Checkbox("Notifications: Homeworld", ref homeworldNotifs)) {
                            configuration.HomeworldNotifs = homeworldNotifs;
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Whether or not to receive notifications about new plots for sale on your homeworld.");
                        }

                        // datacenter alerts
                        var datacenterNotifs = configuration.DatacenterNotifs;
                        if (ImGui.Checkbox("Notifications: Datacenter", ref datacenterNotifs)) {
                            configuration.DatacenterNotifs = datacenterNotifs;
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Whether or not to receive notifications about all new plots for sale on your home data center.");
                        }

                        // all world alerts
                        var allNotifs = configuration.AllNotifs;
                        if (ImGui.Checkbox("Notifications: All", ref allNotifs)) {
                            configuration.AllNotifs = allNotifs;
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Whether or not to receive notifications about all new plots for sale, regardless of world or data center.");
                        }
                        ImGui.EndTabItem();
                    }
                    DrawTabItemForDistrict("Mist", configuration.Mist);
                    DrawTabItemForDistrict("The Lavender Beds", configuration.LavenderBeds);
                    DrawTabItemForDistrict("The Goblet", configuration.Goblet);
                    DrawTabItemForDistrict("Shirogane", configuration.Shirogane);
                    DrawTabItemForDistrict("Empyrean", configuration.Empyrean);
                    ImGui.EndTabBar();
                    ImGui.Separator();
                }

                // save and close
                if (ImGui.Button("Save and Close")) {
                    configuration.Save();
                    SettingsVisible = false;
                }
            }
            ImGui.End();
        }

        private void DrawTabItemForDistrict(string districtName, DistrictNotifConfig notifConfig)
        {
            if (ImGui.BeginTabItem(districtName)) {
                ImGui.Text($"This tab controls which houses to receive notifications for in {districtName}.\nIt won't affect the output when sweeping the whole district.");
                var small = notifConfig.Small;
                if (ImGui.Checkbox("Small", ref small)) {
                    notifConfig.Small = small;
                }
                var medium = notifConfig.Medium;
                if (ImGui.Checkbox("Medium", ref medium)) {
                    notifConfig.Medium = medium;
                }
                var large = notifConfig.Large;
                if (ImGui.Checkbox("Large", ref large)) {
                    notifConfig.Large = large;
                }
                ImGui.EndTabItem();
            }
        }

        public void Dispose()
        {
            // we don't need to do anything here
        }
    }
}
