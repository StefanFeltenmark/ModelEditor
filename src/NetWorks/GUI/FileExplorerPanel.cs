using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Core.Models;
using Core.Services;

namespace GUI
{
    public partial class FileExplorerPanel : UserControl
    {
        private readonly RunConfigurationManager configManager;
        private TabControl tabControl;
        private TreeView fileTreeView;
        private TreeView configurationsTreeView;
        private string currentRootDirectory;
        private RunConfiguration? activeConfiguration;

        public event EventHandler<RunConfiguration> ConfigurationSelected;
        public event EventHandler<RunConfiguration> RunConfigurationRequested;
        public event EventHandler<RunConfiguration> ParseConfigurationRequested;
        public event EventHandler<string> FileDoubleClicked;
        public event EventHandler<string> OutputMessageRequested;

        public FileExplorerPanel(RunConfigurationManager manager)
        {
            configManager = manager;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Create tab control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Files tab
            var filesTab = new TabPage("Files");
            CreateFilesTab(filesTab);
            tabControl.TabPages.Add(filesTab);

            // Configurations tab
            var configTab = new TabPage("Configurations");
            CreateConfigurationsTab(configTab);
            tabControl.TabPages.Add(configTab);

            this.Controls.Add(tabControl);
            this.ResumeLayout();
        }

        private void CreateFilesTab(TabPage tab)
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            // Toolbar
            var toolbar = new ToolStrip { Dock = DockStyle.Top };
            toolbar.Items.Add(new ToolStripButton("📁 Open Folder", null, OpenFolder_Click));
            toolbar.Items.Add(new ToolStripButton("🔄 Refresh", null, Refresh_Click));
            panel.Controls.Add(toolbar);

            // Tree view
            fileTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                ImageList = CreateImageList()
            };
            fileTreeView.NodeMouseDoubleClick += FileTreeView_NodeMouseDoubleClick;
            
            // Context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Add to Model Files", null, AddToModelFiles_Click);
            contextMenu.Items.Add("Add to Data Files", null, AddToDataFiles_Click);
            contextMenu.Items.Add("Set as Settings File", null, SetAsSettingsFile_Click);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Open", null, OpenFile_Click);
            contextMenu.Items.Add("Show in Explorer", null, ShowInExplorer_Click);
            fileTreeView.ContextMenuStrip = contextMenu;

