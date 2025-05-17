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
        // Description: Private constructor that loads default translations and sets language to English.
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
        // Description: Loads translations from file or creates default ones if file doesn't exist.
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "translations.json");
            
            if (!File.Exists(filePath))
            {
                CreateDefaultTranslationFile(filePath);
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                allTranslations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonString) 
                    ?? new Dictionary<string, Dictionary<string, string>>();
                
                if (!allTranslations.ContainsKey("en"))
                {
                    allTranslations["en"] = CreateDefaultTranslations("en");
                }
                
                if (!allTranslations.ContainsKey("fr"))
                {
                    allTranslations["fr"] = CreateDefaultTranslations("fr");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading translations: {ex.Message}");
                allTranslations = new Dictionary<string, Dictionary<string, string>>
                {
                    ["en"] = CreateDefaultTranslations("en"),
                    ["fr"] = CreateDefaultTranslations("fr")
                };
            }
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
                Console.WriteLine($"Language {languageCode} not found, defaulting to English");
                
                if (allTranslations.TryGetValue("en", out var defaultTranslations))
                {
                    translations = defaultTranslations;
                    currentLanguage = "en";
                    
                    OnLanguageChanged();
                }
            }
        }

        private void CreateDefaultTranslationFile(string filePath)
        // In: filePath (string)
        // Out: void
        // Description: Creates a translation file with default French and English translations.
        {
            try
            {
                var defaultTranslations = new Dictionary<string, Dictionary<string, string>>
                {
                    ["en"] = CreateDefaultTranslations("en"),
                    ["fr"] = CreateDefaultTranslations("fr")
                };
                
                string jsonString = JsonSerializer.Serialize(defaultTranslations, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
                File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default translation file: {ex.Message}");
            }
        }

        private Dictionary<string, string> CreateDefaultTranslations(string languageCode)
        // In: languageCode (string)
        // Out: Dictionary<string, string>
        // Description: Returns a dictionary of default translations for the given language code.
        {
            var defaultTranslations = new Dictionary<string, string>();
            
            if (languageCode == "fr")
            {
                defaultTranslations["window.title"] = "EasySave";
                defaultTranslations["menu.create"] = "Créer";
                defaultTranslations["menu.execute"] = "Exécuter";
                defaultTranslations["menu.execute_all"] = "Exécuter tout";
                defaultTranslations["menu.delete"] = "Supprimer";
                defaultTranslations["menu.quit"] = "Quitter";
                defaultTranslations["menu.language"] = "Langue";
                defaultTranslations["menu.execution_mode"] = "Mode d'exécution";
                defaultTranslations["execution.sequential"] = "Séquentiel";
                defaultTranslations["execution.concurrent"] = "Concurrent";
                defaultTranslations["list.title"] = "Liste des sauvegardes";
                defaultTranslations["list.empty"] = "Aucune sauvegarde n'est définie. Cliquez sur 'Créer' pour ajouter une nouvelle sauvegarde.";
                defaultTranslations["backup.name"] = "Nom de la sauvegarde";
                defaultTranslations["backup.name_label"] = "Nom:";
                defaultTranslations["backup.source"] = "Chemin du dossier source";
                defaultTranslations["backup.target"] = "Chemin du dossier de destination";
                defaultTranslations["backup.type"] = "Type";
                defaultTranslations["backup.type_full"] = "COMPLÈTE";
                defaultTranslations["backup.type_differential"] = "DIFFÉRENTIELLE";
                defaultTranslations["backup.success"] = "Sauvegarde '{0}' créée avec succès";
                defaultTranslations["backup.complete"] = "Sauvegarde '{0}' terminée avec succès";
                defaultTranslations["backup.all.complete"] = "Toutes les sauvegardes ont été exécutées avec succès";
                defaultTranslations["backup.delete_confirm"] = "Êtes-vous sûr de vouloir supprimer la sauvegarde '{0}' ?";
                defaultTranslations["backup.no_saves"] = "Aucune sauvegarde n'est définie.";
                defaultTranslations["backup.start"] = "Exécution de '{0}'...";
                defaultTranslations["progress.title"] = "Sauvegarde en cours";
                defaultTranslations["progress.info"] = "Traitement en cours...";
                defaultTranslations["progress.cancel"] = "Annuler";
                defaultTranslations["progress.files"] = "Fichiers traités: {0} / {1}";
                defaultTranslations["error.name_required"] = "Le nom de la sauvegarde est requis";
                defaultTranslations["error.source_required"] = "Le chemin source est requis";
                defaultTranslations["error.target_required"] = "Le chemin de destination est requis";
                defaultTranslations["error.execution"] = "Erreur lors de l'exécution de la sauvegarde '{0}': {1}";
                defaultTranslations["error.delete"] = "Erreur lors de la suppression: {0}";
                defaultTranslations["error.create"] = "Erreur lors de la création: {0}";
            }
            else
            {
                defaultTranslations["window.title"] = "EasySave";
                defaultTranslations["menu.create"] = "Create";
                defaultTranslations["menu.execute"] = "Execute";
                defaultTranslations["menu.execute_all"] = "Execute All";
                defaultTranslations["menu.delete"] = "Delete";
                defaultTranslations["menu.quit"] = "Quit";
                defaultTranslations["menu.language"] = "Language";
                defaultTranslations["menu.execution_mode"] = "Execution Mode";
                defaultTranslations["execution.sequential"] = "Sequential";
                defaultTranslations["execution.concurrent"] = "Concurrent";
                defaultTranslations["list.title"] = "Backup List";
                defaultTranslations["list.empty"] = "No backups are defined. Click 'Create' to add a new backup.";
                defaultTranslations["backup.name"] = "Backup Name";
                defaultTranslations["backup.name_label"] = "Name:";
                defaultTranslations["backup.source"] = "Source Folder Path";
                defaultTranslations["backup.target"] = "Destination Folder Path";
                defaultTranslations["backup.type"] = "Type";
                defaultTranslations["backup.type_full"] = "FULL";
                defaultTranslations["backup.type_differential"] = "DIFFERENTIAL";
                defaultTranslations["backup.success"] = "Backup '{0}' created successfully";
                defaultTranslations["backup.complete"] = "Backup '{0}' completed successfully";
                defaultTranslations["backup.all.complete"] = "All backups have been executed successfully";
                defaultTranslations["backup.delete_confirm"] = "Are you sure you want to delete the backup '{0}'?";
                defaultTranslations["backup.no_saves"] = "No backups are defined.";
                defaultTranslations["backup.start"] = "Executing '{0}'...";
                defaultTranslations["progress.title"] = "Backup in Progress";
                defaultTranslations["progress.info"] = "Processing...";
                defaultTranslations["progress.cancel"] = "Cancel";
                defaultTranslations["progress.files"] = "Files processed: {0} / {1}";
                defaultTranslations["error.name_required"] = "Backup name is required";
                defaultTranslations["error.source_required"] = "Source path is required";
                defaultTranslations["error.target_required"] = "Destination path is required";
                defaultTranslations["error.execution"] = "Error executing backup '{0}': {1}";
                defaultTranslations["error.delete"] = "Error deleting: {0}";
                defaultTranslations["error.create"] = "Error creating: {0}";
            }
            
            return defaultTranslations;
        }

        public string GetUITranslation(string key)
        // In: key (string)
        // Out: string
        // Description: Returns a translation string from the current language.
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