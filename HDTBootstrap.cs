using Hearthstone_Deck_Tracker.Plugins;
using System;
using System.Reflection;
using System.Windows.Controls;

namespace AmalgadonPlugin
{
    /// <summary>
    /// Wires up the plugin's logic once HDT loads it into the session.
    /// Mirrors the HDTBootstrap.cs pattern from the HDTPluginTemplate.
    /// </summary>
    public class HDTBootstrap : IPlugin
    {
        public AmalgadonPlugin pluginInstance;

        public string Author      => "amalgadon@proton.me";
        public string ButtonText  => "Open in Amalgadon";
        public string Description => "Capture your current Battlegrounds board and open it in Amalgadon.";
        public MenuItem MenuItem  { get; set; } = null;
        public string Name        => "Amalgadon Board Capture";
        public Version Version    => Assembly.GetExecutingAssembly().GetName().Version;

        private void AddMenuItem()
        {
            // Checkable toggle in HDT's main menu: Plugins → Amalgadon Board Capture
            MenuItem = new MenuItem
            {
                Header = "Amalgadon Board Capture",
                IsCheckable = true,
                IsChecked = true,
            };
            MenuItem.Click += (sender, args) => pluginInstance?.Toggle(MenuItem.IsChecked);
        }

        // Options button in HDT Settings → Plugins tab: treat as a direct capture.
        public void OnButtonPress() => BoardCapture.OpenCurrentBoard();

        public void OnLoad()
        {
            pluginInstance = new AmalgadonPlugin();
            AddMenuItem();
        }

        public void OnUnload()
        {
            pluginInstance?.CleanUp();
            pluginInstance = null;
        }

        public void OnUpdate() { }
    }
}
