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

            // Try multi-bracket pattern first: dvar float+ name[...][...]... [in bounds]
            var multiBracketPattern =
                @"^dvar\s+(float|int|bool)\s*([+\-])?\s+([a-zA-Z][a-zA-Z0-9_]*)\s*((?:\[[^\]]+\]){2,})(?:\s+in\s+(.+))?$";
            var multiBracketMatch = Regex.Match(statement, multiBracketPattern, RegexOptions.IgnoreCase);

            if (multiBracketMatch.Success)
            {
                return ParseMultiBracketDvar(multiBracketMatch, out variable, out error);
            }

            // Single bracket or scalar: dvar type[+|-] name [indexing] [in bounds]
            var pattern = @"^dvar\s+(float|int|bool)\s*([+\-])?\s+([a-zA-Z][a-zA-Z0-9_]*)(?:\s*\[([^\]]+)\])?(?:\s+in\s+(.+))?$";
            var match = Regex.Match(statement, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Invalid dvar syntax";
                return false;
            }

            string typeStr = match.Groups[1].Value.ToLower();
            string signConstraint = match.Groups[2].Value;
            string varName = match.Groups[3].Value;
            string indexingPart = match.Groups[4].Value;
            string boundsExpr = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null;

            VariableType varType = ParseVariableType(typeStr);
            ParseSignConstraint(signConstraint, out double? lowerBound, out double? upperBound);

            if (!string.IsNullOrEmpty(boundsExpr))
            {
                if (!ParseBounds(boundsExpr, out var lb, out var ub, out error))
                    return false;
                if (lb.HasValue) lowerBound = lb.Value;
                if (ub.HasValue) upperBound = ub.Value;
            }

            if (string.IsNullOrEmpty(indexingPart))
            {
                variable = new IndexedVariable(varName, null, varType, null)
                {
                    LowerBound = lowerBound,
                    UpperBound = upperBound
                };
                return true;
            }

            var indexSets = ParseIndexSets(indexingPart);
            if (indexSets.Count == 0)
            {
                error = "No valid index sets found";
                return false;
            }

            if (!ValidateIndexSets(indexSets, out error))
                return false;

            variable = CreateVariable(varName, varType, indexSets, lowerBound, upperBound);
            return true;
        }

        private bool ParseMultiBracketDvar(Match match, out IndexedVariable? variable, out string error)
        {
            variable = null;
            error = string.Empty;

            string typeStr = match.Groups[1].Value.ToLower();
            string signConstraint = match.Groups[2].Value;
            string varName = match.Groups[3].Value;
            string bracketsStr = match.Groups[4].Value;
            string boundsExpr = match.Groups[5].Success ? match.Groups[5].Value.Trim() : null;

            VariableType varType = ParseVariableType(typeStr);
            ParseSignConstraint(signConstraint, out double? lowerBound, out double? upperBound);

            if (!string.IsNullOrEmpty(boundsExpr))
            {
                // Bounds may contain complex expressions (tuple field access) — be lenient
                if (ParseBounds(boundsExpr, out var lb, out var ub, out _))
                {
                    if (lb.HasValue) lowerBound = lb.Value;
                    if (ub.HasValue) upperBound = ub.Value;
                }
            }

            var bracketMatches = Regex.Matches(bracketsStr, @"\[([^\]]+)\]");
            var indexSets = new List<string>();

            foreach (Match bm in bracketMatches)
            {
                string content = bm.Groups[1].Value.Trim();
                var iterMatch = Regex.Match(content, @"^[a-zA-Z][a-zA-Z0-9_]*\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)$");
                if (iterMatch.Success)
                {
                    indexSets.Add(iterMatch.Groups[1].Value.Trim());
                }
                else if (Regex.IsMatch(content, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                {
                    indexSets.Add(content);
                }
                else
                {
                    error = $"Invalid dvar index: '{content}'";
                    return false;
                }
            }

            if (!ValidateIndexSets(indexSets, out error))
                return false;

            variable = CreateVariable(varName, varType, indexSets, lowerBound, upperBound);
            return true;
        }

        private static VariableType ParseVariableType(string typeStr) => typeStr switch
        {
            "float" => VariableType.Float,
            "int" => VariableType.Integer,
            "bool" => VariableType.Boolean,
            _ => VariableType.Float
        };

        private static void ParseSignConstraint(string sign, out double? lb, out double? ub)
        {
            lb = null;
            ub = null;
            if (sign == "+") lb = 0.0;
            else if (sign == "-") ub = 0.0;
        }

        private bool ValidateIndexSets(List<string> indexSets, out string error)
        {
            error = string.Empty;
            foreach (var setName in indexSets)
            {
                if (!modelManager.IndexSets.ContainsKey(setName) &&
                    !modelManager.Ranges.ContainsKey(setName) &&
                    !modelManager.TupleSets.ContainsKey(setName) &&
                    !modelManager.PrimitiveSets.ContainsKey(setName) &&
                    !modelManager.TupleSchemas.ContainsKey(setName) &&
                    !modelManager.ComputedSets.ContainsKey(setName))
                {
                    error = $"Index set '{setName}' not found";
                    return false;
                }
            }
            return true;
        }

        private static IndexedVariable CreateVariable(string name, VariableType type,
            List<string> indexSets, double? lb, double? ub)
        {
            string firstSet = indexSets.Count > 0 ? indexSets[0] : null;
            string secondSet = indexSets.Count > 1 ? indexSets[1] : null;

            var variable = new IndexedVariable(name, firstSet, type, secondSet)
            {
                LowerBound = lb,
                UpperBound = ub
            };

            if (indexSets.Count > 2)
            {
                variable.AdditionalIndexSets = indexSets.Skip(2).ToList();
            }

            return variable;
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