using System.Text.RegularExpressions;
using Core;
using ModelEditorApp.Models;
using ModelEditorApp.Services;

namespace ModelEditorApp
{
    public partial class Form1 : Form
    {
        private readonly ModelManager modelManager;
        private readonly EquationParser parser;
        private readonly DataFileParser dataParser;
        private readonly SyntaxHighlighter syntaxHighlighter;
        private readonly SessionManager sessionManager;
        private System.Windows.Forms.Timer highlightTimer;
        private bool isLoadingFile = false;
        private bool isHighlighting = false;
        private readonly List<FileTab> fileTabs = new List<FileTab>();
        private readonly ModelParsingService parsingService;

        public Form1()
        {
            InitializeComponent();
            
            // Initialize services
            modelManager = new ModelManager();
            parser = new EquationParser(modelManager);
            dataParser = new DataFileParser(modelManager);
            parsingService = new ModelParsingService(modelManager, parser, dataParser);
            sessionManager = new SessionManager();
            syntaxHighlighter = new SyntaxHighlighter(null!);
            
            // Initialize timer for delayed syntax highlighting
            highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 300;
            highlightTimer.Tick += HighlightTimer_Tick;
            
            // Enable double buffering
            this.DoubleBuffered = true;
            
            // Handle form closing event
            this.FormClosing += Form1_FormClosing;
            
            // Try to restore previous session
            if (!RestoreSession())
            {
                // Create initial tab if no session was restored
                CreateNewTab(FileType.Model);
            }
            
            UpdateStatusBar();
        }

        private FileTab? GetActiveTab()
        {
            if (tabControl1.SelectedIndex >= 0 && tabControl1.SelectedIndex < fileTabs.Count)
            {
                return fileTabs[tabControl1.SelectedIndex];
            }
            return null;
        }

        private RichTextBox? GetActiveEditor()
        {
            return GetActiveTab()?.Editor;
        }

        private FileTab CreateNewTab(FileType fileType, string filePath = "")
        {
            var fileTab = new FileTab(fileType);
            
            // Configure editor
            typeof(RichTextBox).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, fileTab.Editor, new object[] { true });
            
            // Wire up events
            fileTab.Editor.TextChanged += RichTextBox_TextChanged;
            fileTab.Editor.SelectionChanged += RichTextBox_SelectionChanged;
            
            if (!string.IsNullOrEmpty(filePath))
            {
                fileTab.FilePath = filePath;
            }
            
            // Create tab page
            var tabPage = new TabPage(fileTab.DisplayName);
            tabPage.Controls.Add(fileTab.Editor);
            
            // Add to collections
            fileTabs.Add(fileTab);
            tabControl1.TabPages.Add(tabPage);
            tabControl1.SelectedTab = tabPage;
            
            return fileTab;
        }