            panel.Controls.Add(fileTreeView);
            tab.Controls.Add(panel);
        }

        private void CreateConfigurationsTab(TabPage tab)
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            // Tree view for configurations
            configurationsTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                FullRowSelect = true
            };
            configurationsTreeView.NodeMouseDoubleClick += ConfigurationsTree_NodeDoubleClick;
            configurationsTreeView.NodeMouseClick += ConfigurationsTree_NodeMouseClick;

            panel.Controls.Add(configurationsTreeView);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };

            var newButton = new Button { Text = "New", Width = 60, Height = 30 };
            newButton.Click += (s, e) => NewConfiguration_Click();
            buttonPanel.Controls.Add(newButton);

            var editButton = new Button { Text = "Edit", Width = 60, Height = 30 };
            editButton.Click += (s, e) => EditConfiguration_Click(s, e);
            buttonPanel.Controls.Add(editButton);

            var runButton = new Button 
            { 
                Text = "Run", 
                Width = 60, 
                Height = 30 
            };
            runButton.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            runButton.Click += (s, e) => RunConfiguration_Click(s, e);
            buttonPanel.Controls.Add(runButton);

            panel.Controls.Add(buttonPanel);
            tab.Controls.Add(panel);

            RefreshConfigurations();
        }

        private ImageList CreateImageList()
        {
            var imageList = new ImageList();
            // Add images for folder, file types, etc.
            // For simplicity, we'll use text-based icons
            return imageList;
        }

        public void SetRootDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                OutputMessageRequested?.Invoke(this, $"ERROR: Directory not found: {path}");
                return;
            }

            currentRootDirectory = path;
            LoadFileTree(path);
        }

        private void LoadFileTree(string rootPath)
        {
            fileTreeView.Nodes.Clear();

            var rootNode = new TreeNode(Path.GetFileName(rootPath) ?? rootPath)
            {
                Tag = rootPath,
                ImageIndex = 0,
                SelectedImageIndex = 0
            };

            LoadDirectory(rootNode, rootPath);
            fileTreeView.Nodes.Add(rootNode);
            rootNode.Expand();
        }

        private void LoadDirectory(TreeNode parentNode, string path)
        {
            try
            {
                // Load subdirectories
                var directories = Directory.GetDirectories(path);
                foreach (var dir in directories.OrderBy(d => d))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(".") || dirName.StartsWith("$"))
                        continue;

                    var dirNode = new TreeNode($"📁 {dirName}")
                    {
                        Tag = dir
                    };

                    LoadDirectory(dirNode, dir);
                    parentNode.Nodes.Add(dirNode);
                }

                // Load files
                var files = Directory.GetFiles(path);
                var relevantExtensions = new[] { ".mod", ".dat", ".json", ".txt", ".lp", ".mps" };

                foreach (var file in files.OrderBy(f => f))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (relevantExtensions.Contains(ext))
                    {
                        var icon = ext switch
                        {
                            ".mod" => "📝",
                            ".dat" => "📊",
                            ".json" => "⚙️",
                            _ => "📄"
                        };

                        var fileNode = new TreeNode($"{icon} {Path.GetFileName(file)}")
                        {
                            Tag = file
                        };
                        parentNode.Nodes.Add(fileNode);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have access to
            }
        }

        public void RefreshConfigurations()
        {
            configManager.LoadAll();
            configurationsTreeView.Nodes.Clear();

            foreach (var config in configManager.GetAll())
            {
                bool isActive = activeConfiguration != null && activeConfiguration.Id == config.Id;
                string prefix = isActive ? "▶ " : "📦 ";

                var configNode = new TreeNode($"{prefix}{config.Name}")
                {
                    Tag = config,
                    NodeFont = new Font(configurationsTreeView.Font, FontStyle.Bold)
                };

                if (isActive)
                {
                    configNode.ForeColor = Color.FromArgb(0, 122, 204);
                }

                // Model Files category
                var modelNode = new TreeNode($"📝 Model Files ({config.ModelFiles.Count})")
                {
                    Tag = "category:model"
                };
                foreach (var file in config.ModelFiles)
                {
                    modelNode.Nodes.Add(new TreeNode($"📝 {Path.GetFileName(file)}")
                    {
                        Tag = new ConfigFileEntry(config, "model", file),
                        ToolTipText = file
                    });
                }
                configNode.Nodes.Add(modelNode);

                // Data Files category
                var dataNode = new TreeNode($"📊 Data Files ({config.DataFiles.Count})")
                {
                    Tag = "category:data"
                };
                foreach (var file in config.DataFiles)
                {
                    dataNode.Nodes.Add(new TreeNode($"📊 {Path.GetFileName(file)}")
                    {
                        Tag = new ConfigFileEntry(config, "data", file),
                        ToolTipText = file
                    });
                }
                configNode.Nodes.Add(dataNode);

                // Settings file
                if (!string.IsNullOrEmpty(config.SettingsFile))
                {
                    var settingsNode = new TreeNode($"⚙️ {Path.GetFileName(config.SettingsFile)}")
                    {
                        Tag = new ConfigFileEntry(config, "settings", config.SettingsFile),
                        ToolTipText = config.SettingsFile
                    };
                    configNode.Nodes.Add(settingsNode);
                }

                configurationsTreeView.Nodes.Add(configNode);
                configNode.Expand();
            }

            configurationsTreeView.ShowNodeToolTips = true;
        }

        public RunConfiguration? GetSelectedConfiguration()
        {
            var node = configurationsTreeView.SelectedNode;
            while (node != null)
            {
                if (node.Tag is RunConfiguration config)
                    return config;
                node = node.Parent;
            }
            return null;
        }

        private RunConfiguration? GetConfigForNode(TreeNode? node)
        {
            while (node != null)
            {
                if (node.Tag is RunConfiguration config)
                    return config;
                node = node.Parent;
            }
            return null;
        }

        private void ConfigurationsTree_NodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is ConfigFileEntry entry && File.Exists(entry.FilePath))
            {
                FileDoubleClicked?.Invoke(this, entry.FilePath);
            }
            else if (e.Node.Tag is RunConfiguration config)
            {
                SetActiveConfiguration(config);
                ConfigurationSelected?.Invoke(this, config);
            }
        }

        public void SetActiveConfiguration(RunConfiguration? config)
        {
            activeConfiguration = config;
            RefreshConfigurations();
        }

        public RunConfiguration? ActiveConfiguration => activeConfiguration;

        private void ConfigurationsTree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            configurationsTreeView.SelectedNode = e.Node;

            if (e.Node.Tag is ConfigFileEntry)
            {
                ShowFileContextMenu(e.Node, e.Location);
            }
            else if (e.Node.Tag is RunConfiguration)
            {
                ShowConfigContextMenu(e.Node, e.Location);
            }
        }

        private void ShowConfigContextMenu(TreeNode node, Point location)
        {
            var menu = new ContextMenuStrip();

            var runItem = new ToolStripMenuItem("Run", null, RunConfiguration_Click);
            runItem.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(runItem);

            menu.Items.Add("Parse", null, (s, e) =>
            {
                if (GetSelectedConfiguration() is RunConfiguration config)
                    ParseConfigurationRequested?.Invoke(this, config);
            });

            menu.Items.Add(new ToolStripSeparator());

            var setActiveItem = new ToolStripMenuItem("Set as Active", null, (s, e) =>
            {
                if (GetSelectedConfiguration() is RunConfiguration config)
                {
                    SetActiveConfiguration(config);
                    ConfigurationSelected?.Invoke(this, config);
                }
            });
            bool isAlreadyActive = node.Tag is RunConfiguration cfg
                && activeConfiguration != null && activeConfiguration.Id == cfg.Id;
            setActiveItem.Checked = isAlreadyActive;
            menu.Items.Add(setActiveItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Edit", null, EditConfiguration_Click);
            menu.Items.Add("Duplicate", null, DuplicateConfiguration_Click);
            menu.Items.Add("Rename", null, RenameConfiguration_Click);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Export...", null, ExportConfiguration_Click);
            menu.Items.Add(new ToolStripSeparator());

            var deleteItem = new ToolStripMenuItem("Delete", null, DeleteConfiguration_Click);
            deleteItem.ForeColor = Color.Red;
            menu.Items.Add(deleteItem);

            menu.Show(configurationsTreeView, location);
        }

        private void ShowFileContextMenu(TreeNode node, Point location)
        {
            var menu = new ContextMenuStrip();

            menu.Items.Add("Open", null, (s, e) =>
            {
                if (node.Tag is ConfigFileEntry entry && File.Exists(entry.FilePath))
                    FileDoubleClicked?.Invoke(this, entry.FilePath);
            });

            menu.Items.Add(new ToolStripSeparator());

            var removeItem = new ToolStripMenuItem("Remove from Configuration", null, (s, e) =>
            {
                if (node.Tag is not ConfigFileEntry entry) return;

                var config = entry.Configuration;
                switch (entry.Category)
                {
                    case "model":
                        config.ModelFiles.Remove(entry.FilePath);
                        break;
                    case "data":
                        config.DataFiles.Remove(entry.FilePath);
                        break;
                    case "settings":
                        config.SettingsFile = null;
                        break;
                }

                configManager.Save(config);
                RefreshConfigurations();
            });
            removeItem.ForeColor = Color.Red;
            menu.Items.Add(removeItem);

            menu.Show(configurationsTreeView, location);
        }

        // Event Handlers
        private void OpenFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SetRootDirectory(dialog.SelectedPath);
                }
            }
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(currentRootDirectory))
            {
                LoadFileTree(currentRootDirectory);
            }
            RefreshConfigurations();
        }

        private void FileTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is string filePath && File.Exists(filePath))
            {
                FileDoubleClicked?.Invoke(this, filePath);
            }
        }

        private void NewConfiguration_Click()
        {
            using (var dialog = new RunConfigurationDialog(configManager))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    RefreshConfigurations();
                }
            }
        }

        private void EditConfiguration_Click(object sender, EventArgs e)
        {
            if (GetSelectedConfiguration() is RunConfiguration config)
            {
                using (var dialog = new RunConfigurationDialog(configManager, config))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        RefreshConfigurations();
                    }
                }
            }
        }

        private void RunConfiguration_Click(object sender, EventArgs e)
        {
            if (GetSelectedConfiguration() is RunConfiguration config)
            {
                RunConfigurationRequested?.Invoke(this, config);
            }
        }

        private void DuplicateConfiguration_Click(object sender, EventArgs e)
        {
            if (GetSelectedConfiguration() is RunConfiguration config)
            {
                configManager.Duplicate(config.Id);
                RefreshConfigurations();
            }
        }

        private void RenameConfiguration_Click(object sender, EventArgs e)
        {
            if (GetSelectedConfiguration() is RunConfiguration config)
            {
                string newName = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter new name:", 
                    "Rename Configuration", 
                    config.Name);

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    configManager.Rename(config.Id, newName);
                    RefreshConfigurations();
                }
            }
        }

        private void DeleteConfiguration_Click(object sender, EventArgs e)
        {
            if (GetSelectedConfiguration() is RunConfiguration config)
            {
                var result = MessageBox.Show(
                    $"Delete configuration '{config.Name}'?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    configManager.Delete(config.Id);
                    RefreshConfigurations();
                }
            }
        }

        private void ExportConfiguration_Click(object sender, EventArgs e)
        {
            if (GetSelectedConfiguration() is RunConfiguration config)
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Configuration Files (*.json)|*.json";
                    dialog.FileName = $"{config.Name}.json";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        configManager.Export(config, dialog.FileName);
                        OutputMessageRequested?.Invoke(this, $"Configuration exported: {dialog.FileName}");
                    }
                }
            }
        }

        private void AddToModelFiles_Click(object sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode?.Tag is string filePath && GetSelectedConfiguration() is RunConfiguration config)
            {
                if (!config.ModelFiles.Contains(filePath))
                {
                    config.ModelFiles.Add(filePath);
                    configManager.Save(config);
                    RefreshConfigurations();
                }
            }
        }

        private void AddToDataFiles_Click(object sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode?.Tag is string filePath && GetSelectedConfiguration() is RunConfiguration config)
            {
                if (!config.DataFiles.Contains(filePath))
                {
                    config.DataFiles.Add(filePath);
                    configManager.Save(config);
                    RefreshConfigurations();
                }
            }
        }

        private void SetAsSettingsFile_Click(object sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode?.Tag is string filePath && GetSelectedConfiguration() is RunConfiguration config)
            {
                config.SettingsFile = filePath;
                configManager.Save(config);
                RefreshConfigurations();
            }
        }

        private void OpenFile_Click(object sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode?.Tag is string filePath)
            {
                FileDoubleClicked?.Invoke(this, filePath);
            }
        }

        private void ShowInExplorer_Click(object sender, EventArgs e)
        {
            if (fileTreeView.SelectedNode?.Tag is string path)
            {
                var dirPath = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                if (!string.IsNullOrEmpty(dirPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", dirPath);
                }
            }
        }
    }

    /// <summary>
    /// Represents a file entry within a run configuration, used as TreeNode.Tag
    /// </summary>
    internal sealed class ConfigFileEntry
    {
        public RunConfiguration Configuration { get; }
        public string Category { get; }
        public string FilePath { get; }

        public ConfigFileEntry(RunConfiguration configuration, string category, string filePath)
        {
            Configuration = configuration;
            Category = category;
            FilePath = filePath;
        }
    }
}