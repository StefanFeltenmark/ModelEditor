using System;
using System.Drawing;
using System.Windows.Forms;
using Core.Models;
using Core.Services;

namespace GUI
{
    public partial class RunConfigurationDialog : Form
    {
        private readonly RunConfigurationManager configManager;
        public RunConfiguration Configuration { get; private set; }

        private TextBox nameTextBox;
        private TextBox descriptionTextBox;
        private ListBox modelFilesListBox;
        private ListBox dataFilesListBox;
        private TextBox settingsFileTextBox;
        private TextBox workingDirTextBox;

        public RunConfigurationDialog(RunConfigurationManager manager, RunConfiguration existing = null)
        {
            configManager = manager;
            Configuration = existing ?? new RunConfiguration();
            
            InitializeComponent();
            LoadConfiguration();
        }

        private void InitializeComponent()
        {
            this.Text = "Run Configuration";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };

            // Name and description
            var headerPanel = CreateHeaderPanel();
            mainPanel.Controls.Add(headerPanel);

            // Tab control for file lists
            var tabControl = CreateFileTabControl();
            mainPanel.Controls.Add(tabControl);

            // Buttons
            var buttonPanel = CreateButtonPanel();
            mainPanel.Controls.Add(buttonPanel);

            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            this.Controls.Add(mainPanel);
        }

        private Panel CreateHeaderPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var nameLabel = new Label { Text = "Configuration Name:", Location = new Point(0, 0), Width = 150 };
            nameTextBox = new TextBox { Location = new Point(0, 25), Width = 560, Anchor = AnchorStyles.Left | AnchorStyles.Right };

            var descLabel = new Label { Text = "Description:", Location = new Point(0, 55), Width = 150 };
            descriptionTextBox = new TextBox 
            { 
                Location = new Point(0, 80), 
                Width = 560, 
                Height = 60,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            panel.Controls.AddRange(new Control[] { nameLabel, nameTextBox, descLabel, descriptionTextBox });
            return panel;
        }

        private TabControl CreateFileTabControl()
        {
            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // Model Files Tab
            var modelTab = new TabPage("Model Files");
            var modelPanel = new Panel { Dock = DockStyle.Fill };
            
            modelFilesListBox = new ListBox { Dock = DockStyle.Fill };
            var modelButtonPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Bottom, 
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };
            
            var addModelBtn = new Button { Text = "Add...", Width = 70 };
            addModelBtn.Click += AddModelFile_Click;
            var removeModelBtn = new Button { Text = "Remove", Width = 70 };
            removeModelBtn.Click += RemoveModelFile_Click;
            
            modelButtonPanel.Controls.AddRange(new Control[] { addModelBtn, removeModelBtn });
            modelPanel.Controls.AddRange(new Control[] { modelFilesListBox, modelButtonPanel });
            modelTab.Controls.Add(modelPanel);

            // Data Files Tab
            var dataTab = new TabPage("Data Files");
            var dataPanel = new Panel { Dock = DockStyle.Fill };
            
