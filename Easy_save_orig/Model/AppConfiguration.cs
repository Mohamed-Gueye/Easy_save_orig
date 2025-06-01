using System;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model
{
    // Description: Manages application configuration and directory paths using a thread-safe Singleton pattern.
    //              Supports persistent config file loading and fallback handling on error.
    public class AppConfiguration
    {
        // Publicly configured paths
        public string LogFilePath { get; set; }
        public string StatusFilePath { get; set; }
        public string SaveListFilePath { get; set; }

        // Internally resolved application paths
        public string AppDataPath { get; private set; } = string.Empty;
        public string BackupConfigPath { get; private set; } = string.Empty;
        public string LogPath { get; private set; } = string.Empty;
        public string StatePath { get; private set; } = string.Empty;

        private static readonly string configFileName = "config.json";
        private static readonly string defaultBasePath = Path.Combine(AppContext.BaseDirectory, "data");

        private static AppConfiguration? _instance;
        private static readonly object _lockObject = new object();

        public static AppConfiguration Instance
        // In: none
        // Out: AppConfiguration
        // Description: Provides singleton access to the configuration instance (thread-safe).
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        public static AppConfiguration LoadConfig()
        // In: none
        // Out: AppConfiguration
        // Description: Loads configuration from file or creates default config with fallback directory creation.
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, configFileName);

            if (!File.Exists(configPath))
            {
                var defaultConfig = new AppConfiguration
                {
                    LogFilePath = Path.Combine(defaultBasePath, "logs"),
                    StatusFilePath = Path.Combine(defaultBasePath, "state.json"),
                    SaveListFilePath = Path.Combine(defaultBasePath, "saves.json")
                };

                Directory.CreateDirectory(defaultBasePath);
                File.WriteAllText(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));

                defaultConfig.SetupDirectoryPaths();
                return defaultConfig;
            }

            string json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json) ?? new AppConfiguration();
            config.SetupDirectoryPaths();
            return config;
        }

        public void SaveConfig()
        // In: none
        // Out: void
        // Description: Saves the current configuration to disk in config.json format.
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, configFileName);
            File.WriteAllText(configPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        public void SetupDirectoryPaths()
        // In: none
        // Out: void
        // Description: Initializes application-specific directories, with fallback handling on failure.
        {
            try
            {
                AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave");

                if (!Directory.Exists(AppDataPath))
                {
                    Directory.CreateDirectory(AppDataPath);
                }

                BackupConfigPath = Path.Combine(AppDataPath, "backups");
                LogPath = Path.Combine(AppDataPath, "logs");
                StatePath = Path.Combine(AppDataPath, "states");

                EnsureDirectoryExists(BackupConfigPath);
                EnsureDirectoryExists(LogPath);
                EnsureDirectoryExists(StatePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in configuration setup: {ex.Message}");

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                AppDataPath = baseDir;
                BackupConfigPath = Path.Combine(baseDir, "backups");
                LogPath = Path.Combine(baseDir, "logs");
                StatePath = Path.Combine(baseDir, "states");

                try
                {
                    EnsureDirectoryExists(BackupConfigPath);
                    EnsureDirectoryExists(LogPath);
                    EnsureDirectoryExists(StatePath);
                }
                catch
                {
                    Console.WriteLine("Using current directory for all application data");
                    AppDataPath = Directory.GetCurrentDirectory();
                    BackupConfigPath = AppDataPath;
                    LogPath = AppDataPath;
                    StatePath = AppDataPath;
                }
            }
        }

        private static void EnsureDirectoryExists(string path)
        // In: path (string)
        // Out: void
        // Description: Creates the directory if it does not exist.
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
