using System;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model
{
    public class Configuration
    {
        public string LogFilePath { get; set; }
        public string StatusFilePath { get; set; }
        public string SaveListFilePath { get; set; }

        private static readonly string configFileName = "config.json";
        private static readonly string defaultBasePath = Path.Combine(AppContext.BaseDirectory, "data");

        public static Configuration LoadConfig()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, configFileName);

            if (!File.Exists(configPath))
            {
                var defaultConfig = new Configuration
                {
                    LogFilePath = Path.Combine(defaultBasePath, "logs"),
                    StatusFilePath = Path.Combine(defaultBasePath, "state.json"),
                    SaveListFilePath = Path.Combine(defaultBasePath, "saves.json")
                };

                Directory.CreateDirectory(defaultBasePath);
                File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));

                return defaultConfig;
            }

            string json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<Configuration>(json) ?? throw new Exception("Configuration file is invalid.");
        }

        public void BackupConfig()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, configFileName);
            File.WriteAllText(configPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
