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

        public ExpressionParser(ModelManager manager)
        {
            modelManager = manager;
            evaluator = new ExpressionEvaluator(manager);
            variableValidator = new VariableValidator(manager);
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
                var tokenCounter = 0;
                var tokenMap = new Dictionary<string, Expression>();

                // **STEP 0: Replace item() expressions**
                // Pattern: item(setName, <key1, key2, ...>).fieldName or just item(setName, <key1, key2>)
                string itemPattern = @"item\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*<([^>]+)>\s*\)(?:\.([a-zA-Z][a-zA-Z0-9_]*))?";

                expression = Regex.Replace(expression, itemPattern, m =>
                {
                    string setName = m.Groups[1].Value;
                    string keyValuesStr = m.Groups[2].Value;
                    string fieldName = m.Groups[3].Value; // May be empty

                    // Parse key values
                    var keyValues = ParseItemKeyValues(keyValuesStr, out string parseError);
                    if (keyValues == null)
                    {
                        throw new Exception($"Error parsing item() key values: {parseError}");
                    }

                    // Validate tuple set exists
                    if (!modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
                    {
                        throw new Exception($"Tuple set '{setName}' not found");
                    }

                    // Create item expression
                    var itemExpr = new ItemExpression(setName, keyValues);

                    Expression resultExpr;
                    if (!string.IsNullOrEmpty(fieldName))
                    {
                        // Field access: item(...).field
                        resultExpr = new ItemFieldAccessExpression(itemExpr, fieldName);
                    }
                    else
                    {
                        // Just item(...) without field access
                        resultExpr = itemExpr;
                    }

                    string token = $"__ITEM{tokenCounter++}__";
                    tokenMap[token] = resultExpr;
                    return token;
                });

                // **STEP 1: Replace tuple field access FIRST (before parameter substitution)**
                // Pattern: tupleSet[index].fieldName
                string tupleAccessPattern = @"([a-zA-Z][a-zA-Z0-9_]*)\[(\d+)\]\.([a-zA-Z][a-zA-Z0-9_]*)";
                
                expression = Regex.Replace(expression, tupleAccessPattern, m =>
                {
                    string setName = m.Groups[1].Value;
                    if (!int.TryParse(m.Groups[2].Value, out int index))
                    {
                        return m.Value; // Keep original if index isn't numeric
                    }
                    string fieldName = m.Groups[3].Value;

                    // Validate tuple set exists
                    if (modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
                    {
                        // Validate field exists in schema
                        if (modelManager.TupleSchemas.TryGetValue(tupleSet.Name, out var schema))
                        {
                            if (!schema.Fields.ContainsKey(fieldName))
                            {
                                throw new Exception($"Field '{fieldName}' not found in tuple schema '{schema.Name}'");
                            }
                        }

                        // Create a token for this tuple field access
                        var tupleExpr = new TupleFieldAccessExpression(setName, index, fieldName);
                        string token = $"__TUPLE{tokenCounter++}__";
                        tokenMap[token] = tupleExpr;
                        return token;
                    }

                    return m.Value; // Keep original if not a tuple set
                });

                // **STEP 2: Pattern for two-dimensional indexed variables/parameters**
                string patternTwoDim = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+),([a-zA-Z0-9]+)\]";
                
                expression = Regex.Replace(expression, patternTwoDim, m =>
                {
                    string name = m.Groups[1].Value;
                    string index1Str = m.Groups[2].Value;
                    string index2Str = m.Groups[3].Value;
                    
                    if (int.TryParse(index1Str, out int numericIndex1) && 
                        int.TryParse(index2Str, out int numericIndex2))
                    {
                        // Check if it's an indexed parameter
                        if (modelManager.Parameters.TryGetValue(name, out var param))
                        {
                            if (param.IsTwoDimensional)
                            {
                                var paramExpr = new IndexedParameterExpression(name, numericIndex1, numericIndex2);
                                string token = $"__PARAM{tokenCounter++}__";
                                tokenMap[token] = paramExpr;
                                return token;
                            }
                        }
                        
                        // It's an indexed variable
                        if (modelManager.IndexedVariables.ContainsKey(name))
                        {
                            var indexedVar = modelManager.IndexedVariables[name];
                            if (indexedVar.IsTwoDimensional)
                            {
                                var indexSet1 = modelManager.IndexSets[indexedVar.IndexSetName];
                                var indexSet2 = modelManager.IndexSets[indexedVar.SecondIndexSetName!];
                                
                                if (!indexSet1.Contains(numericIndex1))
                                {
                                    throw new Exception($"First index {numericIndex1} is out of range for variable {name}");
                                }
                                
                                if (!indexSet2.Contains(numericIndex2))
                                {
                                    throw new Exception($"Second index {numericIndex2} is out of range for variable {name}");
                                }
                            }
                            return $"{name}{numericIndex1}_{numericIndex2}";
                        }
                    }
                    else
                    {
                        return $"{name}_idx_{index1Str}_{index2Str}";
                    }
                    
                    return m.Value;
                });

                // **STEP 3: Pattern for single-dimensional indexed variables/parameters**
                string patternIndexed = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+)\]";
                
                expression = Regex.Replace(expression, patternIndexed, m =>
                {
                    string name = m.Groups[1].Value;
                    string indexStr = m.Groups[2].Value;
                    
                    if (int.TryParse(indexStr, out int numericIndex))
                    {
                        // Check if it's an indexed parameter
                        if (modelManager.Parameters.TryGetValue(name, out var param))
                        {
                            if (param.IsIndexed && !param.IsTwoDimensional)
                            {
                                var paramExpr = new IndexedParameterExpression(name, numericIndex);
                                string token = $"__PARAM{tokenCounter++}__";
                                tokenMap[token] = paramExpr;
                                return token;
                            }
                        }
                        
                        // It's an indexed variable
                        if (modelManager.IndexedVariables.ContainsKey(name))
                        {
                            var indexedVar = modelManager.IndexedVariables[name];
                            if (!indexedVar.IsScalar && !indexedVar.IsTwoDimensional)
                            {
                                var indexSet = modelManager.IndexSets[indexedVar.IndexSetName];
                                
                                if (!indexSet.Contains(numericIndex))
                                {
                                    throw new Exception($"Index {numericIndex} is out of range for variable {name}");
                                }
                            }
                            return $"{name}{numericIndex}";
                        }
                    }
                    else
                    {
                        return $"{name}_idx_{indexStr}";
                    }
                    
                    return m.Value;
                });

                // **STEP 4: Parse coefficients and variables**
                // Pattern: coefficient * variable or just variable
                //string patternWithMultiply = @"([+-]?[\d.]+|__PARAM\d+__|__TUPLE\d+__)\*([a-zA-Z][a-zA-Z0-9_]*)";
                string patternWithMultiply = @"(\([^()]+\)|[+-]?[\d.]+|__PARAM\d+__|__TUPLE\d+__)\*([a-zA-Z][a-zA-Z0-9_]*)";
                

                bool foundVariables = false;
                var processedIndices = new HashSet<int>();

                // Process explicit multiplication
                MatchCollection explicitMatches = Regex.Matches(expression, patternWithMultiply);
                foreach (Match match in explicitMatches)
                {
                    string coeffStr = match.Groups[1].Value;
                    string variable = match.Groups[2].Value;

                    if (string.IsNullOrEmpty(variable))
                        continue;

                    foundVariables = true;

                    Expression coeffExpr;
    
                    // **NEW: Handle parenthesized expressions**
                    if (coeffStr.StartsWith("(") && coeffStr.EndsWith(")"))
                    {
                        string parenContent = coeffStr.Substring(1, coeffStr.Length - 2);
                        
                        // Check if it contains tokens (parameters or tuples)
                        if (parenContent.Contains("__PARAM") || parenContent.Contains("__TUPLE"))
                        {
                            coeffExpr = ParseTokenizedExpression(parenContent, tokenMap, out error);
                            if (coeffExpr == null)
                            {
                                error = $"Could not parse coefficient expression '{coeffStr}' for variable '{variable}': {error}";
                                return false;
                            }
                        }
                        else
                        {
                            // Pure numeric expression - evaluate it
                            var evalResult = evaluator.EvaluateFloatExpression(parenContent);
                            if (!evalResult.IsSuccess)
                            {
                                error = $"Could not evaluate coefficient expression '{coeffStr}' for variable '{variable}': {evalResult.ErrorMessage}";
                                return false;
                            }
                            coeffExpr = new ConstantExpression(evalResult.Value);
                        }
                    }
                    else if (tokenMap.TryGetValue(coeffStr, out var tokenExpr))
                    {
                        coeffExpr = tokenExpr;
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

                    if (coefficients.ContainsKey(variable))
                    {
                        coefficients[variable] = new BinaryExpression(
                            coefficients[variable], 
                            BinaryOperator.Add, 
                            coeffExpr);
                    }
                    else
                    {
                        coefficients[variable] = coeffExpr;
                    }

                    for (int i = match.Index; i < match.Index + match.Length; i++)
                    {
                        processedIndices.Add(i);
                    }
                }

                // Build remaining expression
                var remainingChars = new System.Text.StringBuilder();
                for (int i = 0; i < expression.Length; i++)
                {
                    if (!processedIndices.Contains(i))
                    {
                        remainingChars.Append(expression[i]);
                    }
                    else
                    {
                        remainingChars.Append(' '); // Use space instead of | for easier parsing
                    }
                }
                string remainingExpression = remainingChars.ToString();

                // Process implicit multiplication
                string patternImplicit = @"([+-]?[\d.]*|__PARAM\d+__|__TUPLE\d+__)([a-zA-Z][a-zA-Z0-9_]*)";
                MatchCollection implicitMatches = Regex.Matches(remainingExpression, patternImplicit);
                foreach (Match match in implicitMatches)
                {
                    string coeffStr = match.Groups[1].Value;
                    string variable = match.Groups[2].Value;

                    if (string.IsNullOrEmpty(variable) || variable.StartsWith("PARAM") || variable.StartsWith("TUPLE"))
                        continue;

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
                    else if (tokenMap.TryGetValue(coeffStr, out var tokenExpr))
                    {
                        coeffExpr = tokenExpr;
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

                    if (coefficients.ContainsKey(variable))
                    {
                        coefficients[variable] = new BinaryExpression(
                            coefficients[variable], 
                            BinaryOperator.Add, 
                            coeffExpr);
                    }
                    else
                    {
                        coefficients[variable] = coeffExpr;
                    }
                }

                // **NEW: Extract constant terms**
                // Pattern to find standalone numbers (not followed by variables)
                // This captures things like: +50, -30, 100
                //string constantPattern = @"(?:^|(?<=[+\-]))([+-]?\d+\.?\d*)(?![a-zA-Z_*])";
                //string constantPattern = @"(?:^|(?<=[+\-]))([+-]?(?:\d+\.\d+|\d+))(?![a-zA-Z_*])";
                string constantPattern = @"(?:^|(?<=[+\-]))(\d+\.\d+|\d+(?!\.\d))(?![a-zA-Z_*])";
                var constantMatches = Regex.Matches(expression, constantPattern);
                
                var constantTerms = new List<Expression>();
                foreach (Match match in constantMatches)
                {
                    // Skip if this number is part of a token (parameter or tuple)
                    if (match.Index > 0 && match.Index < expression.Length - 1)
                    {
                        string checkToken = expression.Substring(Math.Max(0, match.Index - 10), 
                            Math.Min(20, expression.Length - Math.Max(0, match.Index - 10)));
                        if (checkToken.Contains("__PARAM") || checkToken.Contains("__TUPLE"))
                            continue;
                    }

                    // Skip if this is part of a coefficient for a variable
                    // Check if there's a variable or * immediately after
                    int endPos = match.Index + match.Length;
                    if (endPos < expression.Length)
                    {
                        string after = expression.Substring(endPos, Math.Min(2, expression.Length - endPos));
                        if (after.StartsWith("*") || char.IsLetter(after[0]))
                            continue; // This is a coefficient, not a constant
                    }

                    string numStr = match.Groups[1].Value;
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double constValue))
                    {
                        constantTerms.Add(new ConstantExpression(constValue));
                    }
                }

                // Combine all constant terms
                if (constantTerms.Count > 0)
                {
                    constant = constantTerms[0];
                    for (int i = 1; i < constantTerms.Count; i++)
                    {
                        constant = new BinaryExpression(constant, BinaryOperator.Add, constantTerms[i]);
                    }
                }

                // If no variables found and no constants extracted, try parsing entire expression as constant
                if (!foundVariables && constantTerms.Count == 0)
                {
                    if (tokenMap.TryGetValue(expression.Trim(), out var constTokenExpr))
                    {
                        constant = constTokenExpr;
                    }
                    else if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out double constValue))
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

        private (string expression, Dictionary<string, Expression> tokenMap) TokenizeIndexedReferences(string expression)
        {
            var tokenMap = new Dictionary<string, Expression>();
            int tokenCounter = 0;

            // Replace 2D indexed parameters: x[1,2]
            expression = Regex.Replace(expression, @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+),([a-zA-Z0-9]+)\]", m =>
            {
                string name = m.Groups[1].Value;
                string index1Str = m.Groups[2].Value;
                string index2Str = m.Groups[3].Value;

                if (int.TryParse(index1Str, out int idx1) && int.TryParse(index2Str, out int idx2))
                {
                    if (modelManager.Parameters.TryGetValue(name, out var param) && param.IsTwoDimensional)
                    {
                        var paramExpr = new IndexedParameterExpression(name, idx1, idx2);
                        string token = $"__PARAM{tokenCounter++}__";
                        tokenMap[token] = paramExpr;
                        return token;
                    }

                    if (modelManager.IndexedVariables.TryGetValue(name, out var indexedVar) && indexedVar.IsTwoDimensional)
                    {
                        ValidateIndices(name, indexedVar, idx1, idx2);
                        return $"{name}{idx1}_{idx2}";
                    }
                }
                else
                {
                    return $"{name}_idx_{index1Str}_{index2Str}";
                }

                return m.Value;
            });

            // Replace 1D indexed parameters: x[1]
            expression = Regex.Replace(expression, @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+)\]", m =>
            {
                string name = m.Groups[1].Value;
                string indexStr = m.Groups[2].Value;

                if (int.TryParse(indexStr, out int idx))
                {
                    if (modelManager.Parameters.TryGetValue(name, out var param) && param.IsIndexed && !param.IsTwoDimensional)
                    {
                        ValidateParameterIndex(name, param, idx);
                        var paramExpr = new IndexedParameterExpression(name, idx);
                        string token = $"__PARAM{tokenCounter++}__";
                        tokenMap[token] = paramExpr;
                        return token;
                    }

                    if (modelManager.IndexedVariables.TryGetValue(name, out var indexedVar) && !indexedVar.IsScalar && !indexedVar.IsTwoDimensional)
                    {
                        ValidateVariableIndex(name, indexedVar, idx);
                        return $"{name}{idx}";
                    }
                }
                else
                {
                    return $"{name}_idx_{indexStr}";
                }

                return m.Value;
            });

            return (expression, tokenMap);
        }

        private Dictionary<string, Expression> ExtractCoefficients(
            string expression,
            Dictionary<string, Expression> tokenMap,
            out HashSet<int> processedIndices,
            out string error)
        {
            error = string.Empty;
            var coefficients = new Dictionary<string, Expression>();
            processedIndices = new HashSet<int>();

            // Pattern 1: Parenthesized coefficients like (1+4)*x or (__PARAM0__+__PARAM1__)*y
            string patternParenCoeff = @"\(([^()]+(?:\([^()]*\)[^()]*)*)\)\*([a-zA-Z][a-zA-Z0-9_]*)";
            foreach (Match match in Regex.Matches(expression, patternParenCoeff))
            {
                if (!ProcessParenthesizedCoefficient(match, tokenMap, coefficients, processedIndices, out error))
                    return coefficients;
            }

            // Pattern 2: Explicit multiplication like 2.5*x or __PARAM0__*x
            string patternWithMultiply = @"([+-]?[\d.]+|__PARAM\d+__)\*([a-zA-Z][a-zA-Z0-9_]*)";
            foreach (Match match in Regex.Matches(expression, patternWithMultiply))
            {
                if (!IsAlreadyProcessed(match, processedIndices))
                {
                    if (!ProcessExplicitCoefficient(match, tokenMap, coefficients, processedIndices, out error))
                        return coefficients;
                }
            }

            // Pattern 3: Implicit multiplication like x, +x, -x
            var remainingExpr = BuildRemainingExpression(expression, processedIndices);
            string patternImplicit = @"([+-]?[\d.]*|__PARAM\d+__)([a-zA-Z][a-zA-Z0-9_]*)";
            foreach (Match match in Regex.Matches(remainingExpr, patternImplicit))
            {
                if (!ProcessImplicitCoefficient(match, tokenMap, coefficients, out error))
                    return coefficients;
            }

            return coefficients;
        }

        private bool ProcessParenthesizedCoefficient(
            Match match,
            Dictionary<string, Expression> tokenMap,
            Dictionary<string, Expression> coefficients,
            HashSet<int> processedIndices,
            out string error)
        {
            error = string.Empty;
            string parenExpr = match.Groups[1].Value;
            string variable = match.Groups[2].Value;

            if (string.IsNullOrEmpty(variable))
                return true;

            Expression coeffExpr;

            if (parenExpr.Contains("__PARAM"))
            {
                coeffExpr = ParseTokenizedExpression(parenExpr, tokenMap, out error);
                if (coeffExpr == null)
                {
                    error = $"Could not parse coefficient expression '({parenExpr})' for variable '{variable}': {error}";
                    return false;
                }
            }
            else
            {
                var evalResult = evaluator.EvaluateFloatExpression(parenExpr);
                if (!evalResult.IsSuccess)
                {
                    error = $"Could not evaluate coefficient expression '({parenExpr})' for variable '{variable}': {evalResult.ErrorMessage}";
                    return false;
                }
                coeffExpr = new ConstantExpression(evalResult.Value);
            }

            AddCoefficient(coefficients, variable, coeffExpr);
            MarkAsProcessed(match, processedIndices);
            return true;
        }

        private bool ProcessExplicitCoefficient(
            Match match,
            Dictionary<string, Expression> tokenMap,
            Dictionary<string, Expression> coefficients,
            HashSet<int> processedIndices,
            out string error)
        {
            error = string.Empty;
            string coeffStr = match.Groups[1].Value;
            string variable = match.Groups[2].Value;

            if (string.IsNullOrEmpty(variable))
                return true;

            Expression coeffExpr;
            if (tokenMap.TryGetValue(coeffStr, out var paramExpr))
            {
                coeffExpr = paramExpr;
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

        private bool ProcessImplicitCoefficient(
            Match match,
            Dictionary<string, Expression> tokenMap,
            Dictionary<string, Expression> coefficients,
            out string error)
        {
            error = string.Empty;
            string coeffStr = match.Groups[1].Value;
            string variable = match.Groups[2].Value;

            if (string.IsNullOrEmpty(variable) || variable.StartsWith("PARAM"))
                return true;

            Expression coeffExpr;
            if (string.IsNullOrEmpty(coeffStr) || coeffStr == "+")
                coeffExpr = new ConstantExpression(1);
            else if (coeffStr == "-")
                coeffExpr = new ConstantExpression(-1);
            else if (tokenMap.TryGetValue(coeffStr, out var paramExpr))
                coeffExpr = paramExpr;
            else if (double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
                coeffExpr = new ConstantExpression(coeff);
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
            Dictionary<string, Expression> tokenMap,
            HashSet<int> processedIndices)
        {
            Expression constant = new ConstantExpression(0);

            // Remove variable terms
            string constantExpr = expression;
            foreach (var variable in coefficients.Keys)
            {
                constantExpr = Regex.Replace(constantExpr,
                    @"[+-]?(?:[\d.]+|__PARAM\d+__|\([^)]+\))*\*?" + Regex.Escape(variable) + @"(?![a-zA-Z0-9_])",
                    "");
            }

            // Clean up
            constantExpr = CleanupConstantExpression(constantExpr);

            // Add standalone parameter tokens
            foreach (var kvp in tokenMap)
            {
                if (constantExpr.Contains(kvp.Key))
                {
                    constantExpr = constantExpr.Replace(kvp.Key, "");
                    constant = AddToConstant(constant, kvp.Value);
                }
            }

            // Parse remaining numeric constant
            constantExpr = NormalizeOperators(constantExpr.Trim());

            if (!string.IsNullOrEmpty(constantExpr) && constantExpr != "+" && constantExpr != "-")
            {
                if (double.TryParse(constantExpr, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    if (Math.Abs(value) > 1e-10)
                        constant = AddToConstant(constant, new ConstantExpression(value));
                }
                else
                {
                    var evalResult = evaluator.EvaluateFloatExpression(constantExpr);
                    if (evalResult.IsSuccess && Math.Abs(evalResult.Value) > 1e-10)
                        constant = AddToConstant(constant, new ConstantExpression(evalResult.Value));
                }
            }

            return constant.Simplify(modelManager);
        }

        private string CleanupConstantExpression(string expr)
        {
            expr = expr.Replace("|", "").Trim();

            // Remove empty/malformed parentheses
            while (true)
            {
                string before = expr;
                expr = expr.Replace("()", "");
                expr = Regex.Replace(expr, @"\([+\-\s]*\)", "");

                if (expr.StartsWith("(") && !expr.Contains(")"))
                    expr = expr.Substring(1);

                if (expr == before)
                    break;
            }

            return expr;
        }

        private string NormalizeOperators(string expr)
        {
            while (expr.Contains("++") || expr.Contains("--") || expr.Contains("+-") || expr.Contains("-+"))
            {
                expr = expr.Replace("++", "+");
                expr = expr.Replace("--", "+");
                expr = expr.Replace("+-", "-");
                expr = expr.Replace("-+", "-");
            }
            return expr.TrimStart('+').Trim();
        }

        private void SimplifyCoefficients(Dictionary<string, Expression> coefficients)
        {
            foreach (var key in coefficients.Keys.ToList())
            {
                coefficients[key] = coefficients[key].Simplify(modelManager);

                if (coefficients[key] is ConstantExpression constCoeff &&
                    Math.Abs(constCoeff.Value) < 1e-10)
                {
                    coefficients.Remove(key);
                }
            }
        }

        private Expression? ParseTokenizedExpression(string expr, Dictionary<string, Expression> tokenMap, out string error)
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

                    if (tokenMap.TryGetValue(term, out var paramExpr))
                        termExpr = paramExpr;
                    else if (double.TryParse(term, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                        termExpr = new ConstantExpression(value);
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

        // Helper methods
        private void ValidateIndices(string name, IndexedVariable var, int idx1, int idx2)
        {
            var indexSet1 = modelManager.IndexSets[var.IndexSetName];
            var indexSet2 = modelManager.IndexSets[var.SecondIndexSetName!];

            if (!indexSet1.Contains(idx1))
                throw new Exception($"First index {idx1} is out of range for variable {name}");

            if (!indexSet2.Contains(idx2))
                throw new Exception($"Second index {idx2} is out of range for variable {name}");
        }

        private void ValidateParameterIndex(string name, Parameter param, int idx)
        {
            if (modelManager.IndexSets.TryGetValue(param.IndexSetName, out var indexSet))
            {
                if (!indexSet.Contains(idx))
                    throw new Exception($"Index {idx} is out of range for parameter {name}");
            }
        }

        private void ValidateVariableIndex(string name, IndexedVariable var, int idx)
        {
            var indexSet = modelManager.IndexSets[var.IndexSetName];
            if (!indexSet.Contains(idx))
                throw new Exception($"Index {idx} is out of range for variable {name}");
        }

        private void AddCoefficient(Dictionary<string, Expression> coefficients, string variable, Expression coeffExpr)
        {
            if (coefficients.ContainsKey(variable))
                coefficients[variable] = new BinaryExpression(coefficients[variable], BinaryOperator.Add, coeffExpr);
            else
                coefficients[variable] = coeffExpr;
        }

        private Expression AddToConstant(Expression constant, Expression addition)
        {
            if (constant is ConstantExpression ce && Math.Abs(ce.Value) < 1e-10)
                return addition;
            else
                return new BinaryExpression(constant, BinaryOperator.Add, addition);
        }

        private bool IsAlreadyProcessed(Match match, HashSet<int> processedIndices)
        {
            for (int i = match.Index; i < match.Index + match.Length; i++)
            {
                if (processedIndices.Contains(i))
                    return true;
            }
            return false;
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

        private List<object>? ParseItemKeyValues(string keyValuesStr, out string error)
        {
            error = string.Empty;
            var keyValues = new List<object>();
    
            // Split by commas, respecting quotes
            var values = SplitByCommaRespectingQuotes(keyValuesStr);
    
            foreach (var valueStr in values)
            {
       