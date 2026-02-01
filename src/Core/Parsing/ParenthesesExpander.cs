using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Parsing
{
    /// <summary>
    /// Expands expressions like 2*(x+y+z) into 2*x+2*y+2*z
    /// This handles the case where a sum expansion creates parenthesized terms
    /// </summary>
    public class ParenthesesExpander
    {
        public string ExpandParenthesesMultiplication(string expression)
        {
            int maxIterations = 100;
            int iterations = 0;

            while (iterations < maxIterations)
            {
                iterations++;

                // Find coefficient*( pattern
                var coeffPattern = @"([+-]?[\d.]+)\s*\*\s*\(";
                var match = Regex.Match(expression, coeffPattern);

                if (!match.Success)
                    break;

                string coeffStr = match.Groups[1].Value;

                // Parse the coefficient
                if (!double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
                {
                    break;
                }

                // Find the matching closing parenthesis
                int openParenIndex = match.Index + match.Length - 1; // Position of '('
                int closeParenIndex = FindMatchingCloseParen(expression, openParenIndex);

                if (closeParenIndex == -1)
                {
                    break; // No matching closing paren found
                }

                // Extract the inner expression
                int innerStart = openParenIndex + 1;
                int innerLength = closeParenIndex - innerStart;
                string innerExpr = expression.Substring(innerStart, innerLength);

                // Split the inner expression by + and - while preserving operators
                var terms = SplitExpressionIntoTerms(innerExpr);

                // Multiply each term by the coefficient
                var expandedTerms = new List<string>();
                foreach (var term in terms)
                {
                    string trimmedTerm = term.Trim();

                    // Handle the sign of the term
                    double termSign = 1.0;
                    if (trimmedTerm.StartsWith("-"))
                    {
                        termSign = -1.0;
                        trimmedTerm = trimmedTerm.Substring(1).Trim();
                    }
                    else if (trimmedTerm.StartsWith("+"))
                    {
                        trimmedTerm = trimmedTerm.Substring(1).Trim();
                    }

                    // Calculate the new coefficient
                    double newCoeff = coeff * termSign;

                    // Build the expanded term
                    string expandedTerm = FormatExpandedTerm(newCoeff, trimmedTerm);
                    expandedTerms.Add(expandedTerm);
                }

                // Join the expanded terms
                string expanded = string.Join("+", expandedTerms);

                // Replace in the original expression
                expression = expression.Substring(0, match.Index) +
                            expanded +
                            expression.Substring(closeParenIndex + 1);
            }

            return expression;
        }

        /// <summary>
        /// Finds the matching closing parenthesis, accounting for nested brackets and parens
        /// </summary>
        private int FindMatchingCloseParen(string expression, int openParenIndex)
        {
            int depth = 1;
            int bracketDepth = 0;

            for (int i = openParenIndex + 1; i < expression.Length; i++)
            {
                char c = expression[i];

                if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    bracketDepth--;
                }
                else if (c == '(' && bracketDepth == 0)
                {
                    depth++;
                }
                else if (c == ')' && bracketDepth == 0)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i; // Found the matching closing paren
                    }
                }
            }

            return -1; // No matching paren found
        }

        /// <summary>
        /// Splits an expression into terms, respecting bracket depth
        /// Example: "x[1]+x[2]" -> ["x[1]", "+x[2]"]
        /// Example: "x[i+1]+x[2]" -> ["x[i+1]", "+x[2]"] (doesn't split on + inside brackets)
        /// </summary>
        private List<string> SplitExpressionIntoTerms(string innerExpr)
        {
            var terms = new List<string>();
            var currentTerm = new StringBuilder();
            int bracketDepth = 0;
            int parenDepth = 0;
            bool isFirstChar = true;

            for (int i = 0; i < innerExpr.Length; i++)
            {
                char c = innerExpr[i];
                
                // Track bracket and parenthesis depth
                if (c == '[')
                {
                    bracketDepth++;
                    currentTerm.Append(c);
                }
                else if (c == ']')
                {
                    bracketDepth--;
                    currentTerm.Append(c);
                }
                else if (c == '(')
                {
                    parenDepth++;
                    currentTerm.Append(c);
                }
                else if (c == ')')
                {
                    parenDepth--;
                    currentTerm.Append(c);
                }
                // Only split on + or - if we're at depth 0
                else if ((c == '+' || c == '-') && bracketDepth == 0 && parenDepth == 0 && !isFirstChar)
                {
                    // Save the current term
                    if (currentTerm.Length > 0)
                    {
                        terms.Add(currentTerm.ToString().Trim());
                        currentTerm.Clear();
                    }
                    
                    // Start a new term with the operator
                    currentTerm.Append(c);
                }
                else
                {
                    currentTerm.Append(c);
                }
                
                // After the first non-whitespace character, we're no longer at the start
                if (c != ' ' && c != '\t')
                {
                    isFirstChar = false;
                }
            }

            // Add the last term
            if (currentTerm.Length > 0)
            {
                terms.Add(currentTerm.ToString().Trim());
            }

            return terms;
        }

        private string FormatExpandedTerm(double coeff, string term)
        {
            if (Math.Abs(coeff - 1.0) < 1e-10)
            {
                return term;
            }
            else if (Math.Abs(coeff + 1.0) < 1e-10)
            {
                return "-" + term;
            }
            else
            {
                return $"{coeff:G}*{term}";
            }
        }
    }
}