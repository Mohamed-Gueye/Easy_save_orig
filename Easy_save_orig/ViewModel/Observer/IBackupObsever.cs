namespace Easy_Save.Model.Observer
{
    /// <summary>
    /// Interface d'observation d'une sauvegarde.
    /// </summary>
    public interface IBackupObserver
    {
        void Update(Backup backup);
    }
}
