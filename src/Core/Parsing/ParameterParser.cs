    using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    public class ParameterParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public ParameterParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        public bool TryParse(string statement, out Parameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            // Try indexed parameter FIRST (both notations)
            if (TryParseIndexedParameter(statement, out param, out error))
            {
                return true;
            }

            // If it failed but not because it's not an indexed parameter, return the error
            if (!string.IsNullOrEmpty(error) && !error.Contains("Not an indexed parameter"))
            {
                return false;
            }

            // Reset error for next attempt
            error = string.Empty;

            // Try regular parameter
            var pattern = @"^(int|float|string|bool)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Not a parameter declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string valueStr = match.Groups[3].Value.Trim();

            // Check for external data marker
            if (valueStr == "...")
            {
                ParameterType paramType = typeStr switch
                {
                    "int" => ParameterType.Integer,
                    "float" => ParameterType.Float,
                    "string" => ParameterType.String,
                    _ => ParameterType.Float
                };

                param = new Parameter(name, paramType, null, isExternal: true);
                return true;
            }

            // Parse the value
            try
            {
                object value;
                ParameterType paramType;

                switch (typeStr)
                {
                    case "int":
                        var intResult = evaluator.EvaluateIntExpression(valueStr);
                        if (!intResult.IsSuccess)
                        {
                            error = $"Invalid integer expression: {intResult.ErrorMessage}";
                            return true;
                        }
                        value = intResult.Value;
                        paramType = ParameterType.Integer;
                        break;

                    case "float":
                        var floatResult = evaluator.EvaluateFloatExpression(valueStr);
                        if (!floatResult.IsSuccess)
                        {
                            error = $"Invalid float expression: {floatResult.ErrorMessage}";
                            return true;
                        }
                        value = floatResult.Value;
                        paramType = ParameterType.Float;
                        break;

                    case "string":
                        if (!valueStr.StartsWith("\"") || !valueStr.EndsWith("\""))
                        {
                            error = "String value must be enclosed in double quotes";
                            return true;
                        }
                        value = valueStr.Substring(1, valueStr.Length - 2);
                        paramType = ParameterType.String;
                        break;

                    default:
                        return false;
                }

                param = new Parameter(name, paramType, value);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Error parsing parameter: {ex.Message}";
                return true;
            }
        }

        /// <summary>
        /// Tries to parse an indexed parameter declaration
        /// Supports both notations:
        ///   float a[I] = ...;         (simple notation)
        ///   float a[i in I] = ...;    (explicit iterator notation)
        /// </summary>
        public bool TryParseIndexedParameter(string statement, out Parameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            // Pattern 1: type name[var in Set] = ...;  (explicit iterator)
            var explicitPattern = @"^(int|float|string|bool)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)\]\s*=\s*(.+)$";
            var explicitMatch = Regex.Match(statement.Trim(), explicitPattern, RegexOptions.IgnoreCase);

            if (explicitMatch.Success)
            {
                return ParseExplicitIndexedParameter(explicitMatch, out param, out error);
            }

            // Pattern 2: type name[Set] = ...;  (simple notation)
            var simplePattern = @"^(int|float|string|bool)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[([a-zA-Z][a-zA-Z0-9_]*)\]\s*=\s*(.+)$";
            var simpleMatch = Regex.Match(statement.Trim(), simplePattern, RegexOptions.IgnoreCase);

            if (simpleMatch.Success)
            {
                return ParseSimpleIndexedParameter(simpleMatch, out param, out error);
            }

            error = "Not an indexed parameter declaration";
            return false;
        }

        /// <summary>
        /// Parses explicit iterator notation: float a[i in I] = ...;
        /// </summary>
        private bool ParseExplicitIndexedParameter(Match match, out Parameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            string typeStr = match.Groups[1].Value.ToLower();
            string paramName = match.Groups[2].Value;
            string iteratorVar = match.Groups[3].Value;  // Not used, but validates syntax
            string indexSetName = match.Groups[4].Value;
            string valueStr = match.Groups[5].Value.Trim();

            // Validate index set exists
            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not defined";
                return false;
            }

            ParameterType paramType = typeStr switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                "bool" => ParameterType.Boolean,
                _ => ParameterType.Float
            };

            bool isExternal = valueStr == "...";

            param = new Parameter(paramName, paramType, indexSetName, isExternal);

            if (!isExternal)
            {
                // TODO: Parse inline values like {10, 20, 30}
                error = "Inline indexed parameter values not yet supported";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses simple notation: float a[I] = ...;
        /// </summary>
        private bool ParseSimpleIndexedParameter(Match match, out Parameter? param, out string error)
        {
            param = null;
            error = string.Empty;

            string typeStr = match.Groups[1].Value.ToLower();
            string paramName = match.Groups[2].Value;
            string indexSetName = match.Groups[3].Value;
            string valueStr = match.Groups[4].Value.Trim();

            // Validate index set exists
            if (!modelManager.IndexSets.ContainsKey(indexSetName))
            {
                error = $"Index set '{indexSetName}' is not defined";
                return false;
            }

            ParameterType paramType = typeStr switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                "bool" => ParameterType.Boolean,
                _ => ParameterType.Float
            };

            bool isExternal = valueStr == "...";

            param = new Parameter(paramName, paramType, indexSetName, isExternal);

            if (!isExternal)
            {
                // Parse inline values like {10, 20, 30}
                if (valueStr.StartsWith("{") && valueStr.EndsWith("}"))
                {
                    string valuesStr = valueStr.Substring(1, valueStr.Length - 2);
                    return ParseInlineIndexedValues(valuesStr, param, out error);
                }
                else if (valueStr.StartsWith("[") && valueStr.EndsWith("]"))
                {
                    // Also support array notation: [10, 20, 30]
                    string valuesStr = valueStr.Substring(1, valueStr.Length - 2);
                    return ParseInlineIndexedValues(valuesStr, param, out error);
                }
                else
                {
                    error = "Indexed parameter values must be in {...} or [...] format";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses inline indexed values: {10, 20, 30} or [10, 20, 30]
        /// </summary>
        private bool ParseInlineIndexedValues(string valuesStr, Parameter param, out string error)
        {
            error = string.Empty;

            var values = valuesStr.Split(',').Select(v => v.Trim()).ToList();
            var indexSet = modelManager.IndexSets[param.IndexSetName!];
            var indices = indexSet.GetIndices().ToList();

            if (values.Count != indices.Count)
            {
                error = $"Expected {indices.Count} values for index set '{param.IndexSetName}', but got {values.Count}";
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                int index = indices[i];
                string valueStr = values[i];

                object parsedValue;
                switch (param.Type)
                {
                    case ParameterType.Integer:
                        if (!int.TryParse(valueStr, out int intVal))
                        {
                            error = $"Invalid integer value: '{valueStr}'";
                            return false;
                        }
                        parsedValue = intVal;
                        break;

                    case ParameterType.Float:
                        if (!double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double floatVal))
                        {
                            error = $"Invalid float value: '{valueStr}'";
                            return false;
                        }
                        parsedValue = floatVal;
                        break;

                    case ParameterType.String:
                        parsedValue = valueStr.Trim('"');
                        break;

                    case ParameterType.Boolean:
                        if (!bool.TryParse(valueStr, out bool boolVal))
                        {
                            error = $"Invalid boolean value: '{valueStr}'";
                            return false;
                        }
                        parsedValue = boolVal;
                        break;

                    default:
                        error = $"Unsupported parameter type: {param.Type}";
                        return false;
                }

                param.SetIndexedValue(index, parsedValue);
            }

            return true;
        }
    }
}