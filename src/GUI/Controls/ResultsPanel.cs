using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Core;
using Core.Models;

namespace GUI.Controls
{
    /// <summary>
    /// Docked panel for displaying parsing results, errors, and model information
    /// </summary>
    public class ResultsPanel : UserControl
    {
        private TabControl tabControl;
        private RichTextBox outputTextBox;
        private ListView errorsListView;
        private TreeView modelTreeView;
        private RichTextBox statisticsTextBox;
        
        public event EventHandler<ErrorNavigationEventArgs> ErrorDoubleClicked;

        public ResultsPanel()
        {
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

            // Output tab
            var outputTab = new TabPage("Output");
            CreateOutputTab(outputTab);
            tabControl.TabPages.Add(outputTab);

            // Errors tab
            var errorsTab = new TabPage("Errors");
            CreateErrorsTab(errorsTab);
            tabControl.TabPages.Add(errorsTab);

            // Model tab
            var modelTab = new TabPage("Model");
            CreateModelTab(modelTab);
            tabControl.TabPages.Add(modelTab);

            // Statistics tab
            var statsTab = new TabPage("Statistics");
            CreateStatisticsTab(statsTab);
            tabControl.TabPages.Add(statsTab);

            this.Controls.Add(tabControl);
            this.ResumeLayout();
        }

        private void CreateOutputTab(TabPage tab)
        {
            outputTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            tab.Controls.Add(outputTextBox);
        }

        private void CreateErrorsTab(TabPage tab)
        {
            errorsListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9)
            };

            errorsListView.Columns.Add("Type", 60);
            errorsListView.Columns.Add("Line", 50);
            errorsListView.Columns.Add("Message", 500);

            errorsListView.MouseDoubleClick += ErrorsListView_MouseDoubleClick;

            // Add context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Copy Error", null, CopyError_Click);
            contextMenu.Items.Add("Copy All Errors", null, CopyAllErrors_Click);
            errorsListView.ContextMenuStrip = contextMenu;

