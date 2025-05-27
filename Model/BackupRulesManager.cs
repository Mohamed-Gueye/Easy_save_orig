using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Linq;

namespace Easy_Save.Model
{
    public class BackupRulesManager
    {
        public long MaxFileSize { get; set; } = 0;
        public List<string> RestrictedExtensions { get; set; } = new List<string>();
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        public string CryptoSoftPath { get; set; } = "";
        public List<string> PriorityExtensions { get; set; } = new List<string>();
        public long LargeFileSizeThresholdKB { get; set; } = 1024;

        private static BackupRulesManager? _instance;
        private static readonly object _lockObject = new object();

        public static BackupRulesManager Instance
        // Out: BackupRulesManager
        // Description: Singleton instance accessor with lazy loading and thread safety.
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        public static BackupRulesManager Load()
        // Out: BackupRulesManager
        // Description: Loads business settings from the config.json file or returns defaults.
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                {
                    return new BackupRulesManager();
                }

                string jsonContent = File.ReadAllText(configPath);
                JsonDocument doc = JsonDocument.Parse(jsonContent);

                var settings = new BackupRulesManager(); if (doc.RootElement.TryGetProperty("BusinessSettings", out JsonElement businessElement))
                {
                    if (businessElement.TryGetProperty("MaxFileSize", out JsonElement maxSizeElement))
                    {
                        settings.MaxFileSize = maxSizeElement.GetInt64();
                    }

                    if (businessElement.TryGetProperty("LargeFileSizeThresholdKB", out JsonElement thresholdElement))
                    {
                        settings.LargeFileSizeThresholdKB = thresholdElement.GetInt64();
                    }

                    if (businessElement.TryGetProperty("RestrictedExtensions", out JsonElement restrictedElement))
                    {
                        settings.RestrictedExtensions = new List<string>();
                        foreach (var ext in restrictedElement.EnumerateArray())
                        {
                            settings.RestrictedExtensions.Add(ext.GetString() ?? "");
                        }
                    }

                    if (businessElement.TryGetProperty("BusinessSoftwareList", out JsonElement softwareElement))
                    {
                        settings.BusinessSoftwareList = new List<string>();
                        foreach (var software in softwareElement.EnumerateArray())
                        {
                            settings.BusinessSoftwareList.Add(software.GetString() ?? "");
                        }
                    }

                    if (businessElement.TryGetProperty("CryptoSoftPath", out JsonElement pathElement))
                    {
                        settings.CryptoSoftPath = pathElement.GetString() ?? "";
                    }

                    if (businessElement.TryGetProperty("PriorityExtensions", out JsonElement priorityElement))
                    {
                        settings.PriorityExtensions = new List<string>();
                        foreach (var ext in priorityElement.EnumerateArray())
                        {
                            settings.PriorityExtensions.Add(ext.GetString() ?? "");
                        }
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading backup rules: {ex.Message}");
                return new BackupRulesManager();
            }
        }

        public bool IsAnyBusinessSoftwareRunning()
        // Out: bool
        // Description: Checks if any defined software package is currently running.
        {
            if (BusinessSoftwareList.Count == 0)
                return false;

            return BusinessSoftwareList.Any(software =>
                !string.IsNullOrWhiteSpace(software) && ProcessMonitor.IsProcessRunning(software));
        }

        public string? GetRunningBusinessSoftware()
        // Out: string? 
        // Description: Returns the name of the first running software package found.
        {
            return BusinessSoftwareList.FirstOrDefault(software =>
                !string.IsNullOrWhiteSpace(software) && ProcessMonitor.IsProcessRunning(software));
        }


        public bool AddBusinessSoftware(string softwareName)
        // In: softwareName (string)
        // Out: bool
        // Description: Adds a software to the business list if not already present.
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;

            softwareName = softwareName.Trim();

            if (BusinessSoftwareList.Any(s => s.Equals(softwareName, StringComparison.OrdinalIgnoreCase)))
                return false;

            BusinessSoftwareList.Add(softwareName);
            Save();
            return true;
        }


        public bool RemoveBusinessSoftware(string softwareName)
        // In: softwareName (string)
        // Out: bool
        // Description: Removes a software from the business list if it exists.
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;

            string? softwareToRemove = BusinessSoftwareList.FirstOrDefault(
                s => s.Equals(softwareName, StringComparison.OrdinalIgnoreCase));

            if (softwareToRemove == null)
                return false;

            BusinessSoftwareList.Remove(softwareToRemove);
            Save();
            return true;
        }

        public void Save()
        // Out: void
        // Description: Saves the current backup rules settings to config.json.
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                JsonDocument? existingDoc = null;

                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    existingDoc = JsonDocument.Parse(jsonContent);
                }

                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();

                        if (existingDoc != null)
                        {
                            using (JsonDocument doc = existingDoc)
                            {
                                foreach (var property in doc.RootElement.EnumerateObject())
                                {
                                    if (property.Name != "BusinessSettings")
                                    {
                                        property.WriteTo(writer);
                                    }
                                }
                            }
                        }

                        writer.WritePropertyName("BusinessSettings");
                        writer.WriteStartObject();
                        writer.WriteNumber("MaxFileSize", MaxFileSize);
                        writer.WriteNumber("LargeFileSizeThresholdKB", LargeFileSizeThresholdKB);

                        writer.WritePropertyName("RestrictedExtensions");
                        writer.WriteStartArray();
                        foreach (var ext in RestrictedExtensions)
                        {
                            writer.WriteStringValue(ext);
                        }
                        writer.WriteEndArray();

                        writer.WritePropertyName("BusinessSoftwareList");
                        writer.WriteStartArray();
                        foreach (var software in BusinessSoftwareList)
                        {
                            writer.WriteStringValue(software);
                        }
                        writer.WriteEndArray();
                        writer.WriteString("CryptoSoftPath", CryptoSoftPath);

                        writer.WritePropertyName("PriorityExtensions");
                        writer.WriteStartArray();
                        foreach (var ext in PriorityExtensions)
                        {
                            writer.WriteStringValue(ext);
                        }
                        writer.WriteEndArray();

                        writer.WriteEndObject();
                        writer.WriteEndObject();
                    }

                    var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                    File.WriteAllText(configPath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving backup rules: {ex.Message}");
            }
        }

        public List<string> GetBusinessSoftwareList()
        // Out: List<string>
        // Description: Returns a copy of the list of software packages.
        {
            return new List<string>(BusinessSoftwareList);
        }
    }
}