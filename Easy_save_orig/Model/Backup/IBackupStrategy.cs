using Easy_Save.Model;
using Easy_Save.Model.IO;
using Easy_Save.Model.Observer;
using System.Threading;

namespace Easy_Save.Interfaces;

public interface IBackupStrategy
{
    void MakeBackup(Backup backup, StatusManager statusManager, LogObserver logObserver, CancellationToken cancellationToken = default);
}