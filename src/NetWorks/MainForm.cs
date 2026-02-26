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
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Padding = new Point(12, 4)
            };
            mainTabControl.DrawItem += MainTabControl_DrawItem;
            mainTabControl.MouseDown += MainTabControl_MouseDown;
            mainTabControl.MouseMove += MainTabControl_MouseMove;

            // Context menu for tabs
            var tabContextMenu = new ContextMenuStrip();
            tabContextMenu.Items.Add("Close", null, (s, e) => CloseTab(mainTabControl.SelectedTab));
            tabContextMenu.Items.Add("Close Others", null, (s, e) => CloseOtherTabs(mainTabControl.SelectedTab));
            tabContextMenu.Items.Add("Close All", null, (s, e) => CloseAllTabs());
            mainTabControl.ContextMenuStrip = tabContextMenu;
            
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
            ((ToolStripMenuItem)fileMenu.DropDownItems.Add("&Save", null, (s, e) => SaveCurrentFile())).ShortcutKeys = Keys.Control | Keys.S;
            fileMenu.DropDownItems.Add("Save &As...", null, (s, e) => SaveFileAs());
            fileMenu.DropDownItems.Add("Save All", null, (s, e) => SaveAllFiles());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => this.Close());

            // Edit menu
            var editMenu = new ToolStripMenuItem("&Edit");
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("&Undo", null, (s, e) => Undo())).ShortcutKeys = Keys.Control | Keys.Z;
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("&Redo", null, (s, e) => Redo())).ShortcutKeys = Keys.Control | Keys.Y;
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("Cu&t", null, (s, e) => Cut())).ShortcutKeys = Keys.Control | Keys.X;
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("&Copy", null, (s, e) => Copy())).ShortcutKeys = Keys.Control | Keys.C;
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("&Paste", null, (s, e) => Paste())).ShortcutKeys = Keys.Control | Keys.V;
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("&Find...", null, (s, e) => Find())).ShortcutKeys = Keys.Control | Keys.F;
            ((ToolStripMenuItem)editMenu.DropDownItems.Add("&Replace...", null, (s, e) => Replace())).ShortcutKeys = Keys.Control | Keys.H;

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
            ((ToolStripMenuItem)configMenu.DropDownItems.Add("&Run Configuration", null, (s, e) => RunCurrentConfiguration())).ShortcutKeys = Keys.F5;

            // Run menu
            var runMenu = new ToolStripMenuItem("&Run");
            ((ToolStripMenuItem)runMenu.DropDownItems.Add("&Run Current Configuration", null, (s, e) => RunCurrentConfiguration())).ShortcutKeys = Keys.F5;
            ((ToolStripMenuItem)runMenu.DropDownItems.Add("Run &Last Configuration", null, (s, e) => RunLastConfiguration())).ShortcutKeys = Keys.Control | Keys.F5;
            runMenu.DropDownItems.Add(new ToolStripSeparator());
            ((ToolStripMenuItem)runMenu.DropDownItems.Add("&Parse Current File", null, (s, e) => ParseCurrentFile())).ShortcutKeys = Keys.F6;

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
• Ctrl+W - Close current tab
• Middle-click tab to close

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
            if (mainTabControl.SelectedTab?.Controls[0] is RichTextBox textBox)
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

            var textBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                Text = content,
                WordWrap = false,
                AcceptsTab = true,
                DetectUrls = false,
                BackColor = SystemColors.Window
            };

            tabPage.Controls.Add(textBox);
            mainTabControl.TabPages.Add(tabPage);
            mainTabControl.SelectedTab = tabPage;

            return tabPage;
        }

        #region Tab Close Functionality

        private void MainTabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tabPage = mainTabControl.TabPages[e.Index];
            var tabRect = mainTabControl.GetTabRect(e.Index);

            // Draw tab background
            bool isSelected = mainTabControl.SelectedIndex == e.Index;
            using (var bgBrush = new SolidBrush(isSelected ? SystemColors.Window : SystemColors.Control))
            {
                e.Graphics.FillRectangle(bgBrush, tabRect);
            }

            // Draw tab title text (leave room for close button)
            var titleRect = new Rectangle(tabRect.X + 4, tabRect.Y + 4, tabRect.Width - 22, tabRect.Height - 4);
            TextRenderer.DrawText(e.Graphics, tabPage.Text, mainTabControl.Font,
                titleRect, SystemColors.ControlText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Draw close button (×)
            var closeRect = GetCloseButtonRect(tabRect);
            var closeColor = isSelected ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160);
            using (var closeBrush = new SolidBrush(closeColor))
            using (var closeFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("×", closeFont, closeBrush, closeRect, sf);
            }
        }

        private void MainTabControl_MouseDown(object? sender, MouseEventArgs e)
        {
            for (int i = 0; i < mainTabControl.TabPages.Count; i++)
            {
                var tabRect = mainTabControl.GetTabRect(i);
                var closeRect = GetCloseButtonRect(tabRect);

                if (closeRect.Contains(e.Location) && e.Button == MouseButtons.Left)
                {
                    CloseTab(mainTabControl.TabPages[i]);
                    return;
                }
            }

            // Middle-click anywhere on a tab to close it
            if (e.Button == MouseButtons.Middle)
            {
                for (int i = 0; i < mainTabControl.TabPages.Count; i++)
                {
                    if (mainTabControl.GetTabRect(i).Contains(e.Location))
                    {
                        CloseTab(mainTabControl.TabPages[i]);
                        return;
                    }
                }
            }
        }

        private void MainTabControl_MouseMove(object? sender, MouseEventArgs e)
        {
            for (int i = 0; i < mainTabControl.TabPages.Count; i++)
            {
                var tabRect = mainTabControl.GetTabRect(i);
                var closeRect = GetCloseButtonRect(tabRect);

                if (closeRect.Contains(e.Location))
                {
                    this.Cursor = Cursors.Hand;
                    return;
                }
            }
            this.Cursor = Cursors.Default;
        }

        private static Rectangle GetCloseButtonRect(Rectangle tabRect)
        {
            return new Rectangle(tabRect.Right - 18, tabRect.Top + (tabRect.Height - 14) / 2, 14, 14);
        }

        private void CloseTab(TabPage? tab)
        {
            if (tab == null) return;

            mainTabControl.TabPages.Remove(tab);
            tab.Dispose();

            if (mainTabControl.TabPages.Count == 0)
            {
                statusLabel.Text = "Ready";
            }
        }

        private void CloseOtherTabs(TabPage? keepTab)
        {
            if (keepTab == null) return;

            var tabsToClose = mainTabControl.TabPages.Cast<TabPage>()
                .Where(t => t != keepTab).ToList();

            foreach (var tab in tabsToClose)
            {
                mainTabControl.TabPages.Remove(tab);
                tab.Dispose();
            }
        }

        private void CloseAllTabs()
        {
            var allTabs = mainTabControl.TabPages.Cast<TabPage>().ToList();
            foreach (var tab in allTabs)
            {
                mainTabControl.TabPages.Remove(tab);
                tab.Dispose();
            }
            statusLabel.Text = "Ready";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.W))
            {
                CloseTab(mainTabControl.SelectedTab);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        private void SaveCurrentFile()
        {
            if (mainTabControl.SelectedTab?.Tag is string filePath)
            {
                var textBox = mainTabControl.SelectedTab.Controls[0] as RichTextBox;
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
                    var textBox = mainTabControl.SelectedTab?.Controls[0] as RichTextBox;
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
                if (tab.Tag is string filePath && tab.Controls[0] is RichTextBox textBox)
                {
                    System.IO.File.WriteAllText(filePath, textBox.Text);
                }
            }
            statusLabel.Text = "All files saved";
        }

        private void ParseCurrentFile()
        {
            var textBox = mainTabControl.SelectedTab?.Controls[0] as RichTextBox;
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
                statusLabel.Text = !result.HasErrors 
                    ? $"Parsing completed: {result.SuccessCount} statements, {result.Errors.Count} errors"
                    : $"Parsing failed: {result.Errors.Count} errors";
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

                // Convert ParseResult to ParseSessionResult for the results panel
                var sessionResult = new ParseSessionResult();
                for (int i = 0; i < result.TotalSuccess; i++)
                    sessionResult.IncrementSuccess();
                foreach (var err in result.Errors)
                    sessionResult.AddError(err, 0);

                resultsPanel.ShowResults(sessionResult, modelManager);

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
            if (mainTabControl.SelectedTab?.Controls[0] is RichTextBox textBox)
                textBox.Undo();
        }

        private void Redo()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is RichTextBox textBox)
                textBox.Redo();
        }

        private void Cut()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is RichTextBox textBox)
                textBox.Cut();
        }

        private void Copy()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is RichTextBox textBox)
                textBox.Copy();
        }

        private void Paste()
        {
            if (mainTabControl.SelectedTab?.Controls[0] is RichTextBox textBox)
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
