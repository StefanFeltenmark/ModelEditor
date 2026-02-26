using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Core;
using Core.Services;
using Core.Models;
using GUI.Controls;

namespace GUI
{
    public partial class MainForm : Form
    {
        private readonly RunConfigurationManager configManager;
        private SplitContainer mainSplitContainer;
        private SplitContainer editorSplitContainer;
        private FileExplorerPanel fileExplorerPanel;
        private TabControl mainTabControl;
        private ResultsPanel resultsPanel;

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
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create main horizontal split (left: explorer, right: editor+results)
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

            // Right panel - Create vertical split (top: editor, bottom: results)
            editorSplitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 500
            };

            // Top: Editor tabs
            mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            
            AddEditorTab("Welcome", GetWelcomeMessage());
            editorSplitContainer.Panel1.Controls.Add(mainTabControl);

            // Bottom: Results panel
            resultsPanel = new ResultsPanel
            {
                Dock = DockStyle.Fill
            };
            resultsPanel.ErrorDoubleClicked += ResultsPanel_ErrorDoubleClicked;
            editorSplitContainer.Panel2.Controls.Add(resultsPanel);

            mainSplitContainer.Panel2.Controls.Add(editorSplitContainer);

            // Add to form
            this.Controls.Add(mainSplitContainer);

            // Add menu bar
            CreateMenuBar();

