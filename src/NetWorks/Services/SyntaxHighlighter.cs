using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ModelEditorApp.Extensions;

namespace ModelEditorApp.Services
{
    /// <summary>
    /// Handles syntax highlighting for OPL model files
    /// </summary>
    public class SyntaxHighlighter
    {
        private readonly RichTextBox richTextBox;
        private bool isHighlighting;
        private System.Windows.Forms.Timer? debounceTimer;

        // Color scheme
        private static readonly Color CommentColor = Color.FromArgb(87, 166, 74);
        private static readonly Color StringColor = Color.FromArgb(214, 157, 133);
        private static readonly Color KeywordColor = Color.FromArgb(86, 156, 214);
        private static readonly Color TypeColor = Color.FromArgb(78, 201, 176);
        private static readonly Color NumberColor = Color.FromArgb(181, 206, 168);
        private static readonly Color OperatorColor = Color.FromArgb(180, 180, 180);
        private static readonly Color LabelColor = Color.FromArgb(220, 220, 170);
        private static readonly Color BracketColor = Color.FromArgb(218, 112, 214);
        private static readonly Color FunctionColor = Color.FromArgb(220, 220, 170);
        private static readonly Color DirectiveColor = Color.FromArgb(155, 155, 155);
        private static readonly Color DefaultColor = Color.FromArgb(212, 212, 212);

        private static readonly string[] Keywords =
        [
            "forall", "sum", "maximize", "minimize", "subject", "to",
            "dvar", "dexpr", "constraint", "tuple", "key",
            "range", "in", "execute",
            "if", "else", "and", "or", "not",
            "item", "ord", "first", "last", "next", "prev", "card"
        ];

        private static readonly string[] TypeKeywords =
        [
            "int", "float", "bool", "string"
        ];

        // Windows API to prevent painting
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);
        private const int WM_SETREDRAW = 11;

        public SyntaxHighlighter(RichTextBox textBox)
        {
            richTextBox = textBox;
        }

        /// <summary>
        /// Attaches the highlighter to the RichTextBox so it re-highlights on edits.
        /// </summary>
        public void Attach()
        {
            debounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                ApplySyntaxHighlighting();
            };

            richTextBox.TextChanged += (s, e) =>
            {
                if (!isHighlighting)
                {
                    debounceTimer.Stop();
                    debounceTimer.Start();
                }
            };

