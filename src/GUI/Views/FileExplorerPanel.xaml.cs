using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Core.Models;
using Core.Services;
using Microsoft.Win32;
using System.Diagnostics;

namespace GUI.Views
{
    public partial class FileExplorerPanel : UserControl
    {
        private readonly RunConfigurationManager configurationManager;
        private RunConfiguration? currentConfiguration;
        private string? currentRootDirectory;

        public event EventHandler<RunConfiguration>? ConfigurationSelected;
        public event EventHandler<RunConfiguration>? RunConfigurationRequested;
        public event EventHandler<string>? FileSelected;

        public FileExplorerPanel()
        {
            InitializeComponent();
            configurationManager = new RunConfigurationManager();
            
            LoadConfigurations();
        }

        public void SetRootDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"Directory not found: {path}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            currentRootDirectory = path;
            LoadFileTree(path);
            StatusText.Text = $"Loaded: {path}";
        }

        private void LoadFileTree(string rootPath)
        {
            FileTreeView.Items.Clear();

            try
            {
                var rootItem = new FileSystemItem
                {
                    Name = Path.GetFileName(rootPath) ?? rootPath,
                    Path = rootPath,
                    IsDirectory = true
                };

                LoadDirectory(rootItem, rootPath);
                FileTreeView.Items.Add(rootItem);
                
                // Expand root
                if (FileTreeView.Items.Count > 0 && FileTreeView.ItemContainerGenerator.ContainerFromItem(rootItem) is TreeViewItem treeItem)
                {
                    treeItem.IsExpanded = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDirectory(FileSystemItem parentItem, string path)
        {
            try
            {
                // Load subdirectories
                var directories = Directory.GetDirectories(path);
                foreach (var dir in directories.OrderBy(d => d))
                {
                    var dirName = Path.GetFileName(dir);
                    
                    // Skip hidden and system folders
                    if (dirName.StartsWith(".") || dirName.StartsWith("$"))
                        continue;

                    var dirItem = new FileSystemItem
                    {
                        Name = dirName,
                        Path = dir,
                        IsDirectory = true
                    };

                    LoadDirectory(dirItem, dir);
                    parentItem.Children.Add(dirItem);
                }

                // Load files (filter for relevant extensions)
                var files = Directory.GetFiles(path);
                var relevantExtensions = new[] { ".mod", ".dat", ".json", ".txt", ".lp", ".mps" };
                
                foreach (var file in files.OrderBy(f => f))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (relevantExtensions.Contains(ext))
                    {
                        parentItem.Children.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(file),
                            Path = file,
                            IsDirectory = false
                        });
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have access to
            }
        }

        private void LoadConfigurations()
        {
            configurationManager.LoadAll();
            RefreshConfigurationsList();
        }

        private void RefreshConfigurationsList()
        {
            ConfigurationsListBox.ItemsSource = null;
            ConfigurationsListBox.ItemsSource = configurationManager.GetAll().ToList();
        }

        // Event Handlers
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder to explore",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SetRootDirectory(dialog.SelectedPath);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentRootDirectory))
            {
                LoadFileTree(currentRootDirectory);
            }
            LoadConfigurations();
        }

        private void FileTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                FileSelected?.Invoke(this, item.Path);
            }
        }

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileSystemItem item)
            {
                StatusText.Text = item.Path;
            }
        }

        private void NewConfiguration_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RunConfigurationDialog();
            if (dialog.ShowDialog() == true && dialog.Configuration != null)
            {
                configurationManager.Save(dialog.Configuration);
                RefreshConfigurationsList();
                StatusText.Text = $"Created: {dialog.Configuration.Name}";
            }
        }

        private void EditConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                var dialog = new RunConfigurationDialog(config);
                if (dialog.ShowDialog() == true)
                {
                    configurationManager.Save(config);
                    RefreshConfigurationsList();
                    StatusText.Text = $"Updated: {config.Name}";
                }
            }
        }

        private void RunConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                RunConfigurationRequested?.Invoke(this, config);
            }
        }

        private void Configuration_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                ConfigurationSelected?.Invoke(this, config);
            }
        }

        private void DuplicateConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                var duplicate = configurationManager.Duplicate(config.Id);
                RefreshConfigurationsList();
                StatusText.Text = $"Duplicated: {duplicate?.Name}";
            }
        }

        private void RenameConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                var newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new name:", 
                    "Rename Configuration", 
                    config.Name);

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    configurationManager.Rename(config.Id, newName);
                    RefreshConfigurationsList();
                }
            }
        }

        private void DeleteConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                var result = MessageBox.Show(
                    $"Delete configuration '{config.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    configurationManager.Delete(config.Id);
                    RefreshConfigurationsList();
                    StatusText.Text = $"Deleted: {config.Name}";
                }
            }
        }

        private void ExportConfiguration_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigurationsListBox.SelectedItem is RunConfiguration config)
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Configuration Files (*.json)|*.json",
                    FileName = $"{config.Name}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    configurationManager.Export(config, dialog.FileName);
                    MessageBox.Show("Configuration exported successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void AddToModelFiles_Click(object sender, RoutedEventArgs e)
        {
            if (FileTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                // TODO: Add to current configuration or prompt to create new
                MessageBox.Show($"Add {item.Name} to model files", "Info");
            }
        }

        private void AddToDataFiles_Click(object sender, RoutedEventArgs e)
        {
            if (FileTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                MessageBox.Show($"Add {item.Name} to data files", "Info");
            }
        }

        private void SetAsSettingsFile_Click(object sender, RoutedEventArgs e)
        {
            if (FileTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                MessageBox.Show($"Set {item.Name} as settings file", "Info");
            }
        }

        private void OpenInEditor_Click(object sender, RoutedEventArgs e)
        {
            if (FileTreeView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                FileSelected?.Invoke(this, item.Path);
            }
        }

        private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (FileTreeView.SelectedItem is FileSystemItem item)
            {
                var path = item.IsDirectory ? item.Path : Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrEmpty(path))
                {
                    Process.Start("explorer.exe", path);
                }
            }
        }
    }

    /// <summary>
    /// Represents a file or directory in the tree
    /// </summary>
    public class FileSystemItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool IsDirectory { get; set; }
        public List<FileSystemItem> Children { get; set; } = new List<FileSystemItem>();

        public string Icon => IsDirectory ? "📁" : GetFileIcon();

        private string GetFileIcon()
        {
            var ext = System.IO.Path.GetExtension(Path).ToLower();
            return ext switch
            {
                ".mod" => "📝",
                ".dat" => "📊",
                ".json" => "⚙️",
                ".txt" => "📄",
                ".lp" => "📋",
                ".mps" => "📋",
                _ => "📄"
            };
        }
    }
}