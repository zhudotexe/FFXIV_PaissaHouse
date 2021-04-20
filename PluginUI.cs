using ImGuiNET;
using System;
using System.Numerics;

namespace AutoSweep
{
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        public PluginUI(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public void Draw()
        {
            DrawSettingsWindow();
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible) {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(450, 160), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("PaissaHouse Configuration", ref this.settingsVisible,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
                // tab bar
                if (ImGui.BeginTabBar("paissatabs")) {
                    // tab: settings
                    if (ImGui.BeginTabItem("Settings")) {
                        // enabled
                        var enabled = this.configuration.Enabled;
                        if (ImGui.Checkbox("Enabled", ref enabled)) {
                            this.configuration.Enabled = enabled;
                        }
                        if (ImGui.IsItemHovered()) {
                            ImGui.SetTooltip("Enable or disable PaissaHouse. If disabled, it will not look for houses, post ward information to PaissaDB, or send notifications.");
                        }

                        // output format
                        var outputFormat = this.configuration.OutputFormat;
                        if (ImGui.BeginCombo("Output Format", outputFormat.ToString())) {
                            foreach (OutputFormat format in Enum.GetValues(typeof(OutputFormat))) {
                                bool selected = format == outputFormat;
                                if (ImGui.Selectable(format.ToString(), selected))
                                    this.configuration.OutputFormat = format;
                                if (selected)
                                    ImGui.SetItemDefaultFocus();
                            }
                            ImGui.EndCombo();
                        }

                        // custom output format
                        if (this.configuration.OutputFormat == OutputFormat.Custom) {
                            string customOutputFormat = this.configuration.OutputFormatString;
                            if (ImGui.InputText("Custom Output Format", ref customOutputFormat, 2000)) {
                                this.configuration.OutputFormatString = customOutputFormat;
                            }
                        }
                        ImGui.EndTabItem();
                    }
                    DrawTabItemForDistrict("Mist", configuration.Mist);
                    DrawTabItemForDistrict("The Lavender Beds", configuration.LavenderBeds);
                    DrawTabItemForDistrict("The Goblet", configuration.Goblet);
                    DrawTabItemForDistrict("Shirogane", configuration.Shirogane);
                    // DrawTabItemForDistrict("The Firmament", configuration.Firmament);
                    ImGui.EndTabBar();
                    ImGui.Separator();
                }

                // save and close
                if (ImGui.Button("Save and Close")) {
                    this.configuration.Save();
                    this.SettingsVisible = false;
                }
            }
            ImGui.End();
        }

        public void DrawTabItemForDistrict(string districtName, DistrictNotifConfig notifConfig)
        {
            if (ImGui.BeginTabItem(districtName)) {
                ImGui.Text($"This tab controls which houses to receive notifications for in {districtName}.");
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
