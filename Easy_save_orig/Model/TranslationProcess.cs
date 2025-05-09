using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Controller
{
	public class TranslationProcess
	{
		private Dictionary<string, string> _translations = new();
		private string _selectedLanguage = "en";

		private readonly string _translationFilePath = Path.Combine(AppContext.BaseDirectory, "data", "translations.json");

		public void LoadTranslation(string languageCode)
		{
			try
			{
				if (!File.Exists(_translationFilePath))
				{
					Console.WriteLine("Fichier de traduction introuvable.");
					return;
				}

				string json = File.ReadAllText(_translationFilePath);
				var allLanguages = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

				if (allLanguages != null && allLanguages.ContainsKey(languageCode))
				{
					_selectedLanguage = languageCode;
					_translations = allLanguages[languageCode];
				}
				else
				{
					Console.WriteLine("Langue non disponible.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Erreur de chargement des traductions : {ex.Message}");
			}
		}

		public string GetTranslation(string key)
		{
			return _translations.ContainsKey(key) ? _translations[key] : $"[{key}]";
		}
	}
}