            dataFilesListBox = new ListBox { Dock = DockStyle.Fill };
            var dataButtonPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Bottom, 
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };
            
            var addDataBtn = new Button { Text = "Add...", Width = 70 };
            addDataBtn.Click += AddDataFile_Click;
            var removeDataBtn = new Button { Text = "Remove", Width = 70 };
            removeDataBtn.Click += RemoveDataFile_Click;
            
            dataButtonPanel.Controls.AddRange(new Control[] { addDataBtn, removeDataBtn });
            dataPanel.Controls.AddRange(new Control[] { dataFilesListBox, dataButtonPanel });
            dataTab.Controls.Add(dataPanel);

            // Settings Tab
            var settingsTab = new TabPage("Settings");
            var settingsPanel = CreateSettingsPanel();
            settingsTab.Controls.Add(settingsPanel);

            tabControl.TabPages.AddRange(new[] { modelTab, dataTab, settingsTab });
            return tabControl;
        }

        private Panel CreateSettingsPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            var settingsLabel = new Label { Text = "Settings File (optional):", Location = new Point(10, 10), Width = 150 };
            settingsFileTextBox = new TextBox { Location = new Point(10, 35), Width = 450, ReadOnly = true };
            var browseSettingsBtn = new Button { Text = "Browse...", Location = new Point(470, 33), Width = 70 };
            browseSettingsBtn.Click += BrowseSettingsFile_Click;

            var workingDirLabel = new Label { Text = "Working Directory:", Location = new Point(10, 70), Width = 150 };
            workingDirTextBox = new TextBox { Location = new Point(10, 95), Width = 450, ReadOnly = true };
            var browseWorkingDirBtn = new Button { Text = "Browse...", Location = new Point(470, 93), Width = 70 };
            browseWorkingDirBtn.Click += BrowseWorkingDirectory_Click;

            panel.Controls.AddRange(new Control[] 
            { 
                settingsLabel, settingsFileTextBox, browseSettingsBtn,
                workingDirLabel, workingDirTextBox, browseWorkingDirBtn
            });

            return panel;
        }

        private Panel CreateButtonPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            var okBtn = new Button { Text = "OK", Width = 75, DialogResult = DialogResult.OK };
            okBtn.Click += OK_Click;
            
            var cancelBtn = new Button { Text = "Cancel", Width = 75, DialogResult = DialogResult.Cancel };

            panel.Controls.AddRange(new Control[] { cancelBtn, okBtn });

            this.AcceptButton = okBtn;
            this.CancelButton = cancelBtn;

            return panel;
        }

        private void LoadConfiguration()
        {
            if (Configuration != null)
            {
                nameTextBox.Text = Configuration.Name;
                descriptionTextBox.Text = Configuration.Description;
                
                modelFilesListBox.Items.Clear();
                foreach (var file in Configuration.ModelFiles)
                {
                    modelFilesListBox.Items.Add(file);
                }

                dataFilesListBox.Items.Clear();
                foreach (var file in Configuration.DataFiles)
                {
                    dataFilesListBox.Items.Add(file);
                }

                settingsFileTextBox.Text = Configuration.SettingsFile;
                workingDirTextBox.Text = Configuration.WorkingDirectory;
            }
        }

        // Event Handlers
        private void AddModelFile_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Model Files (*.mod)|*.mod|All Files (*.*)|*.*";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        if (!Configuration.ModelFiles.Contains(file))
                        {
                            Configuration.ModelFiles.Add(file);
                            modelFilesListBox.Items.Add(file);
                        }
                    }
                }
            }
        }

        private void RemoveModelFile_Click(object sender, EventArgs e)
        {
            if (modelFilesListBox.SelectedItem is string file)
            {
                Configuration.ModelFiles.Remove(file);
                modelFilesListBox.Items.Remove(file);
            }
        }

        private void AddDataFile_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Data Files (*.dat)|*.dat|JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        if (!Configuration.DataFiles.Contains(file))
                        {
                            Configuration.DataFiles.Add(file);
                            dataFilesListBox.Items.Add(file);
                        }
                    }
                }
            }
        }

        private void RemoveDataFile_Click(object sender, EventArgs e)
        {
            if (dataFilesListBox.SelectedItem is string file)
            {
                Configuration.DataFiles.Remove(file);
                dataFilesListBox.Items.Remove(file);
            }
        }

        private void BrowseSettingsFile_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Configuration.SettingsFile = dialog.FileName;
                    settingsFileTextBox.Text = dialog.FileName;
                }
            }
        }

        private void BrowseWorkingDirectory_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Configuration.WorkingDirectory = dialog.SelectedPath;
                    workingDirTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("Please enter a configuration name.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.DialogResult = DialogResult.None;
                return;
            }

            Configuration.Name = nameTextBox.Text;
            Configuration.Description = descriptionTextBox.Text;
            
            configManager.Save(Configuration);
        }
    }
}