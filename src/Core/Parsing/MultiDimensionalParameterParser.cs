    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses multi-dimensional parameter declarations
    /// Examples:
    ///   ArcTData arcT[s in HydroArcs][t in T] = item(HydroArcTs, <s.id,t>);
    ///   float productionCost[stations][T][priceSegment] = ...;
    ///   float data[I][J] = ...;
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

            statement = statement.Trim();

            // Pattern: Type name[indexing1][indexing2]...[indexingN] = value;
            // Indexing can be: [SetName] or [var in SetName]
            var pattern = @"^(float|int|string|bool|[A-Z][a-zA-Z0-9_]*)\s+([a-zA-Z][a-zA-Z0-9_]*)(\[[^\]]+\])+\s*=\s*(.+)$";
            var match = Regex.Match(statement, pattern);

            if (!match.Success)
            {
                error = "Not a multi-dimensional parameter";
                return false;
            }

            string typeStr = match.Groups[1].Value;
            string paramName = match.Groups[2].Value;
            string indexingPart = match.Groups[3].Value;
            string valueExpr = match.Groups[4].Value.Trim();

            // Determine parameter type
            ParameterType paramType = GetParameterType(typeStr, out bool isTupleType, out string? tupleTypeName);

            // Parse all index sets
            var indexSets = ParseIndexSets(indexingPart, out var indexVarNames, out error);
            if (indexSets == null)
                return false;

            // Validate all index sets exist
            foreach (var setName in indexSets)
            {
                if (!modelManager.IndexSets.ContainsKey(setName) &&
                    !modelManager.Ranges.ContainsKey(setName) &&
                    !modelManager.TupleSets.ContainsKey(setName))
                {
                    error = $"Index set '{setName}' not found";
                    return false;
                }
            }

            // Check if external or computed
            bool isExternal = valueExpr == "...";

            if (isExternal)
            {
                // External multi-dimensional parameter
                parameter = new Parameter(paramName, paramType, indexSets, isExternal: true);
                
                if (isTupleType)
                {
                    // Store tuple type information
                    parameter.Value = tupleTypeName; // Store schema name
                }
            }
            else
            {
                // Computed parameter (e.g., with item() function)
                if (!TryParseComputeExpression(valueExpr, indexVarNames, indexSets, out var computeExpr, out error))
                {
                    return false;
                }

                parameter = new Parameter(paramName, paramType, indexSets, computeExpr);
                
                if (isTupleType)
                {
                    parameter.Value = tupleTypeName;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses all [indexing] parts from a parameter declaration
        /// Returns list of index set names and list of iterator variable names
        /// </summary>
        private List<string>? ParseIndexSets(string indexingPart, out List<string?> iteratorVarNames, out string error)
        {
            error = string.Empty;
            iteratorVarNames = new List<string?>();
            var indexSets = new List<string>();

            // Extract all [...] parts
            var bracketPattern = @"\[([^\]]+)\]";
            var matches = Regex.Matches(indexingPart, bracketPattern);

            if (matches.Count == 0)
            {
                error = "No index sets found";
                return null;
            }

            foreach (Match match in matches)
            {
                string content = match.Groups[1].Value.Trim();

                // Two formats:
                // 1. Simple: "SetName"
                // 2. OPL-style: "var in SetName"

                if (content.Contains(" in "))
                {
                    // OPL-style: extract both var name and set name
                    var parts = content.Split(new[] { " in " }, StringSplitOptions.None);
                    if (parts.Length != 2)
                    {
                        error = $"Invalid index format: '{content}'";
                        return null;
                    }

                    string varName = parts[0].Trim();
                    string setName = parts[1].Trim();

                    iteratorVarNames.Add(varName);
                    indexSets.Add(setName);
                }
                else
                {
                    // Simple format: just set name
                    iteratorVarNames.Add(null); // No explicit variable name
                    indexSets.Add(content);
                }
            }

            return indexSets;
        }

        /// <summary>
        /// Determines parameter type from string
        /// </summary>
        private ParameterType GetParameterType(string typeStr, out bool isTupleType, out string? tupleTypeName)
        {
            isTupleType = false;
            tupleTypeName = null;

            switch (typeStr.ToLower())
            {
                case "float":
                    return ParameterType.Float;
                case "int":
                    return ParameterType.Integer;
                case "string":
                    return ParameterType.String;
                case "bool":
                    return ParameterType.Boolean;
                default:
                    // Must be a tuple type
                    if (modelManager.TupleSchemas.ContainsKey(typeStr))
                    {
                        isTupleType = true;
                        tupleTypeName = typeStr;
                        return ParameterType.String; // Use string to store tuple reference
                    }
                    return ParameterType.Float; // Default
            }
        }

        /// <summary>
        /// Parses the compute expression (e.g., item(...))
        /// </summary>
        private bool TryParseComputeExpression(string exprStr, List<string?> iteratorVarNames, 
            List<string> indexSets, out Expression? expression, out string error)
        {
            expression = null;
            error = string.Empty;

            // For now, we primarily support item() expressions
            // Example: item(HydroArcTs, <s.id,t>)

            if (!exprStr.Contains("item("))
            {
                error = "Only item() expressions are currently supported for computed multi-dimensional parameters";
                return false;
            }

            // Parse the item() expression
            if (!ItemFunctionParser.TryParse(exprStr, modelManager, out expression, out error))
            {
                return false;
            }

            // Wrap in a multi-dimensional context expression that knows about the iterators
            expression = new MultiDimParameterExpression(
                expression,
                iteratorVarNames.Select((name, idx) => (name, indexSets[idx])).ToList()
            );

            return true;
        }
    }
}