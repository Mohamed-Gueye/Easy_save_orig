using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model.IO
{
    /// <summary>
    /// Singleton g�n�rique pour la lecture/�criture de fichiers JSON.
    /// </summary>
    public class WriterManager
    {
        private static WriterManager? _instance;
        private static readonly object _lock = new();

        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static WriterManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new WriterManager();
                }
            }
        }

        private WriterManager() { }

        /// <summary>
        /// �crit un objet g�n�rique dans un fichier JSON.
        /// </summary>
        public void WriteJson<T>(T data, string path)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, _options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur d'�criture JSON : {ex.Message}");
            }
        }

        /// <summary>
        /// Charge un objet JSON g�n�rique depuis un fichier.
        /// </summary>
        public T? LoadJson<T>(string path)
        {
            try
            {
                if (!File.Exists(path)) return default;
                string content = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur de lecture JSON : {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// �crit le fichier de configuration.
        /// </summary>
        public void WriteLogConfig(Configuration config)
        {
            WriteJson(config, "config.json");
        }
    }
}