            // Add toolbar
            CreateToolbar();

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
            fileMenu.DropDownItems.Add("&Save", null, (s, e) => SaveCurrentFile()).ShortcutKeys = Keys.Control | Keys.S;
            fileMenu.DropDownItems.Add("Save &As...", null, (s, e) => SaveFileAs());
            fileMenu.DropDownItems.Add("Save All", null, (s, e) => SaveAllFiles());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => this.Close());

            // Edit menu
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add("&Undo", null, (s, e) => Undo()).ShortcutKeys = Keys.Control | Keys.Z;
            editMenu.DropDownItems.Add("&Redo", null, (s, e) => Redo()).ShortcutKeys = Keys.Control | Keys.Y;
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("Cu&t", null, (s, e) => Cut()).ShortcutKeys = Keys.Control | Keys.X;
            editMenu.DropDownItems.Add("&Copy", null, (s, e) => Copy()).ShortcutKeys = Keys.Control | Keys.C;
            editMenu.DropDownItems.Add("&Paste", null, (s, e) => Paste()).ShortcutKeys = Keys.Control | Keys.V;
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("&Find...", null, (s, e) => Find()).ShortcutKeys = Keys.Control | Keys.F;
            editMenu.DropDownItems.Add("&Replace...", null, (s, e) => Replace()).ShortcutKeys = Keys.Control | Keys.H;

            // View menu
            var viewMenu = new ToolStripMenuItem("&View");
            var toggleExplorerItem = new ToolStripMenuItem("&File Explorer", null, (s, e) => ToggleFileExplorer());
            toggleExplorerItem.Checked = true;
            viewMenu.DropDownItems.Add(toggleExplorerItem);
            
            var toggleResultsItem = new ToolStripMenuItem("&Results Panel", null, (s, e) => ToggleResultsPanel());
            toggleResultsItem.Checked = true;
            viewMenu.DropDownItems.Add(toggleResultsItem);

            // Configuration menu
            var configMenu = new ToolStripMenuItem("&Configuration");
            configMenu.DropDownItems.Add("&New Configuration...", null, (s, e) => NewConfiguration());
            configMenu.DropDownItems.Add("&Edit Configuration...", null, (s, e) => EditCurrentConfiguration());
            configMenu.DropDownItems.Add(new ToolStripSeparator());
            configMenu.DropDownItems.Add("&Run Configuration", null, (s, e) => RunCurrentConfiguration()).ShortcutKeys = Keys.F5;

            // Run menu
            var runMenu = new ToolStripMenuItem("&Run");
            runMenu.DropDownItems.Add("&Run Current Configuration", null, (s, e) => RunCurrentConfiguration()).ShortcutKeys = Keys.F5;
            runMenu.DropDownItems.Add("Run &Last Configuration", null, (s, e) => RunLastConfiguration()).ShortcutKeys = Keys.Control | Keys.F5;
            runMenu.DropDownItems.Add(new ToolStripSeparator());
            runMenu.DropDownItems.Add("&Parse Current File", null, (s, e) => ParseCurrentFile()).ShortcutKeys = Keys.F6;

            // Help menu
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&Documentation", null, (s, e) => ShowDocumentation());
            helpMenu.DropDownItems.Add("&About", null, (s, e) => ShowAbout());

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(editMenu);
            menuStrip.Items.Add(viewMenu);
            menuStrip.Items.Add(configMenu);
            menuStrip.Items.Add(runMenu);
            menuStrip.Items.Add(helpMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        private void CreateToolbar()
        {
            var toolStrip = new ToolStrip
            {
                ImageScalingSize = new Size(24, 24),
                GripStyle = ToolStripGripStyle.Hidden
            };

            toolStrip.Items.Add(new ToolStripButton("📄 New", null, (s, e) => NewModel()) { ToolTipText = "New Model" });
            toolStrip.Items.Add(new ToolStripButton("📁 Open", null, (s, e) => OpenFile()) { ToolTipText = "Open File" });
            toolStrip.Items.Add(new ToolStripButton("💾 Save", null, (s, e) => SaveCurrentFile()) { ToolTipText = "Save" });
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(new ToolStripButton("▶️ Run", null, (s, e) => RunCurrentConfiguration()) 
            { 
                ToolTipText = "Run Configuration (F5)",
                Font = new Font(toolStrip.Font, FontStyle.Bold)
            });
            toolStrip.Items.Add(new ToolStripButton("🔍 Parse", null, (s, e) => ParseCurrentFile()) { ToolTipText = "Parse Current File (F6)" });
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(new ToolStripButton("🗑️ Clear Results", null, (s, e) => resultsPanel.Clear()) { ToolTipText = "Clear Results" });

            this.Controls.Add(toolStrip);
        }

        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel configLabel;

        private void CreateStatusBar()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            configLabel = new ToolStripStatusLabel("No Configuration") { TextAlign = ContentAlignment.MiddleRight };
            
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(new ToolStripStatusLabel("|"));
            statusStrip.Items.Add(configLabel);
            
            this.Controls.Add(statusStrip);
        }

        private void LoadConfigurations()
        {
            configManager.LoadAll();
        }

        private string GetWelcomeMessage()
        {
            return @"Welcome to Optimization Modeler!

Getting Started:
1. Use the File Explorer panel (left) to browse your project files
2. Create a Run Configuration to organize your model, data, and settings files
3. Click 'Run' (F5) to parse and solve your optimization model

Features:
• Multiple file editor with syntax highlighting
• Docked results panel showing errors and model structure
• Run configurations for managing multiple scenarios
• File explorer with context menus

Keyboard Shortcuts:
• F5  - Run current configuration
• F6  - Parse current file
• Ctrl+S - Save current file
• Ctrl+O - Open file

For help, press F1 or visit the documentation.
";
        }

        // Event Handlers
        private void FileExplorerPanel_ConfigurationSelected(object sender, RunConfiguration config)
        {
            configLabel.Text = $"Config: {config.Name}";
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

        private void ResultsPanel_ErrorDoubleClicked(object sender, ErrorNavigationEventArgs e)
        {
            // Navigate to error line in current editor
            if (mainTabControl.SelectedTab?.Controls[0] is TextBox textBox)
            {
                try
                {
                    var lines = textBox.Lines;
                    if (e.LineNumber > 0 && e.LineNumber <= lines.Length)
                    {
                        int charIndex = 0;
                        for (int i = 0; i < e.LineNumber - 1; i++)
                        {
                            charIndex += lines[i].Length + Environment.NewLine.Length;
                        }

                        textBox.Select(charIndex, lines[e.LineNumber - 1].Length);
                        textBox.ScrollToCaret();
                        textBox.Focus();
                    }
                }
                catch { }
            }
        }

        // File Operations (implementations from previous code)
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

        private void SaveAllFiles()
        {
            foreach (TabPage tab in mainTabControl.TabPages)
            {
                if (tab.Tag is string filePath && tab.Controls[0] is TextBox textBox)
                {
                    System.IO.File.WriteAllText(filePath, textBox.Text);
                }
            }
            statusLabel.Text = "All files saved";
        }

        private void ParseCurrentFile()
        {
            var textBox = mainTabControl.SelectedTab?.Controls[0] as TextBox;
            if (textBox == null)
            {
                MessageBox.Show("No file open to parse", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            statusLabel.Text = "Parsing...";
            resultsPanel.Clear();

            try
            {
                var modelManager = new ModelManager();
                var parser = new EquationParser(modelManager);

                var result = parser.Parse(textBox.Text);

                resultsPanel.ShowResults(result, modelManager);
                statusLabel.Text = result.Success 
                    ? $"Parsing completed: {result.TotalSuccess} statements, {result.TotalErrors} errors"
                    : $"Parsing failed: {result.TotalErrors} errors";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error parsing file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Parsing error";
            }
        }

        private void RunConfiguration(RunConfiguration config)
        {
            if (!config.ValidateFiles(out var missingFiles))
            {
                MessageBox.Show(
                    $"The following files are missing:\n{string.Join("\n", missingFiles)}",
                    "Missing Files",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            config.LastRun = DateTime.Now;
            configManager.Save(config);

            configLabel.Text = $"Config: {config.Name}";
            statusLabel.Text = $"Running configuration: {config.Name}...";
            resultsPanel.Clear();

            try
            {
                var modelManager = new ModelManager();
                var parser = new EquationParser(modelManager);
                var dataParser = new DataFileParser(modelManager);
                var service = new ModelParsingService(modelManager, parser, dataParser);

                var modelTexts = config.ModelFiles.Select(f => System.IO.File.ReadAllText(f)).ToList();
                var dataTexts = config.DataFiles.Select(f => System.IO.File.ReadAllText(f)).ToList();

                var result = service.ParseModel(modelTexts, dataTexts);

                resultsPanel.ShowResults(result, modelManager);
                
                statusLabel.Text = result.Success
                    ? $"Configuration '{config.Name}' completed successfully"
                    : $"Configuration '{config.Name}' failed with {result.TotalErrors} errors";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Error running configuration";
            }
        }

        // Edit operations
        private void Undo()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is TextBox textBox)
                textBox.Undo();
        }

        private void Redo()
        {
            // TextBox doesn't have built-in Redo
            MessageBox.Show("Redo is not available for basic TextBox. Consider using RichTextBox or a third-party editor.");
        }

        private void Cut()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is TextBox textBox)
                textBox.Cut();
        }

        private void Copy()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is TextBox textBox)
                textBox.Copy();
        }

        private void Paste()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is TextBox textBox)
                textBox.Paste();
        }

        private void Find()
        {
            MessageBox.Show("Find dialog not yet implemented", "Info");
        }

        private void Replace()
        {
            MessageBox.Show("Replace dialog not yet implemented", "Info");
        }

        // View operations
        private void ToggleFileExplorer()
        {
            mainSplitContainer.Panel1Collapsed = !mainSplitContainer.Panel1Collapsed;
        }

        private void ToggleResultsPanel()
        {
            editorSplitContainer.Panel2Collapsed = !editorSplitContainer.Panel2Collapsed;
        }

        // Configuration operations (from previous implementation)
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

        private void ShowDocumentation()
        {
            MessageBox.Show("Documentation will open in your default browser.", "Documentation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "Optimization Modeler\nVersion 1.0\n\nA professional tool for creating and solving optimization models.\n\nFeatures:\n• Advanced model editor\n• Run configurations\n• Comprehensive results panel\n• File explorer",
                "About Optimization Modeler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}