            tab.Controls.Add(errorsListView);
        }

        private void CreateModelTab(TabPage tab)
        {
            modelTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true
            };

            tab.Controls.Add(modelTreeView);
        }

        private void CreateStatisticsTab(TabPage tab)
        {
            statisticsTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            tab.Controls.Add(statisticsTextBox);
        }

        /// <summary>
        /// Displays parsing results
        /// </summary>
        public void ShowResults(ModelParsingResult result, ModelManager modelManager)
        {
            // Clear previous results
            Clear();

            // Show output
            ShowOutput(result);

            // Show errors
            ShowErrors(result);

            // Show model structure
            ShowModel(modelManager);

            // Show statistics
            ShowStatistics(result, modelManager);

            // Select appropriate tab
            if (result.TotalErrors > 0)
            {
                tabControl.SelectedIndex = 1; // Errors tab
            }
            else
            {
                tabControl.SelectedIndex = 0; // Output tab
            }
        }

        private void ShowOutput(ModelParsingResult result)
        {
            outputTextBox.Clear();

            // Title
            AppendColoredText("═══════════════════════════════════════════════\n", Color.Cyan);
            AppendColoredText("  PARSING RESULTS\n", Color.Cyan, FontStyle.Bold);
            AppendColoredText("═══════════════════════════════════════════════\n\n", Color.Cyan);

            // Summary
            if (result.Success)
            {
                AppendColoredText("✓ ", Color.LightGreen, FontStyle.Bold);
                AppendColoredText("Parsing completed successfully!\n\n", Color.LightGreen);
            }
            else
            {
                AppendColoredText("✗ ", Color.Red, FontStyle.Bold);
                AppendColoredText("Parsing completed with errors.\n\n", Color.Red);
            }

            // Statistics
            AppendColoredText($"Total Statements:  ", Color.Gray);
            AppendColoredText($"{result.TotalSuccess}\n", Color.White);
            
            AppendColoredText($"Total Errors:      ", Color.Gray);
            if (result.TotalErrors > 0)
            {
                AppendColoredText($"{result.TotalErrors}\n", Color.Red);
            }
            else
            {
                AppendColoredText($"{result.TotalErrors}\n", Color.LightGreen);
            }

            // Errors detail
            if (result.TotalErrors > 0)
            {
                AppendColoredText("\n───────────────────────────────────────────────\n", Color.DarkGray);
                AppendColoredText("ERRORS:\n", Color.Yellow, FontStyle.Bold);
                AppendColoredText("───────────────────────────────────────────────\n\n", Color.DarkGray);

                foreach (var error in result.Errors.Take(20))
                {
                    AppendColoredText("• ", Color.Red);
                    AppendColoredText($"{error}\n", Color.White);
                }

                if (result.Errors.Count > 20)
                {
                    AppendColoredText($"\n... and {result.Errors.Count - 20} more errors.\n", Color.Gray);
                    AppendColoredText("See 'Errors' tab for full list.\n", Color.Gray);
                }
            }

            AppendColoredText("\n═══════════════════════════════════════════════\n", Color.Cyan);
            AppendColoredText($"Completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", Color.Gray);
        }

        private void ShowErrors(ModelParsingResult result)
        {
            errorsListView.Items.Clear();

            foreach (var error in result.Errors)
            {
                // Try to extract line number from error message
                var lineNumber = ExtractLineNumber(error);
                var errorType = error.Contains("Error") ? "Error" : "Warning";
                
                var item = new ListViewItem(new[]
                {
                    errorType,
                    lineNumber.ToString(),
                    error
                });

                item.Tag = lineNumber;
                
                if (errorType == "Error")
                {
                    item.ForeColor = Color.Red;
                }
                else
                {
                    item.ForeColor = Color.Orange;
                }

                errorsListView.Items.Add(item);
            }

            // Auto-size columns
            if (errorsListView.Items.Count > 0)
            {
                errorsListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            }

            // Update tab text with count
            tabControl.TabPages[1].Text = $"Errors ({result.TotalErrors})";
        }

        private void ShowModel(ModelManager modelManager)
        {
            modelTreeView.Nodes.Clear();

            // Parameters
            var paramsNode = new TreeNode($"Parameters ({modelManager.Parameters.Count})")
            {
                ImageIndex = 0
            };
            foreach (var param in modelManager.Parameters.Values.OrderBy(p => p.Name))
            {
                var paramText = param.IsScalar 
                    ? $"{param.Name} = {param.Value}"
                    : $"{param.Name}[{param.IndexSetName}]";
                paramsNode.Nodes.Add(new TreeNode(paramText));
            }
            modelTreeView.Nodes.Add(paramsNode);

            // Ranges
            var rangesNode = new TreeNode($"Ranges ({modelManager.Ranges.Count})");
            foreach (var range in modelManager.Ranges.Values.OrderBy(r => r.Name))
            {
                rangesNode.Nodes.Add(new TreeNode($"{range.Name} = {range.Start}..{range.End}"));
            }
            modelTreeView.Nodes.Add(rangesNode);

            // Index Sets
            var indexSetsNode = new TreeNode($"Index Sets ({modelManager.IndexSets.Count})");
            foreach (var indexSet in modelManager.IndexSets.Values.OrderBy(i => i.Name))
            {
                indexSetsNode.Nodes.Add(new TreeNode($"{indexSet.Name}: {indexSet.StartIndex}..{indexSet.EndIndex}"));
            }
            modelTreeView.Nodes.Add(indexSetsNode);

            // Variables
            var varsNode = new TreeNode($"Variables ({modelManager.IndexedVariables.Count})");
            foreach (var variable in modelManager.IndexedVariables.Values.OrderBy(v => v.Name))
            {
                var varText = $"{variable.Name}";
                if (!string.IsNullOrEmpty(variable.IndexSetName))
                {
                    varText += $"[{variable.IndexSetName}]";
                }
                varsNode.Nodes.Add(new TreeNode(varText));
            }
            modelTreeView.Nodes.Add(varsNode);

            // Equations
            var equationsNode = new TreeNode($"Equations ({modelManager.Equations.Count})");
            foreach (var equation in modelManager.Equations.OrderBy(e => e.Label ?? e.BaseName))
            {
                var eqText = equation.Label ?? equation.BaseName;
                equationsNode.Nodes.Add(new TreeNode(eqText));
            }
            modelTreeView.Nodes.Add(equationsNode);

            // Objective
            if (modelManager.Objective != null)
            {
                var objNode = new TreeNode($"Objective: {modelManager.Objective.Sense}");
                modelTreeView.Nodes.Add(objNode);
            }

            // Tuple Schemas
            var tuplesNode = new TreeNode($"Tuple Schemas ({modelManager.TupleSchemas.Count})");
            foreach (var schema in modelManager.TupleSchemas.Values.OrderBy(s => s.Name))
            {
                var schemaNode = new TreeNode($"{schema.Name} ({schema.Fields.Count} fields)");
                foreach (var field in schema.Fields)
                {
                    schemaNode.Nodes.Add(new TreeNode($"{field.Key}: {field.Value}"));
                }
                tuplesNode.Nodes.Add(schemaNode);
            }
            modelTreeView.Nodes.Add(tuplesNode);

            // Tuple Sets
            var tupleSetsNode = new TreeNode($"Tuple Sets ({modelManager.TupleSets.Count})");
            foreach (var tupleSet in modelManager.TupleSets.Values.OrderBy(s => s.Name))
            {
                tupleSetsNode.Nodes.Add(new TreeNode($"{tupleSet.Name} ({tupleSet.Instances.Count} instances)"));
            }
            modelTreeView.Nodes.Add(tupleSetsNode);

            // Expand main nodes
            modelTreeView.ExpandAll();
        }

        private void ShowStatistics(ModelParsingResult result, ModelManager modelManager)
        {
            statisticsTextBox.Clear();

            statisticsTextBox.AppendText("═══════════════════════════════════════════════\n");
            statisticsTextBox.AppendText("  MODEL STATISTICS\n");
            statisticsTextBox.AppendText("═══════════════════════════════════════════════\n\n");

            statisticsTextBox.AppendText("PARSING:\n");
            statisticsTextBox.AppendText($"  Success Count:       {result.TotalSuccess}\n");
            statisticsTextBox.AppendText($"  Error Count:         {result.TotalErrors}\n");
            statisticsTextBox.AppendText($"  Success Rate:        {GetSuccessRate(result)}%\n\n");

            statisticsTextBox.AppendText("MODEL COMPONENTS:\n");
            statisticsTextBox.AppendText($"  Parameters:          {modelManager.Parameters.Count}\n");
            statisticsTextBox.AppendText($"  Ranges:              {modelManager.Ranges.Count}\n");
            statisticsTextBox.AppendText($"  Index Sets:          {modelManager.IndexSets.Count}\n");
            statisticsTextBox.AppendText($"  Variables:           {modelManager.IndexedVariables.Count}\n");
            statisticsTextBox.AppendText($"  Equations:           {modelManager.Equations.Count}\n");
            statisticsTextBox.AppendText($"  Tuple Schemas:       {modelManager.TupleSchemas.Count}\n");
            statisticsTextBox.AppendText($"  Tuple Sets:          {modelManager.TupleSets.Count}\n");
            statisticsTextBox.AppendText($"  Primitive Sets:      {modelManager.PrimitiveSets.Count}\n");
            statisticsTextBox.AppendText($"  Decision Exprs:      {modelManager.DecisionExpressions.Count}\n");
            statisticsTextBox.AppendText($"  Objective:           {(modelManager.Objective != null ? "Yes" : "No")}\n\n");

            if (modelManager.Objective != null)
            {
                statisticsTextBox.AppendText("OBJECTIVE:\n");
                statisticsTextBox.AppendText($"  Type:                {modelManager.Objective.Sense}\n");
                statisticsTextBox.AppendText($"  Terms:               {modelManager.Objective.Coefficients.Count}\n\n");
            }

            statisticsTextBox.AppendText("VARIABLES:\n");
            var scalarVars = modelManager.IndexedVariables.Values.Count(v => string.IsNullOrEmpty(v.IndexSetName));
            var indexedVars = modelManager.IndexedVariables.Values.Count(v => !string.IsNullOrEmpty(v.IndexSetName));
            statisticsTextBox.AppendText($"  Scalar:              {scalarVars}\n");
            statisticsTextBox.AppendText($"  Indexed:             {indexedVars}\n\n");

            statisticsTextBox.AppendText("PARAMETERS:\n");
            var scalarParams = modelManager.Parameters.Values.Count(p => p.IsScalar);
            var indexedParams = modelManager.Parameters.Values.Count(p => !p.IsScalar);
            var externalParams = modelManager.Parameters.Values.Count(p => p.IsExternal);
            statisticsTextBox.AppendText($"  Scalar:              {scalarParams}\n");
            statisticsTextBox.AppendText($"  Indexed:             {indexedParams}\n");
            statisticsTextBox.AppendText($"  External:            {externalParams}\n\n");

            statisticsTextBox.AppendText("═══════════════════════════════════════════════\n");
        }

        public void Clear()
        {
            outputTextBox.Clear();
            errorsListView.Items.Clear();
            modelTreeView.Nodes.Clear();
            statisticsTextBox.Clear();
            tabControl.TabPages[1].Text = "Errors";
        }

        public void AppendOutput(string text, Color color = default)
        {
            if (color == default)
                color = Color.White;

            AppendColoredText(text + "\n", color);
        }

        private void AppendColoredText(string text, Color color, FontStyle style = FontStyle.Regular)
        {
            outputTextBox.SelectionStart = outputTextBox.TextLength;
            outputTextBox.SelectionLength = 0;
            outputTextBox.SelectionColor = color;
            outputTextBox.SelectionFont = new Font(outputTextBox.Font, style);
            outputTextBox.AppendText(text);
            outputTextBox.SelectionColor = outputTextBox.ForeColor;
        }

        private int ExtractLineNumber(string errorMessage)
        {
            // Try to extract line number from error messages like "Line 5: Error message"
            var match = System.Text.RegularExpressions.Regex.Match(errorMessage, @"(?:Line|line)\s+(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int lineNum))
            {
                return lineNum;
            }
            return 0;
        }

        private double GetSuccessRate(ModelParsingResult result)
        {
            var total = result.TotalSuccess + result.TotalErrors;
            if (total == 0) return 0;
            return Math.Round((double)result.TotalSuccess / total * 100, 2);
        }

        // Event Handlers
        private void ErrorsListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (errorsListView.SelectedItems.Count > 0)
            {
                var item = errorsListView.SelectedItems[0];
                if (item.Tag is int lineNumber && lineNumber > 0)
                {
                    ErrorDoubleClicked?.Invoke(this, new ErrorNavigationEventArgs(lineNumber, item.SubItems[2].Text));
                }
            }
        }

        private void CopyError_Click(object sender, EventArgs e)
        {
            if (errorsListView.SelectedItems.Count > 0)
            {
                var error = errorsListView.SelectedItems[0].SubItems[2].Text;
                Clipboard.SetText(error);
            }
        }

        private void CopyAllErrors_Click(object sender, EventArgs e)
        {
            var allErrors = string.Join("\n", errorsListView.Items.Cast<ListViewItem>()
                .Select(item => item.SubItems[2].Text));
            Clipboard.SetText(allErrors);
        }
    }

    /// <summary>
    /// Event args for error navigation
    /// </summary>
    public class ErrorNavigationEventArgs : EventArgs
    {
        public int LineNumber { get; }
        public string ErrorMessage { get; }

        public ErrorNavigationEventArgs(int lineNumber, string errorMessage)
        {
            LineNumber = lineNumber;
            ErrorMessage = errorMessage;
        }
    }
}