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
        private ListBox configurationsListBox;
        private string currentRootDirectory;

        public event EventHandler<RunConfiguration> ConfigurationSelected;
        public event EventHandler<RunConfiguration> RunConfigurationRequested;
        public event EventHandler<string> FileDoubleClicked;

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

            // List box
            configurationsListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "Name"
            };
            configurationsListBox.DoubleClick += ConfigurationsList_DoubleClick;

            // Context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Run", null, RunConfiguration_Click); //{ Font = new Font(contextMenu.Font, FontStyle.Bold) };
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Edit", null, EditConfiguration_Click);
            contextMenu.Items.Add("Duplicate", null, DuplicateConfiguration_Click);
            contextMenu.Items.Add("Rename", null, RenameConfiguration_Click);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Export...", null, ExportConfiguration_Click);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Delete", null, DeleteConfiguration_Click);// { ForeColor = Color.Red };
            configurationsListBox.ContextMenuStrip = contextMenu;

            panel.Controls.Add(configurationsListBox);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };
            buttonPanel.Controls.Add(new Button { Text = "New", Width = 60, Height = 30 });
            buttonPanel.Controls.Add(new Button { Text = "Edit", Width = 60, Height = 30 });
            buttonPanel.Controls.Add(new Button { Text = "Run", Width = 60, Height = 30, Font = new Font("Segoe UI", 9, FontStyle.Bold) });
            
            ((Button)buttonPanel.Controls[0]).Click += (s, e) => NewConfiguration_Click();
            ((Button)buttonPanel.Controls[1]).Click += (s, e) => EditConfiguration_Click(s, e);
            ((Button)buttonPanel.Controls[2]).Click += (s, e) => RunConfiguration_Click(s, e);

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
                MessageBox.Show($"Directory not found: {path}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            configurationsListBox.Items.Clear();
            
            foreach (var config in configManager.GetAll())
            {
                configurationsListBox.Items.Add(config);
            }
        }

        public RunConfiguration GetSelectedConfiguration()
        {
            return configurationsListBox.SelectedItem as RunConfiguration;
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

        private void ConfigurationsList_DoubleClick(object sender, EventArgs e)
        {
            if (configurationsListBox.SelectedItem is RunConfiguration config)
            {
                ConfigurationSelected?.Invoke(this, config);
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
            if (configurationsListBox.SelectedItem is RunConfiguration config)
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
            if (configurationsListBox.SelectedItem is RunConfiguration config)
            {
                RunConfigurationRequested?.Invoke(this, config);
            }
        }

        private void DuplicateConfiguration_Click(object sender, EventArgs e)
        {
            if (configurationsListBox.SelectedItem is RunConfiguration config)
            {
                configManager.Duplicate(config.Id);
                RefreshConfigurations();
            }
        }

        private void RenameConfiguration_Click(object sender, EventArgs e)
        {
            if (configurationsListBox.SelectedItem is RunConfiguration config)
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
            if (configurationsListBox.SelectedItem is RunConfiguration config)
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
            if (configurationsListBox.SelectedItem is RunConfiguration config)
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Configuration Files (*.json)|*.json";
                    dialog.FileName = $"{config.Name}.json";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        configManager.Export(config, dialog.FileName);
                        MessageBox.Show("Configuration exported successfully!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void AddToModelFiles_Click(object sender, EventArgs e)
        {
            // TODO: Implement adding file to current configuration
            MessageBox.Show("Add to model files functionality", "Info");
        }

        private void AddToDataFiles_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Add to data files functionality", "Info");
        }

        private void SetAsSettingsFile_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Set as settings file functionality", "Info");
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
}