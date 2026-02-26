using System.Windows;
using Microsoft.Win32;
using Core.Models;

namespace GUI.Dialogs
{
    public partial class RunConfigurationDialog : Window
    {
        public RunConfiguration? Configuration { get; private set; }

        public RunConfigurationDialog(RunConfiguration? existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                Configuration = existing;
                LoadConfiguration(existing);
            }
            else
            {
                Configuration = new RunConfiguration();
            }
        }

        private void LoadConfiguration(RunConfiguration config)
        {
            NameTextBox.Text = config.Name;
            DescriptionTextBox.Text = config.Description;
            ModelFilesListBox.ItemsSource = config.ModelFiles;
            DataFilesListBox.ItemsSource = config.DataFiles;
            SettingsFileTextBox.Text = config.SettingsFile;
            WorkingDirectoryTextBox.Text = config.WorkingDirectory;
        }

        private void AddModelFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Model Files (*.mod)|*.mod|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (Configuration != null && !Configuration.ModelFiles.Contains(file))
                    {
                        Configuration.ModelFiles.Add(file);
                    }
                }
                ModelFilesListBox.Items.Refresh();
            }
        }

        private void RemoveModelFile_Click(object sender, RoutedEventArgs e)
        {
            if (ModelFilesListBox.SelectedItem is string file && Configuration != null)
            {
                Configuration.ModelFiles.Remove(file);
                ModelFilesListBox.Items.Refresh();
            }
        }

        private void AddDataFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Data Files (*.dat)|*.dat|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (Configuration != null && !Configuration.DataFiles.Contains(file))
                    {
                        Configuration.DataFiles.Add(file);
                    }
                }
                DataFilesListBox.Items.Refresh();
            }
        }

        private void RemoveDataFile_Click(object sender, RoutedEventArgs e)
        {
            if (DataFilesListBox.SelectedItem is string file && Configuration != null)
            {
                Configuration.DataFiles.Remove(file);
                DataFilesListBox.Items.Refresh();
            }
        }

        private void BrowseSettingsFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true && Configuration != null)
            {
                Configuration.SettingsFile = dialog.FileName;
                SettingsFileTextBox.Text = dialog.FileName;
            }
        }

        private void ClearSettingsFile_Click(object sender, RoutedEventArgs e)
        {
            if (Configuration != null)
            {
                Configuration.SettingsFile = null;
                SettingsFileTextBox.Text = string.Empty;
            }
        }

        private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && Configuration != null)
            {
                Configuration.WorkingDirectory = dialog.SelectedPath;
                WorkingDirectoryTextBox.Text = dialog.SelectedPath;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a configuration name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Configuration != null)
            {
                Configuration.Name = NameTextBox.Text;
                Configuration.Description = DescriptionTextBox.Text;
            }

            DialogResult = true;
            Close();
        }
    }
}