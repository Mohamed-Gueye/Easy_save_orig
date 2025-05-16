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
        {
            try
            {
                // Set up base directory for application data
                AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasySave");
                
                // Ensure the application directory exists
                if (!Directory.Exists(AppDataPath))
                {
                    Directory.CreateDirectory(AppDataPath);
                }
                
                // Set up paths for various application files
                BackupConfigPath = Path.Combine(AppDataPath, "backups");
                LogPath = Path.Combine(AppDataPath, "logs");
                StatePath = Path.Combine(AppDataPath, "states");
                
                // Ensure all subdirectories exist
                EnsureDirectoryExists(BackupConfigPath);
                EnsureDirectoryExists(LogPath);
                EnsureDirectoryExists(StatePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in configuration setup: {ex.Message}");
                
                // Fallback to application directory if there's an error
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                AppDataPath = baseDir;
                BackupConfigPath = Path.Combine(baseDir, "backups");
                LogPath = Path.Combine(baseDir, "logs");
                StatePath = Path.Combine(baseDir, "states");
                
                // Try to ensure directories in fallback location
                try
                {
                    EnsureDirectoryExists(BackupConfigPath);
                    EnsureDirectoryExists(LogPath);
                    EnsureDirectoryExists(StatePath);
                }
                catch
                {
                    // Last resort: just use the current directory for everything
                    Console.WriteLine("Using current directory for all application data");
                    AppDataPath = Directory.GetCurrentDirectory();
                    BackupConfigPath = AppDataPath;
                    LogPath = AppDataPath;
                    StatePath = AppDataPath;
                }
            }
        }
        
        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
} 