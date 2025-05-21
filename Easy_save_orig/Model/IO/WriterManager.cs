using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Easy_Save.Model.IO
{
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

        public void WriteJson<T>(T data, string path)
        // In: data (T), path (string)
        // Out: void
        // Description: Serializes data to JSON and writes it to the specified file path.
        {
            try
            {
                string json = JsonSerializer.Serialize(data, _options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur d'écriture JSON : {ex.Message}");
            }
        }

        public T? LoadJson<T>(string path)
        // In: path (string)
        // Out: T 
        // Description: Reads JSON from file and deserializes it into the specified type.
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

        public void WriteLogConfig(AppConfiguration config)
        // In: config (AppConfiguration)
        // Out: void
        // Description: Writes the configuration to the default config file.
        {
            WriteJson(config, "config.json");
        }
    }
}
