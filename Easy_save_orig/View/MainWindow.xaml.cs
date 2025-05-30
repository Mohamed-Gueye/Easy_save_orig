using System;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Easy_Save.Controller;
using Easy_Save.Model;
using Easy_Save.Model.Status;
using System.Text.Json;
using System.Windows.Threading;
using Easy_Save.Network;

namespace Easy_Save.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Pour garder une trace de la sauvegarde sélectionnée
        private Border? selectedBackupItem = null;

        // Contrôleur pour gérer les sauvegardes
        private readonly BackupProcess backupProcess;

        // Configuration personnalisée
        private readonly string configPath;

        // Gestionnaire de traduction
        private readonly TranslationManager translationManager;

        // Gestionnaire de progression des sauvegardes
        private readonly BackupProgressTracker progressTracker;        // Jeton d'annulation pour les opérations de sauvegarde
        private CancellationTokenSource? cancellationTokenSource;

        // Nom de la sauvegarde actuellement en cours d'exécution
        private string? currentRunningBackup = null;

        private RemoteServer remoteServer;
        private bool broadcastingEnabled = false;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize required fields
            backupProcess = new BackupProcess();

            // Use the current directory for config path instead of a hardcoded path
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

            translationManager = TranslationManager.Instance;

            // Initialiser le gestionnaire de progression avec le nouveau constructeur
            progressTracker = new BackupProgressTracker(backupProcess);

            try
            {
                // Configurer les chemins des fichiers
                Console.WriteLine("Setting up configuration...");
                AppConfiguration.Instance.SetupDirectoryPaths();

                // Ensure config.json exists in the output directory
                EnsureConfigFileExists();

                if (File.Exists(configPath))
                {
                    Console.WriteLine($"Configuration file found at: {configPath}");
                }
                else
                {
                    Console.WriteLine($"Warning: Configuration file not found at: {configPath}");
                }

                // Abonnement aux événements de changement de langue
                translationManager.LanguageChanged += TranslationManager_LanguageChanged;

                // S'abonner aux événements du BackupManager pour les notifications de pause/reprise
                backupProcess.BackupManager.BackupPaused += BackupManager_BackupPaused;
                backupProcess.BackupManager.BackupResumed += BackupManager_BackupResumed;

                // Actualiser l'interface utilisateur
                UpdateUILanguage();
                RefreshBackupList();

                // Charger les paramètres du logiciel métier
                LoadBackupRules();

                // Initialiser la valeur de bande passante
                RefreshBandwidthThresholdValue();

                // Initialiser le serveur distant mais ne pas le démarrer
                remoteServer = new RemoteServer(backupProcess);
                broadcastingEnabled = false;
                btnToggleServer.Content = "Start Server";
                serverStatusIndicator.Fill = Brushes.Gray;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing application: {ex.Message}\n\nStack Trace: {ex.StackTrace}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleBroadcasting()
        {
            try
            {
                if (!broadcastingEnabled)
                {
                    if (remoteServer == null)
                    {
                        remoteServer = new RemoteServer(backupProcess);
                        remoteServer.Start();
                    }
                    else if (!remoteServer.IsRunning)
                    {
                        remoteServer.Start();
                    }
                    broadcastingEnabled = true;
                    btnToggleServer.Content = "Stop Server";
                    serverStatusIndicator.Fill = Brushes.Green;
                }
                else
                {
                    if (remoteServer != null)
                    {
                        remoteServer.Stop();
                    }
                    broadcastingEnabled = false;
                    btnToggleServer.Content = "Start Server";
                    serverStatusIndicator.Fill = Brushes.Gray;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling server: {ex.Message}");
            }
        }

        private void BtnToggleServer_Click(object sender, RoutedEventArgs e)
        {
            ToggleBroadcasting();
        }

        private void UpdateBackupProgressServer(string backupName, int percentage)
        {
            try
            {
                var progressBar = FindProgressBar(backupName);
                var percentageText = FindPercentageText(backupName);

                if (progressBar != null) progressBar.Value = percentage;
                if (percentageText != null) percentageText.Text = $"{percentage}%";

                // Broadcast to client
                if (broadcastingEnabled && remoteServer != null)
                {
                    remoteServer.BroadcastProgress(backupName, percentage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating progress for {backupName}: {ex.Message}");
            }
        }

        private void EnsureConfigFileExists()
        {
            // If config.json doesn't exist in the output directory, create it
            if (!File.Exists(configPath))
            {
                try
                {
                    var config = new
                    {
                        BackupSettings = new
                        {
                            LogDirectory = "logs",
                            BackupDirectory = "backups",
                            StateDirectory = "states",
                            StatusFilePath = "states/status.json",
                            LogFilePath = "logs/log.json",
                            SaveListFilePath = "backups/saves.json"
                        },
                        BusinessSettings = new
                        {
                            MaxFileSize = 0,
                            RestrictedExtensions = new string[] { },
                            BusinessSoftware = "",
                            CryptoSoftPath = ""
                        },
                        EncryptionSettings = new
                        {
                            EncryptionEnabled = false,
                            EncryptExtensions = new string[] { ".txt", ".doc", ".docx", ".pdf", ".xls", ".xlsx", ".ppt", ".pptx" },
                            EncryptionKey = "defaultkey"
                        }
                    };

                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configPath, json);
                    Console.WriteLine($"Created configuration file at: {configPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating config file: {ex.Message}");
                }
            }
        }

        private void TranslationManager_LanguageChanged(object sender, EventArgs e)
        {
            UpdateUILanguage();
        }

        private void UpdateUILanguage()
        {
            // Mettre à jour les textes de l'interface
            Title = translationManager.GetUITranslation("window.title");

            // Menu principal
            btnCreate.Content = translationManager.GetUITranslation("menu.create");
            btnExecute.Content = translationManager.GetUITranslation("menu.execute");
            btnExecuteAll.Content = translationManager.GetUITranslation("menu.execute_all");
            btnDelete.Content = translationManager.GetUITranslation("menu.delete");
            btnQuit.Content = translationManager.GetUITranslation("menu.quit");

            // Menu langue
            txtLanguage.Text = translationManager.GetUITranslation("menu.language");            // Menu paramètres métier
            txtBusinessSettings.Text = translationManager.GetUITranslation("business.settings");

            // Menu extensions prioritaires  
            txtPriorityExtensions.Text = translationManager.GetUITranslation("priority.extensions");            // Menu paramètres de bande passante
            txtBandwidthSettings.Text = translationManager.GetUITranslation("bandwidth.settings");
            txtBandwidthThreshold.Text = translationManager.GetUITranslation("bandwidth.threshold");
            txtBandwidthHint.Text = translationManager.GetUITranslation("bandwidth.threshold.hint");
            // Conserver le signe "+" comme contenu du bouton
            btnUpdateBandwidthThreshold.Content = "+";

            // Rafraîchir la valeur actuelle de la bande passante
            RefreshBandwidthThresholdValue();

            // Rafraîchir la liste des logiciels métier
            RefreshBusinessSoftwareList();

            // Rafraîchir la liste des extensions prioritaires
            RefreshPriorityExtensionsList();

            // Liste des sauvegardes
            txtListTitle.Text = translationManager.GetUITranslation("list.title");

            // Vue création
            txtBackupName.Text = translationManager.GetUITranslation("backup.name");
            txtSourcePathLabel.Text = translationManager.GetUITranslation("backup.source");
            txtDestinationPathLabel.Text = translationManager.GetUITranslation("backup.target");
            txtType.Text = translationManager.GetUITranslation("backup.type");
            btnFormCreate.Content = translationManager.GetUITranslation("menu.create");
            btnBack.Content = "←"; // Pas de traduction pour ce bouton            // Mettre à jour les types de sauvegarde
            rbFull.Content = translationManager.GetUITranslation("backup.type_full");
            rbDifferential.Content = translationManager.GetUITranslation("backup.type_differential");

            // Rafraîchir la liste des backups
            RefreshBackupList();
        }
        private void RefreshBackupList()
        {
            // Effacer la liste actuelle
            backupListPanel.Children.Clear();

            // Récupérer la liste des sauvegardes
            var backups = backupProcess.GetAllBackup();

            // Si la liste est vide, afficher un message
            if (backups.Count == 0)
            {
                TextBlock emptyMessage = new TextBlock
                {
                    Text = translationManager.GetUITranslation("list.empty"),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    Margin = new Thickness(20)
                };

                backupListPanel.Children.Add(emptyMessage);
            }
            else
            {
                // Ajouter chaque sauvegarde à la liste
                foreach (var backup in backups)
                {
                    Border border = new Border
                    {
                        Style = FindResource("BackupItemStyle") as Style,
                        Tag = backup.Name,
                        Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#007C80")
                    };

                    border.MouseLeftButtonUp += BackupItem_Click;

                    // Grid principal pour organiser le contenu
                    Grid mainGrid = new Grid();
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Informations de base de la sauvegarde
                    StackPanel infoPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.name_label")} {backup.Name}",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        FontSize = 14
                    });

                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.type")}: {backup.Type}",
                        Foreground = Brushes.White,
                        FontSize = 12,
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                    Grid.SetRow(infoPanel, 0);
                    mainGrid.Children.Add(infoPanel);

                    // Chemins source et destination
                    StackPanel pathsPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

                    pathsPanel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.source")}: {backup.SourceDirectory}",
                        Foreground = Brushes.LightGray,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                    pathsPanel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.target")}: {backup.TargetDirectory}",
                        Foreground = Brushes.LightGray,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                    Grid.SetRow(pathsPanel, 1);
                    mainGrid.Children.Add(pathsPanel);                    // Grid pour la barre de progression et les boutons (caché par défaut)
                    Grid progressGrid = new Grid
                    {
                        // Utilisez un ID sans caractères spéciaux pour éviter les erreurs de nommage WPF
                        Name = $"progressGrid{backup.Name.Replace("_", "").Replace(" ", "")}",
                        Margin = new Thickness(0, 5, 0, 0),
                        Visibility = Visibility.Collapsed  // Caché par défaut
                    };
                    progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Barre de progression avec pourcentage intégré
                    Border progressBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(51, 102, 102)),
                        CornerRadius = new CornerRadius(10),
                        Height = 25,
                        Margin = new Thickness(0, 0, 10, 0)
                    };

                    Grid progressBarGrid = new Grid();

                    // Barre de progression réelle
                    ProgressBar progressBar = new ProgressBar
                    {
                        Name = $"progressBar{backup.Name.Replace("_", "").Replace(" ", "")}",
                        Value = 0,
                        Maximum = 100,
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 204, 204)),
                        BorderThickness = new Thickness(0),
                        Height = 25
                    };

                    // Texte du pourcentage au centre
                    TextBlock percentageText = new TextBlock
                    {
                        Name = $"percentageText{backup.Name.Replace("_", "").Replace(" ", "")}",
                        Text = "0%",
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11,
                        FontWeight = FontWeights.Bold
                    };

                    progressBarGrid.Children.Add(progressBar);
                    progressBarGrid.Children.Add(percentageText);
                    progressBorder.Child = progressBarGrid;

                    Grid.SetColumn(progressBorder, 0);
                    progressGrid.Children.Add(progressBorder);

                    // Bouton Pause/Play
                    Button pausePlayButton = new Button
                    {
                        Name = $"btnPausePlay{backup.Name.Replace("_", "").Replace(" ", "")}",
                        Width = 35,
                        Height = 25,
                        Background = new SolidColorBrush(Color.FromRgb(0, 124, 128)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Content = "⏸", // Icône Pause par défaut
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 5, 0),
                        IsEnabled = false,
                        Tag = backup.Name
                    };
                    pausePlayButton.Click += PausePlayButton_Click;

                    Grid.SetColumn(pausePlayButton, 1);
                    progressGrid.Children.Add(pausePlayButton);

                    // Bouton Stop
                    Button stopButton = new Button
                    {
                        Name = $"btnStop{backup.Name.Replace("_", "").Replace(" ", "")}",
                        Width = 35,
                        Height = 25,
                        Background = new SolidColorBrush(Color.FromRgb(0, 124, 128)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Content = "⏹", // Icône Stop
                        FontSize = 12,
                        IsEnabled = false,
                        Tag = backup.Name
                    };
                    stopButton.Click += StopButton_Click;

                    Grid.SetColumn(stopButton, 2);
                    progressGrid.Children.Add(stopButton); Grid.SetRow(progressGrid, 2);
                    mainGrid.Children.Add(progressGrid);

                    // Status text for showing backup state (priority coordination, etc.)
                    TextBlock statusText = new TextBlock
                    {
                        Name = $"statusText{backup.Name.Replace("_", "").Replace(" ", "")}",
                        Text = "Ready",
                        Foreground = Brushes.LightGray,
                        FontSize = 10,
                        FontStyle = FontStyles.Italic,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0),
                        Visibility = Visibility.Collapsed  // Hidden by default, shown when needed
                    };

                    Grid.SetRow(statusText, 3);
                    mainGrid.Children.Add(statusText);

                    border.Child = mainGrid;
                    backupListPanel.Children.Add(border);
                }
            }

            // Désélectionner la sauvegarde actuelle
            if (selectedBackupItem != null)
            {
                selectedBackupItem.BorderBrush = null;
                selectedBackupItem.BorderThickness = new Thickness(0);
                selectedBackupItem.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#007C80");
            }
            selectedBackupItem = null;
            btnExecute.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        private void BackupItem_Click(object sender, MouseButtonEventArgs e)
        {
            // Récupérer l'élément sélectionné
            Border clickedItem = sender as Border;

            // Désélectionner l'élément précédemment sélectionné
            if (selectedBackupItem != null)
            {
                selectedBackupItem.BorderBrush = null;
                selectedBackupItem.BorderThickness = new Thickness(0);
                selectedBackupItem.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#007C80");
            }

            // Sélectionner le nouvel élément
            selectedBackupItem = clickedItem;
            selectedBackupItem.BorderBrush = Brushes.Black;
            selectedBackupItem.BorderThickness = new Thickness(3);
            selectedBackupItem.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#00A0A6");

            // Activer les boutons
            btnExecute.IsEnabled = true;
            btnDelete.IsEnabled = true;
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            // Afficher la vue de création et masquer la vue liste
            createView.Visibility = Visibility.Visible;
            listView.Visibility = Visibility.Collapsed;

            // Réinitialiser le formulaire
            txtJobName.Text = string.Empty;
            txtSourcePath.Text = string.Empty;
            txtDestinationPath.Text = string.Empty;

            // Sélectionner FULL comme type par défaut
            rbFull.IsChecked = true;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // Revenir à la vue liste
            createView.Visibility = Visibility.Collapsed;
            listView.Visibility = Visibility.Visible;
        }
        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (selectedBackupItem == null)
                return;

            string backupName = selectedBackupItem.Tag as string;
            if (string.IsNullOrEmpty(backupName))
                return;

            // Vérifier si un des logiciels métier est en cours d'exécution
            var backupRulesManager = BackupRulesManager.Instance;
            if (backupRulesManager.IsAnyBusinessSoftwareRunning())
            {
                string? runningSoftware = backupRulesManager.GetRunningBusinessSoftware();
                string message = translationManager.GetFormattedUITranslation("business.software.running", runningSoftware);
                MessageBox.Show(message, "EasySave", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }            // Désactiver les boutons pendant l'exécution
            btnExecute.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnExecuteAll.IsEnabled = false;
            btnCreate.IsEnabled = false;

            // Afficher la barre de progression et les boutons de contrôle
            ShowHideBackupProgress(backupName, true);            // Activer les boutons de contrôle de la sauvegarde
            EnableBackupControls(backupName, true);

            // Set current backup as running
            currentRunningBackup = backupName;
            UpdateBackupStatusText(backupName, "Running");
            
            // Notifier les clients que la sauvegarde démarre
            if (broadcastingEnabled && remoteServer != null)
            {
                remoteServer.BroadcastBackupStarted(backupName);
            }

            try
            {
                // Créer un nouveau jeton d'annulation
                cancellationTokenSource = new CancellationTokenSource();                // Obtenir la sauvegarde et connecter directement l'événement de progression
                var backup = backupProcess.GetAllBackup().FirstOrDefault(b => b.Name == backupName);
                if (backup != null)
                {
                    // Handler pour la progression
                    Action<int> progressHandler = null;

                    // Handler pour l'initialisation du tracker
                    Action<ByteProgressTracker> trackerInitHandler = (tracker) =>
                    {
                        progressHandler = (percentage) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateBackupProgress(backupName, percentage);
                            });
                        };
                        tracker.ProgressChanged += progressHandler;
                    };

                    // Exécuter la sauvegarde de manière asynchrone
                    await Task.Run(() =>
                    {
                        // Connecter l'événement d'initialisation du tracker
                        backup.ProgressTrackerInitialized += trackerInitHandler;

                        try
                        {
                            backupProcess.ExecuteBackup(backupName);
                        }
                        finally
                        {
                            // Déconnecter les événements
                            backup.ProgressTrackerInitialized -= trackerInitHandler;
                            if (backup.ProgressTracker != null && progressHandler != null)
                            {
                                backup.ProgressTracker.ProgressChanged -= progressHandler;
                            }
                        }
                    }, cancellationTokenSource.Token);
                }

                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Vérifier l'état final de la sauvegarde dans le fichier state.json
                    var statusEntry = GetCurrentBackupStatus(backupName); if (statusEntry != null && statusEntry.State == "COMPLETED")
                    {
                        // Update status to completed
                        UpdateBackupStatusText(backupName, "Completed");
                        
                        // Notifier les clients que la sauvegarde est terminée
                        if (broadcastingEnabled && remoteServer != null)
                        {
                            remoteServer.BroadcastBackupCompleted(backupName);
                        }

                        // Afficher un message de succès uniquement si la sauvegarde est bien marquée comme terminée
                        MessageBox.Show(
                            translationManager.GetFormattedUITranslation("backup.complete", backupName),
                            "EasySave",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // L'utilisateur a annulé l'opération
                Console.WriteLine("Backup operation cancelled by user.");

                // Informer l'utilisateur que la sauvegarde a été annulée
                MessageBox.Show(
                    translationManager.GetFormattedUITranslation("backup.cancelled", backupName),
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Réinitialiser l'état de la sauvegarde pour permettre une nouvelle exécution
                var backup = backupProcess.GetBackup(backupName);
                if (backup != null)
                {
                    backup.Reset();

                    // Mettre à jour le statut dans le fichier state.json pour réinitialiser l'état
                    var statusManager = new Easy_Save.Model.IO.StatusManager();
                    var statusEntry = GetCurrentBackupStatus(backupName);
                    if (statusEntry != null)
                    {
                        statusEntry.State = "READY";
                        statusEntry.Progression = 0;
                        statusEntry.NbFilesLeftToDo = statusEntry.TotalFilesToCopy;
                        statusManager.UpdateStatus(statusEntry);
                    }
                }
                
                // Notifier les clients que la sauvegarde a été arrêtée
                if (broadcastingEnabled && remoteServer != null)
                {
                    remoteServer.BroadcastBackupStopped(backupName);
                }
            }
            catch (Exception ex)
            {
                // Une erreur s'est produite
                MessageBox.Show(
                    translationManager.GetFormattedUITranslation("error.execution", backupName, ex.Message),
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                    
                // Notifier les clients que la sauvegarde a été arrêtée en cas d'erreur
                if (broadcastingEnabled && remoteServer != null)
                {
                    remoteServer.BroadcastBackupStopped(backupName);
                }
            }
            finally
            {
                // Réactiver les boutons
                btnExecute.IsEnabled = true;
                btnDelete.IsEnabled = true;
                btnExecuteAll.IsEnabled = true;
                btnCreate.IsEnabled = true;

                // Cacher la barre de progression et les boutons de contrôle
                ShowHideBackupProgress(backupName, false);

                // Désactiver les boutons de contrôle de la sauvegarde
                EnableBackupControls(backupName, false);                // Réinitialiser la barre de progression et le statut
                UpdateBackupProgress(backupName, 0);
                UpdateBackupStatusText(backupName, "Ready");

                // Clear current running backup
                currentRunningBackup = null;
            }
        }

        private async void BtnExecuteAll_Click(object sender, RoutedEventArgs e)
        {
            var backups = backupProcess.GetAllBackup();

            if (backups.Count == 0)
            {
                MessageBox.Show(translationManager.GetUITranslation("backup.no_saves"),
                    "EasySave", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Vérifier si un des logiciels métier est en cours d'exécution
            var backupRulesManager = BackupRulesManager.Instance;
            if (backupRulesManager.IsAnyBusinessSoftwareRunning())
            {
                string? runningSoftware = backupRulesManager.GetRunningBusinessSoftware();
                string message = translationManager.GetFormattedUITranslation("business.software.running", runningSoftware);
                MessageBox.Show(message, "EasySave", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Désactiver les boutons pendant l'exécution
            btnExecute.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnExecuteAll.IsEnabled = false;
            btnCreate.IsEnabled = false; try
            {                // Créer un nouveau jeton d'annulation
                cancellationTokenSource = new CancellationTokenSource();

                Console.WriteLine("Executing all backups in concurrent mode with priority file management");

                // Afficher les barres de progression pour toutes les sauvegardes
                foreach (var backup in backups)
                {
                    ShowHideBackupProgress(backup.Name, true);
                    EnableBackupControls(backup.Name, true);
                }

                // Initialiser le suivi de progression pour toutes les sauvegardes
                var progress = new Progress<(string BackupName, int Current, int Total)>(progressData =>
                {
                    // Utiliser le Dispatcher pour mettre à jour l'UI depuis un thread d'arrière-plan
                    Dispatcher.Invoke(() =>
                    {
                        // Mettre à jour la barre de progression intégrée pour chaque backup
                        UpdateBackupProgress(progressData.BackupName, progressData.Current);
                    });
                });                // Exécuter toutes les sauvegardes avec le nouveau système de progression
                int successCount = await progressTracker.ExecuteAllBackupsWithProgressAsync(progress, cancellationTokenSource.Token);
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // L'opération a été annulée par l'utilisateur
                    Console.WriteLine("All backup operations were cancelled by user.");

                    // Informer l'utilisateur que les sauvegardes ont été annulées
                    MessageBox.Show(
                        translationManager.GetUITranslation("backup.all.cancelled"),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);                    // Réinitialiser l'état des sauvegardes pour permettre une nouvelle exécution
                    var statusManager = new Easy_Save.Model.IO.StatusManager();
                    foreach (var backup in backups)
                    {
                        backup.Reset();

                        // Mettre à jour également le statut dans le fichier state.json
                        var statusEntry = GetCurrentBackupStatus(backup.Name);
                        if (statusEntry != null)
                        {
                            statusEntry.State = "READY";
                            statusEntry.Progression = 0;
                            statusEntry.NbFilesLeftToDo = statusEntry.TotalFilesToCopy;
                            statusManager.UpdateStatus(statusEntry);
                        }
                    }
                }
                else
                {
                    // Afficher un message de réussite
                    MessageBox.Show(
                        translationManager.GetUITranslation("backup.all.complete"),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Afficher l'erreur
                MessageBox.Show(
                    $"Error executing backups: {ex.Message}",
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Cacher les barres de progression pour toutes les sauvegardes
                foreach (var backup in backups)
                {
                    ShowHideBackupProgress(backup.Name, false);
                    EnableBackupControls(backup.Name, false);
                    UpdateBackupProgress(backup.Name, 0);
                }

                // Réactiver les boutons
                btnExecute.IsEnabled = selectedBackupItem != null;
                btnDelete.IsEnabled = selectedBackupItem != null;
                btnExecuteAll.IsEnabled = true;
                btnCreate.IsEnabled = true;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedBackupItem != null)
            {
                string backupName = selectedBackupItem.Tag.ToString();

                MessageBoxResult result = MessageBox.Show(
                    translationManager.GetFormattedUITranslation("backup.delete_confirm", backupName),
                    "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        backupProcess.DeleteBackup(backupName);
                        RefreshBackupList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(translationManager.GetFormattedUITranslation("error.delete", ex.Message),
                            "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnQuit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void BtnFormCreate_Click(object sender, RoutedEventArgs e)
        {
            string jobName = txtJobName.Text;
            string sourcePath = txtSourcePath.Text;
            string destinationPath = txtDestinationPath.Text;
            string backupType = "FULL";

            if (rbDifferential.IsChecked == true)
            {
                backupType = "DIFFERENTIAL";
            }

            // Valider les entrées
            if (string.IsNullOrWhiteSpace(jobName))
            {
                MessageBox.Show(translationManager.GetUITranslation("error.name_required"),
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                MessageBox.Show(translationManager.GetUITranslation("error.source_required"),
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                MessageBox.Show(translationManager.GetUITranslation("error.target_required"),
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Créer la sauvegarde en utilisant le contrôleur existant
                backupProcess.CreateBackup(jobName, sourcePath, destinationPath, backupType);

                // Actualiser la liste et revenir à la vue liste
                RefreshBackupList();
                createView.Visibility = Visibility.Collapsed;
                listView.Visibility = Visibility.Visible;

                MessageBox.Show(translationManager.GetFormattedUITranslation("backup.success", jobName),
                    "EasySave", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(translationManager.GetFormattedUITranslation("error.create", ex.Message),
                    "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    // Déterminer quel TextBox mettre à jour en fonction du bouton cliqué
                    var button = sender as Button;
                    var parent = button?.Parent as DockPanel;

                    // Chercher le TextBox dans le DockPanel
                    TextBox textBox = null;
                    foreach (var child in parent.Children)
                    {
                        if (child is TextBox)
                        {
                            textBox = child as TextBox;
                            break;
                        }
                    }

                    if (textBox != null)
                    {
                        textBox.Text = dialog.SelectedPath;
                    }
                    else
                    {
                        // Déterminer quel TextBox mettre à jour en fonction du parent du bouton
                        if (parent != null)
                        {
                            if (parent == txtSourcePath.Parent)
                                txtSourcePath.Text = dialog.SelectedPath;
                            else if (parent == txtDestinationPath.Parent)
                                txtDestinationPath.Text = dialog.SelectedPath;
                        }
                    }
                }
            }
        }

        // Gestion des boutons de langue
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Afficher/masquer le menu de langue avec l'overlay
            languageMenuOverlay.Visibility = languageMenuOverlay.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void CloseSettingsMenu()
        {
            if (languageMenuOverlay.Visibility == Visibility.Visible)
            {
                languageMenuOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void LanguageMenuOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Vérifier si le clic a été effectué en dehors du menu
            Point clickPoint = e.GetPosition(languageMenu);

            // Si le clic est en dehors des limites du menu, fermer le menu
            if (clickPoint.X < 0 || clickPoint.Y < 0 ||
                clickPoint.X > languageMenu.ActualWidth || clickPoint.Y > languageMenu.ActualHeight)
            {
                CloseSettingsMenu();
                e.Handled = true;  // Marquer l'événement comme traité
            }
        }

        private void LanguageMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Empêcher l'événement de se propager à l'overlay parent
            e.Handled = true;
        }

        private void BtnFrench_Click(object sender, RoutedEventArgs e)
        {
            translationManager.LoadTranslation("fr");
            languageMenuOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnEnglish_Click(object sender, RoutedEventArgs e)
        {
            translationManager.LoadTranslation("en");
            languageMenuOverlay.Visibility = Visibility.Collapsed;
        }
        private void LoadBackupRules()
        {
            try
            {
                if (!File.Exists(configPath))
                    return;

                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (config == null)
                    return;

                if (config.TryGetValue("BusinessSettings", out var businessSettingsObj) && businessSettingsObj is Dictionary<string, object> businessSettings)
                {
                    if (businessSettings.TryGetValue("MaxFileSize", out var maxFileSizeObj) && maxFileSizeObj is double maxFileSize)
                    {
                        backupProcess.MaxFileSize = (long)maxFileSize;
                    }

                    if (businessSettings.TryGetValue("RestrictedExtensions", out var restrictedExtensionsObj) && restrictedExtensionsObj is List<object> restrictedExtensions)
                    {
                        backupProcess.RestrictedExtensions = restrictedExtensions.Select(e => e.ToString()).ToArray();
                    }

                    if (businessSettings.TryGetValue("CryptoSoftPath", out var cryptoSoftPathObj) && cryptoSoftPathObj is string cryptoSoftPath)
                    {
                        backupProcess.CryptoSoftPath = cryptoSoftPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading backup rules: {ex.Message}");
            }
        }

        private void RefreshPriorityExtensionsList()
        {
            // Effacer la liste actuelle
            priorityExtensionsListPanel.Children.Clear();

            // Récupérer la liste des extensions prioritaires
            var priorityExtensions = backupProcess.GetPriorityExtensionsList();

            // Si la liste est vide, afficher un message
            if (priorityExtensions.Count == 0)
            {
                TextBlock emptyMessage = new TextBlock
                {
                    Text = translationManager.GetUITranslation("priority.extensions.list.empty"),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    Margin = new Thickness(5),
                    Foreground = Brushes.White
                };

                priorityExtensionsListPanel.Children.Add(emptyMessage);
            }
            else
            {
                // Ajouter chaque extension prioritaire à la liste
                foreach (var extension in priorityExtensions)
                {
                    Grid grid = new Grid();

                    // Définir les colonnes
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Texte de l'extension
                    TextBlock textBlock = new TextBlock
                    {
                        Text = extension,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(textBlock, 0);
                    grid.Children.Add(textBlock);

                    // Bouton de suppression
                    Button deleteButton = new Button
                    {
                        Content = "×",
                        Width = 20,
                        Height = 20,
                        Margin = new Thickness(5, 0, 0, 0),
                        Background = Brushes.IndianRed,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        Tag = extension
                    };
                    deleteButton.Click += BtnRemovePriorityExtension_Click;
                    Grid.SetColumn(deleteButton, 1);
                    grid.Children.Add(deleteButton);

                    // Ajouter le Grid à l'intérieur d'une Border avec un style
                    Border border = new Border
                    {
                        Child = grid,
                        Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#007C80"),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    priorityExtensionsListPanel.Children.Add(border);
                }
            }
        }
        private void RefreshBusinessSoftwareList()
        {
            // Effacer la liste actuelle
            businessSoftwareListPanel.Children.Clear();

            // Récupérer la liste des logiciels métier
            var businessSoftwareList = backupProcess.GetBusinessSoftwareList();

            // Si la liste est vide, afficher un message
            if (businessSoftwareList.Count == 0)
            {
                TextBlock emptyMessage = new TextBlock
                {
                    Text = translationManager.GetUITranslation("business.software.list.empty"),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    Margin = new Thickness(5),
                    Foreground = Brushes.White
                };

                businessSoftwareListPanel.Children.Add(emptyMessage);
            }
            else
            {
                // Ajouter chaque logiciel métier à la liste
                foreach (var software in businessSoftwareList)
                {
                    Grid grid = new Grid();

                    // Définir les colonnes
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Texte du logiciel
                    TextBlock textBlock = new TextBlock
                    {
                        Text = software,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(textBlock, 0);
                    grid.Children.Add(textBlock);

                    // Bouton de suppression
                    Button deleteButton = new Button
                    {
                        Content = "×",
                        Width = 20,
                        Height = 20,
                        Margin = new Thickness(5, 0, 0, 0),
                        Background = Brushes.IndianRed,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        Tag = software
                    };
                    deleteButton.Click += BtnRemoveBusinessSoftware_Click;
                    Grid.SetColumn(deleteButton, 1);
                    grid.Children.Add(deleteButton);

                    // Ajouter le Grid à l'intérieur d'une Border avec un style
                    Border border = new Border
                    {
                        Style = FindResource("BusinessSoftwareItemStyle") as Style,
                        Child = grid,
                        Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#007C80")
                    };

                    businessSoftwareListPanel.Children.Add(border);
                }
            }

            // Vider le champ de texte
            txtBusinessSoftware.Text = string.Empty;
        }

        private void BtnAddBusinessSoftware_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Récupérer le nom du logiciel métier
                string softwareName = txtBusinessSoftware.Text.Trim();

                if (string.IsNullOrWhiteSpace(softwareName))
                {
                    MessageBox.Show(
                        translationManager.GetUITranslation("business.software.name_required"),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Ajouter le logiciel métier
                bool added = backupProcess.AddBusinessSoftware(softwareName);

                if (added)
                {
                    // Rafraîchir la liste des logiciels métier
                    RefreshBusinessSoftwareList();

                    // Afficher un message de confirmation
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("business.software.added", softwareName),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Afficher un message d'erreur
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("business.software.already_exists", softwareName),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnRemoveBusinessSoftware_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Récupérer le bouton et le nom du logiciel métier
                Button button = sender as Button;
                string softwareName = button?.Tag as string;

                if (string.IsNullOrEmpty(softwareName))
                    return;

                // Demander confirmation
                MessageBoxResult result = MessageBox.Show(
                    translationManager.GetFormattedUITranslation("business.software.delete_confirm", softwareName),
                    "EasySave",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Supprimer le logiciel métier
                bool removed = backupProcess.RemoveBusinessSoftware(softwareName);

                if (removed)
                {
                    // Rafraîchir la liste des logiciels métier
                    RefreshBusinessSoftwareList();

                    // Afficher un message de confirmation
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("business.software.removed", softwareName),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Afficher un message d'erreur
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("business.software.not_found", softwareName),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnAddPriorityExtension_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Récupérer l'extension prioritaire
                string extension = txtPriorityExtension.Text.Trim();

                if (string.IsNullOrWhiteSpace(extension))
                {
                    MessageBox.Show(
                        translationManager.GetUITranslation("priority.extension.name_required"),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Ajouter l'extension prioritaire
                bool added = backupProcess.AddPriorityExtension(extension);

                if (added)
                {
                    // Vider le champ de texte
                    txtPriorityExtension.Text = string.Empty;

                    // Rafraîchir la liste des extensions prioritaires
                    RefreshPriorityExtensionsList();

                    // Afficher un message de confirmation
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("priority.extension.added", extension),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Afficher un message d'erreur
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("priority.extension.already_exists", extension),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TxtPriorityExtension_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnAddPriorityExtension_Click(sender, new RoutedEventArgs());
            }
        }

        private void BtnRemovePriorityExtension_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Récupérer le bouton et l'extension prioritaire
                Button button = sender as Button;
                string extension = button?.Tag as string;

                if (string.IsNullOrEmpty(extension))
                    return;

                // Demander confirmation
                MessageBoxResult result = MessageBox.Show(
                    translationManager.GetFormattedUITranslation("priority.extension.delete_confirm", extension),
                    "EasySave",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Supprimer l'extension prioritaire
                bool removed = backupProcess.RemovePriorityExtension(extension);

                if (removed)
                {
                    // Rafraîchir la liste des extensions prioritaires
                    RefreshPriorityExtensionsList();

                    // Afficher un message de confirmation
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("priority.extension.removed", extension),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Afficher un message d'erreur
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("priority.extension.not_found", extension),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void RefreshBandwidthThresholdValue()
        {
            long currentThreshold = backupProcess.GetBandwidthThreshold();
            txtBandwidthThresholdValue.Text = currentThreshold.ToString();
        }

        private void BtnUpdateBandwidthThreshold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string thresholdText = txtBandwidthThresholdValue.Text.Trim();

                if (string.IsNullOrWhiteSpace(thresholdText))
                {
                    MessageBox.Show(
                        translationManager.GetUITranslation("bandwidth.threshold.invalid"),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!long.TryParse(thresholdText, out long threshold) || threshold < 1)
                {
                    MessageBox.Show(
                        translationManager.GetUITranslation("bandwidth.threshold.invalid"),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                bool updated = backupProcess.UpdateBandwidthThreshold(threshold);

                if (updated)
                {
                    RefreshBandwidthThresholdValue();
                    MessageBox.Show(
                        translationManager.GetFormattedUITranslation("bandwidth.threshold.updated", threshold.ToString()),
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TxtBandwidthThreshold_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnUpdateBandwidthThreshold_Click(sender, new RoutedEventArgs());
            }
        }

        private Model.Status.StatusEntry GetCurrentBackupStatus(string backupName)
        {
            try
            {
                if (File.Exists("state.json"))
                {
                    string json = File.ReadAllText("state.json");
                    List<Model.Status.StatusEntry> statusEntries = JsonSerializer.Deserialize<List<Model.Status.StatusEntry>>(json);

                    if (statusEntries != null)
                    {
                        return statusEntries.FirstOrDefault(e => e.Name == backupName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la lecture du fichier d'état: {ex.Message}");
            }

            return null;
        }

        #region Nouvelles méthodes pour la progression intégrée

        private void PausePlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string backupName)
            {
                try
                {
                    var backup = backupProcess.GetBackup(backupName);
                    if (backup != null)
                    {
                        // Check if backup is paused for priority coordination
                        if (backup.State == Easy_Save.Model.Enum.BackupJobState.PAUSED_FOR_PRIORITY)
                        {
                            MessageBox.Show(
                                translationManager.GetUITranslation("priority.files.paused") ??
                                "This backup is paused due to priority file coordination and cannot be manually resumed until priority processing is complete.",
                                "EasySave",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            return;
                        }

                        if (backup.State == Easy_Save.Model.Enum.BackupJobState.RUNNING)
                        {
                            // Mettre en pause
                            backup.Pause();
                            button.Content = "▶"; // Icône Play
                            UpdateBackupStatusText(backupName, "Paused (manual)");
                            
                            // Notifier les clients que la sauvegarde est en pause
                            if (broadcastingEnabled && remoteServer != null)
                            {
                                remoteServer.BroadcastBackupPaused(backupName);
                            }
                        }
                        else if (backup.State == Easy_Save.Model.Enum.BackupJobState.PAUSED)
                        {
                            // Reprendre
                            backup.Play();
                            button.Content = "⏸"; // Icône Pause
                            UpdateBackupStatusText(backupName, "Running");
                            
                            // Notifier les clients que la sauvegarde a repris
                            if (broadcastingEnabled && remoteServer != null)
                            {
                                remoteServer.BroadcastBackupStarted(backupName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error controlling backup: {ex.Message}",
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string backupName)
            {
                try
                {
                    // Utiliser la nouvelle méthode StopBackup du BackupProcess
                    backupProcess.StopBackup(backupName);

                    // Annuler également l'opération via le token de cancellation si c'est la sauvegarde courante                    if (currentRunningBackup == backupName)
                    {
                        cancellationTokenSource?.Cancel();
                    }

                    // Clear status text
                    UpdateBackupStatusText(backupName, "");

                    // Cacher la barre de progression et désactiver les boutons de contrôle
                    ShowHideBackupProgress(backupName, false);
                    EnableBackupControls(backupName, false);

                    // Réinitialiser la barre de progression et le texte de statut
                    UpdateBackupProgress(backupName, 0);
                    UpdateBackupStatusText(backupName, "Ready");
                    
                    // Notifier les clients que la sauvegarde a été arrêtée
                    if (broadcastingEnabled && remoteServer != null)
                    {
                        remoteServer.BroadcastBackupStopped(backupName);
                    }

                    Console.WriteLine($"Stop button clicked for backup: {backupName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error stopping backup: {ex.Message}",
                        "EasySave",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void UpdateBackupProgress(string backupName, int percentage)
        {
            try
            {
                // Trouver la barre de progression correspondante
                var progressBar = FindProgressBar(backupName);
                var percentageText = FindPercentageText(backupName);

                if (progressBar != null)
                {
                    progressBar.Value = percentage;
                }

                if (percentageText != null)
                {
                    percentageText.Text = $"{percentage}%";
                }
                
                // Également mettre à jour le serveur pour propager aux clients
                UpdateBackupProgressServer(backupName, percentage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating progress for {backupName}: {ex.Message}");
            }
        }

        private void EnableBackupControls(string backupName, bool enabled)
        {
            try
            {
                var pausePlayButton = FindPausePlayButton(backupName);
                var stopButton = FindStopButton(backupName);

                if (pausePlayButton != null)
                {
                    pausePlayButton.IsEnabled = enabled;
                    if (enabled)
                    {
                        pausePlayButton.Content = "⏸"; // Icône Pause par défaut quand on démarre
                    }
                }

                if (stopButton != null)
                {
                    stopButton.IsEnabled = enabled;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enabling controls for {backupName}: {ex.Message}");
            }
        }

        private ProgressBar FindProgressBar(string backupName)
        {
            return FindNamedElement<ProgressBar>($"progressBar{backupName.Replace("_", "").Replace(" ", "")}");
        }

        private TextBlock FindPercentageText(string backupName)
        {
            return FindNamedElement<TextBlock>($"percentageText{backupName.Replace("_", "").Replace(" ", "")}");
        }

        private Button FindPausePlayButton(string backupName)
        {
            return FindNamedElement<Button>($"btnPausePlay{backupName.Replace("_", "").Replace(" ", "")}");
        }

        private Button FindStopButton(string backupName)
        {
            return FindNamedElement<Button>($"btnStop{backupName.Replace("_", "").Replace(" ", "")}");
        }

        private T FindNamedElement<T>(string name) where T : FrameworkElement
        {
            foreach (Border border in backupListPanel.Children.OfType<Border>())
            {
                var element = FindElementInVisualTree<T>(border, name);
                if (element != null)
                    return element;
            }
            return null;
        }

        private T FindElementInVisualTree<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var result = FindElementInVisualTree<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void ShowHideBackupProgress(string backupName, bool show)
        {
            try
            {
                var progressGrid = FindProgressGrid(backupName);
                if (progressGrid != null)
                {
                    progressGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing/hiding progress for {backupName}: {ex.Message}");
            }
        }
        private Grid FindProgressGrid(string backupName)
        {
            return FindNamedElement<Grid>($"progressGrid{backupName.Replace("_", "").Replace(" ", "")}");
        }

        #endregion

        #region BackupManager Event Handlers

        private void BackupManager_BackupPaused(object sender, BackupPausedEventArgs e)
        {
            // Exécuter dans le thread de l'interface utilisateur
            Dispatcher.Invoke(() =>
            {
                // Update UI visual indicators for paused backup
                UpdateBackupPauseIndicator(e.BackupName, true, e.SoftwareName);

                // Show appropriate message based on pause reason
                string message;
                if (e.SoftwareName.Contains("Priority files"))
                {
                    message = translationManager.GetUITranslation("priority.files.paused")
                        ?? $"Backup '{e.BackupName}' paused due to priority file coordination";
                }
                else
                {
                    message = translationManager.GetUITranslation("business.software.paused")
                        ?.Replace("{0}", e.SoftwareName)
                        ?? $"Backup '{e.BackupName}' paused due to business software: {e.SoftwareName}";
                }

                // Show status in UI rather than intrusive message box for priority coordination
                if (e.SoftwareName.Contains("Priority files"))
                {
                    UpdateBackupStatusText(e.BackupName, "Paused for priority files");
                }
                else
                {
                    MessageBox.Show(message, "Easy Save", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private void BackupManager_BackupResumed(object sender, BackupResumedEventArgs e)
        {
            // Exécuter dans le thread de l'interface utilisateur
            Dispatcher.Invoke(() =>
            {
                // Update UI visual indicators for resumed backup
                UpdateBackupPauseIndicator(e.BackupName, false, "");

                // Show appropriate message based on resume reason
                string message;
                if (e.SoftwareName.Contains("Priority coordination"))
                {
                    message = translationManager.GetUITranslation("priority.files.resumed")
                        ?? $"Backup '{e.BackupName}' resumed - priority coordination complete";
                    UpdateBackupStatusText(e.BackupName, "Running");
                }
                else
                {
                    message = translationManager.GetUITranslation("business.software.resumed")
                        ?.Replace("{0}", e.SoftwareName)
                        ?? $"Backup '{e.BackupName}' resumed - business software stopped: {e.SoftwareName}";
                    MessageBox.Show(message, "Easy Save", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private void UpdateBackupPauseIndicator(string backupName, bool isPaused, string reason)
        {
            try
            {
                var pausePlayButton = FindPausePlayButton(backupName);
                if (pausePlayButton != null)
                {
                    if (isPaused)
                    {
                        pausePlayButton.Content = "⏸⚠"; // Pause icon with warning for priority/business pause
                        if (reason.Contains("Priority files"))
                        {
                            pausePlayButton.ToolTip = "Paused due to priority file coordination";
                            pausePlayButton.Background = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange for priority
                        }
                        else
                        {
                            pausePlayButton.ToolTip = $"Paused due to business software: {reason}";
                            pausePlayButton.Background = new SolidColorBrush(Color.FromRgb(255, 69, 0)); // Red-orange for business software
                        }
                    }
                    else
                    {
                        pausePlayButton.Content = "⏸"; // Normal pause icon
                        pausePlayButton.ToolTip = null;
                        pausePlayButton.Background = new SolidColorBrush(Color.FromRgb(0, 124, 128)); // Default color
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating pause indicator for {backupName}: {ex.Message}");
            }
        }

        private void UpdateBackupStatusText(string backupName, string status)
        {
            try
            {
                // Find a status text element to update
                var statusText = FindNamedElement<TextBlock>($"statusText{backupName.Replace("_", "").Replace(" ", "")}");
                if (statusText != null)
                {
                    statusText.Text = status;

                    // Show status text when there's something important to display
                    if (status.Contains("priority") || status.Contains("Paused") || status.Contains("business"))
                    {
                        statusText.Visibility = Visibility.Visible;
                    }
                    else if (status.Contains("Running") || status.Contains("Ready"))
                    {
                        statusText.Visibility = Visibility.Collapsed;
                    }

                    // Set appropriate colors based on status type
                    if (status.Contains("priority"))
                    {
                        statusText.Foreground = Brushes.Orange;
                    }
                    else if (status.Contains("Running"))
                    {
                        statusText.Foreground = Brushes.LightGreen;
                    }
                    else if (status.Contains("business"))
                    {
                        statusText.Foreground = Brushes.Red;
                    }
                    else
                    {
                        statusText.Foreground = Brushes.LightGray;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating status text for {backupName}: {ex.Message}");
            }
        }

        #endregion
    }
}
