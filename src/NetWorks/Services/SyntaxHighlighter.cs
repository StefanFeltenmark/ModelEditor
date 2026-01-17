using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ModelEditorApp.Extensions;

namespace ModelEditorApp.Services
{
    /// <summary>
    /// Handles syntax highlighting for the equation editor
    /// </summary>
    public class SyntaxHighlighter
    {
        private readonly RichTextBox richTextBox;
        private bool isHighlighting = false;

        // Color scheme
        private static readonly Color CommentColor = Color.Gray;
        private static readonly Color StringColor = Color.Brown;
        private static readonly Color KeywordColor = Color.Blue;
        private static readonly Color TypeColor = Color.DarkBlue;
        private static readonly Color NumberColor = Color.DarkOrange;
        private static readonly Color OperatorColor = Color.Red;
        private static readonly Color LabelColor = Color.DarkCyan;
        private static readonly Color ArithmeticOperatorColor = Color.Crimson;
        private static readonly Color BracketColor = Color.DarkMagenta;
        private static readonly Color SemicolonColor = Color.Purple;
        private static readonly Color IdentifierColor = Color.DarkGreen;
        private static readonly Color RangeOperatorColor = Color.DarkViolet;

        // Windows API to prevent painting
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);
        private const int WM_SETREDRAW = 11;

        public SyntaxHighlighter(RichTextBox textBox)
        {
            richTextBox = textBox;
        }

        public void ApplySyntaxHighlighting()
        {
            if (isHighlighting)
                return;

            isHighlighting = true;

            // Suspend drawing to reduce flicker
            SendMessage(richTextBox.Handle, WM_SETREDRAW, false, 0);

            try
            {
                // Save current selection and scroll position
                int selectionStart = richTextBox.SelectionStart;
                int selectionLength = richTextBox.SelectionLength;
                int scrollPosition = GetScrollPos();

                // Reset all text to default color
                richTextBox.SelectAll();
                richTextBox.SelectionColor = Color.Black;
                richTextBox.SelectionFont = new Font(richTextBox.Font, FontStyle.Regular);

                string text = richTextBox.Text;
                var highlightedPositions = new HashSet<int>();

                // Apply highlighting in order of priority (first wins for overlapping regions)
                HighlightPatternWithTracking(text, @"//.*$", CommentColor, RegexOptions.Multiline, highlightedPositions, FontStyle.Italic);
                HighlightPatternWithTracking(text, @"""[^""]*""", StringColor, RegexOptions.None, highlightedPositions);
                HighlightPatternWithTracking(text, @"\.\.", RangeOperatorColor, RegexOptions.None, highlightedPositions);
                HighlightPatternWithTracking(text, @"(==|<=|>=|≤|≥|<|>)", OperatorColor, RegexOptions.None, highlightedPositions, FontStyle.Bold);
                HighlightPatternWithTracking(text, @"\b(var|range|equation|execute)\b", KeywordColor, RegexOptions.None, highlightedPositions, FontStyle.Bold);
                HighlightPatternWithTracking(text, @"\b(float|int|bool|string)\b", TypeColor, RegexOptions.None, highlightedPositions, FontStyle.Bold);
                HighlightPatternWithTracking(text, @"\b\d+(?:\.\d+)?\b|\.\d+\b", NumberColor, RegexOptions.None, highlightedPositions);
                HighlightLabelPattern(text, @"([a-zA-Z][a-zA-Z0-9_]*)\s*:", LabelColor, highlightedPositions, FontStyle.Bold);
                HighlightPatternWithTracking(text, @"[+\-*]", ArithmeticOperatorColor, RegexOptions.None, highlightedPositions);
                HighlightPatternWithTracking(text, @"[\[\],{}]", BracketColor, RegexOptions.None, highlightedPositions);
                HighlightPatternWithTracking(text, @";", SemicolonColor, RegexOptions.None, highlightedPositions);
                HighlightPatternWithTracking(text, @"\b[a-zA-Z][a-zA-Z0-9_]*\b", IdentifierColor, RegexOptions.None, highlightedPositions);

                // Restore selection and scroll position
                richTextBox.Select(selectionStart, selectionLength);
                richTextBox.SelectionColor = Color.Black;
                richTextBox.Select(selectionStart, 0);
                SetScrollPos(scrollPosition);
            }
            finally
            {
                // Resume drawing
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
            if (position >= 0)
            {
                int currentFirstLine = richTextBox.GetFirstVisibleLineIndex();
                if (currentFirstLine != position)
                {
                    richTextBox.SelectionStart = richTextBox.GetFirstCharIndexFromLine(position);
                    richTextBox.ScrollToCaret();
                }
            }
        }

        private void HighlightPatternWithTracking(string text, string pattern, Color color, RegexOptions options, HashSet<int> highlightedPositions, FontStyle fontStyle = FontStyle.Regular)
        {
            Regex regex = new Regex(pattern, options);
            foreach (Match match in regex.Matches(text))
            {
                bool alreadyHighlighted = false;
                for (int i = match.Index; i < match.Index + match.Length; i++)
                {
                    if (highlightedPositions.Contains(i))
                    {
                        alreadyHighlighted = true;
                        break;
                    }
                }

                if (!alreadyHighlighted)
                {
                    richTextBox.Select(match.Index, match.Length);
                    richTextBox.SelectionColor = color;
                    richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, richTextBox.Font.Size, fontStyle);

                    for (int i = match.Index; i < match.Index + match.Length; i++)
                    {
                        highlightedPositions.Add(i);
                    }
                }
            }
        }

        private void HighlightLabelPattern(string text, string pattern, Color color, HashSet<int> highlightedPositions, FontStyle fontStyle = FontStyle.Regular)
        {
            Regex regex = new Regex(pattern);
            foreach (Match match in regex.Matches(text))
            {
                var labelGroup = match.Groups[1];
                
                string labelText = labelGroup.Value.ToLower();
                // Skip keywords that shouldn't be highlighted as labels
                if (labelText == "var" || labelText == "range" || labelText == "equation" || labelText == "execute" ||
                    labelText == "float" || labelText == "int" || labelText == "bool" || labelText == "string")
                {
                    continue;
                }

                bool alreadyHighlighted = false;
                for (int i = labelGroup.Index; i < labelGroup.Index + labelGroup.Length; i++)
                {
                    if (highlightedPositions.Contains(i))
                    {
                        alreadyHighlighted = true;
                        break;
                    }
                }

                if (!alreadyHighlighted)
                {
                    richTextBox.Select(labelGroup.Index, labelGroup.Length);
                    richTextBox.SelectionColor = color;
                    richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, richTextBox.Font.Size, fontStyle);

                    for (int i = labelGroup.Index; i < labelGroup.Index + labelGroup.Length; i++)
                    {
                        highlightedPositions.Add(i);
                    }
                }
            }
        }
    }
}