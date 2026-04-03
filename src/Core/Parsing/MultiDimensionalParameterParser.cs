using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses multi-dimensional parameter declarations (2D, 3D, N-D)
    /// Examples:
    ///   ArcTData arcT[s in HydroArcs][t in T] = item(HydroArcTs, &lt;s.id,t&gt;);
    ///   float productionCost[stations][T][priceSegment] = ...;
    ///   float data[I][J] = ...;
    ///   HydroNode reservoirNode[j in reservoirs] = item(HydroNodes, &lt;j.HydroNodeindex&gt;);
    /// </summary>
    public class MultiDimensionalParameterParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public MultiDimensionalParameterParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        public bool TryParse(string statement, out Parameter? parameter, out string error)
        {
            parameter = null;
            error = string.Empty;

            // Match declarations with one or more bracket groups:
            //   type name[...] = value          (1D with primitive type)
            //   type name[...][...] = value     (2D+)
            // where type is a primitive type OR a tuple schema name
            var match = Regex.Match(statement.Trim(),
                @"^\s*([a-zA-Z][a-zA-Z0-9_]*)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*((?:\[[^\]]+\])+)\s*=\s*(.+)$");

            if (!match.Success)
            {
                error = "Not a multi-dimensional parameter declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value;
            string paramName = match.Groups[2].Value;
            string bracketsStr = match.Groups[3].Value;
            string valueStr = match.Groups[4].Value.Trim();

            // Determine if this is a primitive type or a tuple schema type
            bool isPrimitive = IsPrimitiveType(typeStr);
            bool isTupleSchema = modelManager.TupleSchemas.ContainsKey(typeStr);

            if (!isPrimitive && !isTupleSchema)
            {
                error = "Not a multi-dimensional parameter declaration";
                return false;
            }

            // Extract all bracket contents
            var bracketMatches = Regex.Matches(bracketsStr, @"\[([^\]]+)\]");

            // For primitive types, require at least 2 dimensions (1D is handled by ParameterParser)
            if (isPrimitive && bracketMatches.Count < 2)
            {
                error = "Not a multi-dimensional parameter declaration";
                return false;
            }

            var indexSetNames = new List<string>();
            foreach (Match bm in bracketMatches)
            {
                string content = bm.Groups[1].Value.Trim();

                // Check for comma-separated dimensions inside a single bracket pair: [I,J]
                // But not if it contains "in" (that's an iterator like [i in I, j in J])
                if (content.Contains(',') && !Regex.IsMatch(content, @"\bin\b"))
                {
                    var parts = content.Split(',');
                    foreach (var part in parts)
                    {
                        string trimmed = part.Trim();
                        if (Regex.IsMatch(trimmed, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                        {
                            indexSetNames.Add(trimmed);
                        }
                        else
                        {
                            error = $"Invalid dimension: '{trimmed}'";
                            return false;
                        }
                    }
                }
                // Check for iterator syntax: "s in HydroArcs" or "t in 1..5"
                else if (Regex.Match(content, @"^[a-zA-Z][a-zA-Z0-9_]*\s+in\s+(.+)$") is { Success: true } iterMatch)
                {
                    indexSetNames.Add(iterMatch.Groups[1].Value.Trim());
                }
                else
                {
                    indexSetNames.Add(content);
                }
            }

            // Validate index sets — accept tuple sets, primitive sets, index sets, ranges
            foreach (var setName in indexSetNames)
            {
                if (!IsKnownSet(setName))
                {
                    error = $"Index set '{setName}' is not defined";
                    return false;
                }

                EnsureIndexSet(setName);
            }

            ParameterType paramType = typeStr.ToLower() switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                "bool" => ParameterType.Boolean,
                _ => ParameterType.Float
            };

            bool isExternal = valueStr == "...";

            parameter = new Parameter(paramName, paramType, indexSetNames, isExternal);

            if (!isExternal && indexSetNames.Count == 2)
            {
                // Attempt to parse inline 2-D matrix: [[v00, v01], [v10, v11], ...]
                if ((valueStr.StartsWith("[[") && valueStr.EndsWith("]]")) ||
                    (valueStr.StartsWith("{") && valueStr.EndsWith("}")))
                {
                    string inner = valueStr.TrimStart('{', '[').TrimEnd('}', ']').Trim();
                    if (!ParseInlineMatrix(parameter, inner, paramType, indexSetNames, out error))
                        return false;
                }
            }

            return true;
        }

        private bool ParseInlineMatrix(Parameter param, string matrixStr, ParameterType type,
            List<string> indexSetNames, out string error)
        {
            error = string.Empty;
            if (!modelManager.IndexSets.TryGetValue(indexSetNames[0], out var set1) ||
                !modelManager.IndexSets.TryGetValue(indexSetNames[1], out var set2))
            {
                error = "Index set not found for inline matrix";
                return false;
            }

            var rows1 = set1.GetIndices().ToList();
            var rows2 = set2.GetIndices().ToList();

            // Split rows: each row is enclosed in [...]
            var rows = new List<string>();
            int depth = 0;
            var current = new System.Text.StringBuilder();
            foreach (char c in matrixStr)
            {
                if (c == '[') { if (depth++ > 0) current.Append(c); }
                else if (c == ']') { if (--depth == 0) { rows.Add(current.ToString()); current.Clear(); } else current.Append(c); }
                else if (depth > 0) current.Append(c);
            }

            if (rows.Count == 0)
            {
                error = "Empty inline matrix";
                return false;
            }
            if (rows.Count != rows1.Count)
            {
                error = $"Inline matrix has {rows.Count} rows but index set '{indexSetNames[0]}' has {rows1.Count} elements";
                return false;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var cells = rows[i].Split(',').Select(v => v.Trim()).ToList();
                if (cells.Count != rows2.Count)
                {
                    error = $"Row {i + 1} has {cells.Count} values but index set '{indexSetNames[1]}' has {rows2.Count} elements";
                    return false;
                }
                for (int j = 0; j < cells.Count; j++)
                {
                    object? val = ParseCell(cells[j], type, out error);
                    if (val == null) return false;
                    param.SetIndexedValue(rows1[i], rows2[j], val);
                }
            }
            return true;
        }

        private static object? ParseCell(string cell, ParameterType type, out string error)
        {
            error = string.Empty;
            return type switch
            {
                ParameterType.Integer when int.TryParse(cell, out int iv) => iv,
                ParameterType.Float when double.TryParse(cell, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double dv) => dv,
                ParameterType.Boolean when bool.TryParse(cell, out bool bv) => bv,
                ParameterType.String => cell.Trim('"'),
                _ => (error = $"Cannot parse '{cell}' as {type}") is string ? null : null
            };
        }

        private bool IsKnownSet(string name)
        {
            return modelManager.IndexSets.ContainsKey(name) ||
                   modelManager.Ranges.ContainsKey(name) ||
                   modelManager.TupleSets.ContainsKey(name) ||
                   modelManager.PrimitiveSets.ContainsKey(name);
        }

        private void EnsureIndexSet(string name)
        {
            if (!modelManager.IndexSets.ContainsKey(name) &&
                modelManager.Ranges.ContainsKey(name))
            {
                var range = modelManager.Ranges[name];
                var indexSetObj = new IndexSet(name,
                    range.GetStart(modelManager), range.GetEnd(modelManager));
                modelManager.AddIndexSet(indexSetObj);
            }
        }

        private static bool IsPrimitiveType(string type)
        {
            return type.ToLower() is "int" or "float" or "string" or "bool";
        }
    }
}