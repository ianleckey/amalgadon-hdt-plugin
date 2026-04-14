using Newtonsoft.Json;
using System;
using System.IO;

namespace AmalgadonPlugin
{
    public class PluginSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HearthstoneDeckTracker", "Plugins", "AmalgadonPlugin.json");

        private static PluginSettings _instance;
        public static PluginSettings Instance => _instance ?? (_instance = Load());

        public double ButtonLeft { get; set; } = 10;
        public double ButtonTop  { get; set; } = 50;

        public static PluginSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<PluginSettings>(File.ReadAllText(SettingsPath))
                           ?? new PluginSettings();
            }
            catch { }
            return new PluginSettings();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath,
                    JsonConvert.SerializeObject(Instance, Formatting.Indented));
            }
            catch { }
        }
    }
}
