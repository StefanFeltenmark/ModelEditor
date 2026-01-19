using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Expands sum(...) expressions and distributes coefficients over parentheses
    /// </summary>
    public class SummationExpander
    {
        private readonly ModelManager modelManager;

        public SummationExpander(ModelManager manager)
        {
            modelManager = manager;
        }

        public string ExpandSummations(string expression, out string error)
        {
            error = string.Empty;

            int maxIterations = 100;
            int iterations = 0;

            while (iterations < maxIterations)
            {
                var match = Regex.Match(expression,
                    @"sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)",
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    break;

                iterations++;

                string indexVar = match.Groups[1].Value;
                string indexSetName = match.Groups[2].Value;

                if (!modelManager.IndexSets.TryGetValue(indexSetName, out var indexSet))
                {
                    error = $"Index set '{indexSetName}' not found in sum expression";
                    return expression;
                }

                int exprStart = match.Index + match.Length;
                int exprEnd = FindSumExpressionEnd(expression, exprStart);

                if (exprEnd <= exprStart)
                {
                    error = $"Empty or invalid sum expression after 'sum({indexVar} in {indexSetName})'";
                    return expression;
                }

                string sumExpr = expression.Substring(exprStart, exprEnd - exprStart).Trim();

                if (string.IsNullOrWhiteSpace(sumExpr))
                {
                    error = $"Empty sum expression after 'sum({indexVar} in {indexSetName})'";
                    return expression;
                }

                var terms = new List<string>();
                foreach (int idx in indexSet.GetIndices())
                {
                    string expandedTerm = ReplaceIndexVariable(sumExpr, indexVar, idx);
                    terms.Add(expandedTerm);
                }

                string expandedSum = terms.Count > 0
                    ? "(" + string.Join("+", terms) + ")"
                    : "0";

                expression = expression.Substring(0, match.Index) + expandedSum + expression.Substring(exprEnd);
            }

            if (iterations >= maxIterations)
            {
                error = "Maximum sum expansion iterations exceeded - possible nested sums issue";
                return expression;
            }

            expression = ExpandParenthesesMultiplication(expression);
            return expression;
        }

        public string ExpandParenthesesMultiplication(string expression)
        {
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

                if (!double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
                    break;

                var terms = SplitPreservingOperators(innerExpr);
                var expandedTerms = new List<string>();

                foreach (var term in terms)
                {
                    string trimmedTerm = term.Trim();

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

                    double newCoeff = coeff * termSign;

                    string expandedTerm;
                    if (Math.Abs(newCoeff - 1.0) < 1e-10)
                        expandedTerm = trimmedTerm;
                    else if (Math.Abs(newCoeff + 1.0) < 1e-10)
                        expandedTerm = "-" + trimmedTerm;
                    else
                        expandedTerm = $"{newCoeff:G}*{trimmedTerm}";

                    expandedTerms.Add(expandedTerm);
                }

                string expanded = string.Join("+", expandedTerms);
                expression = expression.Substring(0, match.Index) +
                            expanded +
                            expression.Substring(match.Index + match.Length);
            }

            return expression;
        }

        private int FindSumExpressionEnd(string expression, int start)
        {
            int parenDepth = 0;
            int bracketDepth = 0;
            bool inNumber = false;

            for (int i = start; i < expression.Length; i++)
            {
                char c = expression[i];

                if (c == '(')
                    parenDepth++;
                else if (c == ')')
                {
                    parenDepth--;
                    if (parenDepth < 0)
                        return i;
                }
                else if (c == '[')
                    bracketDepth++;
                else if (c == ']')
                    bracketDepth--;

                if (parenDepth == 0 && bracketDepth == 0)
                {
                    if (char.IsDigit(c) || c == '.')
                    {
                        inNumber = true;
                        continue;
                    }

                    if (i + 1 < expression.Length)
                    {
                        string twoChar = expression.Substring(i, 2);
                        if (twoChar == "==" || twoChar == "<=" || twoChar == ">=" || twoChar == "≥" || twoChar == "≤")
                            return i;
                    }

                    if (c == '<' || c == '>' || c == '=')
                        return i;

                    if ((c == '+' || c == '-') && !inNumber)
                    {
                        if (i > start)
                        {
                            char prev = expression[i - 1];
                            if (prev != '*' && prev != '/' && prev != '(' && prev != '[' && prev != ',')
                                return i;
                        }
                    }

                    if (c != ' ' && c != '\t')
                        inNumber = false;
                }
            }

            return expression.Length;
        }

        private string ReplaceIndexVariable(string expr, string indexVar, int indexValue)
        {
            string escapedVar = Regex.Escape(indexVar);

            expr = Regex.Replace(expr, $@"\[{escapedVar}\]", $"[{indexValue}]");
            expr = Regex.Replace(expr, $@"\[{escapedVar}\s*,", $"[{indexValue},");
            expr = Regex.Replace(expr, $@",\s*{escapedVar}\]", $",{indexValue}]");

            return expr;
        }

        private List<string> SplitPreservingOperators(string expr)
        {
            var terms = new List<string>();
            var currentTerm = new StringBuilder();
            bool isFirstChar = true;

            foreach (char c in expr)
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
                terms.Add(currentTerm.ToString().Trim());

            return terms;
        }

        /// <summary>
        /// Expands an equation template by replacing an index variable with a specific value
        /// </summary>
        public string ExpandEquationTemplate(string template, string indexVar, int indexValue)
        {
            if (string.IsNullOrWhiteSpace(template))
                return template;

            // Simple replacement: replace [indexVar with [indexValue
            // This handles most common cases
            return template
                .Replace($"[{indexVar}]", $"[{indexValue}]")
                .Replace($"[{indexVar},", $"[{indexValue},")
                .Replace($",{indexVar}]", $",{indexValue}]")
                .Replace($",{indexVar},", $",{indexValue},")
                .Replace($"[ {indexVar} ]", $"[{indexValue}]")
                .Replace($"[ {indexVar},", $"[{indexValue},")
                .Replace($",{indexVar} ]", $",{indexValue}]")
                .Replace($", {indexVar},", $",{indexValue},");
        }
    }
}