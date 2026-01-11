using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core
{
    /// <summary>
    /// Parses equation text and populates the model
    /// </summary>
    public class EquationParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public EquationParser(ModelManager manager)
        {
            modelManager = manager;
            evaluator = new ExpressionEvaluator(manager);
        }

        public ParseSessionResult Parse(string text)
        {
            var result = new ParseSessionResult();

            if (string.IsNullOrWhiteSpace(text))
            {
                result.AddError("No text to parse");
                return result;
            }

            // Split by lines first to handle comments properly
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var processedLines = new List<string>();

            foreach (string line in lines)
            {
                string lineWithoutComment = line.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();
                if (!string.IsNullOrWhiteSpace(lineWithoutComment))
                {
                    processedLines.Add(lineWithoutComment);
                }
            }

            string processedText = string.Join(" ", processedLines);
            string[] statements = processedText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int i = 0; i < statements.Length; i++)
            {
                string stmt = statements[i].Trim();
                
                if (string.IsNullOrWhiteSpace(stmt))
                    continue;

                ProcessStatement(stmt, i + 1, result);
            }

            return result;
        }

        private void ProcessStatement(string statement, int statementNumber, ParseSessionResult result)
        {
            // Try each parser in order
            if (TryParseParameter(statement, out var param))
            {
                modelManager.AddParameter(param);
                result.IncrementSuccess();
                return;
            }

            if (TryParseIndexSet(statement, out var indexSet, out var error))
            {
                if (indexSet != null)
                {
                    modelManager.AddIndexSet(indexSet);
                    result.IncrementSuccess();
                    return;
                }
            }

            if (TryParseVariableDeclaration(statement, out var variable, out error))
            {
                if (variable != null)
                {
                    modelManager.AddIndexedVariable(variable);
                    result.IncrementSuccess();
                    return;
                }
            }

            if (TryParseIndexedEquation(statement, statementNumber, out error, result))
            {
                result.IncrementSuccess();
                return;
            }

            if (TryParseEquation(statement, out var equation, out error))
            {
                if (equation != null)
                {
                    try
                    {
                        modelManager.AddEquation(equation);
                        result.IncrementSuccess();
                        return;
                    }
                    catch (Exception ex)
                    {
                        result.AddError($"Statement {statementNumber}: Error adding equation - {ex.Message}");
                        return;
                    }
                }
            }

            result.AddError($"Statement {statementNumber}: \"{statement}\"\n  Error: {error}");
        }

        private bool TryParseParameter(string statement, out Parameter? parameter)
        {
            parameter = null;
            
            string pattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
                return false;

            string typeStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string valueStr = match.Groups[3].Value.Trim();

            ParameterType paramType = typeStr switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                _ => ParameterType.Float
            };

            try
            {
                object value;
                switch (paramType)
                {
                    case ParameterType.Integer:
                        var intResult = evaluator.EvaluateIntExpression(valueStr);
                        if (!intResult.IsSuccess)
                            return false;
                        value = intResult.Value;
                        break;

                    case ParameterType.Float:
                        var floatResult = evaluator.EvaluateFloatExpression(valueStr);
                        if (!floatResult.IsSuccess)
                            return false;
                        value = floatResult.Value;
                        break;

                    case ParameterType.String:
                        if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                        {
                            value = valueStr.Substring(1, valueStr.Length - 2);
                        }
                        else
                        {
                            return false;
                        }
                        break;

                    default:
                        return false;
                }

                parameter = new Parameter(name, paramType, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseIndexSet(string statement, out IndexSet? indexSet, out string error)
        {
            indexSet = null;
            error = string.Empty;

            string pattern = @"^\s*range\s+([a-zA-Z][a-zA-Z0-9]*)\s*=\s*([a-zA-Z0-9]+)\.\.([a-zA-Z0-9]+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an index set declaration";
                return false;
            }

            string name = match.Groups[1].Value;
            string startStr = match.Groups[2].Value;
            string endStr = match.Groups[3].Value;

            var startResult = evaluator.EvaluateIntExpression(startStr);
            if (!startResult.IsSuccess)
            {
                error = $"Invalid start index '{startStr}': {startResult.ErrorMessage}";
                return false;
            }

            var endResult = evaluator.EvaluateIntExpression(endStr);
            if (!endResult.IsSuccess)
            {
                error = $"Invalid end index '{endStr}': {endResult.ErrorMessage}";
                return false;
            }

            int start = startResult.Value;
            int end = endResult.Value;

            if (start > end)
            {
                error = $"Invalid range: start index {start} is greater than end index {end}";
                return false;
            }

            indexSet = new IndexSet(name, start, end);
            return true;
        }

        private bool TryParseVariableDeclaration(string statement, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            // Try indexed variable first: var [type] name[IndexSet]
            string indexedPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9]*)\s*\]$";
            var indexedMatch = Regex.Match(statement.Trim(), indexedPattern);

            if (indexedMatch.Success)
            {
                string typeStr = indexedMatch.Groups[1].Value;
                string varName = indexedMatch.Groups[2].Value;
                string indexSetName = indexedMatch.Groups[3].Value;

                VariableType varType = VariableType.Float;
                if (!string.IsNullOrEmpty(typeStr))
                {
                    varType = typeStr.ToLower() switch
                    {
                        "float" => VariableType.Float,
                        "int" => VariableType.Integer,
                        "bool" => VariableType.Boolean,
                        _ => VariableType.Float
                    };
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName))
                {
                    error = $"Index set '{indexSetName}' is not declared";
                    return false;
                }

                variable = new IndexedVariable(varName, indexSetName, varType);
                return true;
            }

            // Try scalar variable: var [type] name;
            string scalarPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9]*)$";
            var scalarMatch = Regex.Match(statement.Trim(), scalarPattern);

            if (scalarMatch.Success)
            {
                string typeStr = scalarMatch.Groups[1].Value;
                string varName = scalarMatch.Groups[2].Value;

                VariableType varType = VariableType.Float;
                if (!string.IsNullOrEmpty(typeStr))
                {
                    varType = typeStr.ToLower() switch
                    {
                        "float" => VariableType.Float,
                        "int" => VariableType.Integer,
                        "bool" => VariableType.Boolean,
                        _ => VariableType.Float
                    };
                }

                // Scalar variables use empty string for IndexSetName to distinguish from indexed
                variable = new IndexedVariable(varName, string.Empty, varType);
                return true;
            }

            error = "Not a variable declaration";
            return false;
        }

        private bool TryParseIndexedEquation(string statement, int statementNumber, out string error, ParseSessionResult result)
        {
            error = string.Empty;

            string pattern = @"^\s*equation\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9]*)\s*\]\s*:\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an indexed equation declaration";
                return false;
            }

            string baseName = match.Groups[1].Value;
            string indexSetName = match.Groups[2].Value;
            string template = match.Groups[3].Value;

            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not declared";
                return false;
            }

            var indexedEquation = new IndexedEquation(baseName, indexSetName, template);
            modelManager.AddIndexedEquationTemplate(indexedEquation);

            // Expand equations
            var indexSet = modelManager.IndexSets[indexSetName];
            foreach (int index in indexSet.GetIndices())
            {
                string expandedEquation = ExpandEquationTemplate(template, indexSetName.ToLower(), index);
                
                if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                {
                    if (eq != null)
                    {
                        eq.BaseName = baseName;
                        eq.Index = index;
                        modelManager.AddEquation(eq);
                    }
                }
                else
                {
                    result.AddError($"Statement {statementNumber} (expanded index {index}): {eqError}");
                }
            }

            return true;
        }

        private bool TryParseEquation(string equation, out LinearEquation? result, out string error)
        {
            result = null;
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(equation))
                {
                    error = "Equation is empty or contains only whitespace";
                    return false;
                }

                string? label = null;
                string equationText = equation.Trim();

                // Check for label (format: label: equation)
                string labelPattern = @"^([a-zA-Z][a-zA-Z0-9_]*)\s*:\s*(.+)$";
                var labelMatch = Regex.Match(equationText, labelPattern);
                
                if (labelMatch.Success)
                {
                    label = labelMatch.Groups[1].Value;
                    equationText = labelMatch.Groups[2].Value;
                }

                // Remove all whitespace for easier parsing
                string cleaned = Regex.Replace(equationText, @"\s+", "");

                // Detect the operator and split the equation
                RelationalOperator op;
                string[] parts;
                
                // Check for operators in order of longest to shortest to avoid conflicts
                if (cleaned.Contains("<="))
                {
                    op = RelationalOperator.LessThanOrEqual;
                    parts = cleaned.Split(new[] { "<=" }, StringSplitOptions.None);
                }
                else if (cleaned.Contains(">="))
                {
                    op = RelationalOperator.GreaterThanOrEqual;
                    parts = cleaned.Split(new[] { ">=" }, StringSplitOptions.None);
                }
                else if (cleaned.Contains("≤"))
                {
                    op = RelationalOperator.LessThanOrEqual;
                    parts = cleaned.Split('≤');
                }
                else if (cleaned.Contains("≥"))
                {
                    op = RelationalOperator.GreaterThanOrEqual;
                    parts = cleaned.Split('≥');
                }
                else if (cleaned.Contains('<'))
                {
                    op = RelationalOperator.LessThan;
                    parts = cleaned.Split('<');
                }
                else if (cleaned.Contains('>'))
                {
                    op = RelationalOperator.GreaterThan;
                    parts = cleaned.Split('>');
                }
                else if (cleaned.Contains('='))
                {
                    op = RelationalOperator.Equal;
                    parts = cleaned.Split('=');
                }
                else
                {
                    error = "Missing relational operator. Must contain =, <, >, <=, or >=";
                    return false;
                }

                // Check for multiple operators
                if (parts.Length > 2)
                {
                    error = "Multiple relational operators found. Only one operator is allowed per equation";
                    return false;
                }

                if (parts.Length != 2)
                {
                    error = "Invalid equation structure";
                    return false;
                }

                string leftSide = parts[0];
                string rightSide = parts[1];

                // Validate both sides are not empty
                if (string.IsNullOrEmpty(leftSide))
                {
                    error = "Left side of equation is empty";
                    return false;
                }

                if (string.IsNullOrEmpty(rightSide))
                {
                    error = "Right side of equation is empty";
                    return false;
                }

                // Parse both sides for coefficients and constants
                if (!TryParseExpression(leftSide, out var leftCoefficients, out var leftConstant, out error))
                {
                    error = $"Error parsing left side: {error}";
                    return false;
                }

                if (!TryParseExpression(rightSide, out var rightCoefficients, out var rightConstant, out error))
                {
                    error = $"Error parsing right side: {error}";
                    return false;
                }

                // Combine: move all terms to the left side
                var finalCoefficients = new Dictionary<string, double>();
                
                // Add left side coefficients
                foreach (var kvp in leftCoefficients)
                {
                    finalCoefficients[kvp.Key] = kvp.Value;
                }

                // Subtract right side coefficients (move to left)
                foreach (var kvp in rightCoefficients)
                {
                    if (finalCoefficients.ContainsKey(kvp.Key))
                        finalCoefficients[kvp.Key] -= kvp.Value;
                    else
                        finalCoefficients[kvp.Key] = -kvp.Value;
                }

                // Calculate final constant: right constant - left constant
                double finalConstant = rightConstant - leftConstant;

                // Remove variables with zero coefficients
                var nonZeroCoefficients = finalCoefficients.Where(kvp => Math.Abs(kvp.Value) > 1e-10)
                                                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                // Check if we have any variables left
                if (nonZeroCoefficients.Count == 0)
                {
                    if (Math.Abs(finalConstant) < 1e-10)
                    {
                        error = "Equation reduces to 0 = 0 (tautology - always true)";
                        return false;
                    }
                    else
                    {
                        error = $"Equation reduces to 0 = {finalConstant} (contradiction - always false)";
                        return false;
                    }
                }

                result = new LinearEquation(nonZeroCoefficients, finalConstant, op, label);
                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                error = "Parsing timeout. Equation might be too complex or contain problematic patterns";
                return false;
            }
            catch (ArgumentException ex)
            {
                error = $"Invalid regex pattern: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Unexpected error: {ex.Message}";
                return false;
            }
        }

        private bool TryParseExpression(string expression, out Dictionary<string, double> coefficients, out double constant, out string error)
        {
            coefficients = new Dictionary<string, double>();
            constant = 0;
            error = string.Empty;

            try
            {
                // Pattern for indexed variables: x[1], var2[5], x[i] (with index variable)
                string patternIndexed = @"([a-zA-Z][a-zA-Z0-9]*)\[([a-zA-Z0-9]+)\]";
                
                // Expand indexed variables first
                expression = Regex.Replace(expression, patternIndexed, m =>
                {
                    string varName = m.Groups[1].Value;
                    string indexStr = m.Groups[2].Value;
                    
                    if (int.TryParse(indexStr, out int numericIndex))
                    {
                        // Numeric index: x[1] -> x1
                        // Validate if variable was declared
                        if (modelManager.IndexedVariables.ContainsKey(varName))
                        {
                            var indexedVar = modelManager.IndexedVariables[varName];
                            var indexSet = modelManager.IndexSets[indexedVar.IndexSetName];
                            
                            if (!indexSet.Contains(numericIndex))
                            {
                                throw new Exception($"Index {numericIndex} is out of range for variable {varName}[{indexedVar.IndexSetName}]. Valid range: {indexSet.StartIndex}..{indexSet.EndIndex}");
                            }
                        }
                        return $"{varName}{numericIndex}";
                    }
                    else
                    {
                        // Variable index: x[i] - keep as is for symbolic processing
                        return $"{varName}_idx_{indexStr}";
                    }
                });

                // Updated patterns to properly match decimal numbers
                // Pattern to match terms with explicit multiplication: 2*x, -3.5*var1, 100.25*x, etc.
                string patternWithMultiply = @"([+-]?\d+(?:\.\d+)?)\*([a-zA-Z][a-zA-Z0-9_]*)";
                
                // Pattern to match terms with implicit multiplication: 2x, -3.5y1, x, etc.
                string patternImplicit = @"([+-]?\d*(?:\.\d+)?)([a-zA-Z][a-zA-Z0-9_]*)";

                bool foundVariables = false;
                var processedIndices = new HashSet<int>();

                // First, process explicit multiplication (2*x)
                MatchCollection explicitMatches = Regex.Matches(expression, patternWithMultiply);
                foreach (Match match in explicitMatches)
                {
                    string coeffStr = match.Groups[1].Value;
                    string variable = match.Groups[2].Value;

                    if (string.IsNullOrEmpty(variable))
                        continue;

                    foundVariables = true;

                    // Use invariant culture for parsing
                    if (!double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double coeff))
                    {
                        error = $"Invalid coefficient '{coeffStr}' for variable '{variable}'. Expected a numeric value";
                        return false;
                    }

                    if (coefficients.ContainsKey(variable))
                        coefficients[variable] += coeff;
                    else
                        coefficients[variable] = coeff;

                    for (int i = match.Index; i < match.Index + match.Length; i++)
                    {
                        processedIndices.Add(i);
                    }
                }

                // Build a modified expression without the explicit multiplication terms
                var remainingChars = new System.Text.StringBuilder();
                for (int i = 0; i < expression.Length; i++)
                {
                    if (!processedIndices.Contains(i))
                    {
                        remainingChars.Append(expression[i]);
                    }
                    else
                    {
                        remainingChars.Append('|');
                    }
                }
                string remainingExpression = remainingChars.ToString();

                // Now process implicit multiplication (2x or x) from remaining expression
                MatchCollection implicitMatches = Regex.Matches(remainingExpression, patternImplicit);
                foreach (Match match in implicitMatches)
                {
                    string coeffStr = match.Groups[1].Value;
                    string variable = match.Groups[2].Value;

                    if (string.IsNullOrEmpty(variable))
                        continue;

                    foundVariables = true;

                    double coeff;
                    if (string.IsNullOrEmpty(coeffStr) || coeffStr == "+")
                    {
                        coeff = 1;
                    }
                    else if (coeffStr == "-")
                    {
                        coeff = -1;
                    }
                    else if (!double.TryParse(coeffStr, NumberStyles.Float, CultureInfo.InvariantCulture, out coeff))
                    {
                        error = $"Invalid coefficient '{coeffStr}' for variable '{variable}'. Expected a numeric value";
                        return false;
                    }

                    if (coefficients.ContainsKey(variable))
                        coefficients[variable] += coeff;
                    else
                        coefficients[variable] = coeff;

                    for (int i = match.Index; i < match.Index + match.Length; i++)
                    {
                        processedIndices.Add(i);
                    }
                }

                // If no variables found, the entire expression should be a constant
                if (!foundVariables)
                {
                    // Use invariant culture for parsing
                    if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out double constValue))
                    {
                        constant = constValue;
                    }
                    else
                    {
                        error = $"Invalid expression: '{expression}'. Expected variables or a numeric constant";
                        return false;
                    }
                }
                else
                {
                    // Extract remaining numeric constants
                    string constantExpression = expression;
                    
                    constantExpression = Regex.Replace(constantExpression, patternWithMultiply, "|");
                    constantExpression = Regex.Replace(constantExpression, patternImplicit, "|");

                    string[] parts = constantExpression.Split(new[] { '|', '+', '-', '*' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts)
                    {
                        string trimmedPart = part.Trim();
                        // Use invariant culture for parsing
                        if (double.TryParse(trimmedPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double constValue))
                        {
                            constant += constValue;
                        }
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

        private string ExpandEquationTemplate(string template, string indexVariable, int indexValue)
        {
            string pattern = $@"\[{indexVariable}\]";
            return Regex.Replace(template, pattern, $"[{indexValue}]", RegexOptions.IgnoreCase);
        }
    }

    public class ParseSessionResult
    {
        public List<string> Errors { get; private set; } = new List<string>();
        public int SuccessCount { get; private set; } = 0;

        public void AddError(string error)
        {
            Errors.Add(error);
        }

        public void IncrementSuccess()
        {
            SuccessCount++;
        }

        public bool HasErrors => Errors.Count > 0;
        public bool HasSuccess => SuccessCount > 0;
    }
}