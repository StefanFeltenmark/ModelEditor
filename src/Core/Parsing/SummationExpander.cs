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

        /// <summary>
        /// Expands sum(...) expressions
        /// Skips expansion for tuple sets and computed sets
        /// </summary>
        public string ExpandSummations(string expression, out string error)
        {
            error = string.Empty;
            
            int maxIterations = 100;
            int iterations = 0;

            while (iterations < maxIterations)
            {
                iterations++;
                
                // Find the next sum(...) expression
                var match = Regex.Match(expression,
                    @"sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)",
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    break;

                string indexVar = match.Groups[1].Value;
                string setName = match.Groups[2].Value;

                // CHECK: Is this a tuple set or computed set?
                if (IsTupleSetOrComputedTupleSet(setName))
                {
                    // DON'T EXPAND - leave as summation expression
                    break;
                }

                // Find the body of the summation
                int exprStart = match.Index + match.Length;
                int exprEnd = FindSumExpressionEnd(expression, exprStart);

                if (exprEnd <= exprStart)
                {
                    error = $"Empty or invalid sum expression after 'sum({indexVar} in {setName})'";
                    return expression;
                }

                string sumBody = expression.Substring(exprStart, exprEnd - exprStart).Trim();

                if (string.IsNullOrWhiteSpace(sumBody))
                {
                    error = $"Empty sum expression after 'sum({indexVar} in {setName})'";
                    return expression;
                }

                // Get the indices to iterate over
                IEnumerable<int>? indices = null;
                
                // Try ranges
                if (modelManager.Ranges.TryGetValue(setName, out var range))
                {
                    indices = range.GetValues(modelManager);
                }
                // Try index sets
                else if (modelManager.IndexSets.TryGetValue(setName, out var indexSet))
                {
                    indices = indexSet.GetIndices();
                }
                // Try Sets dictionary
                else if (modelManager.Sets.TryGetValue(setName, out var set))
                {
                    indices = set;
                }
                else
                {
                    error = $"Index set or range '{setName}' not found";
                    return expression;
                }

                // Expand the summation
                var terms = new List<string>();
                foreach (int index in indices)
                {
                    string expandedTerm = ExpandEquationTemplate(sumBody, indexVar, index);
                    // Don't wrap individual terms - we'll wrap the whole sum if needed
                    terms.Add(expandedTerm);
                }

                // Build the expanded sum
                string expandedSum;
                if (terms.Count == 0)
                {
                    expandedSum = "0";
                }
                else if (terms.Count == 1)
                {
                    // Single term doesn't need parentheses
                    expandedSum = terms[0];
                }
                else
                {
                    // Multiple terms - wrap in parentheses to preserve operation order
                    // This is crucial for expressions like: 2*sum(...) 
                    expandedSum = "(" + string.Join("+", terms) + ")";
                }

                // Replace the sum expression with the expanded version
                expression = expression.Substring(0, match.Index) + 
                           expandedSum + 
                           expression.Substring(exprEnd);
            }

            if (iterations >= maxIterations)
            {
                error = "Maximum sum expansion iterations exceeded - possible nested sums issue";
                return expression;
            }

            return expression;
        }

        /// <summary>
        /// Checks if a set is a tuple set or computed set (which should not be expanded)
        /// </summary>
        private bool IsTupleSetOrComputedTupleSet(string setName)
        {
            // Check if it's a tuple set
            if (modelManager.TupleSets.ContainsKey(setName))
            {
                return true;
            }
            
            // Check if it's a computed set (which might contain tuples)
            if (modelManager.ComputedSets.ContainsKey(setName))
            {
                return true;
            }
            
            // Check if it's a primitive set - these can be expanded if needed
            if (modelManager.PrimitiveSets.ContainsKey(setName))
            {
                return false; // Primitive sets can be expanded
            }
            
            return false;
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

        /// <summary>
        /// Expands an equation template by replacing an index variable with a specific value
        /// </summary>
        public string ExpandEquationTemplate(string template, string indexVar, int indexValue)
        {
            if (string.IsNullOrWhiteSpace(template))
                return template;

            // Simple replacement: replace [indexVar with [indexValue
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
    }
}