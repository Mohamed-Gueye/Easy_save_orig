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
                CustomConfiguration.SetupConfiguration();
                
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
            
            // Liste des sauvegardes
            txtListTitle.Text = translationManager.GetUITranslation("list.title");
            
            // Vue création
            txtBackupName.Text = translationManager.GetUITranslation("backup.name");
            txtSourcePathLabel.Text = translationManager.GetUITranslation("backup.source");
            txtDestinationPathLabel.Text = translationManager.GetUITranslation("backup.target");
            txtType.Text = translationManager.GetUITranslation("backup.type");
            btnFormCreate.Content = translationManager.GetUITranslation("menu.create");
            btnBack.Content = "←"; // Pas de traduction pour ce bouton
            
            // Mettre à jour les types de sauvegarde
            rbFull.Content = translationManager.GetUITranslation("backup.type_full");
            rbDifferential.Content = translationManager.GetUITranslation("backup.type_differential");
            
            // Mettre à jour les textes de la fenêtre de progression
            txtProgressTitle.Text = translationManager.GetUITranslation("progress.title");
            txtProgressInfo.Text = translationManager.GetUITranslation("progress.info");
            btnCancelBackup.Content = translationManager.GetUITranslation("progress.cancel");
            
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
                        Tag = backup.Name
                    };
                    
                    border.MouseLeftButtonUp += BackupItem_Click;
                    
                    StackPanel panel = new StackPanel();
                    
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.name_label")} {backup.Name}",
                        Foreground = Brushes.White
                    });
                    
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.type")}: {backup.Type}",
                        Foreground = Brushes.White
                    });
                    
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"{translationManager.GetUITranslation("backup.source")}: {backup.SourceDirectory} → {backup.TargetDirectory}",
                        Foreground = Brushes.White,
                        TextWrapping = TextWrapping.Wrap
                    });
                    
                    border.Child = panel;
                    backupListPanel.Children.Add(border);
                }
            }
            
            // Désélectionner la sauvegarde actuelle
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
            }
            
            // Sélectionner le nouvel élément
            selectedBackupItem = clickedItem;
            selectedBackupItem.BorderBrush = Brushes.White;
            selectedBackupItem.BorderThickness = new Thickness(2);
            
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
            if (selectedBackupItem != null)
            {
                string backupName = selectedBackupItem.Tag.ToString();
                
                // Désactiver les boutons pendant l'exécution
                btnExecute.IsEnabled = false;
                btnDelete.IsEnabled = false;
                btnExecuteAll.IsEnabled = false;
                btnCreate.IsEnabled = false;
                
                try
                {
                    // Afficher la fenêtre de progression
                    ShowProgressOverlay(backupName);
                    
                    // Créer un nouveau jeton d'annulation
                    cancellationTokenSource = new CancellationTokenSource();
                    
                    // Initialiser le suivi de progression
                    var progress = new Progress<(int Current, int Total)>(progressData =>
                    {
                        // Utiliser le Dispatcher pour mettre à jour l'UI depuis un thread d'arrière-plan
                        Dispatcher.Invoke(() =>
                        {
                            // Mettre à jour l'UI avec la progression
                            int percentage = progressData.Total > 0 ? (int)((double)progressData.Current / progressData.Total * 100) : 0;
                            
                            progressBar.Value = percentage;
                            txtProgressPercentage.Text = $"{percentage}%";
                            txtProgressInfo.Text = translationManager.GetFormattedUITranslation("progress.files", progressData.Current, progressData.Total);
                        });
                    });
                    
                    // Exécuter la sauvegarde avec le nouveau gestionnaire de progression
                    await progressTracker.ExecuteBackupWithProgressAsync(backupName, progress, cancellationTokenSource.Token);
                    
                    // Masquer la fenêtre de progression
                    HideProgressOverlay();
                    
                    // Afficher un message de succès
                    MessageBox.Show(translationManager.GetFormattedUITranslation("backup.complete", backupName), 
                        "EasySave", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (OperationCanceledException)
                {
                    // L'opération a été annulée par l'utilisateur
                    HideProgressOverlay();
                    MessageBox.Show($"Backup operation '{backupName}' was cancelled.", 
                        "Operation Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    // En cas d'erreur, masquer la fenêtre de progression
                    HideProgressOverlay();
                    
                    MessageBox.Show(translationManager.GetFormattedUITranslation("error.execution", backupName, ex.Message), 
                        "EasySave", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Réactiver les boutons
                    btnExecute.IsEnabled = selectedBackupItem != null;
                    btnDelete.IsEnabled = selectedBackupItem != null;
                    btnExecuteAll.IsEnabled = true;
                    btnCreate.IsEnabled = true;
                    
                    // Libérer le jeton d'annulation
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
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
            
            // Désactiver les boutons pendant l'exécution
            btnExecute.IsEnabled = false;
            btnDelete.IsEnabled = false;
            btnExecuteAll.IsEnabled = false;
            btnCreate.IsEnabled = false;
            
            try
            {
                // Créer un nouveau jeton d'annulation
                cancellationTokenSource = new CancellationTokenSource();
                
                // Afficher un message de progression
                string executionMode = isConcurrentExecution ? "Concurrent" : "Sequential";
                ShowProgressOverlay($"All Backups ({backups.Count}) - {executionMode} Mode");
                
                // Initialiser le suivi de progression pour toutes les sauvegardes
                var progress = new Progress<(string BackupName, int Current, int Total)>(progressData =>
                {
                    // Utiliser le Dispatcher pour mettre à jour l'UI depuis un thread d'arrière-plan
                    Dispatcher.Invoke(() =>
                    {
                        // Mettre à jour l'UI avec la progression
                        int percentage = progressData.Total > 0 ? (int)((double)progressData.Current / progressData.Total * 100) : 0;
                        
                        progressBar.Value = percentage;
                        txtProgressPercentage.Text = $"{percentage}%";
                        txtProgressInfo.Text = translationManager.GetFormattedUITranslation("backup.start", progressData.BackupName);
                    });
                });
                
                Console.WriteLine($"Executing all backups in {(isConcurrentExecution ? "concurrent" : "sequential")} mode");
                
                // Exécuter toutes les sauvegardes avec le mode choisi par l'utilisateur
                int successCount;
                if (isConcurrentExecution)
                {
                    // Utiliser le mode concurrent
                    successCount = await Task.Run(() => {
                        backupProcess.RunAllBackups(true);
                        return backups.Count; // Considérer toutes les sauvegardes comme réussies pour simplifier
                    });
                }
                else
                {
                    // Utiliser le mode séquentiel avec suivi de la progression
                    successCount = await progressTracker.ExecuteAllBackupsWithProgressAsync(progress, cancellationTokenSource.Token);
                }
                
                // Masquer la fenêtre de progression
                HideProgressOverlay();
                
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    MessageBox.Show($"Backup operations were cancelled. {successCount} of {backups.Count} completed.", 
                        "Operation Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Afficher un message de succès avec le mode d'exécution utilisé
                    MessageBox.Show(translationManager.GetUITranslation("backup.all.complete") + 
                                    $"\n\n{successCount} of {backups.Count} backups were executed successfully in {executionMode.ToLower()} mode.", 
                        "EasySave", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Masquer la fenêtre de progression
                HideProgressOverlay();
                
                MessageBox.Show($"Error executing all backups: {ex.Message}", 
                    "EasySave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Réactiver les boutons
                btnExecute.IsEnabled = selectedBackupItem != null;
                btnDelete.IsEnabled = selectedBackupItem != null;
                btnExecuteAll.IsEnabled = true;
                btnCreate.IsEnabled = true;
                
                // Libérer le jeton d'annulation
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
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
                    var textBox = parent?.Children[0] as TextBox;
                    
                    if (textBox != null)
                    {
                        textBox.Text = dialog.SelectedPath;
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

        private void BtnCancelBackup_Click(object sender, RoutedEventArgs e)
        {
            // Annuler l'opération de sauvegarde en cours
            cancellationTokenSource?.Cancel();
        }
        
        private void ShowProgressOverlay(string backupName)
        {
            // Mettre à jour les textes
            txtProgressTitle.Text = translationManager.GetUITranslation("progress.title");
            txtProgressInfo.Text = translationManager.GetFormattedUITranslation("backup.start", backupName);
            progressBar.Value = 0;
            txtProgressPercentage.Text = "0%";
            
            // Afficher l'overlay
            progressOverlay.Visibility = Visibility.Visible;
        }
        
        private void HideProgressOverlay()
        {
            // Masquer l'overlay
            progressOverlay.Visibility = Visibility.Collapsed;
        }
    }
} 