using System;
using System.Diagnostics;
using System.Linq;

namespace Easy_Save.Model
{
    public class ProcessMonitor
    {
        public static bool IsProcessRunning(string processName)
        // In: processName (string)
        // Out: bool 
        // Description: Checks whether the specified process is currently running on the system.
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);

            try
            {
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