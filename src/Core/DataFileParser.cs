using System.Globalization;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core
{
    /// <summary>
    /// Parses data files (.dat) for external parameter values
    /// </summary>
    public class DataFileParser
    {
        private readonly ModelManager modelManager;

        public DataFileParser(ModelManager manager)
        {
            modelManager = manager;
        }

        /// <summary>
        /// Parses the provided text and returns the result of the parsing session.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>A <see cref="EquationParser.ParseSessionResult"/> object containing the results of the parse operation.</returns>
        public ParseSessionResult Parse(string text)
        {
            var result = new ParseSessionResult();

            if (string.IsNullOrWhiteSpace(text))
            {
                result.AddError("No text to parse", 0);
                return result;
            }

            // Split by lines and handle comments
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var processedLines = new List<(string content, int lineNumber)>();
            
            int lineNumber = 1;
            foreach (string line in lines)
            {
                string lineWithoutComment = line.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();
                if (!string.IsNullOrWhiteSpace(lineWithoutComment))
                {
                    processedLines.Add((lineWithoutComment, lineNumber));
                }
                lineNumber++;
            }

            // Group by statements (split by semicolons)
            var statements = new List<(string content, int lineNumber)>();
            string currentStatement = "";
            int statementStartLine = 0;

            foreach (var (content, lineNum) in processedLines)
            {
                if (string.IsNullOrEmpty(currentStatement))
                {
                    statementStartLine = lineNum;
                }
                
                currentStatement += " " + content;
                
                if (content.Contains(';'))
                {
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

            // Process each statement
            foreach (var (statement, lineNum) in statements)
            {
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    ProcessStatement(statement, lineNum, result);
                }
            }

            return result;
        }

        private void ProcessStatement(string statement, int lineNumber, ParseSessionResult result)
        {
            string error = string.Empty;

            // Try vector assignment: paramName = [value1, value2, ...]
            if (TryParseVectorAssignment(statement, out error))
            {
                result.IncrementSuccess();
                return;
            }

            // Try two-dimensional indexed parameter: paramName[i,j] = value
            if (TryParseTwoDimensionalAssignment(statement, out error))
            {
                result.IncrementSuccess();
                return;
            }

            // Try single-dimensional indexed parameter: paramName[i] = value
            if (TryParseSingleDimensionalAssignment(statement, out error))
            {
                result.IncrementSuccess();
                return;
            }

            // Try scalar parameter: paramName = value or type paramName = value
            if (TryParseScalarAssignment(statement, out error))
            {
                result.IncrementSuccess();
                return;
            }

            // Try tuple data: setName = { <field1, field2, ...>, <...>, ... };
            if (TryParseTupleData(statement, out error))
            {
                result.IncrementSuccess();
                return;
            }

            result.AddError($"\"{statement}\"\n  Error: {error}", lineNumber);
        }

        private bool TryParseVectorAssignment(string statement, out string error)
        {
            error = string.Empty;

            // Pattern: paramName = [value1, value2, ...] or [[row1], [row2], ...]
            string pattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*\[(.+)\]$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a vector assignment";
                return false;
            }

            string paramName = match.Groups[1].Value;
            string valuesStr = match.Groups[2].Value.Trim();

            // Check if parameter exists
            if (!modelManager.Parameters.TryGetValue(paramName, out var param))
            {
                error = $"Parameter '{paramName}' is not declared";
                return false;
            }

            // Must be an indexed parameter
            if (param.IsScalar)
            {
                error = $"Parameter '{paramName}' is scalar, not indexed. Use 'paramName = value' syntax";
                return false;
            }

            // Check if it's a 2D matrix assignment: [[...], [...], ...]
            if (valuesStr.TrimStart().StartsWith("["))
            {
                return ParseMatrixAssignment(param, valuesStr, out error);
            }
            else
            {
                return Parse1DVectorAssignment(param, valuesStr, out error);
            }
        }

        private bool Parse1DVectorAssignment(Parameter param, string valuesStr, out string error)
        {
            error = string.Empty;

            if (param.IsTwoDimensional)
            {
                error = $"Parameter '{param.Name}' is two-dimensional. Use matrix notation: [[row1], [row2], ...]";
                return false;
            }

            // Parse the values
            var values = ParseValueList(valuesStr, param.Type, out error);
            if (values == null)
            {
                return false;
            }

            // Get the index set to determine valid indices
            if (!modelManager.IndexSets.TryGetValue(param.IndexSetName, out var indexSet))
            {
                error = $"Index set '{param.IndexSetName}' not found";
                return false;
            }

            var indices = indexSet.GetIndices().ToList();

            // Check if the number of values matches the index set size
            if (values.Count != indices.Count)
            {
                error = $"Parameter '{param.Name}' expects {indices.Count} values (index set '{param.IndexSetName}' range: {indexSet.StartIndex}..{indexSet.EndIndex}), but {values.Count} were provided";
                return false;
            }

            // Assign values to the indexed parameter
            for (int i = 0; i < values.Count; i++)
            {
                param.SetIndexedValue(indices[i], values[i]);
            }

            return true;
        }

        private bool ParseMatrixAssignment(Parameter param, string matrixStr, out string error)
        {
            error = string.Empty;

            if (!param.IsTwoDimensional)
            {
                error = $"Parameter '{param.Name}' is one-dimensional. Use vector notation: [value1, value2, ...]";
                return false;
            }

            // Get the index sets
            if (!modelManager.IndexSets.TryGetValue(param.IndexSetName, out var indexSet1))
            {
                error = $"Index set '{param.IndexSetName}' not found";
                return false;
            }

            if (!modelManager.IndexSets.TryGetValue(param.SecondIndexSetName!, out var indexSet2))
            {
                error = $"Index set '{param.SecondIndexSetName}' not found";
                return false;
            }

            var indices1 = indexSet1.GetIndices().ToList();
            var indices2 = indexSet2.GetIndices().ToList();

            // Parse the matrix: [[row1], [row2], ...]
            var rows = ParseMatrixRows(matrixStr, out error);
            if (rows == null)
            {
                return false;
            }

            // Validate row count
            if (rows.Count != indices1.Count)
            {
                error = $"Parameter '{param.Name}' expects {indices1.Count} rows (index set '{indexSet1.Name}' range: {indexSet1.StartIndex}..{indexSet1.EndIndex}), but {rows.Count} were provided";
                return false;
            }

            // Validate and assign each row
            for (int i = 0; i < rows.Count; i++)
            {
                var rowValues = ParseValueList(rows[i], param.Type, out error);
                if (rowValues == null)
                {
                    error = $"Error in row {i + 1}: {error}";
                    return false;
                }

                if (rowValues.Count != indices2.Count)
                {
                    error = $"Row {i + 1}: Expected {indices2.Count} values (index set '{param.SecondIndexSetName}' range: {indexSet2.StartIndex}..{indexSet2.EndIndex}), but {rowValues.Count} were provided";
                    return false;
                }

                // Assign values for this row
                for (int j = 0; j < rowValues.Count; j++)
                {
                    param.SetIndexedValue(indices1[i], indices2[j], rowValues[j]);
                }
            }

            return true;
        }

        private List<string>? ParseMatrixRows(string matrixStr, out string error)
        {
            error = string.Empty;
            var rows = new List<string>();
            
            int depth = 0;
            var currentRow = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < matrixStr.Length; i++)
            {
                char c = matrixStr[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    currentRow.Append(c);
                }
                else if (!inQuotes)
                {
                    if (c == '[')
                    {
                        if (depth == 0)
                        {
                            // Start of a new row
                            currentRow.Clear();
                        }
                        else
                        {
                            currentRow.Append(c);
                        }
                        depth++;
                    }
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            // End of a row
                            rows.Add(currentRow.ToString().Trim());
                            currentRow.Clear();
                        }
                        else
                        {
                            currentRow.Append(c);
                        }
                    }
                    else if (depth > 0)
                    {
                        currentRow.Append(c);
                    }
                    // Skip whitespace and commas between rows when depth == 0
                }
                else
                {
                    currentRow.Append(c);
                }
            }

            if (depth != 0)
            {
                error = "Unbalanced brackets in matrix notation";
                return null;
            }

            if (rows.Count == 0)
            {
                error = "Empty matrix - no rows found";
                return null;
            }

            return rows;
        }

        private List<object>? ParseValueList(string valuesStr, ParameterType paramType, out string error)
        {
            error = string.Empty;
            var values = new List<object>();

            // Split by commas or whitespace, handling quoted strings
            var parts = SplitByCommaOrWhitespace(valuesStr);

            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                object? value = ParseValueForType(trimmed, paramType, out error);
                if (!string.IsNullOrEmpty(error))
                {
                    return null;
                }

                values.Add(value!);
            }

            return values;
        }

        private List<string> SplitByCommaOrWhitespace(string input)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            // First check if there are any commas (not in quotes)
            bool hasComma = false;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (input[i] == ',' && !inQuotes)
                {
                    hasComma = true;
                    break;
                }
            }

            if (hasComma)
            {
                // Comma-separated parsing
                inQuotes = false;
                for (int i = 0; i < input.Length; i++)
                {
                    char c = input[i];

                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                        current.Append(c);
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }

                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                }
            }
            else
            {
                // Whitespace-separated parsing (for format like "1 2 3")
                var tokens = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    result.Add(token.Trim());
                }
            }

            return result;
        }

        private bool TryParseTwoDimensionalAssignment(string statement, out string error)
        {
            error = string.Empty;

            // Pattern: paramName[index1, index2] = value
            string pattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*(\d+)\s*,\s*(\d+)\s*\]\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a 2D indexed parameter assignment";
                return false;
            }

            string paramName = match.Groups[1].Value;
            string index1Str = match.Groups[2].Value;
            string index2Str = match.Groups[3].Value;
            string valueStr = match.Groups[4].Value.Trim();

            if (!int.TryParse(index1Str, out int index1) || !int.TryParse(index2Str, out int index2))
            {
                error = "Invalid index format";
                return false;
            }

            if (!modelManager.Parameters.TryGetValue(paramName, out var param))
            {
                error = $"Parameter '{paramName}' is not declared";
                return false;
            }

            if (!param.IsTwoDimensional)
            {
                error = $"Parameter '{paramName}' is not two-dimensional";
                return false;
            }

            // Validate indices are in range
            var indexSet1 = modelManager.IndexSets[param.IndexSetName];
            var indexSet2 = modelManager.IndexSets[param.SecondIndexSetName!];

            if (!indexSet1.Contains(index1))
            {
                error = $"First index {index1} is out of range for parameter '{paramName}'. Valid range: {indexSet1.StartIndex}..{indexSet1.EndIndex}";
                return false;
            }

            if (!indexSet2.Contains(index2))
            {
                error = $"Second index {index2} is out of range for parameter '{paramName}'. Valid range: {indexSet2.StartIndex}..{indexSet2.EndIndex}";
                return false;
            }

            // Parse and set value
            object? value = ParseValueForType(valueStr, param.Type, out error);
            if (!string.IsNullOrEmpty(error))
                return false;

            param.SetIndexedValue(index1, index2, value!);
            return true;
        }

        private bool TryParseSingleDimensionalAssignment(string statement, out string error)
        {
            error = string.Empty;

            // Pattern: paramName[index] = value
            string pattern = @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*(\d+)\s*\]\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a 1D indexed parameter assignment";
                return false;
            }

            string paramName = match.Groups[1].Value;
            string indexStr = match.Groups[2].Value;
            string valueStr = match.Groups[3].Value.Trim();

            if (!int.TryParse(indexStr, out int index))
            {
                error = "Invalid index format";
                return false;
            }

            if (!modelManager.Parameters.TryGetValue(paramName, out var param))
            {
                error = $"Parameter '{paramName}' is not declared";
                return false;
            }

            if (param.IsScalar)
            {
                error = $"Parameter '{paramName}' is scalar, not indexed";
                return false;
            }

            if (param.IsTwoDimensional)
            {
                error = $"Parameter '{paramName}' is two-dimensional, use [i,j] syntax";
                return false;
            }

            // Validate index is in range
            var indexSet = modelManager.IndexSets[param.IndexSetName];
            if (!indexSet.Contains(index))
            {
                error = $"Index {index} is out of range for parameter '{paramName}'. Valid range: {indexSet.StartIndex}..{indexSet.EndIndex}";
                return false;
            }

            // Parse and set value
            object? value = ParseValueForType(valueStr, param.Type, out error);
            if (!string.IsNullOrEmpty(error))
                return false;

            param.SetIndexedValue(index, value!);
            return true;
        }

        private bool TryParseScalarAssignment(string statement, out string error)
        {
            error = string.Empty;

            // Pattern: [type] paramName = value
            string pattern = @"^\s*(?:(int|float|string)\s+)?([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
            {
                error = "Not a scalar parameter assignment";
                return false;
            }

            string typeStr = match.Groups[1].Value.ToLower();
            string paramName = match.Groups[2].Value;
            string valueStr = match.Groups[3].Value.Trim();

            if (!modelManager.Parameters.TryGetValue(paramName, out var param))
            {
                error = $"Parameter '{paramName}' is not declared";
                return false;
            }

            if (!param.IsScalar)
            {
                error = $"Parameter '{paramName}' is indexed. Use vector notation [v1, v2, ...] or indexed assignment paramName[i] = value";
                return false;
            }

            // Type checking if specified
            if (!string.IsNullOrEmpty(typeStr))
            {
                ParameterType declaredType = typeStr switch
                {
                    "int" => ParameterType.Integer,
                    "float" => ParameterType.Float,
                    "string" => ParameterType.String,
                    _ => ParameterType.Float
                };

                if (param.Type != declaredType)
                {
                    error = $"Type mismatch: parameter '{paramName}' is declared as {param.Type}, but assigned as {declaredType}";
                    return false;
                }
            }

            // Parse and set value
            object? value = ParseValueForType(valueStr, param.Type, out error);
            if (!string.IsNullOrEmpty(error))
                return false;

            param.Value = value;
            return true;
        }

        // Add this method to parse tuple data
        private bool TryParseTupleData(string statement, out string error)
        {
            error = string.Empty;

            // Pattern: setName = { <field1, field2, ...>, <...>, ... };
            var setMatch = Regex.Match(statement, @"^([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*\{(.+)\}$");
            
            if (!setMatch.Success)
            {
                return false;
            }

            string setName = setMatch.Groups[1].Value;
            string tupleData = setMatch.Groups[2].Value;

            // Find the tuple set
            if (!modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
            {
                error = $"Tuple set '{setName}' is not declared";
                return false;
            }

            // Get the schema
            if (!modelManager.TupleSchemas.TryGetValue(tupleSet.SchemaName, out var schema))
            {
                error = $"Tuple schema '{tupleSet.SchemaName}' is not found";
                return false;
            }

            // Parse tuple instances: <v1, v2, v3>, <v1, v2, v3>, ...
            var tuplePattern = @"<([^>]+)>";
            var tupleMatches = Regex.Matches(tupleData, tuplePattern);

            foreach (Match tupleMatch in tupleMatches)
            {
                string instanceData = tupleMatch.Groups[1].Value;
                var values = instanceData.Split(',').Select(v => v.Trim()).ToArray();

                if (values.Length != schema.Fields.Count)
                {
                    error = $"Tuple instance has {values.Length} values but schema '{schema.Name}' requires {schema.Fields.Count} fields";
                    return false;
                }

                var instance = new TupleInstance(schema.Name);
                int fieldIndex = 0;

                foreach (var field in schema.Fields)
                {
                    string fieldName = field.Key;
                    VariableType fieldType = field.Value;
                    string valueStr = values[fieldIndex++];

                    object parsedValue = ParseTupleValue(valueStr, fieldType, out string parseError);
                    
                    if (!string.IsNullOrEmpty(parseError))
                    {
                        error = $"Error parsing field '{fieldName}': {parseError}";
                        return false;
                    }

                    instance.SetValue(fieldName, parsedValue);
                }

                tupleSet.AddInstance(instance);
            }

            return true;
        }

        private object ParseTupleValue(string valueStr, VariableType type, out string error)
        {
            error = string.Empty;

            try
            {
                return type switch
                {
                    VariableType.String => valueStr.Trim('"'),
                    VariableType.Integer => int.Parse(valueStr),
                    VariableType.Float => double.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture),
                    VariableType.Boolean => bool.Parse(valueStr),
                    _ => throw new InvalidOperationException($"Unknown type: {type}")
                };
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null!;
            }
        }

        private object? ParseValueForType(string valueStr, ParameterType type, out string error)
        {
            error = string.Empty;
            valueStr = valueStr.Trim();

            try
            {
                switch (type)
                {
                    case ParameterType.Integer:
                        if (int.TryParse(valueStr, out int intValue))
                        {
                            return intValue;
                        }
                        else
                        {
                            error = $"Cannot parse '{valueStr}' as integer";
                            return null;
                        }

                    case ParameterType.Float:
                        if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatValue))
                        {
                            return floatValue;
                        }
                        else
                        {
                            error = $"Cannot parse '{valueStr}' as float";
                            return null;
                        }

                    case ParameterType.String:
                        // Remove quotes if present
                        if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                        {
                            return valueStr.Substring(1, valueStr.Length - 2);
                        }
                        return valueStr;

                    default:
                        error = $"Unsupported parameter type: {type}";
                        return null;
                }
            }
            catch (Exception ex)
            {
                error = $"Error parsing value '{valueStr}': {ex.Message}";
                return null;
            }
        }
    }
}