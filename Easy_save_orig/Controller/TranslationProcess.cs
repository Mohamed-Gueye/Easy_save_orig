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
        // Description: Initializes with English translation by default.
        {
            LoadTranslation("en");
        }

        public void LoadTranslation(string languageCode)
        // In: languageCode (string)
        // Out: void
        // Description: Loads translation from the JSON file based on the provided language code.
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
        // In: languageCode (string), filePath (string)
        // Out: void
        // Description: Creates a translation file if none exists.
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
        // In: languageCode (string)
        // Out: void
        // Description: Loads default translations for usage in the application.
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
            }
            else
            {
                // English console app translations (default setting)
                translations["menu.title"] = "EasySave Menu";
                translations["menu.create"] = "1. Create new backup";
                translations["menu.execute"] = "2. Execute a backup";
                translations["menu.execute_all"] = "3. Execute all backups";
                translations["menu.delete"] = "4. Delete a backup";
                translations["menu.language"] = "5. Change language (fr/en)";
                translations["menu.exit"] = "6. Exit";
                translations["prompt.choice"] = "Your choice: ";
            }
        }

        public string GetTranslation(string key)
        // In: key (string)
        // Out: string (translation or fallback key)
        // Description: Returns the translation string for a given key.
        {
            if (translations.TryGetValue(key, out string? value))
            {
                return value;
            }
            
            return key;
        }

        public string GetCurrentLanguage()
        // Out: string (current language code)
        // Description: Returns the code of the currently loaded language.
        {
            return currentLanguage;
        }
    }
} 