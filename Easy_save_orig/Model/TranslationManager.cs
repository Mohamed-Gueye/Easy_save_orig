using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model
{
    public class TranslationManager
    {
        private static TranslationManager? instance;
        private Dictionary<string, string> translations = new Dictionary<string, string>();
        private Dictionary<string, Dictionary<string, string>> allTranslations = new Dictionary<string, Dictionary<string, string>>();
        private string currentLanguage = "en";

        public event EventHandler? LanguageChanged;

        private TranslationManager()
        // Description: Private constructor that loads translations from file and sets language to English.
        {
            LoadTranslations();
            SetLanguage("en");
        }

        public static TranslationManager Instance
        // Out: TranslationManager
        // Description: Returns the singleton instance of the TranslationManager.
        {
            get
            {
                if (instance == null)
                {
                    instance = new TranslationManager();
                }
                return instance;
            }
        }

        private void LoadTranslations()
        // Out: void
        // Description: Loads translations from file only. Throws if file is missing.
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations.json");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Le fichier de traduction 'translations.json' est manquant !");
            }

            string jsonString = File.ReadAllText(filePath);
            allTranslations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonString)
                ?? new Dictionary<string, Dictionary<string, string>>();
        }

        public void LoadTranslation(string languageCode)
        // In: languageCode (string)
        // Out: void
        // Description: Public method to load translations depending on the language code.
        {
            SetLanguage(languageCode);
        }

        private void SetLanguage(string languageCode)
        // In: languageCode (string)
        // Out: void
        // Description: Sets the current language and triggers language changed event if found.
        {
            if (allTranslations.TryGetValue(languageCode, out var langTranslations))
            {
                translations = langTranslations;
                currentLanguage = languageCode;
                OnLanguageChanged();
            }
            else
            {
                throw new Exception($"La langue '{languageCode}' n'est pas disponible dans translations.json !");
            }
        }

        public string GetUITranslation(string key)
        // In: key (string)
        // Out: string
        // Description: Returns a translation string from the current language, or the key if not found.
        {
            if (translations.TryGetValue(key, out string? value))
            {
                return value;
            }
            return key;
        }

        public string GetFormattedUITranslation(string key, params object[] args)
        // In: key (string), args (object[])
        // Out: string
        // Description: Formats a translation string using provided arguments.
        {
            string formatString = GetUITranslation(key);
            return string.Format(formatString, args);
        }

        protected virtual void OnLanguageChanged()
        // Out: void
        // Description: Raises the LanguageChanged event to notify listeners.
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
} 