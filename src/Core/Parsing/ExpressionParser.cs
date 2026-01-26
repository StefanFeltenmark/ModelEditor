using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses mathematical expressions to extract coefficients and constants
    /// </summary>
    public class ExpressionParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;
        private readonly VariableValidator variableValidator;
        private readonly TokenizationOrchestrator tokenizationOrchestrator;

        public ExpressionParser(ModelManager manager)
        {
            modelManager = manager;
            evaluator = new ExpressionEvaluator(manager);
            variableValidator = new VariableValidator(manager);
            tokenizationOrchestrator = new TokenizationOrchestrator();
        }

        public bool TryParseExpression(
            string expression,
            out Dictionary<string, Expression> coefficients,
            out Expression constant,
            out string error)
        {
            coefficients = new Dictionary<string, Expression>();
            constant = new ConstantExpression(0);
            error = string.Empty;

            try
            {
                var tokenManager = new TokenManager();

                // **Apply all tokenization strategies**
                expression = tokenizationOrchestrator.TokenizeExpression(expression, tokenManager, modelManager);

                // **Parse coefficients and variables**
                bool foundVariables = false;
                var processedIndices = new HashSet<int>();

                // Process explicit multiplication with parenthesized expressions
                string patternWithMultiply = @"(\([^()]+\)|[+-]?[\d.]+|__PARAM\d+__|__TUPLE\d+__|__ITEM\d+__)\*([a-zA-Z][a-zA-Z0-9_]*)";

                MatchCollection explicitMatches = Regex.Matches(expression, patternWithMultiply);
                foreach (Match match in explicitMatches)
                {
                    if (!ProcessExplicitMultiplication(match, tokenManager, coefficients, 
                        processedIndices, ref foundVariables, out error))
                    {
                        return false;
                    }
                }

                // Process implicit multiplication
                string remainingExpression = BuildRemainingExpression(expression, processedIndices);
                string patternImplicit = @"([+-]?[\d.]*|__PARAM\d+__|__TUPLE\d+__|__ITEM\d+__)([a-zA-Z][a-zA-Z0-9_]*)";
                
                MatchCollection implicitMatches = Regex.Matches(remainingExpression, patternImplicit);
                foreach (Match match in implicitMatches)
                {
                    if (!ProcessImplicitMultiplication(match, tokenManager, coefficients, 
                        ref foundVariables, out error))
                    {
                        return false;
                    }
                }

                // **Extract constant terms**
                constant = ExtractConstants(expression, coefficients, tokenManager, processedIndices);

                // If no variables found, try parsing entire expression as constant
                if (!foundVariables && constant is ConstantExpression ce && Math.Abs(ce.Value) < 1e-10)
                {
                    if (tokenManager.TryGetExpression(expression.Trim(), out var constTokenExpr))
                    {
                        constant = constTokenExpr!;
                    }
                    else if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, 
                        out double constValue))
                    {
                        constant = new ConstantExpression(constValue);
                    }
                    else
                    {
                        error = $"Invalid expression: '{expression}'";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Error parsing expression: {ex.Message}";
                return false;
            }
        }

            private bool ProcessExplicitMultiplication(
            Match match,
            TokenManager tokenManager,
            Dictionary<string, Expression> coefficients,
            HashSet<int> processedIndices,
            ref bool foundVariables,
            out string error)
        {
            error = string.Empty;
            string coeffStr = match.Groups[1].Value;
            string variable = match.Groups[2].Value;

            if (string.IsNullOrEmpty(variable))
                return true;

            foundVariables = true;

            Expression coeffExpr;

            // Handle parenthesized expressions
            if (coeffStr.StartsWith("(") && coeffStr.EndsWith(")"))
            {
                string parenContent = coeffStr.Substring(1, coeffStr.Length - 2);
                
                if (tokenManager.ContainsTokens(parenContent))
                {
                    coeffExpr = ParseTokenizedExpression(parenContent, tokenManager, out error);
                    if (coeffExpr == null)
                    {
                        error = $"Could not parse coefficient expression '{coeffStr}' for variable '{variable}': {error}";
                        return false;
                    }
                }
                else
                {
                    var evalResult = evaluator.EvaluateFloatExpression(parenContent);
                    if (!evalResult.IsSuccess)
                    {
                        error = $"Could not evaluate coefficient expression '{coeffStr}' for variable '{variable}': {evalResult.ErrorMessage}";
                        return false;
                    }
                    coeffExpr = new ConstantExpression(evalResult.Value);
                }
            }
            else if (tokenManager.TryGetExpression(coeffStr, out var tokenExpr))
            {
                coeffExpr = tokenExpr!;
            }
            else if (double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
            {
                coeffExpr = new ConstantExpression(coeff);
            }
            else
            {
                error = $"Invalid coefficient '{coeffStr}' for variable '{variable}'";
                return false;
            }

            AddCoefficient(coefficients, variable, coeffExpr);
            MarkAsProcessed(match, processedIndices);
            
            return true;
        }

        private bool ProcessImplicitMultiplication(
            Match match,
            TokenManager tokenManager,
            Dictionary<string, Expression> coefficients,
            ref bool foundVariables,
            out string error)
        {
            error = string.Empty;
            string coeffStr = match.Groups[1].Value;
            string variable = match.Groups[2].Value;

            if (string.IsNullOrEmpty(variable) || 
                variable.StartsWith("PARAM") || 
                variable.StartsWith("TUPLE") ||
                variable.StartsWith("ITEM"))
            {
                return true;
            }

            foundVariables = true;

            Expression coeffExpr;
            
            if (string.IsNullOrEmpty(coeffStr) || coeffStr == "+")
            {
                coeffExpr = new ConstantExpression(1);
            }
            else if (coeffStr == "-")
            {
                coeffExpr = new ConstantExpression(-1);
            }
            else if (tokenManager.TryGetExpression(coeffStr, out var tokenExpr))
            {
                coeffExpr = tokenExpr!;
            }
            else if (double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
            {
                coeffExpr = new ConstantExpression(coeff);
            }
            else
            {
                error = $"Invalid coefficient '{coeffStr}' for variable '{variable}'";
                return false;
            }

            AddCoefficient(coefficients, variable, coeffExpr);
            return true;
        }

        private Expression ExtractConstants(
            string expression,
            Dictionary<string, Expression> coefficients,
            TokenManager tokenManager,
            HashSet<int> processedIndices)
        {
            Expression constant = new ConstantExpression(0);

            // Pattern to find standalone numbers
            string constantPattern = @"(?:^|(?<=[+\-]))(\d+\.\d+|\d+(?!\.\d))(?![a-zA-Z_*])";
            var constantMatches = Regex.Matches(expression, constantPattern);

            var constantTerms = new List<Expression>();
            
            foreach (Match match in constantMatches)
            {
                // Skip if part of a token
                if (match.Index > 0 && match.Index < expression.Length - 1)
                {
                    string checkToken = expression.Substring(Math.Max(0, match.Index - 10),
                        Math.Min(20, expression.Length - Math.Max(0, match.Index - 10)));
                    if (checkToken.Contains("__PARAM") || checkToken.Contains("__TUPLE") || checkToken.Contains("__ITEM"))
                        continue;
                }

                // Skip if part of a coefficient
                int endPos = match.Index + match.Length;
                if (endPos < expression.Length)
                {
                    string after = expression.Substring(endPos, Math.Min(2, expression.Length - endPos));
                    if (after.StartsWith("*") || char.IsLetter(after[0]))
                        continue;
                }

                string numStr = match.Groups[1].Value;
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double constValue))
                {
                    constantTerms.Add(new ConstantExpression(constValue));
                }
            }

            // Combine constant terms
            if (constantTerms.Count > 0)
            {
                constant = constantTerms[0];
                for (int i = 1; i < constantTerms.Count; i++)
                {
                    constant = new BinaryExpression(constant, BinaryOperator.Add, constantTerms[i]);
                }
            }

            return constant.Simplify(modelManager);
        }

        private Expression? ParseTokenizedExpression(string expr, TokenManager tokenManager, out string error)
        {
            error = string.Empty;

            try
            {
                var parts = new List<(string op, string term)>();
                var currentTerm = new StringBuilder();
                string currentOp = "+";

                for (int i = 0; i < expr.Length; i++)
                {
                    char c = expr[i];

                    if ((c == '+' || c == '-') && currentTerm.Length > 0 && !currentTerm.ToString().EndsWith("E"))
                    {
                        parts.Add((currentOp, currentTerm.ToString().Trim()));
                        currentTerm.Clear();
                        currentOp = c.ToString();
                    }
                    else
                    {
                        currentTerm.Append(c);
                    }
                }

                if (currentTerm.Length > 0)
                    parts.Add((currentOp, currentTerm.ToString().Trim()));

                Expression? result = null;

                foreach (var (op, term) in parts)
                {
                    if (string.IsNullOrWhiteSpace(term))
                        continue;

                    Expression termExpr;

                    if (tokenManager.TryGetExpression(term, out var tokenExpr))
                    {
                        termExpr = tokenExpr!;
                    }
                    else if (double.TryParse(term, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    {
                        termExpr = new ConstantExpression(value);
                    }
                    else
                    {
                        error = $"Unrecognized term in expression: '{term}'";
                        return null;
                    }

                    if (result == null)
                    {
                        result = op == "-"
                            ? new UnaryExpression(UnaryOperator.Negate, termExpr)
                            : termExpr;
                    }
                    else
                    {
                        var binOp = op == "-" ? BinaryOperator.Subtract : BinaryOperator.Add;
                        result = new BinaryExpression(result, binOp, termExpr);
                    }
                }

                return result ?? new ConstantExpression(0);
            }
            catch (Exception ex)
            {
                error = $"Error parsing expression: {ex.Message}";
                return null;
            }
        }

        private void AddCoefficient(Dictionary<string, Expression> coefficients, string variable, Expression coeffExpr)
        {
            if (coefficients.ContainsKey(variable))
                coefficients[variable] = new BinaryExpression(coefficients[variable], BinaryOperator.Add, coeffExpr);
            else
                coefficients[variable] = coeffExpr;
        }

        private void MarkAsProcessed(Match match, HashSet<int> processedIndices)
        {
            for (int i = match.Index; i < match.Index + match.Length; i++)
            {
                processedIndices.Add(i);
            }
        }

        private string BuildRemainingExpression(string expression, HashSet<int> processedIndices)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < expression.Length; i++)
            {
                sb.Append(processedIndices.Contains(i) ? ' ' : expression[i]);
            }
            return sb.ToString();
        }
    }
}