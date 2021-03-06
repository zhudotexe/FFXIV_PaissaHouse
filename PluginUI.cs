using ImGuiNET;
using System;
using System.Numerics;

namespace AutoSweep
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
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
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(300, 160), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("AutoSweep Configuration", ref this.settingsVisible, 
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // enabled
                var enabled = this.configuration.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled))
                {
                    this.configuration.Enabled = enabled;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Whether or not the plugin is enabled. If disabled, it will not look for houses.");
                }

                // post
                var postInfo = this.configuration.PostInfo;
                if (ImGui.Checkbox("Contribute to SDHA", ref postInfo))
                {
                    this.configuration.PostInfo = postInfo;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Whether or not the plugin sends housing ward information to SDHA, the housing alerts Discord server.");
                }

                // output format
                var outputFormat = this.configuration.OutputFormat;
                if (ImGui.BeginCombo("Output Format", outputFormat.ToString()))
                {
                    foreach (OutputFormat format in Enum.GetValues(typeof(OutputFormat)))
                    {
                        bool selected = format == outputFormat;
                        if (ImGui.Selectable(format.ToString(), selected))
                            this.configuration.OutputFormat = format;
                        if (selected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }

                // save and close
                if (ImGui.Button("Save and Close"))
                {
                    this.configuration.Save();
                    this.SettingsVisible = false;
                }
            }
            ImGui.End();
        }

        public void Dispose()
        {
            // we don't need to do anything here
        }
    }
}
