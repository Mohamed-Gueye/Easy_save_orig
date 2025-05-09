using System;
using Easy_Save.Model;

namespace Easy_Save.Interfaces;

public interface IBackupStrategy
{
    void MakeBackup(Backup backup);
}