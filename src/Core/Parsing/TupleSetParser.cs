using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing
{
    /// <summary>
    /// Parses tuple set declarations
    /// Examples:
    ///   tupleset Routes = {(1,2), (1,3), (2,3)};
    ///   tupleset Arcs = {(i,j) | i in I, j in J, i < j};
    ///   tupleset Connections = ...;
    /// </summary>
    public class TupleSetParser
    {
        private readonly ModelManager modelManager;
        private readonly ExpressionEvaluator evaluator;

        public TupleSetParser(ModelManager manager, ExpressionEvaluator eval)
        {
            modelManager = manager;
            evaluator = eval;
        }

        public bool TryParse(string statement, out TupleSet? tupleSet, out string error)
        {
            tupleSet = null;
            error = string.Empty;

            // Pattern for external tuple set: tupleset Name = ...;
            string externalPattern = @"^\s*tupleset\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*\.\.\.\s*$";
            var externalMatch = Regex.Match(statement.Trim(), externalPattern);
            
            if (externalMatch.Success)
            {
                string name = externalMatch.Groups[1].Value;
                // For external tuple sets, dimension will be determined when data is loaded
                tupleSet = new TupleSet(name, 2, true); // Default to 2D, can be updated
                return true;
            }

            // Pattern for explicit tuple list: tupleset Name = {(1,2), (3,4), ...};
            string explicitPattern = @"^\s*tupleset\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*\{(.+)\}\s*$";
            var explicitMatch = Regex.Match(statement.Trim(), explicitPattern);
            
            if (explicitMatch.Success)
            {
                string name = explicitMatch.Groups[1].Value;
                string tuplesStr = explicitMatch.Groups[2].Value.Trim();
                
                return ParseExplicitTuples(name, tuplesStr, out tupleSet, out error);
            }

            // Pattern for computed tuple set: tupleset Name = {(i,j) | i in I, j in J, condition};
            string computedPattern = @"^\s*tupleset\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*\{([^|]+)\|(.+)\}\s*$";
            var computedMatch = Regex.Match(statement.Trim(), computedPattern);
            
            if (computedMatch.Success)
            {
                string name = computedMatch.Groups[1].Value;
                string tupleVars = computedMatch.Groups[2].Value.Trim();
                string conditions = computedMatch.Groups[3].Value.Trim();
                
                return ParseComputedTuples(name, tupleVars, conditions, out tupleSet, out error);
            }

            error = "Not a tuple set declaration";
            return false;
        }

        private bool ParseExplicitTuples(string name, string tuplesStr, out TupleSet? tupleSet, out string error)
        {
            tupleSet = null;
            error = string.Empty;

            // Split by comma, but respect parentheses
            var tuples = SplitTuples(tuplesStr);
            
            if (tuples.Count == 0)
            {
                error = "Empty tuple set";
                return false;
            }

            // Parse first tuple to determine dimension
            var firstTuple = ParseSingleTuple(tuples[0], out error);
            if (firstTuple == null)
            {
                return false;
            }

            int dimension = firstTuple.Length;
            
            if (dimension == 2)
            {
                var tupleList = new List<Tuple<int, int>>();
                
                foreach (var tupleStr in tuples)
                {
                    var values = ParseSingleTuple(tupleStr, out error);
                    if (values == null || values.Length != 2)
                    {
                        error = $"Invalid 2D tuple: {tupleStr}";
                        return false;
                    }
                    tupleList.Add(Tuple.Create(values[0], values[1]));
                }
                
                tupleSet = new TupleSet(name, tupleList);
                return true;
            }
            else if (dimension == 3)
            {
                var tupleList = new List<Tuple<int, int, int>>();
                
                foreach (var tupleStr in tuples)
                {
                    var values = ParseSingleTuple(tupleStr, out error);
                    if (values == null || values.Length != 3)
                    {
                        error = $"Invalid 3D tuple: {tupleStr}";
                        return false;
                    }
                    tupleList.Add(Tuple.Create(values[0], values[1], values[2]));
                }
                
                tupleSet = new TupleSet(name, tupleList);
                return true;
            }
            else
            {
                error = $"Unsupported tuple dimension: {dimension}. Only 2D and 3D tuples are supported";
                return false;
            }
        }

        private List<string> SplitTuples(string tuplesStr)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            
            for (int i = 0; i < tuplesStr.Length; i++)
            {
                if (tuplesStr[i] == '(')
                    depth++;
                else if (tuplesStr[i] == ')')
                    depth--;
                else if (tuplesStr[i] == ',' && depth == 0)
                {
                    result.Add(tuplesStr.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            
            if (start < tuplesStr.Length)
            {
                result.Add(tuplesStr.Substring(start).Trim());
            }
            
            return result;
        }

        private int[]? ParseSingleTuple(string tupleStr, out string error)
        {
            error = string.Empty;
            
            // Match (val1, val2) or (val1, val2, val3)
            var match = Regex.Match(tupleStr.Trim(), @"^\((.+)\)$");
            if (!match.Success)
            {
                error = $"Invalid tuple format: {tupleStr}. Expected (val1,val2) or (val1,val2,val3)";
                return null;
            }
            
            string valuesStr = match.Groups[1].Value;
            var values = valuesStr.Split(',').Select(v => v.Trim()).ToArray();
            
            var intValues = new List<int>();
            foreach (var valueStr in values)
            {
                var result = evaluator.EvaluateIntExpression(valueStr);
                if (!result.IsSuccess)
                {
                    error = $"Invalid tuple value '{valueStr}': {result.ErrorMessage}";
                    return null;
                }
                intValues.Add(result.Value);
            }
            
            return intValues.ToArray();
        }

        private bool ParseComputedTuples(
            string name, 
            string tupleVars, 
            string conditions, 
            out TupleSet? tupleSet, 
            out string error)
        {
            tupleSet = null;
            error = string.Empty;

            // Parse tuple variables: (i,j) or (i,j,k)
            var tupleMatch = Regex.Match(tupleVars.Trim(), @"^\(([a-zA-Z][a-zA-Z0-9_]*),([a-zA-Z][a-zA-Z0-9_]*)(?:,([a-zA-Z][a-zA-Z0-9_]*))?\)$");
            if (!tupleMatch.Success)
            {
                error = $"Invalid tuple variable format: {tupleVars}. Expected (i,j) or (i,j,k)";
                return false;
            }

            string var1 = tupleMatch.Groups[1].Value;
            string var2 = tupleMatch.Groups[2].Value;
            string? var3 = tupleMatch.Groups[3].Success ? tupleMatch.Groups[3].Value : null;
            
            int dimension = var3 != null ? 3 : 2;

            // Parse conditions: "i in I, j in J, i < j"
            var conditionParts = conditions.Split(',').Select(c => c.Trim()).ToList();
            
            // Extract "in" clauses
            var inClauses = new Dictionary<string, string>();
            var filterConditions = new List<string>();
            
            foreach (var condition in conditionParts)
            {
                var inMatch = Regex.Match(condition, @"^([a-zA-Z][a-zA-Z0-9_]*)\s+in\s+([a-zA-Z][a-zA-Z0-9_]*)$");
                if (inMatch.Success)
                {
                    string varName = inMatch.Groups[1].Value;
                    string setName = inMatch.Groups[2].Value;
                    inClauses[varName] = setName;
                }
                else
                {
                    filterConditions.Add(condition);
                }
            }

            // Validate index sets exist
            if (!ValidateIndexSets(inClauses, out error))
            {
                return false;
            }

            // Generate tuples by iterating over index sets
            if (dimension == 2)
            {
                tupleSet = GenerateTwoDimensionalTuples(name, var1, var2, inClauses, filterConditions, out error);
            }
            else
            {
                tupleSet = GenerateThreeDimensionalTuples(name, var1, var2, var3!, inClauses, filterConditions, out error);
            }

            return tupleSet != null;
        }

        private bool ValidateIndexSets(Dictionary<string, string> inClauses, out string error)
        {
            error = string.Empty;
            
            foreach (var clause in inClauses)
            {
                if (!modelManager.IndexSets.ContainsKey(clause.Value))
                {
                    error = $"Index set '{clause.Value}' not found";
                    return false;
                }
            }
            
            return true;
        }

        private TupleSet? GenerateTwoDimensionalTuples(
            string name,
            string var1,
            string var2,
            Dictionary<string, string> inClauses,
            List<string> filterConditions,
            out string error)
        {
            error = string.Empty;
            
            if (!inClauses.ContainsKey(var1) || !inClauses.ContainsKey(var2))
            {
                error = $"Variables {var1} and {var2} must both have 'in' clauses";
                return null;
            }

            var set1 = modelManager.IndexSets[inClauses[var1]];
            var set2 = modelManager.IndexSets[inClauses[var2]];
            
            var tuples = new List<Tuple<int, int>>();

            foreach (int val1 in set1.GetIndices())
            {
                foreach (int val2 in set2.GetIndices())
                {
                    // Evaluate filter conditions
                    bool passesFilters = EvaluateFilters(filterConditions, 
                        new Dictionary<string, int> { { var1, val1 }, { var2, val2 } });
                    
                    if (passesFilters)
                    {
                        tuples.Add(Tuple.Create(val1, val2));
                    }
                }
            }

            return new TupleSet(name, tuples);
        }

        private TupleSet? GenerateThreeDimensionalTuples(
            string name,
            string var1,
            string var2,
            string var3,
            Dictionary<string, string> inClauses,
            List<string> filterConditions,
            out string error)
        {
            error = string.Empty;
            
            if (!inClauses.ContainsKey(var1) || !inClauses.ContainsKey(var2) || !inClauses.ContainsKey(var3))
            {
                error = $"Variables {var1}, {var2}, and {var3} must all have 'in' clauses";
                return null;
            }

            var set1 = modelManager.IndexSets[inClauses[var1]];
            var set2 = modelManager.IndexSets[inClauses[var2]];
            var set3 = modelManager.IndexSets[inClauses[var3]];
            
            var tuples = new List<Tuple<int, int, int>>();

            foreach (int val1 in set1.GetIndices())
            {
                foreach (int val2 in set2.GetIndices())
                {
                    foreach (int val3 in set3.GetIndices())
                    {
                        bool passesFilters = EvaluateFilters(filterConditions,
                            new Dictionary<string, int> { { var1, val1 }, { var2, val2 }, { var3, val3 } });
                        
                        if (passesFilters)
                        {
                            tuples.Add(Tuple.Create(val1, val2, val3));
                        }
                    }
                }
            }

            return new TupleSet(name, tuples);
        }

        private bool EvaluateFilters(List<string> filters, Dictionary<string, int> variables)
        {
            foreach (var filter in filters)
            {
                // Replace variables with values
                string expression = filter;
                foreach (var var in variables)
                {
                    expression = Regex.Replace(expression, @"\b" + var.Key + @"\b", var.Value.ToString());
                }

                // Evaluate the condition
                if (!EvaluateCondition(expression))
                {
                    return false;
                }
            }
            
            return true;
        }

        private bool EvaluateCondition(string condition)
        {
            // Handle comparison operators
            if (condition.Contains("<") || condition.Contains(">") || 
                condition.Contains("==") || condition.Contains("!="))
            {
                return evaluator.EvaluateBooleanExpression(condition);
            }
            
            // If no operator, try to evaluate as boolean
            var result = evaluator.EvaluateIntExpression(condition);
            return result.IsSuccess && result.Value != 0;
        }
    }
}