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
        private readonly JavaScriptEvaluator jsEvaluator;

        public EquationParser(ModelManager manager)
        {
            modelManager = manager;
            evaluator = new ExpressionEvaluator(manager);
            jsEvaluator = new JavaScriptEvaluator(manager);
        }

        public ParseSessionResult Parse(string text)
        {
            var result = new ParseSessionResult();

            if (string.IsNullOrWhiteSpace(text))
            {
                result.AddError("No text to parse", 0);
                return result;
            }

            // Extract and process JavaScript execute blocks FIRST, before any other processing
            var (processedText, lineMapping) = ExtractAndProcessExecuteBlocks(text, result);

            // Split by lines first to handle comments properly
            string[] lines = processedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var processedLines = new List<(string content, int lineNumber)>();
            
            int currentLineNumber = 1;
            foreach (string line in lines)
            {
                string lineWithoutComment = line.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();
                if (!string.IsNullOrWhiteSpace(lineWithoutComment))
                {
                    // Map to original line number
                    int originalLineNumber = lineMapping.ContainsKey(currentLineNumber) 
                        ? lineMapping[currentLineNumber] 
                        : currentLineNumber;
                    processedLines.Add((lineWithoutComment, originalLineNumber));
                }
                currentLineNumber++;
            }

            // Group lines by statements (split by semicolons)
            var statements = new List<(string content, int lineNumber)>();
            string currentStatement = "";
            int statementStartLine = 0;

            foreach (var (content, lineNumber) in processedLines)
            {
                if (string.IsNullOrEmpty(currentStatement))
                {
                    statementStartLine = lineNumber;
                }
                
                currentStatement += " " + content;
                
                // Check if this line contains a semicolon
                if (content.Contains(';'))
                {
                    // Split by semicolons
                    var parts = currentStatement.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var part in parts)
                    {
                        if (!string.IsNullOrWhiteSpace(part))
                        {
                            statements.Add((part.Trim(), statementStartLine));
                        }
                    }
                    currentStatement = "";
                }
            }

            // Process any remaining statement without semicolon
            if (!string.IsNullOrWhiteSpace(currentStatement))
            {
                statements.Add((currentStatement.Trim(), statementStartLine));
            }

            // Process each statement
            foreach (var (statement, lineNumber) in statements)
            {
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    ProcessStatement(statement, lineNumber, result);
                }
            }

            return result;
        }

        private (string processedText, Dictionary<int, int> lineMapping) ExtractAndProcessExecuteBlocks(string text, ParseSessionResult result)
        {
            var lineMapping = new Dictionary<int, int>();
            
            // Pattern to match execute{...} blocks, including nested braces
            string pattern = @"execute\s*\{";
            var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                // No execute blocks, return original text with identity mapping
                var lines = text.Split(new[] { '\r', '\n' });
                for (int i = 0; i < lines.Length; i++)
                {
                    lineMapping[i + 1] = i + 1;
                }
                return (text, lineMapping);
            }

            var resultText = new System.Text.StringBuilder();
            int lastIndex = 0;
            int blockNumber = 0;
            int currentOutputLine = 1;
            int currentInputLine = 1;

            foreach (Match match in matches)
            {
                blockNumber++;
                
                // Find the matching closing brace
                int openBraceCount = 1;
                int searchIndex = match.Index + match.Length;
                int closingBraceIndex = -1;

                while (searchIndex < text.Length && openBraceCount > 0)
                {
                    if (text[searchIndex] == '{')
                        openBraceCount++;
                    else if (text[searchIndex] == '}')
                    {
                        openBraceCount--;
                        if (openBraceCount == 0)
                        {
                            closingBraceIndex = searchIndex;
                            break;
                        }
                    }
                    searchIndex++;
                }

                if (closingBraceIndex == -1)
                {
                    result.AddError($"Execute block {blockNumber}: Missing closing brace '}}' ", 
                        text.Substring(0, match.Index).Count(c => c == '\n') + 1);
                    continue;
                }

                // Extract the JavaScript code
                int jsStartIndex = match.Index + match.Length;
                string jsCode = text.Substring(jsStartIndex, closingBraceIndex - jsStartIndex).Trim();
                int blockStartLine = text.Substring(0, match.Index).Count(c => c == '\n') + 1;

                // Append text before this execute block
                string beforeBlock = text.Substring(lastIndex, match.Index - lastIndex);
                resultText.Append(beforeBlock);
                
                // Update line mapping for the text before the block
                var beforeLines = beforeBlock.Split(new[] { '\r', '\n' });
                foreach (var line in beforeLines)
                {
                    lineMapping[currentOutputLine] = currentInputLine;
                    currentOutputLine++;
                    currentInputLine++;
                }

                // Process the execute block
                if (string.IsNullOrWhiteSpace(jsCode))
                {
                    result.AddError($"Execute block {blockNumber}: JavaScript code is empty", blockStartLine);
                }
                else
                {
                    var executeResult = jsEvaluator.ExecuteCodeBlock(jsCode);

                    if (!executeResult.IsSuccess)
                    {
                        result.AddError($"Execute block {blockNumber}: {executeResult.ErrorMessage}", blockStartLine);
                    }
                    else
                    {
                        // Add results as parameters
                        foreach (var kvp in executeResult.Value)
                        {
                            string name = kvp.Key;
                            object value = kvp.Value;

                            // Determine parameter type and add to model
                            if (value is double || value is float)
                            {
                                var param = new Parameter(name, ParameterType.Float, Convert.ToDouble(value));
                                modelManager.AddParameter(param);
                                result.IncrementSuccess();
                            }
                            else if (value is int || value is long)
                            {
                                var param = new Parameter(name, ParameterType.Integer, Convert.ToInt32(value));
                                modelManager.AddParameter(param);
                                result.IncrementSuccess();
                            }
                            else if (value is string)
                            {
                                var param = new Parameter(name, ParameterType.String, value);
                                modelManager.AddParameter(param);
                                result.IncrementSuccess();
                            }
                            else if (value is List<object> list)
                            {
                                // For arrays, store as a string representation for now
                                var arrayStr = string.Join(", ", list);
                                var param = new Parameter(name, ParameterType.String, $"[{arrayStr}]");
                                modelManager.AddParameter(param);
                                result.IncrementSuccess();
                            }
                        }
                    }
                }

                // Skip the input lines consumed by the execute block
                int blockEndLine = text.Substring(0, closingBraceIndex + 1).Count(c => c == '\n') + 1;
                currentInputLine = blockEndLine;

                // Move lastIndex past the closing brace
                lastIndex = closingBraceIndex + 1;
            }

            // Append remaining text after the last execute block
            if (lastIndex < text.Length)
            {
                string afterBlock = text.Substring(lastIndex);
                resultText.Append(afterBlock);
                
                var afterLines = afterBlock.Split(new[] { '\r', '\n' });
                foreach (var line in afterLines)
                {
                    lineMapping[currentOutputLine] = currentInputLine;
                    currentOutputLine++;
                    currentInputLine++;
                }
            }

            return (resultText.ToString(), lineMapping);
        }

        private void ProcessStatement(string statement, int lineNumber, ParseSessionResult result)
        {
            string error = string.Empty;
            
            // Try parameter parsing FIRST (handles: int x = 5, float y = 2.5 * 4, string z = "text")
            if (TryParseParameter(statement, out var param))
            {
                modelManager.AddParameter(param);
                result.IncrementSuccess();
                return;
            }

            // Try index set parsing (handles: range I = 1..10)
            if (TryParseIndexSet(statement, out var indexSet, out error))
            {
                if (indexSet != null)
                {
                    modelManager.AddIndexSet(indexSet);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    // IndexSet was recognized but had an error
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(error))
            {
                // Check if this was a partial match with an error
                if (!error.Equals("Not an index set declaration", StringComparison.Ordinal))
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }

            // Try variable declaration (handles: var float x[I])
            if (TryParseVariableDeclaration(statement, out var variable, out error))
            {
                if (variable != null)
                {
                    modelManager.AddIndexedVariable(variable);
                    result.IncrementSuccess();
                    return;
                }
                else
                {
                    // Variable was recognized but had an error
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(error))
            {
                // Check if this was a partial match with an error
                if (!error.Equals("Not a variable declaration", StringComparison.Ordinal))
                {
                    result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
                    return;
                }
            }

            // Try indexed equation (handles: equation name[I]: x[i] + y[i] == 10)
            if (TryParseIndexedEquation(statement, lineNumber, out error, result))
            {
                result.IncrementSuccess();
                return;
            }

            // Try regular equation (handles: 2*x + 3*y == 10)
            // NOTE: This should NOT match statements with single '=' because TryParseEquation
            // only accepts '==' for equality
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
                        result.AddError($"Error adding equation - {ex.Message}", lineNumber);
                        return;
                    }
                }
            }

            result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
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

            // Try two-dimensional indexed variable: var [type] name[IndexSet1,IndexSet2]
            // Updated pattern to handle optional whitespace around the comma
            string twoDimPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\]$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                string typeStr = twoDimMatch.Groups[1].Value;
                string varName = twoDimMatch.Groups[2].Value;
                string indexSetName1 = twoDimMatch.Groups[3].Value;
                string indexSetName2 = twoDimMatch.Groups[4].Value;

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

                if (!modelManager.IndexSets.ContainsKey(indexSetName1))
                {
                    error = $"First index set '{indexSetName1}' is not declared";
                    return false;
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName2))
                {
                    error = $"Second index set '{indexSetName2}' is not declared";
                    return false;
                }

                variable = new IndexedVariable(varName, indexSetName1, varType, indexSetName2);
                return true;
            }

            // Try single-dimensional indexed variable: var [type] name[IndexSet]
            string indexedPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\]$";
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

            // Try scalar variable: var [type] name
            string scalarPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)$";
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

        private bool TryParseIndexedEquation(string statement, int lineNumber, out string error, ParseSessionResult result)
        {
            error = string.Empty;

            // Try two-dimensional indexed equation: equation name[I,J]: template
            string twoDimPattern = @"^\s*equation\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                string baseName = twoDimMatch.Groups[1].Value;
                string indexSetName1 = twoDimMatch.Groups[2].Value;
                string indexSetName2 = twoDimMatch.Groups[3].Value;
                string template = twoDimMatch.Groups[4].Value;

                if (!modelManager.IndexSets.ContainsKey(indexSetName1))
                {
                    error = $"First index set '{indexSetName1}' is not declared";
                    return false;
                }

                if (!modelManager.IndexSets.ContainsKey(indexSetName2))
                {
                    error = $"Second index set '{indexSetName2}' is not declared";
                    return false;
                }

                var indexedEquation = new IndexedEquation(baseName, indexSetName1, template, indexSetName2);
                modelManager.AddIndexedEquationTemplate(indexedEquation);

                // Expand equations for all combinations of indices (nested loops)
                var indexSet1 = modelManager.IndexSets[indexSetName1];
                var indexSet2 = modelManager.IndexSets[indexSetName2];

                foreach (int index1 in indexSet1.GetIndices())
                {
                    foreach (int index2 in indexSet2.GetIndices())
                    {
                        // Replace both index variables in the template
                        string expandedEquation = ExpandEquationTemplate(template, indexSetName1.ToLower(), index1);
                        expandedEquation = ExpandEquationTemplate(expandedEquation, indexSetName2.ToLower(), index2);

                        if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                        {
                            if (eq != null)
                            {
                                eq.BaseName = baseName;
                                eq.Index = index1;
                                eq.SecondIndex = index2;
                                modelManager.AddEquation(eq);
                            }
                        }
                        else
                        {
                            result.AddError($"Expanded indices [{index1},{index2}]: {eqError}", lineNumber);
                        }
                    }
                }

                return true;
            }

            // Try single-dimensional indexed equation: equation name[I]: template
            string pattern = @"^\s*equation\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9]*)\s*\]\s*:\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an indexed equation declaration";
                return false;
            }

            string  baseName1 = match.Groups[1].Value;
            string indexSetName = match.Groups[2].Value;
            string template1 = match.Groups[3].Value;

            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not declared";
                return false;
            }

            var indexedEquation1 = new IndexedEquation(baseName1, indexSetName, template1);
            modelManager.AddIndexedEquationTemplate(indexedEquation1);

            // Expand equations
            var indexSet = modelManager.IndexSets[indexSetName];
            foreach (int index in indexSet.GetIndices())
            {
                string expandedEquation = ExpandEquationTemplate(template1, indexSetName.ToLower(), index);
                
                if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                {
                    if (eq != null)
                    {
                        eq.BaseName = baseName1;
                        eq.Index = index;
                        modelManager.AddEquation(eq);
                    }
                }
                else
                {
                    result.AddError($"Expanded index {index}: {eqError}", lineNumber);
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
                // IMPORTANT: Check == before other operators to avoid matching = twice
                if (cleaned.Contains("=="))
                {
                    op = RelationalOperator.Equal;
                    parts = cleaned.Split(new[] { "==" }, StringSplitOptions.None);
                }
                else if (cleaned.Contains("<="))
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
                    // Single = is not allowed for equations
                    error = "Invalid operator '='. Use '==' for equality in equations";
                    return false;
                }
                else
                {
                    error = "Missing relational operator. Must contain ==, <, >, <=, or >=";
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
                        error = "Equation reduces to 0 == 0 (tautology - always true)";
                        return false;
                    }
                    else
                    {
                        error = $"Equation reduces to 0 == {finalConstant} (contradiction - always false)";
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
                // Pattern for two-dimensional indexed variables: x[1,2], var2[i,j]
                string patternTwoDim = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+),([a-zA-Z0-9]+)\]";
                
                // Expand two-dimensional indexed variables FIRST
                expression = Regex.Replace(expression, patternTwoDim, m =>
                {
                    string varName = m.Groups[1].Value;
                    string index1Str = m.Groups[2].Value;
                    string index2Str = m.Groups[3].Value;
                    
                    if (int.TryParse(index1Str, out int numericIndex1) && int.TryParse(index2Str, out int numericIndex2))
                    {
                        // Numeric indices: x[1,2] -> x1_2
                        if (modelManager.IndexedVariables.ContainsKey(varName))
                        {
                            var indexedVar = modelManager.IndexedVariables[varName];
                            if (indexedVar.IsTwoDimensional)
                            {
                                var indexSet1 = modelManager.IndexSets[indexedVar.IndexSetName];
                                var indexSet2 = modelManager.IndexSets[indexedVar.SecondIndexSetName!];
                                
                                if (!indexSet1.Contains(numericIndex1))
                                {
                                    throw new Exception($"First index {numericIndex1} is out of range for variable {varName}[{indexedVar.IndexSetName},{indexedVar.SecondIndexSetName}]. Valid range: {indexSet1.StartIndex}..{indexSet1.EndIndex}");
                                }
                                
                                if (!indexSet2.Contains(numericIndex2))
                                {
                                    throw new Exception($"Second index {numericIndex2} is out of range for variable {varName}[{indexedVar.IndexSetName},{indexedVar.SecondIndexSetName}]. Valid range: {indexSet2.StartIndex}..{indexSet2.EndIndex}");
                                }
                            }
                        }
                        return $"{varName}{numericIndex1}_{numericIndex2}";
                    }
                    else
                    {
                        // Variable indices: x[i,j] - keep as symbolic
                        return $"{varName}_idx_{index1Str}_{index2Str}";
                    }
                });

                // Pattern for single-dimensional indexed variables: x[1], var2[5], x[i]
                string patternIndexed = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+)\]";
                
                // Expand indexed variables
                expression = Regex.Replace(expression, patternIndexed, m =>
                {
                    string varName = m.Groups[1].Value;
                    string indexStr = m.Groups[2].Value;
                    
                    if (int.TryParse(indexStr, out int numericIndex))
                    {
                        // Numeric index: x[1] -> x1
                        if (modelManager.IndexedVariables.ContainsKey(varName))
                        {
                            var indexedVar = modelManager.IndexedVariables[varName];
                            if (!indexedVar.IsScalar && !indexedVar.IsTwoDimensional)
                            {
                                var indexSet = modelManager.IndexSets[indexedVar.IndexSetName];
                                
                                if (!indexSet.Contains(numericIndex))
                                {
                                    throw new Exception($"Index {numericIndex} is out of range for variable {varName}[{indexedVar.IndexSetName}]. Valid range: {indexSet.StartIndex}..{indexSet.EndIndex}");
                                }
                            }
                        }
                        return $"{varName}{numericIndex}";
                    }
                    else
                    {
                        // Variable index: x[i] - keep as symbolic
                        return $"{varName}_idx_{indexStr}";
                    }
                });

                // Rest of the parsing logic remains the same...
                string patternWithMultiply = @"([+-]?\d+(?:\.\d+)?)\*([a-zA-Z][a-zA-Z0-9_]*)";
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

                    string[] parts = constantExpression.Split(['|', '+', '-', '*'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (string part in parts)
                    {
                        string trimmedPart = part.Trim();
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

        /// <summary>
        /// Gets the model manager (useful for testing)
        /// </summary>
        public ModelManager GetModelManager() => modelManager;
    }

    public class ParseSessionResult
    {
        public List<(string Message, int LineNumber)> Errors { get; private set; } = new List<(string, int)>();
        public int SuccessCount { get; private set; } = 0;

        public void AddError(string error, int lineNumber)
        {
            Errors.Add(($"Line {lineNumber}: {error}", lineNumber));
        }

        public void IncrementSuccess()
        {
            SuccessCount++;
        }

        public bool HasErrors => Errors.Count > 0;
        public bool HasSuccess => SuccessCount > 0;

        /// <summary>
        /// Gets all error messages as formatted strings
        /// </summary>
        public IEnumerable<string> GetErrorMessages()
        {
            return Errors.Select(e => e.Message);
        }

        /// <summary>
        /// Gets errors for a specific line number
        /// </summary>
        public IEnumerable<string> GetErrorsForLine(int lineNumber)
        {
            return Errors.Where(e => e.LineNumber == lineNumber).Select(e => e.Message);
        }
    }
}