            // Initial highlight
            ApplySyntaxHighlighting();
        }

        public void ApplySyntaxHighlighting()
        {
            if (isHighlighting || !richTextBox.IsHandleCreated)
                return;

            isHighlighting = true;

            SendMessage(richTextBox.Handle, WM_SETREDRAW, false, 0);

            try
            {
                int selectionStart = richTextBox.SelectionStart;
                int selectionLength = richTextBox.SelectionLength;
                int scrollPosition = GetScrollPos();

                // Reset all text to default
                richTextBox.SelectAll();
                richTextBox.SelectionColor = DefaultColor;
                richTextBox.SelectionFont = new Font(richTextBox.Font, FontStyle.Regular);

                string text = richTextBox.Text;
                var highlighted = new HashSet<int>();

                // 1. Block comments: /* ... */
                HighlightPattern(text, @"/\*.*?\*/", CommentColor, RegexOptions.Singleline, highlighted, FontStyle.Italic);

                // 2. Line comments: // ...
                HighlightPattern(text, @"//.*$", CommentColor, RegexOptions.Multiline, highlighted, FontStyle.Italic);

                // 3. Strings: "..."
                HighlightPattern(text, @"""[^""]*""", StringColor, RegexOptions.None, highlighted);

                // 4. External data marker
                HighlightPattern(text, @"\.\.\.", DirectiveColor, RegexOptions.None, highlighted);

                // 5. Range operator: ..
                HighlightPattern(text, @"\.\.", OperatorColor, RegexOptions.None, highlighted, FontStyle.Bold);

                // 6. Relational operators
                HighlightPattern(text, @"(==|!=|<=|>=|&&|\|\||<|>)", OperatorColor, RegexOptions.None, highlighted);

                // 7. Keywords (bold blue)
                string keywordPattern = @"\b(" + string.Join("|", Keywords) + @")\b";
                HighlightPattern(text, keywordPattern, KeywordColor, RegexOptions.None, highlighted, FontStyle.Bold);

                // 8. Type keywords (teal, bold)
                string typePattern = @"\b(" + string.Join("|", TypeKeywords) + @")\b";
                HighlightPattern(text, typePattern, TypeColor, RegexOptions.None, highlighted, FontStyle.Bold);

                // 9. Sign modifiers on types: float+, float-
                HighlightPattern(text, @"\b(float|int)[+\-]", TypeColor, RegexOptions.None, highlighted, FontStyle.Bold);

                // 10. Numbers
                HighlightPattern(text, @"\b\d+(\.\d+)?([eE][+\-]?\d+)?\b", NumberColor, RegexOptions.None, highlighted);

                // 11. Labels: Name[...]: or Name:  (before constraints)
                HighlightLabelPattern(text, @"^\s*([a-zA-Z][a-zA-Z0-9_]*(?:\[[^\]]*\])*)\s*:", LabelColor, highlighted, FontStyle.Regular, RegexOptions.Multiline);

                // 12. Brackets and braces
                HighlightPattern(text, @"[\[\]{}()<>]", BracketColor, RegexOptions.None, highlighted);

                // Restore
                richTextBox.Select(selectionStart, selectionLength);
                richTextBox.SelectionColor = DefaultColor;
                richTextBox.Select(selectionStart, 0);
                SetScrollPos(scrollPosition);
            }
            finally
            {
                SendMessage(richTextBox.Handle, WM_SETREDRAW, true, 0);
                richTextBox.Invalidate();

                isHighlighting = false;
            }
        }

        private int GetScrollPos()
        {
            return richTextBox.GetFirstVisibleLineIndex();
        }

        private void SetScrollPos(int position)
        {
            if (position >= 0 && position < richTextBox.Lines.Length)
            {
                int charIndex = richTextBox.GetFirstCharIndexFromLine(position);
                if (charIndex >= 0)
                {
                    richTextBox.SelectionStart = charIndex;
                    richTextBox.ScrollToCaret();
                }
            }
        }

        private void HighlightPattern(string text, string pattern, Color color, RegexOptions options,
            HashSet<int> highlighted, FontStyle fontStyle = FontStyle.Regular)
        {
            foreach (Match match in Regex.Matches(text, pattern, options))
            {
                bool skip = false;
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    if (highlighted.Contains(i)) { skip = true; break; }
                }
                if (skip) continue;

                richTextBox.Select(match.Index, match.Length);
                richTextBox.SelectionColor = color;
                richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, richTextBox.Font.Size, fontStyle);

                for (int i = match.Index; i < match.Index + match.Length; i++)
                    highlighted.Add(i);
            }
        }

        private void HighlightLabelPattern(string text, string pattern, Color color,
            HashSet<int> highlighted, FontStyle fontStyle, RegexOptions options)
        {
            foreach (Match match in Regex.Matches(text, pattern, options))
            {
                var group = match.Groups[1];
                string label = group.Value.Split('[')[0].ToLower();

                // Skip if label is a keyword or type
                if (Keywords.Contains(label) || TypeKeywords.Contains(label))
                    continue;

                bool skip = false;
                for (int i = group.Index; i < group.Index + group.Length; i++)
                {
                    if (highlighted.Contains(i)) { skip = true; break; }
                }
                if (skip) continue;

                richTextBox.Select(group.Index, group.Length);
                richTextBox.SelectionColor = color;
                richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, richTextBox.Font.Size, fontStyle);

                for (int i = group.Index; i < group.Index + group.Length; i++)
                    highlighted.Add(i);
            }
        }
    }
}