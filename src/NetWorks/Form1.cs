using System.Runtime.InteropServices;
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
        }

        // File Menu Events
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmSaveChanges())
            {
                richTextBox1.Clear();
                currentFilePath = string.Empty;
                modelManager.Clear();
                UpdateTitle();
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ConfirmSaveChanges())
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                    openFileDialog.DefaultExt = "txt";

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            richTextBox1.Text = File.ReadAllText(openFileDialog.FileName);
                            currentFilePath = openFileDialog.FileName;
                            UpdateTitle();
                        }
                        catch (Exception ex)
                        {
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

        // Syntax Highlighting
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            highlightTimer.Stop();
            highlightTimer.Start();
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
                
                string text = richTextBox1.Text;

                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("No text to parse.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var result = parser.Parse(text);

                if (result.HasSuccess)
                {
                    ShowParseResults();
                    
                    if (result.HasErrors)
                    {
                        var errorMsg = new System.Text.StringBuilder();
                        errorMsg.AppendLine($"Parsed {result.SuccessCount} statement(s) successfully, but {result.Errors.Count} error(s) occurred:\n");
                        foreach (var error in result.Errors)
                        {
                            errorMsg.AppendLine(error);
                            errorMsg.AppendLine();
                        }
                        MessageBox.Show(errorMsg.ToString(), "Parsing Warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    var errorMsg = new System.Text.StringBuilder();
                    errorMsg.AppendLine("No valid statements found.\n");
                    errorMsg.AppendLine("Errors encountered:\n");
                    foreach (var error in result.Errors)
                    {
                        errorMsg.AppendLine(error);
                        errorMsg.AppendLine();
                    }
                    errorMsg.AppendLine("\nSupported formats:");
                    errorMsg.AppendLine("  Comments: // This is a comment");
                    errorMsg.AppendLine("  Parameters: int T = 10; float capacity = 100.5; string name = \"value\"");
                    errorMsg.AppendLine("  Index sets: range I = 1..10 or range I = 1..T");
                    errorMsg.AppendLine("  Variables: var float x[I]; var int y[J]; var bool z[K]");
                    errorMsg.AppendLine("  Indexed equations: equation constraint[I]: x[i] + y[i] <= 10");
                    errorMsg.AppendLine("  Equations: 2x + 3y = 5");
                    errorMsg.AppendLine("  Labeled equations: eq1: 2x + 3y = 5");
                    
                    MessageBox.Show(errorMsg.ToString(), "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
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
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = "txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    SaveFile(saveFileDialog.FileName);
                    currentFilePath = saveFileDialog.FileName;
                    UpdateTitle();
                }
            }
        }

        private bool ConfirmSaveChanges()
        {
            return true;
        }

        private void UpdateTitle()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                this.Text = "Simple Text Editor - Untitled";
            }
            else
            {
                this.Text = $"Simple Text Editor - {Path.GetFileName(currentFilePath)}";
            }
        }

        private void ShowParseResults()
        {
            string report = modelManager.GenerateParseResultsReport();
            MessageBox.Show(report, "Parse Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Public API (if needed by other forms/components)
        public ModelManager GetModelManager() => modelManager;
    }
}   
