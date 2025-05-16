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
        // Propriétés pour la taille maximale de fichier
        public long MaxFileSize { get; set; } = 0;
        
        // Liste des extensions de fichiers à traiter différemment
        public List<string> RestrictedExtensions { get; set; } = new List<string>();
        
        // Liste des logiciels métier qui bloquent l'exécution des sauvegardes quand ils sont actifs
        public List<string> BusinessSoftwareList { get; set; } = new List<string>();
        
        // Chemin vers l'outil CryptoSoft
        public string CryptoSoftPath { get; set; } = "";

        private static BusinessSettings? _instance;
        private static readonly object _lockObject = new object();

        public static BusinessSettings Instance
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

        /// <summary>
        /// Charge les paramètres métier depuis le fichier de configuration
        /// </summary>
        public static BusinessSettings Load()
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

                    // Chargement de la liste des logiciels métier
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
                    // Compatibilité avec l'ancienne version qui utilisait une seule chaîne
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

        /// <summary>
        /// Vérifie si un des logiciels métier est en cours d'exécution
        /// </summary>
        public bool IsAnyBusinessSoftwareRunning()
        {
            // Si la liste est vide, aucun logiciel ne peut être en cours d'exécution
            if (BusinessSoftwareList.Count == 0)
                return false;
            
            // Vérifie si au moins un des logiciels métier est en cours d'exécution
            return BusinessSoftwareList.Any(software => 
                !string.IsNullOrWhiteSpace(software) && ProcessMonitor.IsProcessRunning(software));
        }
        
        /// <summary>
        /// Retourne le nom du premier logiciel métier en cours d'exécution, ou null si aucun n'est en cours d'exécution
        /// </summary>
        public string? GetRunningBusinessSoftware()
        {
            return BusinessSoftwareList.FirstOrDefault(software => 
                !string.IsNullOrWhiteSpace(software) && ProcessMonitor.IsProcessRunning(software));
        }
        
        /// <summary>
        /// Ajoute un logiciel métier à la liste s'il n'est pas déjà présent
        /// </summary>
        public bool AddBusinessSoftware(string softwareName)
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;
                
            // Normalisation du nom (suppression des espaces en début/fin)
            softwareName = softwareName.Trim();
            
            // Vérifier si le logiciel n'est pas déjà dans la liste (insensible à la casse)
            if (BusinessSoftwareList.Any(s => s.Equals(softwareName, StringComparison.OrdinalIgnoreCase)))
                return false;
                
            BusinessSoftwareList.Add(softwareName);
            Save();
            return true;
        }
        
        /// <summary>
        /// Supprime un logiciel métier de la liste
        /// </summary>
        public bool RemoveBusinessSoftware(string softwareName)
        {
            if (string.IsNullOrWhiteSpace(softwareName))
                return false;
                
            // Trouver le logiciel à supprimer (insensible à la casse)
            string? softwareToRemove = BusinessSoftwareList.FirstOrDefault(
                s => s.Equals(softwareName, StringComparison.OrdinalIgnoreCase));
                
            if (softwareToRemove == null)
                return false;
                
            BusinessSoftwareList.Remove(softwareToRemove);
            Save();
            return true;
        }

        /// <summary>
        /// Sauvegarde les paramètres métier dans le fichier de configuration
        /// </summary>
        public void Save()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                
                // Si le fichier n'existe pas, créez un nouvel objet JSON
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

                // Créer un nouvel objet JSON avec les paramètres mis à jour
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();

                        // Copier toutes les propriétés existantes sauf BusinessSettings
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

                        // Écrire les paramètres métier mis à jour
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
                        
                        // Écrire la liste des logiciels métier
                        writer.WritePropertyName("BusinessSoftwareList");
                        writer.WriteStartArray();
                        foreach (var software in BusinessSoftwareList)
                        {
                            writer.WriteStringValue(software);
                        }
                        writer.WriteEndArray();
                        
                        writer.WriteString("CryptoSoftPath", CryptoSoftPath);
                        
                        writer.WriteEndObject(); // BusinessSettings
                        
                        writer.WriteEndObject(); // Root
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

        /// <summary>
        /// Retourne la liste des logiciels métier
        /// </summary>
        public List<string> GetBusinessSoftwareList()
        {
            return new List<string>(BusinessSoftwareList);
        }
    }
} 