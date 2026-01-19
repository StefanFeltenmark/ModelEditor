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

        public bool TryParse(string statement, out Parameter? parameter, out string error)
        {
            parameter = null;
            error = string.Empty;

            // Try two-dimensional indexed parameter: type name[IndexSet1,IndexSet2] = ...
            if (TryParseTwoDimensionalParameter(statement, out parameter, out error))
            {
                return parameter != null || !string.IsNullOrEmpty(error);
            }

            // Try single-dimensional indexed parameter: type name[IndexSet] = ...
            if (TryParseSingleDimensionalParameter(statement, out parameter, out error))
            {
                return parameter != null || !string.IsNullOrEmpty(error);
            }

            // Try scalar parameter: type name = value
            return TryParseScalarParameter(statement, out parameter, out error);
        }

        private bool TryParseTwoDimensionalParameter(string statement, out Parameter? parameter, out string error)
        {
            parameter = null;
            error = string.Empty;

            string pattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*([a-zA-Z][a-zA-Z0-9_]*)\s*\]\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
                return false;

            string typeStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string indexSetName1 = match.Groups[3].Value;
            string indexSetName2 = match.Groups[4].Value;
            string valueStr = match.Groups[5].Value.Trim();

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
                error = $"First index set '{indexSetName1}' is not declared";
                return true;
            }

            if (!modelManager.IndexSets.ContainsKey(indexSetName2))
            {
                error = $"Second index set '{indexSetName2}' is not declared";
                return true;
            }

            // Check for external data marker
            if (valueStr == "...")
            {
                parameter = new Parameter(name, paramType, indexSetName1, indexSetName2, isExternal: true);
                return true;
            }

            // For now, we don't support inline array initialization
            error = "Inline initialization of 2D arrays not supported. Use '...' for external data.";
            return true;
        }

        private bool TryParseSingleDimensionalParameter(string statement, out Parameter? parameter, out string error)
        {
            parameter = null;
            error = string.Empty;

            string pattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[\s*([a-zA-Z][a-zA-Z0-9_]*)\s*]\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
                return false;

            string typeStr = match.Groups[1].Value.ToLower();
            string name = match.Groups[2].Value;
            string indexSetName = match.Groups[3].Value;
            string valueStr = match.Groups[4].Value.Trim();

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
                error = $"Index set '{indexSetName}' is not declared";
                return true;
            }

            // Check for external data marker
            if (valueStr == "...")
            {
                parameter = new Parameter(name, paramType, indexSetName, isExternal: true);
                return true;
            }

            // For now, we don't support inline array initialization
            error = "Inline initialization of indexed arrays not supported. Use '...' for external data.";
            return true;
        }

        private bool TryParseScalarParameter(string statement, out Parameter? parameter, out string error)
        {
            parameter = null;
            error = string.Empty;

            string pattern = @"^\s*(int|float|string)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(.+)$";
            var match = Regex.Match(statement.Trim(), pattern);

            if (!match.Success)
                return false;

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

                parameter = new Parameter(name, paramType, null, isExternal: true);
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

                parameter = new Parameter(name, paramType, value, isExternal: false);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Error parsing parameter: {ex.Message}";
                return true;
            }
        }
    }
}