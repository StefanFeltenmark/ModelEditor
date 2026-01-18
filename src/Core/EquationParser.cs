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
            
            // Try two-dimensional indexed parameter: type name[IndexSet1,IndexSet2] = ...
            string twoDimPattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*=\s*(.+)$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);
            
            if (twoDimMatch.Success)
            {
                string typeStr = twoDimMatch.Groups[1].Value.ToLower();
                string name = twoDimMatch.Groups[2].Value;
                string indexSetName1 = twoDimMatch.Groups[3].Value;
                string indexSetName2 = twoDimMatch.Groups[4].Value;
                string valueStr = twoDimMatch.Groups[5].Value.Trim();

                ParameterType paramType = typeStr switch
                {
                    "int" => ParameterType.Integer,
                    "float" => ParameterType.Float,
                    "string" => ParameterType.String,
                    _ => ParameterType.Float
                };

                // Validate index sets exist
                if (!modelManager.IndexSets.ContainsKey(indexSetName1))
                {
                    // This will be caught as an error in ProcessStatement
                    return false;
                }
                
                if (!modelManager.IndexSets.ContainsKey(indexSetName2))
                {
                    return false;
                }

                // Check for external data marker
                if (valueStr == "...")
    {
        parameter = new Parameter(name, paramType, indexSetName1, indexSetName2, isExternal: true);
        return true;
    }

    // For now, we don't support inline array initialization in model files
    // Data must come from data files
    return false;
}
    
    // Try single-dimensional indexed parameter: type name[IndexSet] = ...
    string indexedPattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*]\s*=\s*(.+)$";
    var indexedMatch = Regex.Match(statement.Trim(), indexedPattern);
    
    if (indexedMatch.Success)
    {
        string typeStr = indexedMatch.Groups[1].Value.ToLower();
        string name = indexedMatch.Groups[2].Value;
        string indexSetName = indexedMatch.Groups[3].Value;
        string valueStr = indexedMatch.Groups[4].Value.Trim();

        ParameterType paramType = typeStr switch
        {
            "int" => ParameterType.Integer,
            "float" => ParameterType.Float,
            "string" => ParameterType.String,
            _ => ParameterType.Float
        };

        // Validate index set exists
        if (!modelManager.IndexSets.ContainsKey(indexSetName))
        {
            return false;
        }

        // Check for external data marker
        if (valueStr == "...")
    {
        parameter = new Parameter(name, paramType, indexSetName, isExternal: true);
        return true;
    }

    // For now, we don't support inline array initialization in model files
    return false;
}
    
    // Try scalar parameter: type name = value
    string scalarPattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
    var scalarMatch = Regex.Match(statement.Trim(), scalarPattern);

    if (!scalarMatch.Success)
        return false;

    string typeStr2 = scalarMatch.Groups[1].Value.ToLower();
    string name2 = scalarMatch.Groups[2].Value;
    string valueStr2 = scalarMatch.Groups[3].Value.Trim();

    // Check if this is an external data reference (three dots)
    if (valueStr2 == "...")
    {
        ParameterType paramType = typeStr2 switch
        {
            "int" => ParameterType.Integer,
            "float" => ParameterType.Float,
            "string" => ParameterType.String,
            _ => ParameterType.Float
        };
        
        parameter = new Parameter(name2, paramType, null, isExternal: true);
        return true;
    }

    ParameterType paramType2 = typeStr2 switch
    {
        "int" => ParameterType.Integer,
        "float" => ParameterType.Float,
        "string" => ParameterType.String,
        _ => ParameterType.Float
    };

    try
    {
        object value;
        switch (paramType2)
        {
            case ParameterType.Integer:
                var intResult = evaluator.EvaluateIntExpression(valueStr2);
                if (!intResult.IsSuccess)
                    return false;
                value = intResult.Value;
                break;

            case ParameterType.Float:
                var floatResult = evaluator.EvaluateFloatExpression(valueStr2);
                if (!floatResult.IsSuccess)
                    return false;
                value = floatResult.Value;
                break;

            case ParameterType.String:
                if (valueStr2.StartsWith("\"") && valueStr2.EndsWith("\""))
                {
                    value = valueStr2.Substring(1, valueStr2.Length - 2);
                }
                else
                {
                    return false;
                }
                break;

            default:
                return false;
        }

        parameter = new Parameter(name2, paramType2, value, isExternal: false);
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

            // Helper function to parse bounds
            bool TryParseBounds(string boundsStr, out double? lower, out double? upper, out string parseError)
            {
                lower = null;
                upper = null;
                parseError = string.Empty;

                // Updated regex pattern to properly handle decimal numbers
                // Pattern: -?\d+(?:\.\d+)? matches optional negative sign, digits, optional decimal point and more digits
                var boundsMatch = Regex.Match(boundsStr, 
                    @"^\s*(-?\d+(?:\.\d+)?|[a-zA-Z][a-zA-Z0-9_]*)\s*\.\.\s*(-?\d+(?:\.\d+)?|[a-zA-Z][a-zA-Z0-9_]*)\s*$");
                
                if (!boundsMatch.Success)
                {
                    parseError = "Invalid bounds format. Expected: in lower..upper";
                    return false;
                }

                string lowerStr = boundsMatch.Groups[1].Value;
                string upperStr = boundsMatch.Groups[2].Value;

                // Try to parse lower bound (could be number or parameter)
                if (double.TryParse(lowerStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lowerVal))
                {
                    lower = lowerVal;
                }
                else if (modelManager.Parameters.TryGetValue(lowerStr, out var lowerParam))
                {
                    if (lowerParam.Type == ParameterType.Integer || lowerParam.Type == ParameterType.Float)
                    {
                        lower = Convert.ToDouble(lowerParam.Value);
                    }
                    else
                    {
                        parseError = $"Parameter '{lowerStr}' must be numeric for bounds";
                        return false;
                    }
                }
                else
                {
                    parseError = $"Lower bound '{lowerStr}' is not a valid number or declared parameter";
                    return false;
                }

                // Try to parse upper bound (could be number or parameter)
                if (double.TryParse(upperStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double upperVal))
                {
                    upper = upperVal;
                }
                else if (modelManager.Parameters.TryGetValue(upperStr, out var upperParam))
                {
                    if (upperParam.Type == ParameterType.Integer || upperParam.Type == ParameterType.Float)
                    {
                        upper = Convert.ToDouble(upperParam.Value);
                    }
                    else
                    {
                        parseError = $"Parameter '{upperStr}' must be numeric for bounds";
                        return false;
                    }
                }
                else
                {
                    parseError = $"Upper bound '{upperStr}' is not a valid number or declared parameter";
                    return false;
                }

                if (lower > upper)
                {
                    parseError = $"Lower bound ({lower}) cannot be greater than upper bound ({upper})";
                    return false;
                }

                return true;
            }

            // Try two-dimensional indexed variable with optional bounds: var [type] name[IndexSet1,IndexSet2] [in l..u]
            string twoDimPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\](?:\s+in\s+(.+))?$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                string typeStr = twoDimMatch.Groups[1].Value;
                string varName = twoDimMatch.Groups[2].Value;
                string indexSetName1 = twoDimMatch.Groups[3].Value;
                string indexSetName2 = twoDimMatch.Groups[4].Value;
                string boundsStr = twoDimMatch.Groups[5].Value;

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

                double? lower = null, upper = null;
                if (!string.IsNullOrEmpty(boundsStr))
                {
                    if (!TryParseBounds(boundsStr, out lower, out upper, out error))
                    {
                        return false;
                    }
                }

                variable = new IndexedVariable(varName, indexSetName1, varType, indexSetName2, lower, upper);
                return true;
            }

            // Try single-dimensional indexed variable with optional bounds: var [type] name[IndexSet] [in l..u]
            string indexedPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\](?:\s+in\s+(.+))?$";
            var indexedMatch = Regex.Match(statement.Trim(), indexedPattern);

            if (indexedMatch.Success)
            {
                string typeStr = indexedMatch.Groups[1].Value;
                string varName = indexedMatch.Groups[2].Value;
                string indexSetName = indexedMatch.Groups[3].Value;
                string boundsStr = indexedMatch.Groups[4].Value;

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

                double? lower = null, upper = null;
                if (!string.IsNullOrEmpty(boundsStr))
                {
                    if (!TryParseBounds(boundsStr, out lower, out upper, out error))
                    {
                        return false;
                    }
                }

                variable = new IndexedVariable(varName, indexSetName, varType, null, lower, upper);
                return true;
            }

            // Try scalar variable with optional bounds: var [type] name [in l..u]
            string scalarPattern = @"^\s*var\s+(?:(float|int|bool)\s+)?([a-zA-Z][a-zA-Z0-9_]*)(?:\s+in\s+(.+))?$";
            var scalarMatch = Regex.Match(statement.Trim(), scalarPattern);

            if (scalarMatch.Success)
            {
                string typeStr = scalarMatch.Groups[1].Value;
                string varName = scalarMatch.Groups[2].Value;
                string boundsStr = scalarMatch.Groups[3].Value;

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

                double? lower = null, upper = null;
                if (!string.IsNullOrEmpty(boundsStr))
                {
                    if (!TryParseBounds(boundsStr, out lower, out upper, out error))
                    {
                        return false;
                    }
                }

                // Scalar variables use empty string for IndexSetName to distinguish from indexed
                variable = new IndexedVariable(varName, string.Empty, varType, null, lower, upper);
                return true;
            }

            error = "Not a variable declaration";
            return false;
        }

        private bool TryParseIndexedEquation(string statement, int lineNumber, out string error, ParseSessionResult result)
        {
            error = string.Empty;

            // Try two-dimensional indexed equation: equation_label[i in I, j in J]: template
            string twoDimPattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
            var twoDimMatch = Regex.Match(statement.Trim(), twoDimPattern);

            if (twoDimMatch.Success)
            {
                string baseName = twoDimMatch.Groups[1].Value;
                string indexVar1 = twoDimMatch.Groups[2].Value;
                string indexSetName1 = twoDimMatch.Groups[3].Value;
                string indexVar2 = twoDimMatch.Groups[4].Value;
                string indexSetName2 = twoDimMatch.Groups[5].Value;
                string template = twoDimMatch.Groups[6].Value;

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

                // Store the index variable names for expansion
                // We'll expand using the explicit variable names from the declaration
                // Note: The template should use the same variable names (e.g., x[i,j])

                return true;
            }

            // Try single-dimensional indexed equation: equation_label[i in I]: template
            string pattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*:\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not an indexed equation declaration";
                return false;
            }

            string baseName1 = match.Groups[1].Value;
            string indexVar = match.Groups[2].Value;
            string indexSetName = match.Groups[3].Value;
            string template1 = match.Groups[4].Value;

            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not declared";
                return false;
            }

            var indexedEquation1 = new IndexedEquation(baseName1, indexSetName, template1);
            modelManager.AddIndexedEquationTemplate(indexedEquation1);

            // The template expansion will use the index variable name from the declaration
            // Note: The template should use the same variable name (e.g., x[i])

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
                    string potentialLabel = labelMatch.Groups[1].Value;
                    string remainingText = labelMatch.Groups[2].Value;
    
                    // Verify that the remaining text contains a relational operator
                    if (remainingText.Contains("==") || remainingText.Contains("<=") || 
                        remainingText.Contains(">=") || remainingText.Contains("<") || 
                        remainingText.Contains(">") || remainingText.Contains("≤") || 
                        remainingText.Contains("≥"))
                    {
                        label = potentialLabel;
                        equationText = remainingText;
                    }
                    else
                    {
                        error = $"Invalid label format. Found '{potentialLabel}:' but no relational operator (==, <=, >=, <, >) in the remaining text";
                        return false;
                    }
                }

                // **IMPORTANT: Expand summations BEFORE removing whitespace**
                equationText = ExpandSummations(equationText, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return false;
                }

                // NOW remove all whitespace for easier parsing
                string cleaned = Regex.Replace(equationText, @"\s+", "");

                // Additional validation: ensure no remaining colons in the cleaned equation
                if (cleaned.Contains(':'))
                {
                    error = "Invalid equation format. Unexpected ':' found in equation. Only one label is allowed at the beginning";
                    return false;
                }

                // Detect the operator and split the equation
                RelationalOperator op;
                string[] parts;
                
                // Check for operators in order of longest to shortest to avoid conflicts
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
                var finalCoefficients = new Dictionary<string, Expression>();
                
                // Add left side coefficients
                foreach (var kvp in leftCoefficients)
                {
                    finalCoefficients[kvp.Key] = kvp.Value;
                }

                // Subtract right side coefficients (move to left)
                foreach (var kvp in rightCoefficients)
                {
                    if (finalCoefficients.ContainsKey(kvp.Key))
                    {
                        finalCoefficients[kvp.Key] = new BinaryExpression(
                            finalCoefficients[kvp.Key],
                            BinaryOperator.Subtract,
                            kvp.Value);
                    }
                    else
                    {
                        finalCoefficients[kvp.Key] = new UnaryExpression(
                            UnaryOperator.Negate,
                            kvp.Value);
                    }
                }

                // Calculate final constant: right constant - left constant
                Expression finalConstant = new BinaryExpression(
                    rightConstant,
                    BinaryOperator.Subtract,
                    leftConstant);

                result = new LinearEquation(finalCoefficients, finalConstant, op, label);
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
        
        
        private bool TryParseExpression(
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
        // STEP 1: Pattern for two-dimensional indexed variables/parameters: x[1,2], a[i,j]
        string patternTwoDim = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+),([a-zA-Z0-9]+)\]";
        
        // Track parameter references for creating expressions
        var parameterReferences = new Dictionary<string, Expression>();
        
        // Replace indexed parameters with placeholder tokens
        var tokenCounter = 0;
        var tokenMap = new Dictionary<string, Expression>();
        
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
                        // Create expression for this parameter reference
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
                            throw new Exception($"First index {numericIndex1} is out of range for variable {name}[{indexedVar.IndexSetName},{indexedVar.SecondIndexSetName}]. Valid range: {indexSet1.StartIndex}..{indexSet1.EndIndex}");
                        }
                        
                        if (!indexSet2.Contains(numericIndex2))
                        {
                            throw new Exception($"Second index {numericIndex2} is out of range for variable {name}[{indexedVar.IndexSetName},{indexedVar.SecondIndexSetName}]. Valid range: {indexSet2.StartIndex}..{indexSet2.EndIndex}");
                        }
                    }
                    return $"{name}{numericIndex1}_{numericIndex2}";
                }
                
                return m.Value;
            }
            else
            {
                return $"{name}_idx_{index1Str}_{index2Str}";
            }
        });

        // Pattern for single-dimensional indexed variables/parameters
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
                        // Validate index
                        if (modelManager.IndexSets.TryGetValue(param.IndexSetName, out var paramIndexSet))
                        {
                            if (!paramIndexSet.Contains(numericIndex))
                            {
                                throw new Exception($"Index {numericIndex} is out of range for parameter {name}[{param.IndexSetName}]. Valid range: {paramIndexSet.StartIndex}..{paramIndexSet.EndIndex}");
                            }
                        }
                        
                        // Create expression for this parameter reference
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
                            throw new Exception($"Index {numericIndex} is out of range for variable {name}[{indexedVar.IndexSetName}]. Valid range: {indexSet.StartIndex}..{indexSet.EndIndex}");
                        }
                    }
                    return $"{name}{numericIndex}";
                }
                
                return m.Value;
            }
            else
            {
                return $"{name}_idx_{indexStr}";
            }
        });

        // Now parse the expression with tokens
        // Pattern: coefficient * variable or just variable
        string patternWithMultiply = @"([+-]?[\d.]+|__PARAM\d+__)\*([a-zA-Z][a-zA-Z0-9_]*)";
        string patternImplicit = @"([+-]?[\d.]*|__PARAM\d+__)([a-zA-Z][a-zA-Z0-9_]*)";

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

            if (coefficients.ContainsKey(variable))
            {
                // Add to existing coefficient
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
                remainingChars.Append('|');
            }
        }
        string remainingExpression = remainingChars.ToString();

        // Process implicit multiplication
        MatchCollection implicitMatches = Regex.Matches(remainingExpression, patternImplicit);
        foreach (Match match in implicitMatches)
        {
            string coeffStr = match.Groups[1].Value;
            string variable = match.Groups[2].Value;

            // Skip if variable is empty or if it looks like a token fragment
            if (string.IsNullOrEmpty(variable) || variable.StartsWith("PARAM"))
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
            else if (tokenMap.TryGetValue(coeffStr, out var paramExpr))
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

        // STEP 3: Simplify all coefficient expressions
        foreach (var key in coefficients.Keys.ToList())
        {
            coefficients[key] = coefficients[key].Simplify(modelManager);
            
            // If simplified to zero, remove the variable
            if (coefficients[key] is ConstantExpression constCoeff && 
                Math.Abs(constCoeff.Value) < 1e-10)
            {
                coefficients.Remove(key);
            }
        }
        
        // STEP 4: Extract and combine constant terms
        // Parse the expression to find all constant terms
        var constantTerms = new List<double>();
        
        // Remove all variable terms and parameter tokens, extract remaining constants
        string constantExpression = expression;
        
        // Remove matched variable patterns
        foreach (var kvp in coefficients)
        {
            // This is a simplified approach - a more robust solution would track
            // what parts of the expression have been consumed
        }
        
        // Simplify the constant expression
        constant = constant.Simplify(modelManager);
        
        // Validate variables
        if (coefficients.Count > 0)
        {
            if (!ValidateVariableDeclarations(coefficients, out string validationError))
            {
                error = validationError;
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

        /// <summary>
/// Expands all sum(...) expressions in the given expression
/// Example: sum(i in I) a[i]*x[i] becomes (a[1]*x[1]+a[2]*x[2]+a[3]*x[3])
/// </summary>
private string ExpandSummations(string expression, out string error)
{
    error = string.Empty;
    
    // Keep expanding until no more sum(...) expressions found
    int maxIterations = 100; // Prevent infinite loops
    int iterations = 0;
    
    while (iterations < maxIterations)
    {
        // Find the first sum(...) expression - case insensitive
        var match = Regex.Match(expression, 
            @"sum\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\)",
            RegexOptions.IgnoreCase);
            
        if (!match.Success)
            break; // No more sums to expand
            
        iterations++;
        
        string indexVar = match.Groups[1].Value;
        string indexSetName = match.Groups[2].Value;
        
        // Validate index set exists
        if (!modelManager.IndexSets.TryGetValue(indexSetName, out var indexSet))
        {
            error = $"Index set '{indexSetName}' not found in sum expression";
            return expression;
        }
        
        // Find where the sum expression ends
        int exprStart = match.Index + match.Length;
        int exprEnd = FindSumExpressionEnd(expression, exprStart);
        
        if (exprEnd <= exprStart)
        {
            error = $"Empty or invalid sum expression after 'sum({indexVar} in {indexSetName})'";
            return expression;
        }
        
        string sumExpr = expression.Substring(exprStart, exprEnd - exprStart).Trim();
        
        // Handle empty expression
        if (string.IsNullOrWhiteSpace(sumExpr))
        {
            error = $"Empty sum expression after 'sum({indexVar} in {indexSetName})'";
            return expression;
        }
        
        // Expand the summation
        var terms = new List<string>();
        foreach (int idx in indexSet.GetIndices())
        {
            // Replace index variable with actual index value
            string expandedTerm = ReplaceIndexVariable(sumExpr, indexVar, idx);
            terms.Add(expandedTerm);
        }
        
        // Join all terms with + and wrap in parentheses
        string expandedSum = terms.Count > 0 
            ? "(" + string.Join("+", terms) + ")" 
            : "0";
        
        // Replace the sum expression with the expanded form
        expression = expression.Substring(0, match.Index) + expandedSum + expression.Substring(exprEnd);
    }
    
    if (iterations >= maxIterations)
    {
        error = "Maximum sum expansion iterations exceeded - possible nested sums issue";
        return expression;
    }
    
    return expression;
}

/// <summary>
/// Finds where a sum expression ends by looking for operators at the same nesting level
/// </summary>
private int FindSumExpressionEnd(string expression, int start)
{
    int parenDepth = 0;
    int bracketDepth = 0;
    bool inNumber = false;
    
    for (int i = start; i < expression.Length; i++)
    {
        char c = expression[i];
        
        // Track parentheses and brackets
        if (c == '(') 
        {
            parenDepth++;
        }
        else if (c == ')') 
        {
            parenDepth--;
            if (parenDepth < 0) // Closing paren from outer expression
                return i;
        }
        else if (c == '[') 
        {
            bracketDepth++;
        }
        else if (c == ']') 
        {
            bracketDepth--;
        }
        
        // Only check for operators when at the top level (depth 0)
        if (parenDepth == 0 && bracketDepth == 0)
        {
            // Check if we're in a number (to avoid breaking on scientific notation like 1e-5)
            if (char.IsDigit(c) || c == '.')
            {
                inNumber = true;
                continue;
            }
            
            // Check for relational operators (these end the sum expression)
            if (i + 1 < expression.Length)
            {
                string twoChar = expression.Substring(i, 2);
                if (twoChar == "==" || twoChar == "<=" || twoChar == ">" || twoChar == "≥" || twoChar == "≤")
                {
                    return i;
                }
            }
            
            // Check for single-character relational operators
            if (c == '<' || c == '>' || c == '=')
            {
                return i;
            }
            
            // Check for addition/subtraction ONLY if not part of a number
            if ((c == '+' || c == '-') && !inNumber)
            {
                // Make sure it's not a unary operator (e.g., after another operator or at start)
                if (i > start)
                {
                    char prev = expression[i - 1];
                    // If previous char is operator or opening paren, this is unary
                    if (prev != '*' && prev != '/' && prev != '(' && prev != '[' && prev != ',')
                    {
                        return i;
                    }
                }
            }
            
            if (c != ' ' && c != '\t')
            {
                inNumber = false;
            }
        }
    }
    
    // Reached end of expression
    return expression.Length;
}

/// <summary>
/// Replaces an index variable with a concrete index value in an expression
/// Example: ReplaceIndexVariable("a[i]*x[i]", "i", 5) returns "a[5]*x[5]"
/// </summary>
private string ReplaceIndexVariable(string expr, string indexVar, int indexValue)
{
    // Escape the index variable name for regex (in case it has special chars, though it shouldn't)
    string escapedVar = Regex.Escape(indexVar);
    
    // Pattern 1: [indexVar] -> [indexValue]
    expr = Regex.Replace(expr, $@"\[{escapedVar}\]", $"[{indexValue}]");
    
    // Pattern 2: [indexVar, something] -> [indexValue, something]
    expr = Regex.Replace(expr, $@"\[{escapedVar}\s*,", $"[{indexValue},");
    
    // Pattern 3: [something, indexVar] -> [something, indexValue]
    expr = Regex.Replace(expr, $@",\s*{escapedVar}\]", $",{indexValue}]");
    
    return expr;
}
       
        private string ExpandEquationTemplate(string template, string indexVariable, int indexValue)
        {
            // Pattern 1: [indexVariable] -> [indexValue]
            // Example: x[i] -> x[1]
            template = Regex.Replace(template, $@"\[{indexVariable}\]", $"[{indexValue}]", RegexOptions.IgnoreCase);
    
            // Pattern 2: [indexVariable, something] -> [indexValue, something]
            // Example: x[i,j] -> x[1,j]
            template = Regex.Replace(template, $@"\[{indexVariable}\s*,", $"[{indexValue},", RegexOptions.IgnoreCase);
    
            // Pattern 3: [something, indexVariable] -> [something, indexValue]
            // Example: x[j,i] -> x[j,1]
            template = Regex.Replace(template, $@",\s*{indexVariable}\]", $",{indexValue}]", RegexOptions.IgnoreCase);
    
            return template;
        }

        /// <summary>
        /// Gets the model manager (useful for testing)
        /// </summary>
        public ModelManager GetModelManager() => modelManager;

        // Add this new method to expand all stored indexed equation templates
        public void ExpandIndexedEquations(ParseSessionResult result)
        {
            foreach (var indexedEquation in modelManager.IndexedEquationTemplates.Values)
            {
                if (indexedEquation.IsTwoDimensional)
                {
                    // Two-dimensional expansion
                    var indexSet1 = modelManager.IndexSets[indexedEquation.IndexSetName];
                    var indexSet2 = modelManager.IndexSets[indexedEquation.SecondIndexSetName!];

                    foreach (int index1 in indexSet1.GetIndices())
                    {
                        foreach (int index2 in indexSet2.GetIndices())
                        {
                            // Use lowercase version of index set name as the variable name by convention
                            string indexVar1 = indexedEquation.IndexSetName.ToLower();
                            string indexVar2 = indexedEquation.SecondIndexSetName!.ToLower();
            
                            // IMPORTANT: Expand the template with actual index values FIRST
                            // before parsing (which includes summation expansion)
                            string expandedEquation = ExpandEquationTemplate(indexedEquation.Template, 
                                indexVar1, index1);
                            expandedEquation = ExpandEquationTemplate(expandedEquation, 
                                indexVar2, index2);

                            if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                            {
                                if (eq != null)
                                {
                                    eq.BaseName = indexedEquation.BaseName;
                                    eq.Index = index1;
                                    eq.SecondIndex = index2;
                                    modelManager.AddEquation(eq);
                                    result.IncrementSuccess();
                                }
                            }
                            else
                            {
                                result.AddError($"Error expanding equation '{indexedEquation.BaseName}[{index1},{index2}]': {eqError}", 0);
                            }
                        }
                    }
                }
                else
                {
                    // Single-dimensional expansion
                    var indexSet = modelManager.IndexSets[indexedEquation.IndexSetName];
    
            // Use lowercase version of index set name as the variable name by convention
            string indexVar = indexedEquation.IndexSetName.ToLower();
    
                    foreach (int index in indexSet.GetIndices())
                    {
                        // IMPORTANT: Expand the template with actual index value FIRST
                        // This must happen BEFORE TryParseEquation (which expands summations)
                        string expandedEquation = ExpandEquationTemplate(indexedEquation.Template, 
                            indexVar, index);
                        
                        if (TryParseEquation(expandedEquation, out var eq, out var eqError))
                        {
                            if (eq != null)
                            {
                                eq.BaseName = indexedEquation.BaseName;
                                eq.Index = index;
                                modelManager.AddEquation(eq);
                                result.IncrementSuccess();
                            }
                        }
                        else
                        {
                            result.AddError($"Error expanding equation '{indexedEquation.BaseName}[{index}]': {eqError}", 0);
                        }
                    }
                }
            }
        }

        // Add these methods at the end of the EquationParser class, before the closing brace

private bool ValidateVariableDeclarations(Dictionary<string, Expression> coefficients, out string error)
{
    error = string.Empty;
    var undeclaredVariables = new List<string>();

    foreach (var variableName in coefficients.Keys)
    {
        string baseVariableName = ExtractBaseVariableName(variableName);

        bool isDeclaredAsVariable = modelManager.IndexedVariables.ContainsKey(baseVariableName);
        bool isDeclaredAsParameter = modelManager.Parameters.ContainsKey(baseVariableName);

        if (!isDeclaredAsVariable && !isDeclaredAsParameter)
        {
            undeclaredVariables.Add(baseVariableName);
        }
    }

    if (undeclaredVariables.Any())
    {
        var uniqueUndeclared = undeclaredVariables.Distinct().OrderBy(v => v).ToList();

        if (uniqueUndeclared.Count == 1)
        {
            error = $"Variable '{uniqueUndeclared[0]}' is used but not declared. Use 'var {uniqueUndeclared[0]}' or 'var {uniqueUndeclared[0]}[IndexSet]' to declare it";
        }
        else
        {
            error = $"Variables {string.Join(", ", uniqueUndeclared.Select(v => $"'{v}'"))} are used but not declared. Variables must be declared before use";
        }

        return false;
    }

    return true;
}

private string ExtractBaseVariableName(string variableName)
{
    // CRITICAL: First check if this exact name exists as a declared variable or parameter
    // If it does, return it as-is (it's NOT an indexed form, it's a complete name)
    // This prevents "x1" (a variable named x1) from being confused with "x[1]" (x indexed at 1)
    if (modelManager.IndexedVariables.ContainsKey(variableName))
    {
        return variableName; // It's declared exactly as-is
    }
    
    if (modelManager.Parameters.ContainsKey(variableName))
    {
        return variableName; // It's a parameter with this exact name
    }

    // NOW check for transformed indexed patterns
    // These patterns are created by the expression parser when it transforms bracket notation
    
    // Pattern for indexed with variable indices: x_idx_i, x_idx_i_j
    if (variableName.Contains("_idx_"))
    {
        int idxPos = variableName.IndexOf("_idx_");
        return variableName.Substring(0, idxPos);
    }

    // Pattern for two-dimensional numeric indices: x1_2 (transformed from x[1,2])
    var match = Regex.Match(variableName, @"^([a-zA-Z][a-zA-Z0-9_]*?)(\d+_\d+)$");
    if (match.Success)
    {
        string baseName = match.Groups[1].Value;
        // Only extract if the base name exists as an indexed variable
        if (modelManager.IndexedVariables.ContainsKey(baseName))
        {
            return baseName;
        }
    }

    // Pattern for single-dimensional numeric index: x1 (transformed from x[1])
    // BUT: Only extract if we can confirm it's actually an indexed variable
    match = Regex.Match(variableName, @"^([a-zA-Z]+)(\d+)$");
    if (match.Success)
    {
        string baseName = match.Groups[1].Value;
        // Only extract if the base name exists as an indexed variable
        if (modelManager.IndexedVariables.ContainsKey(baseName))
        {
            return baseName;
        }
    }

    // No indexed pattern matched, return as-is
    // This is an undeclared variable/parameter with this exact name
    return variableName;
}
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