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
        private readonly BackupProgressTracker progressTracker;

        // Jeton d'annulation pour les opérations de sauvegarde
        private CancellationTokenSource? cancellationTokenSource;

        // Mode d'exécution des sauvegardes (true = concurrent, false = séquentiel)
        private bool isConcurrentExecution = false;

        // Nom de la sauvegarde actuellement en cours d'exécution
        private string? currentRunningBackup = null;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize required fields
            backupProcess = new BackupProcess();

            // Use the current directory for config path instead of a hardcoded path
            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

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

                // Actualiser l'interface utilisateur
                UpdateUILanguage();
                RefreshBackupList();

                // Charger les paramètres du logiciel métier
                LoadBackupRules();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing application: {ex.Message}\n\nStack Trace: {ex.StackTrace}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            txtLanguage.Text = translationManager.GetUITranslation("menu.language");

            // Menu mode d'exécution
            txtExecutionMode.Text = translationManager.GetUITranslation("menu.execution_mode");
            rbSequential.Content = translationManager.GetUITranslation("execution.sequential");
            rbConcurrent.Content = translationManager.GetUITranslation("execution.concurrent");

            // Menu paramètres métier
            txtBusinessSettings.Text = translationManager.GetUITranslation("business.settings");

            // Rafraîchir la liste des logiciels métier
            RefreshBusinessSoftwareList();

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
                        Name = $"progressGrid_{backup.Name}",
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
                        Name = $"progressBar_{backup.Name}",
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
                        Name = $"percentageText_{backup.Name}",
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
                        Name = $"btnPausePlay_{backup.Name}",
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
                        Name = $"btnStop_{backup.Name}",
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
                    progressGrid.Children.Add(stopButton);

                    Grid.SetRow(progressGrid, 2);
                    mainGrid.Children.Add(progressGrid);

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
            ShowHideBackupProgress(backupName, true);

            // Activer les boutons de contrôle de la sauvegarde
            EnableBackupControls(backupName, true);

            try
            {
                // Créer un nouveau jeton d'annulation
                cancellationTokenSource = new CancellationTokenSource();

                // Initialiser le suivi de progression
                var progress = new Progress<(int Current, int Total)>(progressData =>
                {
                    // Utiliser le Dispatcher pour mettre à jour l'UI depuis un thread d'arrière-plan
                    Dispatcher.Invoke(() =>
                    {
                        // Mise à jour de la barre de progression intégrée
                        UpdateBackupProgress(backupName, progressData.Current);
                    });
                });

                // Exécuter la sauvegarde
                await progressTracker.ExecuteBackupWithProgressAsync(backupName, progress, cancellationTokenSource.Token);

                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Vérifier l'état final de la sauvegarde dans le fichier state.json
                    var statusEntry = GetCurrentBackupStatus(backupName);
                    if (statusEntry != null && statusEntry.State == "COMPLETED")
                    {
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
            }
            catch (Exception ex)
            {
                // Une erreur s'est produite
                MessageBox.Show(
                    translationManager.GetFormattedUITranslation("error.execution", backupName, ex.Message),
                    "EasySave",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                EnableBackupControls(backupName, false);

                // Réinitialiser la barre de progression
                UpdateBackupProgress(backupName, 0);
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

                Console.WriteLine($"Executing all backups in {(isConcurrentExecution ? "concurrent" : "sequential")} mode");

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
                });

                // Exécuter toutes les sauvegardes avec le mode choisi par l'utilisateur
                int successCount;
                if (isConcurrentExecution)
                {
                    // Utiliser le mode concurrent
                    successCount = await Task.Run(() =>
                    {
                        backupProcess.RunAllBackups(true);
                        return backups.Count; // Considérer toutes les sauvegardes comme réussies pour simplifier
                    });
                }
                else
                {
                    // Utiliser le mode séquentiel avec suivi de la progression
                    successCount = await progressTracker.ExecuteAllBackupsWithProgressAsync(progress, cancellationTokenSource.Token);
                }
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
        private void ExecutionMode_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                isConcurrentExecution = radioButton.Name == "rbConcurrent";
                Console.WriteLine($"Execution mode changed to: {(isConcurrentExecution ? "Concurrent" : "Sequential")}");
            }
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
                        if (backup.State == Easy_Save.Model.Enum.BackupJobState.RUNNING)
                        {
                            // Mettre en pause
                            backup.Pause();
                            button.Content = "▶"; // Icône Play
                        }
                        else if (backup.State == Easy_Save.Model.Enum.BackupJobState.PAUSED)
                        {
                            // Reprendre
                            backup.Play();
                            button.Content = "⏸"; // Icône Pause
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
                    var backup = backupProcess.GetBackup(backupName);
                    if (backup != null)
                    {
                        // Arrêter la sauvegarde
                        backup.Stop();

                        // Annuler également l'opération via le token de cancellation si c'est la sauvegarde courante
                        if (currentRunningBackup == backupName)
                        {
                            cancellationTokenSource?.Cancel();
                        }

                        // Mettre à jour immédiatement le statut dans le fichier state.json
                        var statusManager = new Easy_Save.Model.IO.StatusManager();
                        var statusEntry = GetCurrentBackupStatus(backupName);
                        if (statusEntry != null)
                        {
                            statusEntry.State = "STOPPED";
                            statusManager.UpdateStatus(statusEntry);
                        }                        // Cacher la barre de progression et désactiver les boutons de contrôle
                        ShowHideBackupProgress(backupName, false);
                        EnableBackupControls(backupName, false);

                        // Réinitialiser la barre de progression
                        UpdateBackupProgress(backupName, 0);
                    }
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
            return FindNamedElement<ProgressBar>($"progressBar_{backupName}");
        }

        private TextBlock FindPercentageText(string backupName)
        {
            return FindNamedElement<TextBlock>($"percentageText_{backupName}");
        }

        private Button FindPausePlayButton(string backupName)
        {
            return FindNamedElement<Button>($"btnPausePlay_{backupName}");
        }

        private Button FindStopButton(string backupName)
        {
            return FindNamedElement<Button>($"btnStop_{backupName}");
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
            return FindNamedElement<Grid>($"progressGrid_{backupName}");
        }

        #endregion
    }
}
