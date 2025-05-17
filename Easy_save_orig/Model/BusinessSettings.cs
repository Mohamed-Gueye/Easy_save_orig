using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Linq;

namespace Easy_Save.Model
{
    public class BusinessSettings
    {
        public long MaxFileSize { get; set; } = 0;
        
        public List<string> RestrictedExtensions { get; set; } = new List<string>();
        
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        
        public string CryptoSoftPath { get; set; } = "";

        private static BusinessSettings? _instance;
        private static readonly object _lockObject = new object();

        public static BusinessSettings Instance
        // Out: BusinessSettings
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

        public static BusinessSettings Load()
        // Out: BusinessSettings
        // Description: Loads business settings from the config.json file or returns defaults.
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                {
                    return new BusinessSettings();
                }

                string jsonContent = File.ReadAllText(configPath);
                JsonDocument doc = JsonDocument.Parse(jsonContent);

                if (doc.RootElement.TryGetProperty("BusinessSettings", out JsonElement businessElement))
                {
                    var settings = new BusinessSettings();

                    if (businessElement.TryGetProperty("MaxFileSize", out JsonElement maxFileSize))
                        settings.MaxFileSize = maxFileSize.GetInt64();

                    if (businessElement.TryGetProperty("RestrictedExtensions", out JsonElement extensions))
                    {
                        settings.RestrictedExtensions = new List<string>();
                        foreach (var ext in extensions.EnumerateArray())
                        {
                            settings.RestrictedExtensions.Add(ext.GetString() ?? "");
                        }
                    }

                    if (businessElement.TryGetProperty("BusinessSoftwareList", out JsonElement softwareList))
                    {
                        settings.BusinessSoftwareList = new List<string>();
                        foreach (var software in softwareList.EnumerateArray())
                        {
                            string? softwareName = software.GetString();
                            if (!string.IsNullOrWhiteSpace(softwareName))
                            {
                                settings.BusinessSoftwareList.Add(softwareName);
                            }
                        }
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

                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement des paramètres métier : {ex.Message}");
            }

            return new BusinessSettings();
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
        // Description: Saves the current business settings to config.json.
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
                        
                        writer.WriteEndObject(); 
                        
                        writer.WriteEndObject(); 
                    }

                    var json = Encoding.UTF8.GetString(stream.ToArray());
                    File.WriteAllText(configPath, json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde des paramètres métier : {ex.Message}");
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