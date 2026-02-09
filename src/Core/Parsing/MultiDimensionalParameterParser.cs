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

            // Pattern: type paramName[indexSet1][indexSet2] = ...;
            // Must match BOTH brackets to be 2D
            string pattern = @"^\s*(int|float|string|bool)\s+([a-zA-Z][a-zA-Z0-9_]*)\s*\[([a-zA-Z][a-zA-Z0-9_]*)\]\s*\[([a-zA-Z][a-zA-Z0-9_]*)\]\s*=\s*(.+)$";

            var match = Regex.Match(statement.Trim(), pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                error = "Not a multi-dimensional parameter declaration";
                return false;
            }

            string typeStr = match.Groups[1].Value.ToLower();
            string paramName = match.Groups[2].Value;
            string indexSet1 = match.Groups[3].Value;  // First dimension (I)
            string indexSet2 = match.Groups[4].Value;  // Second dimension (J)
            string valueStr = match.Groups[5].Value.Trim();

            // Validate index sets exist
            if (!modelManager.IndexSets.ContainsKey(indexSet1) && 
                !modelManager.Ranges.ContainsKey(indexSet1))
            {
                error = $"Index set '{indexSet1}' is not defined";
                return false;
            }

            if (!modelManager.IndexSets.ContainsKey(indexSet2) && 
                !modelManager.Ranges.ContainsKey(indexSet2))
            {
                error = $"Index set '{indexSet2}' is not defined";
                return false;
            }

            // Convert index sets to ranges if needed
            if (!modelManager.IndexSets.ContainsKey(indexSet1))
            {
                var range = modelManager.Ranges[indexSet1];
                var indexSetObj = new IndexSet(indexSet1, range.GetStart(modelManager), range.GetEnd(modelManager));
                modelManager.AddIndexSet(indexSetObj);
            }

            if (!modelManager.IndexSets.ContainsKey(indexSet2))
            {
                var range = modelManager.Ranges[indexSet2];
                var indexSetObj = new IndexSet(indexSet2, range.GetStart(modelManager), range.GetEnd(modelManager));
                modelManager.AddIndexSet(indexSetObj);
            }

            // Determine parameter type
            ParameterType paramType = typeStr switch
            {
                "int" => ParameterType.Integer,
                "float" => ParameterType.Float,
                "string" => ParameterType.String,
                "bool" => ParameterType.Boolean,
                _ => ParameterType.Float
            };

            // Check if external
            bool isExternal = valueStr.Trim() == "...";

            // Create 2D parameter
            parameter = new Parameter(paramName, paramType, [indexSet1, indexSet2], isExternal);

            // If inline data provided, parse it
            if (!isExternal)
            {
                // TODO: Parse inline 2D data if needed
                error = "Inline 2D parameter data not yet supported. Use '...' for external data";
                return false;
            }

            return true;
        }
    }
}