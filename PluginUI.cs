using System;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Utility;
using ImGuiNET;

namespace AutoSweep {
    class PluginUI : IDisposable {
        private readonly Configuration configuration;

        private bool settingsVisible = false;

        public PluginUI(Configuration configuration) {
            this.configuration = configuration;
        }

        public bool SettingsVisible {
            get => settingsVisible;
            set => settingsVisible = value;
        }

        public void Dispose() {
            // we don't need to do anything here
        }

        public void Draw() {
            DrawSettingsWindow();
        }

        private void DrawSettingsWindow() {
            if (!SettingsVisible) return;

            ImGui.SetNextWindowSize(new Vector2(450, 210), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("PaissaHouse Configuration", ref settingsVisible,
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
                // tab bar
                if (ImGui.BeginTabBar("paissatabs")) {
                    // tab: settings
                    if (ImGui.BeginTabItem("Settings")) {
                        // enabled
                        bool enabled = configuration.Enabled;
                        if (ImGui.Checkbox("Enabled", ref enabled)) configuration.Enabled = enabled;
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Enable or disable PaissaHouse. If disabled, it will not look for houses, post ward information to PaissaDB, or send notifications.");

                        // output format
                        OutputFormat outputFormat = configuration.OutputFormat;
                        if (ImGui.BeginCombo("Output Format", outputFormat.ToString())) {
                            foreach (OutputFormat format in Enum.GetValues(typeof(OutputFormat))) {
                                bool selected = format == outputFormat;
                                if (ImGui.Selectable(format.ToString(), selected))
                                    configuration.OutputFormat = format;
                                if (selected)
                                    ImGui.SetItemDefaultFocus();
                            }
                            ImGui.EndCombo();
                        }

                        // custom output format
                        if (configuration.OutputFormat == OutputFormat.Custom) {
                            string customOutputFormat = configuration.OutputFormatString;
                            if (ImGui.InputText("Custom Output Format", ref customOutputFormat, 2000)) configuration.OutputFormatString = customOutputFormat;
                        }

                        // homeworld alerts
                        bool homeworldNotifs = configuration.HomeworldNotifs;
                        if (ImGui.Checkbox("Notifications: Homeworld", ref homeworldNotifs)) configuration.HomeworldNotifs = homeworldNotifs;
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Whether or not to receive notifications about new plots for sale on your homeworld.");

                        // datacenter alerts
                        bool datacenterNotifs = configuration.DatacenterNotifs;
                        if (ImGui.Checkbox("Notifications: Datacenter", ref datacenterNotifs)) configuration.DatacenterNotifs = datacenterNotifs;
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Whether or not to receive notifications about all new plots for sale on your home data center.");

                        // all world alerts
                        bool allNotifs = configuration.AllNotifs;
                        if (ImGui.Checkbox("Notifications: All", ref allNotifs)) configuration.AllNotifs = allNotifs;
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Whether or not to receive notifications about all new plots for sale, regardless of world or data center.");

                        // chat type
                        XivChatType outputChatType = configuration.ChatType;
                        if (ImGui.BeginCombo("Output Chat Channel", outputChatType.ToString())) {
                            foreach (XivChatType chatType in Enum.GetValues(typeof(XivChatType))) {
                                bool selected = chatType == outputChatType;
                                if (ImGui.Selectable(chatType.GetAttribute<XivChatTypeInfoAttribute>()?.FancyName ?? chatType.ToString(), selected))
                                    configuration.ChatType = chatType;
                                if (selected)
                                    ImGui.SetItemDefaultFocus();
                            }
                            ImGui.EndCombo();
                        }

                        // sweep chat alert
                        bool chatSweepAlert = configuration.ChatSweepAlert;
                        if (ImGui.Checkbox("Chat Sweep Notifications", ref chatSweepAlert)) configuration.ChatSweepAlert = chatSweepAlert;
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Whether or not PaissaHouse should print information about ward sweeps when viewing housing districts from an aetheryte or ferry.");

                        ImGui.EndTabItem();
                    }
                    DrawTabItemForDistrict("Mist", configuration.Mist);
                    DrawTabItemForDistrict("The Lavender Beds", configuration.LavenderBeds);
                    DrawTabItemForDistrict("The Goblet", configuration.Goblet);
                    DrawTabItemForDistrict("Shirogane", configuration.Shirogane);
                    DrawTabItemForDistrict("Empyreum", configuration.Empyreum);
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

        private void DrawTabItemForDistrict(string districtName, DistrictNotifConfig notifConfig) {
            if (ImGui.BeginTabItem(districtName)) {
                ImGui.Text($"This tab controls which houses to receive notifications for in {districtName}.\nIt won't affect the output when sweeping the whole district.");
                bool small = notifConfig.Small;
                if (ImGui.Checkbox("Small", ref small)) notifConfig.Small = small;
                bool medium = notifConfig.Medium;
                if (ImGui.Checkbox("Medium", ref medium)) notifConfig.Medium = medium;
                bool large = notifConfig.Large;
                if (ImGui.Checkbox("Large", ref large)) notifConfig.Large = large;
                ImGui.Separator();
                bool fc = notifConfig.FreeCompany;
                if (ImGui.Checkbox("Free Company Purchase Allowed", ref fc)) notifConfig.FreeCompany = fc;
                bool solo = notifConfig.Individual;
                if (ImGui.Checkbox("Individual Purchase Allowed", ref solo)) notifConfig.Individual = solo;
                ImGui.EndTabItem();
            }
        }
    }
}
