using System;
using System.IO;

namespace Easy_Save.Model
{
    public static class CustomConfiguration
    {
        public static string AppDataPath { get; private set; } = string.Empty;
        public static string BackupConfigPath { get; private set; } = string.Empty;
        public static string LogPath { get; private set; } = string.Empty;
        public static string StatePath { get; private set; } = string.Empty;
        
        public static void SetupConfiguration()
        // Out: void
        // Description: Initializes and ensures the directory structure for application data.
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
        // Description: Ensures a directory exists at the specified path, creating it if necessary.
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
} 