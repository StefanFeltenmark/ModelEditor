using System;
using System.Drawing;
using System.Windows.Forms;
using Core.Services;
using Core.Models;

namespace GUI
{
    public partial class MainForm : Form
    {
        private readonly RunConfigurationManager configManager;
        private SplitContainer mainSplitContainer;
        private FileExplorerPanel fileExplorerPanel;
        private TabControl mainTabControl;

        public MainForm()
        {
            InitializeComponent();
            configManager = new RunConfigurationManager();
            InitializeUI();
            LoadConfigurations();
        }

        private void InitializeUI()
        {
            this.Text = "Optimization Modeler";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create main split container
            mainSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                FixedPanel = FixedPanel.Panel1
            };

            // Left panel - File Explorer
            fileExplorerPanel = new FileExplorerPanel(configManager);
            fileExplorerPanel.Dock = DockStyle.Fill;
            fileExplorerPanel.ConfigurationSelected += FileExplorerPanel_ConfigurationSelected;
            fileExplorerPanel.RunConfigurationRequested += FileExplorerPanel_RunConfigurationRequested;
            fileExplorerPanel.FileDoubleClicked += FileExplorerPanel_FileDoubleClicked;
            
            mainSplitContainer.Panel1.Controls.Add(fileExplorerPanel);

            // Right panel - Main content area
            mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            
            // Add initial tabs
            AddEditorTab("Welcome", "Welcome to Optimization Modeler!\n\nUse the left panel to:\n- Browse files\n- Create run configurations\n- Manage your models");
            
            mainSplitContainer.Panel2.Controls.Add(mainTabControl);

            // Add to form
            this.Controls.Add(mainSplitContainer);

            // Add menu bar
            CreateMenuBar();

            // Add status bar
            CreateStatusBar();
        }

        private void CreateMenuBar()
        {
            var menuStrip = new MenuStrip();

            // File menu
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("&New Model", null, (s, e) => NewModel());
            fileMenu.DropDownItems.Add("&Open File...", null, (s, e) => OpenFile());
            fileMenu.DropDownItems.Add("Open &Folder...", null, (s, e) => OpenFolder());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("&Save", null, (s, e) => SaveCurrentFile());
            fileMenu.DropDownItems.Add("Save &As...", null, (s, e) => SaveFileAs());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => this.Close());

            // Configuration menu
            var configMenu = new ToolStripMenuItem("&Configuration");
            configMenu.DropDownItems.Add("&New Configuration...", null, (s, e) => NewConfiguration());
            configMenu.DropDownItems.Add("&Edit Configuration...", null, (s, e) => EditCurrentConfiguration());
            configMenu.DropDownItems.Add(new ToolStripSeparator());
            configMenu.DropDownItems.Add("&Run Configuration", null, (s, e) => RunCurrentConfiguration());

            // Run menu
            var runMenu = new ToolStripMenuItem("&Run");
            runMenu.DropDownItems.Add("&Run Current Configuration", null, (s, e) => RunCurrentConfiguration());
            runMenu.DropDownItems.Add("Run &Last Configuration", null, (s, e) => RunLastConfiguration());

            // Help menu
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&About", null, (s, e) => ShowAbout());

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(configMenu);
            menuStrip.Items.Add(runMenu);
            menuStrip.Items.Add(helpMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        private void CreateStatusBar()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Ready");
            statusStrip.Items.Add(statusLabel);
            this.Controls.Add(statusStrip);
        }

        private void LoadConfigurations()
        {
            configManager.LoadAll();
        }

        // Event Handlers
        private void FileExplorerPanel_ConfigurationSelected(object sender, RunConfiguration config)
        {
            statusLabel.Text = $"Configuration selected: {config.Name}";
        }

        private void FileExplorerPanel_RunConfigurationRequested(object sender, RunConfiguration config)
        {
            RunConfiguration(config);
        }

        private void FileExplorerPanel_FileDoubleClicked(object sender, string filePath)
        {
            OpenFileInEditor(filePath);
        }

        // File Operations
        private void NewModel()
        {
            AddEditorTab("New Model", "// New Model File\n");
        }

