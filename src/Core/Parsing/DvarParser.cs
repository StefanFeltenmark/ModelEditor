using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses dvar (decision variable) declarations from OPL syntax
    /// Supports: dvar float+ x[I]; dvar float x[I,J] in 0..10; etc.
    /// </summary>
    public class DvarParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public DvarParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        /// <summary>
        /// Attempts to parse a dvar declaration
        /// </summary>
        public bool TryParse(string statement, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            statement = statement.Trim();

            if (!statement.StartsWith("dvar ", StringComparison.OrdinalIgnoreCase))
            {
                error = "Not a dvar declaration";
                return false;
            }

            // Patterns to match:
            // dvar float+ x;                           (scalar, non-negative)
            // dvar float x[I];                         (1D)
            // dvar float+ x[I];                        (1D, non-negative)
            // dvar float x[I,J];                       (2D)
            // dvar float x[I] in 0..100;               (1D with bounds)
            // dvar float+ x[I] in 0..maxVal;           (1D, non-negative with upper bound)

            // Full pattern: dvar type[+|-] name [indexing] [in bounds];
            var pattern = @"^dvar\s+(float|int|bool)\s*([+\-])?\s+([a-zA-Z][a-zA-Z0-9_]*)(?:\s*\[([^\]]+)\])?(?:\s+in\s+(.+))?$";
            var match = Regex.Match(statement, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Invalid dvar syntax";
                return false;
            }

            // Extract components
            string typeStr = match.Groups[1].Value.ToLower();
            string signConstraint = match.Groups[2].Value; // +, -, or empty
            string varName = match.Groups[3].Value;
            string indexingPart = match.Groups[4].Value; // May be empty (scalar)
            string boundsExpr = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null;

            // Determine variable type
            VariableType varType = typeStr switch
            {
                "float" => VariableType.Float,
                "int" => VariableType.Integer,
                "bool" => VariableType.Boolean,
                _ => VariableType.Float
            };

            // Parse bounds
            double? lowerBound = null;
            double? upperBound = null;

            if (!string.IsNullOrEmpty(signConstraint))
            {
                // + means >= 0, - means <= 0
                if (signConstraint == "+")
                {
                    lowerBound = 0.0;
                }
                else if (signConstraint == "-")
                {
                    upperBound = 0.0;
                }
            }

            if (!string.IsNullOrEmpty(boundsExpr))
            {
                if (!ParseBounds(boundsExpr, out var lb, out var ub, out error))
                {
                    return false;
                }

                // Explicit bounds override sign constraints for lower bound
                if (lb.HasValue)
                    lowerBound = lb.Value;
                if (ub.HasValue)
                    upperBound = ub.Value;
            }

            // Parse indexing
            if (string.IsNullOrEmpty(indexingPart))
            {
                // Scalar variable
                variable = new IndexedVariable(varName, null, varType, null)
                {
                    LowerBound = lowerBound,
                    UpperBound = upperBound
                };
                return true;
            }

            // Parse index sets (may be comma-separated for multi-dimensional)
            var indexSets = ParseIndexSets(indexingPart);

            if (indexSets.Count == 0)
            {
                error = "No valid index sets found";
                return false;
            }

            // Validate all index sets exist
            foreach (var setName in indexSets)
            {
                if (!modelManager.IndexSets.ContainsKey(setName) && 
                    !modelManager.Ranges.ContainsKey(setName))
                {
                    error = $"Index set '{setName}' not found";
                    return false;
                }
            }

            // Create variable based on dimensionality
            if (indexSets.Count == 1)
            {
                // 1D
                variable = new IndexedVariable(varName, indexSets[0], varType, null)
                {
                    LowerBound = lowerBound,
                    UpperBound = upperBound
                };
            }
            else if (indexSets.Count == 2)
            {
                // 2D
                variable = new IndexedVariable(varName, indexSets[0], varType, indexSets[1])
                {
                    LowerBound = lowerBound,
                    UpperBound = upperBound
                };
            }
            else
            {
                // Multi-dimensional (3D+) - would need IndexedVariable enhancement
                error = $"Variables with {indexSets.Count} dimensions not yet supported. Maximum is 2.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses index sets from indexing part: "I" or "I,J" or "i in I, j in J"
        /// </summary>
        private List<string> ParseIndexSets(string indexingPart)
        {
            var indexSets = new List<string>();

            // Two patterns:
            // 1. Simple: "I" or "I,J"
            // 2. OPL-style: "i in I" or "i in I, j in J"

            // Check for "in" keyword
            if (indexingPart.Contains(" in "))
            {
                // OPL-style: extract set names after "in"
                var pattern = @"[a-zA-Z][a-zA-Z0-9_]*\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)";
                var matches = Regex.Matches(indexingPart, pattern);

                foreach (Match match in matches)
                {
                    indexSets.Add(match.Groups[1].Value);
                }
            }
            else
            {
                // Simple comma-separated list
                var parts = indexingPart.Split(',');
                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        indexSets.Add(trimmed);
                    }
                }
            }

            return indexSets;
        }

        /// <summary>
        /// Parses bounds expression: "0..100", "minVal..maxVal", "..100", "0.."
        /// </summary>
        private bool ParseBounds(string boundsExpr, out double? lowerBound, out double? upperBound, out string error)
        {
            lowerBound = null;
            upperBound = null;
            error = string.Empty;

            boundsExpr = boundsExpr.Trim();

            // Pattern: lowerBound..upperBound
            if (!boundsExpr.Contains(".."))
            {
                error = "Bounds must be in format 'lower..upper'";
                return false;
            }

            var parts = boundsExpr.Split(new[] { ".." }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                error = "Invalid bounds format";
                return false;
            }

            string lowerStr = parts[0].Trim();
            string upperStr = parts[1].Trim();

            // Parse lower bound (may be empty for "..upper")
            if (!string.IsNullOrEmpty(lowerStr))
            {
                if (double.TryParse(lowerStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double lower))
                {
                    lowerBound = lower;
                }
                else if (modelManager.Parameters.TryGetValue(lowerStr, out var lowerParam))
                {
                    // It's a parameter
                    if (lowerParam.Value != null)
                    {
                        lowerBound = Convert.ToDouble(lowerParam.Value);
                    }
                }
                else
                {
                    error = $"Invalid lower bound: '{lowerStr}'";
                    return false;
                }
            }

            // Parse upper bound (may be empty for "lower..")
            if (!string.IsNullOrEmpty(upperStr))
            {
                if (double.TryParse(upperStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double upper))
                {
                    upperBound = upper;
                }
                else if (modelManager.Parameters.TryGetValue(upperStr, out var upperParam))
                {
                    // It's a parameter
                    if (upperParam.Value != null)
                    {
                        upperBound = Convert.ToDouble(upperParam.Value);
                    }
                }
                else
                {
                    error = $"Invalid upper bound: '{upperStr}'";
                    return false;
                }
            }

            return true;
        }
    }
}