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

            return true;
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