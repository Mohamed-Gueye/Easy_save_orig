using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Controller
{
    public class TranslationProcess
    {
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private string currentLanguage = "en";

        public TranslationProcess()
        {
            LoadTranslation("en");
        }

        public void LoadTranslation(string languageCode)
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"translations_{languageCode}.json");
            
            if (!File.Exists(filePath))
            {
                CreateDefaultTranslation(languageCode, filePath);
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                translations = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? new Dictionary<string, string>();
                currentLanguage = languageCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading translation: {ex.Message}");
                LoadDefaultTranslations(languageCode);
            }
        }

        private void CreateDefaultTranslation(string languageCode, string filePath)
        {
            try
            {
                LoadDefaultTranslations(languageCode);
                string jsonString = JsonSerializer.Serialize(translations, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default translation file: {ex.Message}");
            }
        }

        private void LoadDefaultTranslations(string languageCode)
        {
            translations.Clear();
            
            if (languageCode == "fr")
            {
                // French console app translations
                translations["menu.title"] = "Menu EasySave";
                translations["menu.create"] = "1. Créer une nouvelle sauvegarde";
                translations["menu.execute"] = "2. Exécuter une sauvegarde";
                translations["menu.execute_all"] = "3. Exécuter toutes les sauvegardes";
                translations["menu.delete"] = "4. Supprimer une sauvegarde";
                translations["menu.language"] = "5. Changer de langue (fr/en)";
                translations["menu.exit"] = "6. Quitter";
                translations["prompt.choice"] = "Votre choix: ";
                // Add more translations as needed
            }
            else
            {
                // English console app translations (default)
                translations["menu.title"] = "EasySave Menu";
                translations["menu.create"] = "1. Create new backup";
                translations["menu.execute"] = "2. Execute a backup";
                translations["menu.execute_all"] = "3. Execute all backups";
                translations["menu.delete"] = "4. Delete a backup";
                translations["menu.language"] = "5. Change language (fr/en)";
                translations["menu.exit"] = "6. Exit";
                translations["prompt.choice"] = "Your choice: ";
                // Add more translations as needed
            }
        }

        public string GetTranslation(string key)
        {
            if (translations.TryGetValue(key, out string? value))
            {
                return value;
            }
            
            return key;
        }

        public string GetCurrentLanguage()
        {
            return currentLanguage;
        }
    }
} 