        private void CloseTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= fileTabs.Count)
                return;
            
            var tab = fileTabs[tabIndex];
            
            // Check for unsaved changes
            if (tab.HasUnsavedChanges)
            {
                string fileName = string.IsNullOrEmpty(tab.FilePath) 
                    ? "Untitled" 
                    : Path.GetFileName(tab.FilePath);
                
                DialogResult result = MessageBox.Show(
                    $"Do you want to save changes to {fileName}?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                switch (result)
                {
                    case DialogResult.Yes:
                        SaveTab(tab);
                        if (tab.HasUnsavedChanges)
                            return;
                        break;
                    case DialogResult.Cancel:
                        return;
                }
            }
            
            // Remove tab
            tabControl1.TabPages.RemoveAt(tabIndex);
            fileTabs.RemoveAt(tabIndex);
            
            // Create new tab if all are closed
            if (fileTabs.Count == 0)
            {
                CreateNewTab(FileType.Model);
            }
        }

        private void UpdateTabDisplay(FileTab tab)
        {
            int index = fileTabs.IndexOf(tab);
            if (index >= 0 && index < tabControl1.TabPages.Count)
            {
                tabControl1.TabPages[index].Text = tab.DisplayName;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Check all tabs for unsaved changes
            foreach (var tab in fileTabs.ToList())
            {
                if (tab.HasUnsavedChanges)
                {
                    int index = fileTabs.IndexOf(tab);
                    tabControl1.SelectedIndex = index;
                    
                    string fileName = string.IsNullOrEmpty(tab.FilePath) 
                        ? "Untitled" 
                        : Path.GetFileName(tab.FilePath);
                    
                    DialogResult result = MessageBox.Show(
                        $"Do you want to save changes to {fileName}?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    switch (result)
                    {
                        case DialogResult.Yes:
                            SaveTab(tab);
                            if (tab.HasUnsavedChanges)
                            {
                                e.Cancel = true;
                                return;
                            }
                            break;
                        case DialogResult.Cancel:
                            e.Cancel = true;
                            return;
                    }
                }
            }

            // Save session state before closing
            SaveSession();
        }

        // Session Management Methods
        private void SaveSession()
        {
            try
            {
                var sessionState = new SessionState
                {
                    ActiveTabIndex = tabControl1.SelectedIndex,
                    Tabs = new List<TabState>()
                };

                foreach (var tab in fileTabs)
                {
                    var tabState = new TabState
                    {
                        FilePath = tab.FilePath,
                        FileType = tab.Type,
                        HasUnsavedChanges = tab.HasUnsavedChanges
                    };

                    // Save content only for unsaved files
                    if (tab.HasUnsavedChanges && string.IsNullOrEmpty(tab.FilePath))
                    {
                        tabState.Content = tab.Editor.Text;
                    }

                    sessionState.Tabs.Add(tabState);
                }

                sessionManager.SaveSession(sessionState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        private bool RestoreSession()
        {
            try
            {
                var sessionState = sessionManager.LoadSession();
                
                if (sessionState == null || sessionState.Tabs.Count == 0)
                {
                    return false;
                }

                // Ask user if they want to restore the previous session
                var result = MessageBox.Show(
                    $"Do you want to restore your previous session?\n({sessionState.Tabs.Count} tab(s) from {sessionState.LastSaved:g})",
                    "Restore Session",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    sessionManager.ClearSession();
                    return false;
                }

                isLoadingFile = true;
                bool anyTabRestored = false;

                foreach (var tabState in sessionState.Tabs)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(tabState.FilePath) && File.Exists(tabState.FilePath))
                        {
                            // Restore saved file
                            var tab = CreateNewTab(tabState.FileType, tabState.FilePath);
                            tab.Editor.Text = File.ReadAllText(tabState.FilePath);
                            tab.HasUnsavedChanges = false;
                            UpdateTabDisplay(tab);
                            anyTabRestored = true;
                        }
                        else if (!string.IsNullOrEmpty(tabState.Content))
                        {
                            // Restore unsaved content
                            var tab = CreateNewTab(tabState.FileType);
                            tab.Editor.Text = tabState.Content;
                            tab.HasUnsavedChanges = true;
                            UpdateTabDisplay(tab);
                            anyTabRestored = true;
                        }
                        // Skip files that don't exist and have no content
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error restoring tab: {ex.Message}");
                    }
                }

                isLoadingFile = false;

                if (anyTabRestored)
                {
                    // Restore active tab
                    if (sessionState.ActiveTabIndex >= 0 && 
                        sessionState.ActiveTabIndex < tabControl1.TabPages.Count)
                    {
                        tabControl1.SelectedIndex = sessionState.ActiveTabIndex;
                    }

                    // Apply syntax highlighting to all restored tabs
                    ApplySyntaxHighlightingToAllTabs();

                    SetParseStatus("Session restored", false);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring session: {ex.Message}");
                return false;
            }
        }

        private void ClearSessionMenuItem_Click(object sender, EventArgs e)
        {
            sessionManager.ClearSession();
            MessageBox.Show("Session data cleared.", "Session", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // File Menu Events
        private void newModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateNewTab(FileType.Model);
            SetParseStatus("Ready", false);
        }

        private void newDataFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateNewTab(FileType.Data);
            SetParseStatus("Ready", false);
        }

        private void openModelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile(FileType.Model);
        }

        private void openDataFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile(FileType.Data);
        }

        private void OpenFile(FileType fileType)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                string extension = fileType == FileType.Model ? "mod" : "dat";
                string description = fileType == FileType.Model ? "Model" : "Data";
                
                openFileDialog.Filter = $"{description} Files (*.{extension})|*.{extension}|All Files (*.*)|*.*";
                openFileDialog.DefaultExt = extension;
                openFileDialog.Title = $"Open {description} File";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Check if file is already open
                        var existingTab = fileTabs.FirstOrDefault(t => 
                            t.FilePath.Equals(openFileDialog.FileName, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingTab != null)
                        {
                            // Switch to existing tab
                            int index = fileTabs.IndexOf(existingTab);
                            tabControl1.SelectedIndex = index;
                            MessageBox.Show("File is already open.", "Information", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        
                        // Create new tab and load file
                        isLoadingFile = true;
                        var newTab = CreateNewTab(fileType, openFileDialog.FileName);
                        newTab.Editor.Text = File.ReadAllText(openFileDialog.FileName);
                        newTab.HasUnsavedChanges = false;
                        isLoadingFile = false;
                        
                        UpdateTabDisplay(newTab);
                        UpdateStatusBar();
                        
                        // Apply syntax highlighting to the newly opened file
                        ApplySyntaxHighlighting(newTab.Editor);
                        
                        SetParseStatus("File loaded - Ready to parse", false);
                    }
                    catch (Exception ex)
                    {
                        isLoadingFile = false;
                        MessageBox.Show($"Error opening file: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void closeTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex >= 0)
            {
                CloseTab(tabControl1.SelectedIndex);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var activeTab = GetActiveTab();
            if (activeTab != null)
            {
                SaveTab(activeTab);
            }
        }

        private void SaveTab(FileTab tab)
        {
            if (string.IsNullOrEmpty(tab.FilePath))
            {
                SaveTabAs(tab);
            }
            else
            {
                SaveFile(tab, tab.FilePath);
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var activeTab = GetActiveTab();
            if (activeTab != null)
            {
                SaveTabAs(activeTab);
            }
        }

        private void SaveTabAs(FileTab tab)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                string extension = tab.GetFileExtension();
                string description = tab.Type == FileType.Model ? "Model" : "Data";
                
                saveFileDialog.Filter = $"{description} Files (*{extension})|*{extension}|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = extension.TrimStart('.');
                saveFileDialog.Title = $"Save {description} File As";
                
                if (!string.IsNullOrEmpty(tab.FilePath))
                {
                    saveFileDialog.FileName = Path.GetFileName(tab.FilePath);
                }
                else
                {
                    saveFileDialog.FileName = $"Untitled{extension}";
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    SaveFile(tab, saveFileDialog.FileName);
                }
            }
        }

        private void SaveFile(FileTab tab, string filePath)
        {
            try
            {
                File.WriteAllText(filePath, tab.Editor.Text);
                tab.FilePath = filePath;
                tab.HasUnsavedChanges = false;
                UpdateTabDisplay(tab);
                SetParseStatus("File saved successfully", false);
                MessageBox.Show("File saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        // Edit Menu Events
        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor != null && editor.SelectionLength > 0)
            {
                editor.Cut();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor != null && editor.SelectionLength > 0)
            {
                editor.Copy();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor != null)
            {
                editor.Paste();
            }
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = GetActiveEditor();
            if (editor != null)
            {
                editor.SelectAll();
            }
        }

        // RichTextBox Events
        private void RichTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!isLoadingFile && !isHighlighting)
            {
                var editor = sender as RichTextBox;
                var tab = fileTabs.FirstOrDefault(t => t.Editor == editor);
                if (tab != null)
                {
                    tab.HasUnsavedChanges = true;
                    UpdateTabDisplay(tab);
                }
            }
            
            UpdateStatusBar();
            
            if (!isHighlighting)
            {
                highlightTimer.Stop();
                highlightTimer.Start();
            }
        }

        private void RichTextBox_SelectionChanged(object sender, EventArgs e)
        {
            UpdateCursorPosition();
        }

        private void HighlightTimer_Tick(object sender, EventArgs e)
        {
            highlightTimer.Stop();
            var editor = GetActiveEditor();
            if (editor != null)
            {
                ApplySyntaxHighlighting(editor);
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStatusBar();
            var activeTab = GetActiveTab();
            if (activeTab != null)
            {
                SetParseStatus("Ready", false);
                
                // Apply syntax highlighting when switching tabs
                // This ensures tabs that were restored but not active get highlighted
                var editor = GetActiveEditor();
                if (editor != null && !string.IsNullOrWhiteSpace(editor.Text))
                {
                    ApplySyntaxHighlighting(editor);
                }
            }
        }

        private void tabControl1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                for (int i = 0; i < tabControl1.TabPages.Count; i++)
                {
                    Rectangle tabRect = tabControl1.GetTabRect(i);
                    if (tabRect.Contains(e.Location))
                    {
                        CloseTab(i);
                        break;
                    }
                }
            }
        }

        // Parse Button Event
        private void btnParse_Click(object sender, EventArgs e)
        {
            try
            {
                modelManager.Clear();
                SetParseStatus("Parsing...", false);

                var modelTabs = fileTabs.Where(t => t.Type == FileType.Model).ToList();
                var dataTabs = fileTabs.Where(t => t.Type == FileType.Data).ToList();

                if (modelTabs.Count == 0)
                {
                    SetParseStatus("No model files to parse", false);
                    MessageBox.Show("No model files open. Please open or create a model file (.mod).", 
                        "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var modelTexts = modelTabs.Select(t => t.Editor.Text).ToList();
                var dataTexts = dataTabs.Select(t => t.Editor.Text).ToList();

                // Call the testable parsing service
                var result = parsingService.ParseModel(modelTexts, dataTexts);

                // Update UI based on result
                SetParseStatus(result.SummaryMessage, !result.Success);

                if (result.Success)
                {
                    // Show success dialog with model summary
                    ShowParseResults();
                }
                else if (result.TotalSuccess > 0 && result.HasErrors)
                {
                    // Partial success - show errors
                    var errorMsg = new System.Text.StringBuilder();
                    errorMsg.AppendLine($"Parsed {result.TotalSuccess} statement(s) successfully, but {result.TotalErrors} error(s) occurred:\n");
                    
                    foreach (var error in result.Errors)
                    {
                        errorMsg.AppendLine(error);
                        errorMsg.AppendLine();
                    }
                    
                    MessageBox.Show(
                        errorMsg.ToString(),
                        "Parsing Errors",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                else
                {
                    // Total failure - show errors and help
                    var errorMsg = new System.Text.StringBuilder();
                    errorMsg.AppendLine("Parse failed with errors:\n");
                    
                    foreach (var error in result.Errors)
                    {
                        errorMsg.AppendLine(error);
                        errorMsg.AppendLine();
                    }
                    
                    errorMsg.AppendLine();
                    errorMsg.AppendLine(ModelParsingService.GetSyntaxHelpMessage());
                    
                    MessageBox.Show(
                        errorMsg.ToString(),
                        "Parse Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetParseStatus("Critical error during parsing", true);
                MessageBox.Show(
                    $"Unexpected error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}",
                    "Critical Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowParseResults()
        {
            string report = modelManager.GenerateParseResultsReport();
            MessageBox.Show(report, "Parse Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateStatusBar()
        {
            UpdateCursorPosition();
            UpdateCharacterAndWordCount();
        }

        private void UpdateCursorPosition()
        {
            var editor = GetActiveEditor();
            if (editor != null)
            {
                int currentLine = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
                int currentColumn = editor.SelectionStart - editor.GetFirstCharIndexOfCurrentLine() + 1;
                
                toolStripStatusLabelLine.Text = $"Ln: {currentLine}";
                toolStripStatusLabelColumn.Text = $"Col: {currentColumn}";
            }
            else
            {
                toolStripStatusLabelLine.Text = "Ln: -";
                toolStripStatusLabelColumn.Text = "Col: -";
            }
        }

        private void UpdateCharacterAndWordCount()
        {
            var editor = GetActiveEditor();
            if (editor != null)
            {
                int charCount = editor.Text.Length;
                int wordCount = CountWords(editor.Text);
                
                toolStripStatusLabelCharCount.Text = $"Characters: {charCount}";
                toolStripStatusLabelWordCount.Text = $"Words: {wordCount}";
            }
            else
            {
                toolStripStatusLabelCharCount.Text = "Characters: 0";
                toolStripStatusLabelWordCount.Text = "Words: 0";
            }
        }

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            
            var matches = Regex.Matches(text, @"\S+");
            return matches.Count;
        }

        private void SetParseStatus(string message, bool isError)
        {
            toolStripStatusLabelParseStatus.Text = message;
            toolStripStatusLabelParseStatus.ForeColor = isError ? Color.Red : Color.Black;
        }

        public ModelManager GetModelManager() => modelManager;

        // File Menu Events - Add these new handlers

private void closeAllTabsToolStripMenuItem_Click(object sender, EventArgs e)
{
    CloseAllTabs();
}

private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
{
    SaveAllTabs();
}

// Session Menu Events

private void restoreSessionToolStripMenuItem_Click(object sender, EventArgs e)
{
    if (!sessionManager.SessionExists())
    {
        MessageBox.Show("No saved session found.", "Session", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
    }

    // Check for unsaved changes in current tabs
    if (HasAnyUnsavedChanges())
    {
        var result = MessageBox.Show(
            "You have unsaved changes in the current workspace. Do you want to save before restoring the session?",
            "Unsaved Changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        switch (result)
        {
            case DialogResult.Yes:
                SaveAllTabs();
                break;
            case DialogResult.Cancel:
                return;
        }
    }

    // Close all current tabs
    CloseAllTabsWithoutPrompt();

    // Restore session
    if (RestoreSession())
    {
        MessageBox.Show("Session restored successfully.", "Session", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    else
    {
        // If restore failed, create a new tab
        CreateNewTab(FileType.Model);
    }
}

private void clearSessionToolStripMenuItem_Click(object sender, EventArgs e)
{
    var result = MessageBox.Show(
        "This will clear the saved session data. Your current open files will not be affected.\n\n" +
        "Do you want to continue?",
        "Clear Session Data",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question);

    if (result == DialogResult.Yes)
    {
        sessionManager.ClearSession();
        MessageBox.Show("Session data cleared successfully.", "Session", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// Helper Methods

private void CloseAllTabs()
{
    // Create a copy of the list to avoid modification during iteration
    var tabsCopy = fileTabs.ToList();
    
    foreach (var tab in tabsCopy)
    {
        int index = fileTabs.IndexOf(tab);
        if (index >= 0)
        {
            CloseTab(index);
            
            // If user cancelled, stop closing
            if (fileTabs.Contains(tab))
            {
                return;
            }
        }
    }

    // Ensure at least one tab exists
    if (fileTabs.Count == 0)
    {
        CreateNewTab(FileType.Model);
    }
}

private void CloseAllTabsWithoutPrompt()
{
    // Close all tabs without prompting for unsaved changes
    while (tabControl1.TabPages.Count > 0)
    {
        tabControl1.TabPages.RemoveAt(0);
    }
    fileTabs.Clear();
}

private void SaveAllTabs()
{
    int savedCount = 0;
    int skippedCount = 0;
    
    foreach (var tab in fileTabs)
    {
        if (tab.HasUnsavedChanges)
        {
            if (string.IsNullOrEmpty(tab.FilePath))
            {
                // Skip unsaved new files - they need SaveAs
                skippedCount++;
            }
            else
            {
                try
                {
                    File.WriteAllText(tab.FilePath, tab.Editor.Text);
                    tab.HasUnsavedChanges = false;
                    UpdateTabDisplay(tab);
                    savedCount++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving {Path.GetFileName(tab.FilePath)}: {ex.Message}", 
                        "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    if (savedCount > 0 || skippedCount > 0)
    {
        string message = $"Saved {savedCount} file(s).";
        if (skippedCount > 0)
        {
            message += $"\n{skippedCount} unsaved file(s) require 'Save As'.";
        }
        
        SetParseStatus($"Saved {savedCount} file(s)", false);
        MessageBox.Show(message, "Save All", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    else
    {
        MessageBox.Show("No files needed saving.", "Save All", 
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

private bool HasAnyUnsavedChanges()
{
    return fileTabs.Any(tab => tab.HasUnsavedChanges);
}

private void StartNewProject()
{
    var result = MessageBox.Show(
        "This will close all current tabs and clear the session.\n\n" +
        "Do you want to save your current work before starting a new project?",
        "Start New Project",
        MessageBoxButtons.YesNoCancel,
        MessageBoxIcon.Question);

    switch (result)
    {
        case DialogResult.Yes:
            SaveAllTabs();
            CloseAllTabsWithoutPrompt();
            sessionManager.ClearSession();
            CreateNewTab(FileType.Model);
            SetParseStatus("New project started", false);
            break;
        
        case DialogResult.No:
            CloseAllTabsWithoutPrompt();
            sessionManager.ClearSession();
            CreateNewTab(FileType.Model);
            SetParseStatus("New project started", false);
            break;
        
        case DialogResult.Cancel:
            // Do nothing
            break;
    }
}

/// <summary>
/// Applies syntax highlighting to a specific editor
/// </summary>
private void ApplySyntaxHighlighting(RichTextBox editor)
{
    if (editor == null) return;
    
    try
    {
        isHighlighting = true;
        var highlighter = new SyntaxHighlighter(editor);
        highlighter.ApplySyntaxHighlighting();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error applying syntax highlighting: {ex.Message}");
    }
    finally
    {
        isHighlighting = false;
    }
}

/// <summary>
/// Applies syntax highlighting to all open tabs
/// </summary>
private void ApplySyntaxHighlightingToAllTabs()
{
    try
    {
        isHighlighting = true;
        
        foreach (var tab in fileTabs)
        {
            if (tab.Editor != null && !string.IsNullOrWhiteSpace(tab.Editor.Text))
            {
                var highlighter = new SyntaxHighlighter(tab.Editor);
                highlighter.ApplySyntaxHighlighting();
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error applying syntax highlighting to all tabs: {ex.Message}");
    }
    finally
    {
        isHighlighting = false;
    }
}
    }
}