using System;
using System.Drawing;
using System.Media;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GUI
{
    /// <summary>
    /// A dockable Find/Replace bar for RichTextBox editors.
    /// </summary>
    public class FindReplaceControl : UserControl
    {
        private TextBox searchBox;
        private TextBox replaceBox;
        private Button findNextButton;
        private Button findPrevButton;
        private Button replaceButton;
        private Button replaceAllButton;
        private Button closeButton;
        private CheckBox matchCaseCheck;
        private CheckBox wholeWordCheck;
        private Label statusLabel;
        private Panel replacePanel;
        private bool replaceVisible;

        private RichTextBox? targetTextBox;
        private int lastSearchIndex = -1;

        public FindReplaceControl()
        {
            InitializeLayout();
        }

        private void InitializeLayout()
        {
            this.Height = 68;
            this.Dock = DockStyle.Top;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.Padding = new Padding(4);

            // --- Find row ---
            var findPanel = new Panel { Dock = DockStyle.Top, Height = 30 };

            closeButton = new Button
            {
                Text = "✕",
                Size = new Size(24, 24),
                Location = new Point(4, 3),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Silver,
                BackColor = Color.FromArgb(45, 45, 48),
                Font = new Font("Segoe UI", 8f)
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => Hide();

            searchBox = new TextBox
            {
                Location = new Point(32, 3),
                Width = 220,
                Height = 24,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            searchBox.KeyDown += SearchBox_KeyDown;
            searchBox.TextChanged += (s, e) => UpdateMatchStatus();

            findPrevButton = CreateSmallButton("◀", 258, "Find Previous (Shift+Enter)");
            findPrevButton.Click += (s, e) => FindPrevious();

            findNextButton = CreateSmallButton("▶", 286, "Find Next (Enter)");
            findNextButton.Click += (s, e) => FindNext();

            matchCaseCheck = new CheckBox
            {
                Text = "Aa",
                Location = new Point(320, 4),
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Consolas", 8.5f, FontStyle.Bold)
            };
            matchCaseCheck.CheckedChanged += (s, e) => UpdateMatchStatus();

            wholeWordCheck = new CheckBox
            {
                Text = "W",
                Location = new Point(365, 4),
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Consolas", 8.5f, FontStyle.Bold)
            };
            wholeWordCheck.CheckedChanged += (s, e) => UpdateMatchStatus();

            var toggleReplaceButton = CreateSmallButton("⇅", 400, "Toggle Replace");
            toggleReplaceButton.Click += (s, e) => ToggleReplace();

            statusLabel = new Label
            {
                Location = new Point(430, 6),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8f)
            };

            findPanel.Controls.AddRange([closeButton, searchBox, findPrevButton, findNextButton,
                matchCaseCheck, wholeWordCheck, toggleReplaceButton, statusLabel]);

            // --- Replace row ---
            replacePanel = new Panel { Dock = DockStyle.Top, Height = 30, Visible = false };

            replaceBox = new TextBox
            {
                Location = new Point(32, 3),
                Width = 220,
                Height = 24,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            replaceBox.KeyDown += ReplaceBox_KeyDown;

            replaceButton = CreateSmallButton("↷", 258, "Replace (Enter)");
            replaceButton.Click += (s, e) => ReplaceCurrent();

            replaceAllButton = CreateSmallButton("↷All", 286, "Replace All");
            replaceAllButton.Width = 40;
            replaceAllButton.Click += (s, e) => ReplaceAll();

            replacePanel.Controls.AddRange([replaceBox, replaceButton, replaceAllButton]);

            // Order matters: replace below find
            this.Controls.Add(replacePanel);
            this.Controls.Add(findPanel);
        }

        private Button CreateSmallButton(string text, int x, string tooltip)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(26, 24),
                Location = new Point(x, 3),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Silver,
                BackColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 8f)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);

            var tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);

            return btn;
        }

        private void ToggleReplace()
        {
            replaceVisible = !replaceVisible;
            replacePanel.Visible = replaceVisible;
            this.Height = replaceVisible ? 68 : 34;
        }

        public void ShowFind(RichTextBox target)
        {
            targetTextBox = target;
            replaceVisible = false;
            replacePanel.Visible = false;
            this.Height = 34;
            this.Visible = true;
            searchBox.Focus();

            // Pre-fill with selected text
            if (!string.IsNullOrEmpty(target.SelectedText) && !target.SelectedText.Contains('\n'))
            {
                searchBox.Text = target.SelectedText;
            }
            searchBox.SelectAll();
            UpdateMatchStatus();
        }

        public void ShowReplace(RichTextBox target)
        {
            targetTextBox = target;
            replaceVisible = true;
            replacePanel.Visible = true;
            this.Height = 68;
            this.Visible = true;
            searchBox.Focus();

            if (!string.IsNullOrEmpty(target.SelectedText) && !target.SelectedText.Contains('\n'))
            {
                searchBox.Text = target.SelectedText;
            }
            searchBox.SelectAll();
            UpdateMatchStatus();
        }

        public void FindNext()
        {
            if (targetTextBox == null || string.IsNullOrEmpty(searchBox.Text))
                return;

            int startPos = targetTextBox.SelectionStart + targetTextBox.SelectionLength;
            int index = FindInText(startPos, forward: true);

            if (index >= 0)
            {
                SelectMatch(index);
            }
            else
            {
                // Wrap around
                index = FindInText(0, forward: true);
                if (index >= 0)
                {
                    SelectMatch(index);
                    statusLabel.Text += " (wrapped)";
                }
                else
                {
                    statusLabel.Text = "No matches";
                    SystemSounds.Beep.Play();
                }
            }
        }

        public void FindPrevious()
        {
            if (targetTextBox == null || string.IsNullOrEmpty(searchBox.Text))
                return;

            int startPos = targetTextBox.SelectionStart - 1;
            if (startPos < 0) startPos = targetTextBox.Text.Length - 1;

            int index = FindInText(startPos, forward: false);

            if (index >= 0)
            {
                SelectMatch(index);
            }
            else
            {
                // Wrap around
                index = FindInText(targetTextBox.Text.Length - 1, forward: false);
                if (index >= 0)
                {
                    SelectMatch(index);
                    statusLabel.Text += " (wrapped)";
                }
                else
                {
                    statusLabel.Text = "No matches";
                    SystemSounds.Beep.Play();
                }
            }
        }

        private void ReplaceCurrent()
        {
            if (targetTextBox == null || string.IsNullOrEmpty(searchBox.Text))
                return;

            var comparison = matchCaseCheck.Checked
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            // If current selection matches the search term, replace it
            if (string.Equals(targetTextBox.SelectedText, searchBox.Text, comparison))
            {
                targetTextBox.SelectedText = replaceBox.Text;
            }

            FindNext();
            UpdateMatchStatus();
        }

        private void ReplaceAll()
        {
            if (targetTextBox == null || string.IsNullOrEmpty(searchBox.Text))
                return;

            string pattern = BuildPattern();
            var options = matchCaseCheck.Checked ? RegexOptions.None : RegexOptions.IgnoreCase;

            int count = Regex.Matches(targetTextBox.Text, pattern, options).Count;
            if (count == 0)
            {
                statusLabel.Text = "No matches";
                return;
            }

            targetTextBox.Text = Regex.Replace(targetTextBox.Text, pattern, replaceBox.Text, options);
            statusLabel.Text = $"Replaced {count} occurrence(s)";
        }

        private int FindInText(int startPos, bool forward)
        {
            if (targetTextBox == null) return -1;

            string text = targetTextBox.Text;
            string search = searchBox.Text;

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
                return -1;

            var comparison = matchCaseCheck.Checked
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            if (forward)
            {
                int index = text.IndexOf(search, startPos, comparison);
                if (index >= 0 && wholeWordCheck.Checked && !IsWholeWord(text, index, search.Length))
                {
                    return FindInText(index + 1, forward: true);
                }
                return index;
            }
            else
            {
                int searchLen = search.Length;
                for (int i = Math.Min(startPos, text.Length - searchLen); i >= 0; i--)
                {
                    if (string.Compare(text, i, search, 0, searchLen, comparison) == 0)
                    {
                        if (wholeWordCheck.Checked && !IsWholeWord(text, i, searchLen))
                            continue;
                        return i;
                    }
                }
                return -1;
            }
        }

        private static bool IsWholeWord(string text, int index, int length)
        {
            bool startOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            bool endOk = index + length >= text.Length || !char.IsLetterOrDigit(text[index + length]);
            return startOk && endOk;
        }

        private void SelectMatch(int index)
        {
            if (targetTextBox == null) return;

            targetTextBox.Select(index, searchBox.Text.Length);
            targetTextBox.ScrollToCaret();
            lastSearchIndex = index;
            UpdateMatchStatus();
        }

        private void UpdateMatchStatus()
        {
            if (targetTextBox == null || string.IsNullOrEmpty(searchBox.Text))
            {
                statusLabel.Text = "";
                return;
            }

            string pattern = BuildPattern();
            var options = matchCaseCheck.Checked ? RegexOptions.None : RegexOptions.IgnoreCase;

            try
            {
                int count = Regex.Matches(targetTextBox.Text, pattern, options).Count;
                statusLabel.Text = count == 0 ? "No matches" : $"{count} match(es)";
                statusLabel.ForeColor = count == 0 ? Color.FromArgb(255, 100, 100) : Color.Gray;
            }
            catch
            {
                statusLabel.Text = "";
            }
        }

        private string BuildPattern()
        {
            string escaped = Regex.Escape(searchBox.Text);
            return wholeWordCheck.Checked ? $@"\b{escaped}\b" : escaped;
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Shift)
            {
                e.SuppressKeyPress = true;
                FindPrevious();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                FindNext();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                this.Visible = false;
                targetTextBox?.Focus();
            }
        }

        private void ReplaceBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ReplaceCurrent();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                this.Visible = false;
                targetTextBox?.Focus();
            }
        }
    }
}
