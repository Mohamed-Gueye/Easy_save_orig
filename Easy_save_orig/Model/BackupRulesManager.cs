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

        public List<string> PriorityExtensions { get; set; } = new List<string>(); // ✅ Ajout

        private static BackupRulesManager? _instance;
        private static readonly object _lockObject = new object();

        public static BackupRulesManager Instance
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

                if (doc.RootElement.TryGetProperty("BusinessSettings", out JsonElement businessElement))
                {
                    var settings = new BackupRulesManager();

                    if (businessElement.TryGetProperty("MaxFileSize", out JsonElement maxFileSize))
                        settings.MaxFileSize = maxFileSize.GetInt64();

                    if (businessElement.TryGetProperty("RestrictedExtensions", out JsonElement extensions))
                    {
                        settings.RestrictedExtensions = extensions.EnumerateArray()
                            .Select(e => e.GetString() ?? "").Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                    }

                    if (businessElement.TryGetProperty("BusinessSoftwareList", out JsonElement softwareList))
                    {
                        settings.BusinessSoftwareList = softwareList.EnumerateArray()
                            .Select(e => e.GetString() ?? "").Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                    }
                    else if (businessElement.TryGetProperty("BusinessSoftware", out JsonElement softwareElement))
                    {
                        string? software = softwareElement.GetString();
                        if (!string.IsNullOrWhiteSpace(software))
                        {
                            settings.BusinessSoftwareList.Add(software);
                        }
                    }

                    if (businessElement.TryGetProperty("CryptoSoftPath", out JsonElement cryptoPath))
                        settings.CryptoSoftPath = cryptoPath.GetString() ?? "";

                    // ✅ Lecture des extensions prioritaires
                    if (businessElement.TryGetProperty("PriorityExtensions", out JsonElement priorityExts))
                    {
                        settings.PriorityExtensions = priorityExts.EnumerateArray()
                            .Select(e => e.GetString() ?? "").Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading backup rules settings: {ex.Message}");
            }

            return new BackupRulesManager();
        }

        public bool IsAnyBusinessSoftwareRunning()
        {
            if (BusinessSoftwareList.Count == 0)
                return false;

            return BusinessSoftwareList.Any(software =>
                !string.IsNullOrWhiteSpace(software) && ProcessMonitor.IsProcessRunning(software));
        }

        public string? GetRunningBusinessSoftware()
        {
            return BusinessSoftwareList.FirstOrDefault(software =>
                !string.IsNullOrWhiteSpace(software) && ProcessMonitor.IsProcessRunning(software));
        }

        public bool AddBusinessSoftware(string softwareName)
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
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

                JsonDocument existingDoc;
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    existingDoc = JsonDocument.Parse(jsonContent);
                }
                else
                {
                    existingDoc = JsonDocument.Parse("{}");
                }

                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();

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

                        writer.WritePropertyName("BusinessSettings");
                        writer.WriteStartObject();

                        writer.WriteNumber("MaxFileSize", MaxFileSize);

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

                        // ✅ Écriture des extensions prioritaires
                        writer.WritePropertyName("PriorityExtensions");
                        writer.WriteStartArray();
                        foreach (var ext in PriorityExtensions)
                        {
                            writer.WriteStringValue(ext);
                        }
                        writer.WriteEndArray();

                        writer.WriteEndObject(); // Fin de BusinessSettings
                        writer.WriteEndObject(); // Fin de root
                    }

                    var json = Encoding.UTF8.GetString(stream.ToArray());
                    File.WriteAllText(configPath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving backup rules settings: {ex.Message}");
            }
        }

        public List<string> GetBusinessSoftwareList()
        {
            return new List<string>(BusinessSoftwareList);
        }
    }
}