        private void OpenFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Model Files (*.mod)|*.mod|Data Files (*.dat)|*.dat|All Files (*.*)|*.*";
                dialog.Multiselect = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OpenFileInEditor(dialog.FileName);
                }
            }
        }

        private void OpenFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder to explore";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    fileExplorerPanel.SetRootDirectory(dialog.SelectedPath);
                    statusLabel.Text = $"Opened folder: {dialog.SelectedPath}";
                }
            }
        }

        private void OpenFileInEditor(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check if already open
            foreach (TabPage tab in mainTabControl.TabPages)
            {
                if (tab.Tag is string existingPath && existingPath == filePath)
                {
                    mainTabControl.SelectedTab = tab;
                    return;
                }
            }

            // Read file content
            string content = System.IO.File.ReadAllText(filePath);
            string fileName = System.IO.Path.GetFileName(filePath);

            var editorTab = AddEditorTab(fileName, content);
            editorTab.Tag = filePath;
        }

        private TabPage AddEditorTab(string title, string content)
        {
            var tabPage = new TabPage(title);
            
            var textBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                Text = content,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };

            tabPage.Controls.Add(textBox);
            mainTabControl.TabPages.Add(tabPage);
            mainTabControl.SelectedTab = tabPage;

            return tabPage;
        }

        private void SaveCurrentFile()
        {
            if (mainTabControl.SelectedTab?.Tag is string filePath)
            {
                var textBox = mainTabControl.SelectedTab.Controls[0] as TextBox;
                if (textBox != null)
                {
                    System.IO.File.WriteAllText(filePath, textBox.Text);
                    statusLabel.Text = $"Saved: {filePath}";
                }
            }
            else
            {
                SaveFileAs();
            }
        }

        private void SaveFileAs()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Model Files (*.mod)|*.mod|Data Files (*.dat)|*.dat|All Files (*.*)|*.*";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var textBox = mainTabControl.SelectedTab?.Controls[0] as TextBox;
                    if (textBox != null)
                    {
                        System.IO.File.WriteAllText(dialog.FileName, textBox.Text);
                        mainTabControl.SelectedTab.Tag = dialog.FileName;
                        mainTabControl.SelectedTab.Text = System.IO.Path.GetFileName(dialog.FileName);
                        statusLabel.Text = $"Saved as: {dialog.FileName}";
                    }
                }
            }
        }

        // Configuration Operations
        private void NewConfiguration()
        {
            using (var dialog = new RunConfigurationDialog(configManager))
            {
                if (dialog.ShowDialog() == DialogResult.OK && dialog.Configuration != null)
                {
                    configManager.Save(dialog.Configuration);
                    fileExplorerPanel.RefreshConfigurations();
                    statusLabel.Text = $"Created configuration: {dialog.Configuration.Name}";
                }
            }
        }

        private void EditCurrentConfiguration()
        {
            var selectedConfig = fileExplorerPanel.GetSelectedConfiguration();
            if (selectedConfig == null)
            {
                MessageBox.Show("Please select a configuration to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new RunConfigurationDialog(configManager, selectedConfig))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    configManager.Save(selectedConfig);
                    fileExplorerPanel.RefreshConfigurations();
                    statusLabel.Text = $"Updated configuration: {selectedConfig.Name}";
                }
            }
        }

        private void RunCurrentConfiguration()
        {
            var selectedConfig = fileExplorerPanel.GetSelectedConfiguration();
            if (selectedConfig != null)
            {
                RunConfiguration(selectedConfig);
            }
            else
            {
                MessageBox.Show("Please select a configuration to run.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RunLastConfiguration()
        {
            var recent = configManager.GetRecent(1).FirstOrDefault();
            if (recent != null)
            {
                RunConfiguration(recent);
            }
            else
            {
                MessageBox.Show("No recent configurations found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void RunConfiguration(RunConfiguration config)
        {
            // Validate files
            if (!config.ValidateFiles(out var missingFiles))
            {
                MessageBox.Show(
                    $"The following files are missing:\n{string.Join("\n", missingFiles)}",
                    "Missing Files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Update last run time
            config.LastRun = DateTime.Now;
            configManager.Save(config);

            statusLabel.Text = $"Running configuration: {config.Name}...";

            // TODO: Integrate with ModelParsingService
            try
            {
                var modelManager = new Core.ModelManager();
                var parser = new Core.EquationParser(modelManager);
                var dataParser = new Core.DataFileParser(modelManager);
                var service = new Core.Services.ModelParsingService(modelManager, parser, dataParser);

                // Load model files
                var modelTexts = config.ModelFiles.Select(f => System.IO.File.ReadAllText(f)).ToList();
                
                // Load data files
                var dataTexts = config.DataFiles.Select(f => System.IO.File.ReadAllText(f)).ToList();

                // Parse
                var result = service.ParseModel(modelTexts, dataTexts);

                // Show results
                if (result.Success)
                {
                    MessageBox.Show(
                        $"Model parsed successfully!\n\nStatements: {result.TotalSuccess}\nErrors: {result.TotalErrors}",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    
                    statusLabel.Text = $"Configuration '{config.Name}' completed successfully";
                }
                else
                {
                    var errorMsg = string.Join("\n", result.Errors.Take(10));
                    MessageBox.Show(
                        $"Model parsing failed:\n\n{errorMsg}\n\n(Showing first 10 errors)",
                        "Errors",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    
                    statusLabel.Text = $"Configuration '{config.Name}' failed with {result.TotalErrors} errors";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error running configuration";
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "Optimization Modeler\nVersion 1.0\n\nA tool for creating and solving optimization models.",
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}