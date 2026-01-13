using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Core;
using NetWorks.Services;

namespace NetWorks
{
    public partial class Form1 : Form
    {
        private string currentFilePath = string.Empty;
        private readonly ModelManager modelManager;
        private readonly EquationParser parser;
        private readonly SyntaxHighlighter syntaxHighlighter;
        private System.Windows.Forms.Timer highlightTimer;
        private bool hasUnsavedChanges = false;
        private bool isLoadingFile = false; // Flag to prevent TextChanged during file load

        public Form1()
        {
            InitializeComponent();
            
            // Initialize services
            modelManager = new ModelManager();
            parser = new EquationParser(modelManager);
            syntaxHighlighter = new SyntaxHighlighter(richTextBox1);
            
            // Initialize timer for delayed syntax highlighting
            highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 300;
            highlightTimer.Tick += HighlightTimer_Tick;
            
            // Enable double buffering
            this.DoubleBuffered = true;
            typeof(RichTextBox).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, richTextBox1, new object[] { true });
            
            // Handle form closing event
            this.FormClosing += Form1_FormClosing;
            
            UpdateTitle();
            UpdateStatusBar();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmSaveChanges())
            {
                e.Cancel = true;
            }
        }

        // File Menu Events
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmSaveChanges())
            {
                isLoadingFile = true;
                richTextBox1.Clear();
                currentFilePath = string.Empty;
                modelManager.Clear();
                hasUnsavedChanges = false;
                isLoadingFile = false;
                UpdateTitle();
                UpdateStatusBar();
                SetParseStatus("Ready", false);
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmSaveChanges())
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Model Files (*.mod)|*.mod|All Files (*.*)|*.*";
                    openFileDialog.DefaultExt = "mod";
                    openFileDialog.Title = "Open Model File";

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            isLoadingFile = true;
                            richTextBox1.Text = File.ReadAllText(openFileDialog.FileName);
                            currentFilePath = openFileDialog.FileName;
                            hasUnsavedChanges = false;
                            isLoadingFile = false;
                            UpdateTitle();
                            UpdateStatusBar();
                            SetParseStatus("File loaded - Ready to parse", false);
                        }
                        catch (Exception ex)
                        {
                            isLoadingFile = false;
                            MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveFileAs();
            }
            else
            {
                SaveFile(currentFilePath);
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileAs();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmSaveChanges())
            {
                Application.Exit();
            }
        }

        // Edit Menu Events
        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (richTextBox1.SelectionLength > 0)
            {
                richTextBox1.Cut();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (richTextBox1.SelectionLength > 0)
            {
                richTextBox1.Copy();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBox1.Paste();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBox1.SelectAll();
        }

        // RichTextBox Events
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            // Don't mark as unsaved if we're loading a file
            if (!isLoadingFile)
            {
                hasUnsavedChanges = true;
            }
            UpdateTitle();
            UpdateStatusBar();
            highlightTimer.Stop();
            highlightTimer.Start();
        }

        private void richTextBox1_SelectionChanged(object sender, EventArgs e)
        {
            UpdateCursorPosition();
        }

        private void HighlightTimer_Tick(object sender, EventArgs e)
        {
            highlightTimer.Stop();
            syntaxHighlighter.ApplySyntaxHighlighting();
        }

        // Parse Button Event
        private void btnParse_Click(object sender, EventArgs e)
        {
            try
            {
                modelManager.Clear();
                SetParseStatus("Parsing...", false);
                
                string text = richTextBox1.Text;

                if (string.IsNullOrWhiteSpace(text))
                {
                    SetParseStatus("No text to parse", false);
                    MessageBox.Show("No text to parse.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var result = parser.Parse(text);

                if (result.HasSuccess)
                {
                    ShowParseResults();
                    
                    if (result.HasErrors)
                    {
                        SetParseStatus($"Parsed with warnings: {result.SuccessCount} statements, {result.Errors.Count} errors", true);
                        
                        var errorMsg = new System.Text.StringBuilder();
                        errorMsg.AppendLine($"Parsed {result.SuccessCount} statement(s) successfully, but {result.Errors.Count} error(s) occurred:\n");
                        foreach (var error in result.Errors)
                        {
                            errorMsg.AppendLine(error.Message);
                            errorMsg.AppendLine();
                        }
                        MessageBox.Show(errorMsg.ToString(), "Parsing Warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        SetParseStatus($"Parse successful: {result.SuccessCount} statements", false);
                    }
                }
                else
                {
                    SetParseStatus($"Parse failed: {result.Errors.Count} errors", true);
                    
                    var errorMsg = new System.Text.StringBuilder();
                    errorMsg.AppendLine("No valid statements found.\n");
                    errorMsg.AppendLine("Errors encountered:\n");
                    foreach (var error in result.Errors)
                    {
                        errorMsg.AppendLine(error.Message);
                        errorMsg.AppendLine();
                    }
                    errorMsg.AppendLine("\nSupported formats:");
                    errorMsg.AppendLine("  Comments: // This is a comment");
                    errorMsg.AppendLine("  Parameters: int T = 10; float capacity = 100.5; string name = \"value\"");
                    errorMsg.AppendLine("  Index sets: range I = 1..10 or range I = 1..T");
                    errorMsg.AppendLine("  Variables: var float x[I]; var int y[J]; var bool z[K]");
                    errorMsg.AppendLine("  Indexed equations: equation constraint[I]: x[i] + y[i] <= 10");
                    errorMsg.AppendLine("  Equations: 2x + 3y == 5  (use == for equality)");
                    errorMsg.AppendLine("  Labeled equations: eq1: 2x + 3y == 5");
                    
                    MessageBox.Show(errorMsg.ToString(), "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetParseStatus("Critical error during parsing", true);
                MessageBox.Show($"Unexpected error parsing: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Helper Methods
        private void SaveFile(string filePath)
        {
            try
            {
                File.WriteAllText(filePath, richTextBox1.Text);
                currentFilePath = filePath;
                hasUnsavedChanges = false;
                UpdateTitle();
                SetParseStatus("File saved successfully", false);
                MessageBox.Show("File saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveFileAs()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Model Files (*.mod)|*.mod|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = "mod";
                saveFileDialog.Title = "Save Model File As";
                
                // Suggest current file name or default
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    saveFileDialog.FileName = Path.GetFileName(currentFilePath);
                }
                else
                {
                    saveFileDialog.FileName = "Untitled.mod";
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    SaveFile(saveFileDialog.FileName);
                }
            }
        }

        private bool ConfirmSaveChanges()
        {
            if (hasUnsavedChanges)
            {
                string fileName = string.IsNullOrEmpty(currentFilePath) 
                    ? "Untitled" 
                    : Path.GetFileName(currentFilePath);
                
                DialogResult result = MessageBox.Show(
                    $"Do you want to save changes to {fileName}?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                switch (result)
                {
                    case DialogResult.Yes:
                        if (string.IsNullOrEmpty(currentFilePath))
                        {
                            SaveFileAs();
                            // Check if user cancelled the save dialog
                            return !hasUnsavedChanges;
                        }
                        else
                        {
                            SaveFile(currentFilePath);
                            return true;
                        }
                    case DialogResult.No:
                        return true;
                    case DialogResult.Cancel:
                        return false;
                    default:
                        return false;
                }
            }
            
            return true;
        }

        private void UpdateTitle()
        {
            string fileName = string.IsNullOrEmpty(currentFilePath) 
                ? "Untitled" 
                : Path.GetFileName(currentFilePath);
            
            string unsavedIndicator = hasUnsavedChanges ? "*" : "";
            
            this.Text = $"Model Editor - {fileName}{unsavedIndicator}";
        }

        private void ShowParseResults()
        {
            string report = modelManager.GenerateParseResultsReport();
            MessageBox.Show(report, "Parse Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Status Bar Update Methods
        private void UpdateStatusBar()
        {
            UpdateCursorPosition();
            UpdateCharacterAndWordCount();
        }

        private void UpdateCursorPosition()
        {
            int currentLine = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart) + 1;
            int currentColumn = richTextBox1.SelectionStart - richTextBox1.GetFirstCharIndexOfCurrentLine() + 1;
            
            toolStripStatusLabelLine.Text = $"Ln: {currentLine}";
            toolStripStatusLabelColumn.Text = $"Col: {currentColumn}";
        }

        private void UpdateCharacterAndWordCount()
        {
            int charCount = richTextBox1.Text.Length;
            int wordCount = CountWords(richTextBox1.Text);
            
            toolStripStatusLabelCharCount.Text = $"Characters: {charCount}";
            toolStripStatusLabelWordCount.Text = $"Words: {wordCount}";
        }

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            
            // Count words using regex (sequences of non-whitespace characters)
            var matches = Regex.Matches(text, @"\S+");
            return matches.Count;
        }

        private void SetParseStatus(string message, bool isError)
        {
            toolStripStatusLabelParseStatus.Text = message;
            toolStripStatusLabelParseStatus.ForeColor = isError ? Color.Red : Color.Black;
        }

        // Public API (if needed by other forms/components)
        public ModelManager GetModelManager() => modelManager;
    }
}   
