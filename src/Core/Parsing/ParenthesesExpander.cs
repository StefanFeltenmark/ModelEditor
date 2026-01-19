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
            // Pattern to match: coefficient*(term1+term2+...)
            var pattern = @"([+-]?[\d.]+)\s*\*\s*\(([^)]+)\)";

            int maxIterations = 100;
            int iterations = 0;

            while (iterations < maxIterations)
            {
                var match = Regex.Match(expression, pattern);

                if (!match.Success)
                    break;

                iterations++;

                string coeffStr = match.Groups[1].Value;
                string innerExpr = match.Groups[2].Value;

                // Parse the coefficient
                if (!double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
                {
                    break;
                }

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
                            expression.Substring(match.Index + match.Length);
            }

            return expression;
        }

        private List<string> SplitExpressionIntoTerms(string innerExpr)
        {
            var terms = new List<string>();
            var currentTerm = new StringBuilder();
            bool isFirstChar = true;

            foreach (char c in innerExpr)
            {
                if ((c == '+' || c == '-') && !isFirstChar)
                {
                    if (currentTerm.Length > 0)
                    {
                        terms.Add(currentTerm.ToString().Trim());
                        currentTerm.Clear();
                    }
                    currentTerm.Append(c);
                }
                else
                {
                    currentTerm.Append(c);
                    isFirstChar = false;
                }
            }

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