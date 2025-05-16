using System;
using System.Diagnostics;
using System.Linq;

namespace Easy_Save.Model
{
    public class ProcessMonitor
    {
        /// <summary>
        /// Vérifie si un processus spécifique est en cours d'exécution
        /// </summary>
        /// <param name="processName">Nom du processus à vérifier (sans l'extension .exe)</param>
        /// <returns>True si le processus est en cours d'exécution, False sinon</returns>
        public static bool IsProcessRunning(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            // Supprime l'extension .exe si elle est présente
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);

            try
            {
                // Récupère tous les processus en cours d'exécution avec ce nom
                var processes = Process.GetProcessesByName(processName);
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification du processus '{processName}': {ex.Message}");
                return false;
            }
        }
    }
} 