using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Core;
using Core.Models;
using Core.Solving;

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

        // Solution tab controls
        private Label solutionStatusLabel;
        private Label solutionObjectiveLabel;
        private Label solutionTimeLabel;
        private Label solutionMipGapLabel;
        private DataGridView solutionVariablesGrid;
        private DataGridView solutionSlacksGrid;
        
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

            // Solution tab
            var solutionTab = new TabPage("Solution");
            CreateSolutionTab(solutionTab);
            tabControl.TabPages.Add(solutionTab);

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
            errorsListView.Columns.Add("File", 120);
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
        public void ShowResults(ParseSessionResult result, ModelManager modelManager)
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
            if (result.Errors.Count > 0)
            {
                tabControl.SelectedIndex = 1; // Errors tab
            }
            else
            {
                tabControl.SelectedIndex = 0; // Output tab
            }
        }

        private void ShowOutput(ParseSessionResult result)
        {
            outputTextBox.Clear();

            // Title
            AppendColoredText("═══════════════════════════════════════════════\n", Color.Cyan);
            AppendColoredText("  PARSING RESULTS\n", Color.Cyan, FontStyle.Bold);
            AppendColoredText("═══════════════════════════════════════════════\n\n", Color.Cyan);

            // Summary
            if (!result.HasErrors)
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
            AppendColoredText($"{result.SuccessCount}\n", Color.White);

            AppendColoredText($"Total Errors:      ", Color.Gray);
            if (result.Errors.Count > 0)
            {
                AppendColoredText($"{result.Errors.Count}\n", Color.Red);
            }
            else
            {
                AppendColoredText($"{result.Errors.Count}\n", Color.LightGreen);
            }

            // Errors detail
            if (result.Errors.Count > 0)
            {
                AppendColoredText("\n───────────────────────────────────────────────\n", Color.DarkGray);
                AppendColoredText("ERRORS:\n", Color.Yellow, FontStyle.Bold);
                AppendColoredText("───────────────────────────────────────────────\n\n", Color.DarkGray);

                foreach (var error in result.GetErrorMessages().Take(20))
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

        private void ShowErrors(ParseSessionResult result)
        {
            errorsListView.Items.Clear();

            foreach (var error in result.Errors)
            {
                var lineNumber = error.LineNumber > 0 ? error.LineNumber : ExtractLineNumber(error.Message);
                var errorType = error.Message.Contains("Error") ? "Error" : "Warning";
                var fileName = !string.IsNullOrEmpty(error.FilePath)
                    ? System.IO.Path.GetFileName(error.FilePath)
                    : "";

                var item = new ListViewItem(new[]
                {
                    errorType,
                    fileName,
                    lineNumber.ToString(),
                    error.Message
                });

                item.Tag = (error.FilePath ?? "", lineNumber);

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
            tabControl.TabPages[1].Text = $"Errors ({result.Errors.Count})";
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
                rangesNode.Nodes.Add(new TreeNode($"{range.Name} = {range.StartExpression}..{range.EndExpression}"));
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
            foreach (var variable in modelManager.IndexedVariables.Values.OrderBy(v => v.BaseName))
            {
                var varText = $"{variable.BaseName}";
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

        private void ShowStatistics(ParseSessionResult result, ModelManager modelManager)
        {
            statisticsTextBox.Clear();

            statisticsTextBox.AppendText("═══════════════════════════════════════════════\n");
            statisticsTextBox.AppendText("  MODEL STATISTICS\n");
            statisticsTextBox.AppendText("═══════════════════════════════════════════════\n\n");

            statisticsTextBox.AppendText("PARSING:\n");
            statisticsTextBox.AppendText($"  Success Count:       {result.SuccessCount}\n");
            statisticsTextBox.AppendText($"  Error Count:         {result.Errors.Count}\n");
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

        private void CreateSolutionTab(TabPage tab)
        {
            // Top info panel
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(8) };

            solutionStatusLabel = new Label
            {
                Text = "No solution",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                Location = new System.Drawing.Point(8, 8)
            };
            solutionObjectiveLabel = new Label { Text = "", AutoSize = true, Location = new System.Drawing.Point(8, 32) };
            solutionTimeLabel = new Label { Text = "", AutoSize = true, Location = new System.Drawing.Point(8, 52) };
            solutionMipGapLabel = new Label { Text = "", AutoSize = true, Location = new System.Drawing.Point(8, 72) };

            topPanel.Controls.AddRange(new Control[]
            {
                solutionStatusLabel, solutionObjectiveLabel, solutionTimeLabel, solutionMipGapLabel
            });

            // Split container for the two grids
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 50,
                Panel1MinSize = 100,
                Panel2MinSize = 100
            };

            // Variable values grid (left)
            solutionVariablesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Consolas", 9),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(250, 250, 250)
            };
            solutionVariablesGrid.Columns.Add("VarName", "Variable");
            solutionVariablesGrid.Columns.Add("VarValue", "Value");
            solutionVariablesGrid.Columns["VarValue"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            splitContainer.Panel1.Controls.Add(solutionVariablesGrid);
            splitContainer.Panel1.Controls.Add(new Label
            {
                Text = "Variable Values",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            });

            // Constraint slacks grid (right)
            solutionSlacksGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Consolas", 9),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(250, 250, 250)
            };
            solutionSlacksGrid.Columns.Add("ConName", "Constraint");
            solutionSlacksGrid.Columns.Add("ConSlack", "Slack");
            solutionSlacksGrid.Columns["ConSlack"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            solutionSlacksGrid.Columns.Add("ConBinding", "Binding");
            solutionSlacksGrid.Columns["ConBinding"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            splitContainer.Panel2.Controls.Add(solutionSlacksGrid);
            splitContainer.Panel2.Controls.Add(new Label
            {
                Text = "Constraint Slacks",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            });

            tab.Controls.Add(splitContainer);
            tab.Controls.Add(topPanel);
        }

        /// <summary>
        /// Populates the Solution tab with solve results.
        /// </summary>
        public void ShowSolution(SolveResult? result)
        {
            solutionVariablesGrid.Rows.Clear();
            solutionSlacksGrid.Rows.Clear();

            if (result == null)
            {
                solutionStatusLabel.Text = "No solution";
                solutionStatusLabel.ForeColor = Color.Gray;
                solutionObjectiveLabel.Text = "";
                solutionTimeLabel.Text = "";
                solutionMipGapLabel.Text = "";
                return;
            }

            // Status
            (string statusText, Color statusColor) = result.Status switch
            {
                SolveStatus.Optimal => ("Optimal", Color.Green),
                SolveStatus.Feasible => ("Feasible (time/node limit)", Color.DarkOrange),
                SolveStatus.Infeasible => ("Infeasible", Color.Red),
                SolveStatus.Unbounded => ("Unbounded", Color.DarkRed),
                _ => ($"Error — {result.StatusMessage}", Color.Red)
            };
            solutionStatusLabel.Text = statusText;
            solutionStatusLabel.ForeColor = statusColor;

            // Objective / time / gap
            solutionObjectiveLabel.Text = result.ObjectiveValue.HasValue
                ? $"Objective: {result.ObjectiveValue.Value:G}"
                : "";
            solutionTimeLabel.Text = $"Solve time: {result.SolveTime.TotalSeconds:F2} s";
            solutionMipGapLabel.Text = result.MipGap.HasValue && result.MipGap.Value > 1e-10
                ? $"MIP gap: {result.MipGap.Value * 100:F4} %"
                : "";

            // Variable values — skip near-zero (< 1e-9) to reduce clutter
            foreach (var kv in result.VariableValues.OrderBy(x => x.Key))
            {
                if (Math.Abs(kv.Value) >= 1e-9)
                    solutionVariablesGrid.Rows.Add(kv.Key, $"{kv.Value:G}");
            }

            // Constraint slacks
            foreach (var kv in result.ConstraintSlacks.OrderBy(x => x.Key))
            {
                bool binding = Math.Abs(kv.Value) < 1e-6;
                solutionSlacksGrid.Rows.Add(kv.Key, $"{kv.Value:G}", binding ? "Active" : "");
                if (binding)
                    solutionSlacksGrid.Rows[solutionSlacksGrid.Rows.Count - 1].DefaultCellStyle.ForeColor
                        = Color.DarkGreen;
            }

            // Switch to Solution tab if we have a result
            if (result.Status is SolveStatus.Optimal or SolveStatus.Feasible)
                tabControl.SelectedIndex = 4;
        }

        public void Clear()
        {
            outputTextBox.Clear();
            errorsListView.Items.Clear();
            modelTreeView.Nodes.Clear();
            statisticsTextBox.Clear();
            solutionVariablesGrid.Rows.Clear();
            solutionSlacksGrid.Rows.Clear();
            solutionStatusLabel.Text = "No solution";
            solutionStatusLabel.ForeColor = Color.Gray;
            solutionObjectiveLabel.Text = "";
            solutionTimeLabel.Text = "";
            solutionMipGapLabel.Text = "";
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

        private double GetSuccessRate(ParseSessionResult result)
        {
            var total = result.SuccessCount + result.Errors.Count;
            if (total == 0) return 0;
            return Math.Round((double)result.SuccessCount / total * 100, 2);
        }

        // Event Handlers
        private void ErrorsListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (errorsListView.SelectedItems.Count > 0)
            {
                var item = errorsListView.SelectedItems[0];
                if (item.Tag is ValueTuple<string, int> tag && tag.Item2 > 0)
                {
                    ErrorDoubleClicked?.Invoke(this,
                        new ErrorNavigationEventArgs(tag.Item2, item.SubItems[3].Text, tag.Item1));
                }
            }
        }

        private void CopyError_Click(object sender, EventArgs e)
        {
            if (errorsListView.SelectedItems.Count > 0)
            {
                var error = errorsListView.SelectedItems[0].SubItems[3].Text;
                Clipboard.SetText(error);
            }
        }

        private void CopyAllErrors_Click(object sender, EventArgs e)
        {
            var allErrors = string.Join("\n", errorsListView.Items.Cast<ListViewItem>()
                .Select(item => item.SubItems[3].Text));
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
        public string? FilePath { get; }

        public ErrorNavigationEventArgs(int lineNumber, string errorMessage, string? filePath = null)
        {
            LineNumber = lineNumber;
            ErrorMessage = errorMessage;
            FilePath = filePath;
        }
    }
}