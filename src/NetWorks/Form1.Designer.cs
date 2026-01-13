namespace NetWorks
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
            openToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            cutToolStripMenuItem = new ToolStripMenuItem();
            copyToolStripMenuItem = new ToolStripMenuItem();
            pasteToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            selectAllToolStripMenuItem = new ToolStripMenuItem();
            richTextBox1 = new RichTextBox();
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
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newToolStripMenuItem, openToolStripMenuItem, saveToolStripMenuItem, saveAsToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // newToolStripMenuItem
            // 
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            newToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newToolStripMenuItem.Size = new Size(186, 22);
            newToolStripMenuItem.Text = "&New";
            newToolStripMenuItem.Click += newToolStripMenuItem_Click;
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openToolStripMenuItem.Size = new Size(186, 22);
            openToolStripMenuItem.Text = "&Open";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
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
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(183, 6);
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
            // richTextBox1
            // 
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Font = new Font("Consolas", 11F);
            richTextBox1.Location = new Point(0, 24);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(1200, 585);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "";
            richTextBox1.WordWrap = false;
            richTextBox1.TextChanged += richTextBox1_TextChanged;
            richTextBox1.SelectionChanged += richTextBox1_SelectionChanged;
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 675);
            Controls.Add(richTextBox1);
            Controls.Add(panel1);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "Simple Text Editor";
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
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem cutToolStripMenuItem;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem pasteToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem selectAllToolStripMenuItem;
        private RichTextBox richTextBox1;
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
