namespace ModelEditorApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            newToolStripMenuItem = new ToolStripMenuItem();
            newModelToolStripMenuItem = new ToolStripMenuItem();
            newDataFileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            openModelToolStripMenuItem = new ToolStripMenuItem();
            openDataFileToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator3 = new ToolStripSeparator();
            closeTabToolStripMenuItem = new ToolStripMenuItem();
            closeAllTabsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            saveToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            saveAllToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator6 = new ToolStripSeparator();
            exportToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            cutToolStripMenuItem = new ToolStripMenuItem();
            copyToolStripMenuItem = new ToolStripMenuItem();
            pasteToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            toolStripSeparator6 =new ToolStripSeparator();
            toolStripSeparator1 = new ToolStripSeparator();
            toolStripSeparator2 = new ToolStripSeparator();
            selectAllToolStripMenuItem = new ToolStripMenuItem();
            exportToMPSToolStripMenuItem = new ToolStripMenuItem();
            restoreSessionToolStripMenuItem = new ToolStripMenuItem();
            sessionToolStripMenuItem = new ToolStripMenuItem();
            clearSessionToolStripMenuItem = new ToolStripMenuItem();
            tabControl1 = new TabControl();
            panel1 = new Panel();
            btnParse = new Button();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabelLine = new ToolStripStatusLabel();
            toolStripStatusLabelColumn = new ToolStripStatusLabel();
            toolStripStatusLabelSeparator1 = new ToolStripStatusLabel();
            toolStripStatusLabelCharCount = new ToolStripStatusLabel();
            toolStripStatusLabelWordCount = new ToolStripStatusLabel();
            toolStripStatusLabelSeparator2 = new ToolStripStatusLabel();
            toolStripStatusLabelParseStatus = new ToolStripStatusLabel();
            toolStripStatusLabelEncoding = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            panel1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
           
            // 
            // newToolStripMenuItem
            // 
            newToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newModelToolStripMenuItem, newDataFileToolStripMenuItem });
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            newToolStripMenuItem.Size = new Size(186, 22);
            newToolStripMenuItem.Text = "&New";
            // 
            // newModelToolStripMenuItem
            // 
            newModelToolStripMenuItem.Name = "newModelToolStripMenuItem";
            newModelToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newModelToolStripMenuItem.Size = new Size(195, 22);
            newModelToolStripMenuItem.Text = "Model File (.mod)";
            newModelToolStripMenuItem.Click += newModelToolStripMenuItem_Click;
            // 
            // newDataFileToolStripMenuItem
            // 
            newDataFileToolStripMenuItem.Name = "newDataFileToolStripMenuItem";
            newDataFileToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;
            newDataFileToolStripMenuItem.Size = new Size(195, 22);
            newDataFileToolStripMenuItem.Text = "Data File (.dat)";
            newDataFileToolStripMenuItem.Click += newDataFileToolStripMenuItem_Click;
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openModelToolStripMenuItem, openDataFileToolStripMenuItem });
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.Size = new Size(186, 22);
            openToolStripMenuItem.Text = "&Open";
            // 
            // openModelToolStripMenuItem
            // 
            openModelToolStripMenuItem.Name = "openModelToolStripMenuItem";
            openModelToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openModelToolStripMenuItem.Size = new Size(195, 22);
            openModelToolStripMenuItem.Text = "Model File (.mod)";
            openModelToolStripMenuItem.Click += openModelToolStripMenuItem_Click;
            // 
            // openDataFileToolStripMenuItem
            // 
            openDataFileToolStripMenuItem.Name = "openDataFileToolStripMenuItem";
            openDataFileToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.O;
            openDataFileToolStripMenuItem.Size = new Size(195, 22);
            openDataFileToolStripMenuItem.Text = "Data File (.dat)";
            openDataFileToolStripMenuItem.Click += openDataFileToolStripMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(183, 6);
            // 
            // closeTabToolStripMenuItem
            // 
            closeTabToolStripMenuItem.Name = "closeTabToolStripMenuItem";
            closeTabToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.W;
            closeTabToolStripMenuItem.Size = new Size(186, 22);
            closeTabToolStripMenuItem.Text = "&Close Tab";
            closeTabToolStripMenuItem.Click += closeTabToolStripMenuItem_Click;
            // 
            // closeAllTabsToolStripMenuItem
            // 
            closeAllTabsToolStripMenuItem.Name = "closeAllTabsToolStripMenuItem";
            closeAllTabsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.W;
            closeAllTabsToolStripMenuItem.Size = new Size(186, 22);
            closeAllTabsToolStripMenuItem.Text = "Close All Tabs";
            closeAllTabsToolStripMenuItem.Click += closeAllTabsToolStripMenuItem_Click;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(183, 6);
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveToolStripMenuItem.Size = new Size(186, 22);
            saveToolStripMenuItem.Text = "&Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAsToolStripMenuItem.Size = new Size(186, 22);
            saveAsToolStripMenuItem.Text = "Save &As";
            saveAsToolStripMenuItem.Click += saveAsToolStripMenuItem_Click;
            // 
            // saveAllToolStripMenuItem
            // 
            saveAllToolStripMenuItem.Name = "saveAllToolStripMenuItem";
            saveAllToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.A;
            saveAllToolStripMenuItem.Size = new Size(186, 22);
            saveAllToolStripMenuItem.Text = "Save All";
            saveAllToolStripMenuItem.Click += saveAllToolStripMenuItem_Click;
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6 = new ToolStripSeparator();
            toolStripSeparator6.Name = "toolStripSeparator6";
            toolStripSeparator6.Size = new Size(183, 6);
            // 
            // exportToMPSToolStripMenuItem
            // 
            exportToMPSToolStripMenuItem.Name = "exportToMPSToolStripMenuItem";
            exportToMPSToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.E;
            exportToMPSToolStripMenuItem.Size = new Size(220, 22);
            exportToMPSToolStripMenuItem.Text = "To MPS Format...";
            exportToMPSToolStripMenuItem.Click += exportToMPSToolStripMenuItem_Click;
            // 
            // exportToolStripMenuItem
            // 
            exportToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { exportToMPSToolStripMenuItem });
            exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            exportToolStripMenuItem.Size = new Size(186, 22);
            exportToolStripMenuItem.Text = "E&xport";
           
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(183, 6);
            // 
            // sessionToolStripMenuItem
            // 
            sessionToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { restoreSessionToolStripMenuItem, clearSessionToolStripMenuItem });
            sessionToolStripMenuItem.Name = "sessionToolStripMenuItem";
            sessionToolStripMenuItem.Size = new Size(186, 22);
            sessionToolStripMenuItem.Text = "Se&ssion";
            // 
            // restoreSessionToolStripMenuItem
            // 
            restoreSessionToolStripMenuItem.Name = "restoreSessionToolStripMenuItem";
            restoreSessionToolStripMenuItem.Size = new Size(180, 22);
            restoreSessionToolStripMenuItem.Text = "Restore Session";
            restoreSessionToolStripMenuItem.Click += restoreSessionToolStripMenuItem_Click;
            // 
            // clearSessionToolStripMenuItem
            // 
            clearSessionToolStripMenuItem.Name = "clearSessionToolStripMenuItem";
            clearSessionToolStripMenuItem.Size = new Size(180, 22);
            clearSessionToolStripMenuItem.Text = "Clear Session";
            clearSessionToolStripMenuItem.Click += clearSessionToolStripMenuItem_Click;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(183, 6);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(186, 22);
            exitToolStripMenuItem.Text = "E&xit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { cutToolStripMenuItem, copyToolStripMenuItem, pasteToolStripMenuItem, toolStripSeparator2, selectAllToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "&Edit";
            // 
            // cutToolStripMenuItem
            // 
            cutToolStripMenuItem.Name = "cutToolStripMenuItem";
            cutToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.X;
            cutToolStripMenuItem.Size = new Size(164, 22);
            cutToolStripMenuItem.Text = "Cu&t";
            cutToolStripMenuItem.Click += cutToolStripMenuItem_Click;
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
            copyToolStripMenuItem.Size = new Size(164, 22);
            copyToolStripMenuItem.Text = "&Copy";
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;
            // 
            // pasteToolStripMenuItem
            // 
            pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            pasteToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.V;
            pasteToolStripMenuItem.Size = new Size(164, 22);
            pasteToolStripMenuItem.Text = "&Paste";
            pasteToolStripMenuItem.Click += pasteToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(161, 6);
            // 
            // selectAllToolStripMenuItem
            // 
            selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
            selectAllToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.A;
            selectAllToolStripMenuItem.Size = new Size(164, 22);
            selectAllToolStripMenuItem.Text = "Select &All";
            selectAllToolStripMenuItem.Click += selectAllToolStripMenuItem_Click;
            // 
            // tabControl1
            // 
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 24);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1200, 585);
            tabControl1.TabIndex = 1;
            tabControl1.SelectedIndexChanged += tabControl1_SelectedIndexChanged;
            tabControl1.MouseDown += tabControl1_MouseDown;
            // 
            // panel1
            // 
            panel1.Controls.Add(btnParse);
            panel1.Dock = DockStyle.Bottom;
            panel1.Location = new Point(0, 609);
            panel1.Name = "panel1";
            panel1.Size = new Size(1200, 40);
            panel1.TabIndex = 2;
            // 
            // btnParse
            // 
            btnParse.Location = new Point(12, 8);
            btnParse.Name = "btnParse";
            btnParse.Size = new Size(100, 23);
            btnParse.TabIndex = 0;
            btnParse.Text = "Parse";
            btnParse.UseVisualStyleBackColor = true;
            btnParse.Click += btnParse_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabelLine, toolStripStatusLabelColumn, toolStripStatusLabelSeparator1, toolStripStatusLabelCharCount, toolStripStatusLabelWordCount, toolStripStatusLabelSeparator2, toolStripStatusLabelParseStatus, toolStripStatusLabelEncoding });
            statusStrip1.Location = new Point(0, 649);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1200, 26);
            statusStrip1.TabIndex = 3;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabelLine
            // 
            toolStripStatusLabelLine.BorderSides = ToolStripStatusLabelBorderSides.Right;
            toolStripStatusLabelLine.Name = "toolStripStatusLabelLine";
            toolStripStatusLabelLine.Size = new Size(47, 21);
            toolStripStatusLabelLine.Text = "Ln: 1";
            toolStripStatusLabelLine.ToolTipText = "Line Number";
            // 
            // toolStripStatusLabelColumn
            // 
            toolStripStatusLabelColumn.BorderSides = ToolStripStatusLabelBorderSides.Right;
            toolStripStatusLabelColumn.Name = "toolStripStatusLabelColumn";
            toolStripStatusLabelColumn.Size = new Size(51, 21);
            toolStripStatusLabelColumn.Text = "Col: 1";
            toolStripStatusLabelColumn.ToolTipText = "Column Number";
            // 
            // toolStripStatusLabelSeparator1
            // 
            toolStripStatusLabelSeparator1.Name = "toolStripStatusLabelSeparator1";
            toolStripStatusLabelSeparator1.Size = new Size(13, 21);
            toolStripStatusLabelSeparator1.Text = "|";
            // 
            // toolStripStatusLabelCharCount
            // 
            toolStripStatusLabelCharCount.Name = "toolStripStatusLabelCharCount";
            toolStripStatusLabelCharCount.Size = new Size(94, 21);
            toolStripStatusLabelCharCount.Text = "Characters: 0";
            toolStripStatusLabelCharCount.ToolTipText = "Total Character Count";
            // 
            // toolStripStatusLabelWordCount
            // 
            toolStripStatusLabelWordCount.BorderSides = ToolStripStatusLabelBorderSides.Right;
            toolStripStatusLabelWordCount.Name = "toolStripStatusLabelWordCount";
            toolStripStatusLabelWordCount.Size = new Size(72, 21);
            toolStripStatusLabelWordCount.Text = "Words: 0";
            toolStripStatusLabelWordCount.ToolTipText = "Total Word Count";
            // 
            // toolStripStatusLabelSeparator2
            // 
            toolStripStatusLabelSeparator2.Name = "toolStripStatusLabelSeparator2";
            toolStripStatusLabelSeparator2.Size = new Size(13, 21);
            toolStripStatusLabelSeparator2.Text = "|";
            // 
            // toolStripStatusLabelParseStatus
            // 
            toolStripStatusLabelParseStatus.Name = "toolStripStatusLabelParseStatus";
            toolStripStatusLabelParseStatus.Size = new Size(773, 21);
            toolStripStatusLabelParseStatus.Spring = true;
            toolStripStatusLabelParseStatus.Text = "Ready";
            toolStripStatusLabelParseStatus.TextAlign = ContentAlignment.MiddleLeft;
            toolStripStatusLabelParseStatus.ToolTipText = "Parse Status";
            // 
            // toolStripStatusLabelEncoding
            // 
            toolStripStatusLabelEncoding.BorderSides = ToolStripStatusLabelBorderSides.Left;
            toolStripStatusLabelEncoding.Name = "toolStripStatusLabelEncoding";
            toolStripStatusLabelEncoding.Size = new Size(56, 21);
            toolStripStatusLabelEncoding.Text = "UTF-8";
            toolStripStatusLabelEncoding.ToolTipText = "File Encoding";

            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1200, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 
                newToolStripMenuItem, 
                openToolStripMenuItem, 
                toolStripSeparator3, 
                closeTabToolStripMenuItem, 
                closeAllTabsToolStripMenuItem, 
                toolStripSeparator4, 
                saveToolStripMenuItem, 
                saveAsToolStripMenuItem, 
                saveAllToolStripMenuItem, 
                toolStripSeparator6,          // NEW
                exportToolStripMenuItem,      // NEW
                toolStripSeparator1, 
                sessionToolStripMenuItem, 
                toolStripSeparator5, 
                exitToolStripMenuItem 
            });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";

            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 675);
            Controls.Add(tabControl1);
            Controls.Add(panel1);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "Model Editor";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            panel1.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem newToolStripMenuItem;
        private ToolStripMenuItem newModelToolStripMenuItem;
        private ToolStripMenuItem newDataFileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem openModelToolStripMenuItem;
        private ToolStripMenuItem openDataFileToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem closeTabToolStripMenuItem;
        private ToolStripMenuItem closeAllTabsToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripMenuItem saveAllToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem exportToolStripMenuItem;
        private ToolStripMenuItem exportToMPSToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem sessionToolStripMenuItem;
        private ToolStripMenuItem restoreSessionToolStripMenuItem;
        private ToolStripMenuItem clearSessionToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem cutToolStripMenuItem;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem pasteToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem selectAllToolStripMenuItem;
        private TabControl tabControl1;
        private Panel panel1;
        private Button btnParse;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabelLine;
        private ToolStripStatusLabel toolStripStatusLabelColumn;
        private ToolStripStatusLabel toolStripStatusLabelSeparator1;
        private ToolStripStatusLabel toolStripStatusLabelCharCount;
        private ToolStripStatusLabel toolStripStatusLabelWordCount;
        private ToolStripStatusLabel toolStripStatusLabelSeparator2;
        private ToolStripStatusLabel toolStripStatusLabelParseStatus;
        private ToolStripStatusLabel toolStripStatusLabelEncoding;
    }
}
