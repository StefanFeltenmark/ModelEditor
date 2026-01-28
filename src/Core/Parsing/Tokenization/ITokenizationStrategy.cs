using System.Text.RegularExpressions;
using Core.Models;

namespace Core.Parsing.Tokenization
{
    /// <summary>
    /// Strategy interface for tokenizing different types of expressions
    /// </summary>
    public interface ITokenizationStrategy
    {
        /// <summary>
        /// Tokenizes the expression by replacing patterns with tokens
        /// </summary>
        /// <param name="expression">Expression to tokenize</param>
        /// <param name="tokenManager">Token manager for creating and storing tokens</param>
        /// <param name="modelManager">Model manager for validation</param>
        /// <returns>Tokenized expression</returns>
        string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager);
        
        /// <summary>
        /// Priority for tokenization (lower = processed first)
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Name of this tokenizer for debugging
        /// </summary>
        string Name { get; }
    }
    
    /// <summary>
    /// Tokenizes item() expressions: item(set, <key1, key2>).field
    /// </summary>
    public class ItemExpressionTokenizer : ITokenizationStrategy
    {
        public int Priority => 1; // Highest precedence
        public string Name => "ItemExpression";
        
        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            // Pattern: item(setName, <key1, key2, ...>).fieldName or just item(setName, <key1, key2>)
            string pattern = @"item\s*\(\s*([a-zA-Z][a-zA-Z0-9_]*)\s*,\s*<([^>]+)>\s*\)(?:\.([a-zA-Z][a-zA-Z0-9_]*))?";
            
            return Regex.Replace(expression, pattern, m =>
            {
                string setName = m.Groups[1].Value;
                string keyValuesStr = m.Groups[2].Value;
                string fieldName = m.Groups[3].Value; // May be empty
                
                // Parse key values
                var keyValues = ParseItemKeyValues(keyValuesStr, out string parseError);
                if (keyValues == null)
                {
                    throw new Exception($"Error parsing item() key values: {parseError}");
                }
                
                // Validate tuple set exists
                if (!modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
                {
                    throw new Exception($"Tuple set '{setName}' not found");
                }
                
                // Create item expression
                var itemExpr = new ItemExpression(setName, keyValues);
                
                Expression resultExpr;
                if (!string.IsNullOrEmpty(fieldName))
                {
                    // Field access: item(...).field
                    resultExpr = new ItemFieldAccessExpression(itemExpr, fieldName);
                }
                else
                {
                    // Just item(...) without field access
                    resultExpr = itemExpr;
                }
                
                return tokenManager.CreateToken(resultExpr, "ITEM");
            });
        }
        
        private List<object>? ParseItemKeyValues(string keyValuesStr, out string error)
        {
            error = string.Empty;
            var keyValues = new List<object>();
            
            var values = ParsingUtilities.SplitByCommaRespectingQuotes(keyValuesStr);
            
            foreach (var valueStr in values)
            {
                string trimmed = valueStr.Trim();
                
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                
                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                {
                    keyValues.Add(trimmed.Substring(1, trimmed.Length - 2));
                }
                else if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    keyValues.Add(bool.Parse(trimmed));
                }
                else if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer,
                             System.Globalization.CultureInfo.InvariantCulture, out int intVal))
                {
                    keyValues.Add(intVal);
                }
                else if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                {
                    keyValues.Add(doubleVal);
                }
                else
                {
                    error = $"Cannot parse key value: '{trimmed}'";
                    return null;
                }
            }
            
            if (keyValues.Count == 0)
            {
                error = "No key values found";
                return null;
            }
            
            return keyValues;
        }
    }
    
    /// <summary>
    /// Tokenizes tuple field access: tupleSet[index].fieldName
    /// </summary>
    public class TupleFieldAccessTokenizer : ITokenizationStrategy
    {
        public int Priority => 2;
        public string Name => "TupleFieldAccess";
        
        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            // Pattern: tupleSet[index].fieldName
            string pattern = @"([a-zA-Z][a-zA-Z0-9_]*)\[(\d+)\]\.([a-zA-Z][a-zA-Z0-9_]*)";
            
            return Regex.Replace(expression, pattern, m =>
            {
                string setName = m.Groups[1].Value;
                if (!int.TryParse(m.Groups[2].Value, out int index))
                {
                    return m.Value;
                }
                string fieldName = m.Groups[3].Value;
                
                // Validate tuple set exists
                if (modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
                {
                    // Validate field exists in schema
                    if (modelManager.TupleSchemas.TryGetValue(tupleSet.SchemaName, out var schema))
                    {
                        if (!schema.Fields.ContainsKey(fieldName))
                        {
                            throw new Exception($"Field '{fieldName}' not found in tuple schema '{schema.Name}'");
                        }
                    }
                    
                    var tupleExpr = new TupleFieldAccessExpression(setName, index, fieldName);
                    return tokenManager.CreateToken(tupleExpr, "TUPLE");
                }
                
                return m.Value;
            });
        }
    }
    
    /// <summary>
    /// Tokenizes two-dimensional indexed parameters and variables: x[i,j]
    /// </summary>
    public class TwoDimensionalIndexTokenizer : ITokenizationStrategy
    {
        public int Priority => 3;
        public string Name => "TwoDimensionalIndex";
        
        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            string pattern = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+),([a-zA-Z0-9]+)\]";
            
            return Regex.Replace(expression, pattern, m =>
            {
                string name = m.Groups[1].Value;
                string index1Str = m.Groups[2].Value;
                string index2Str = m.Groups[3].Value;
                
                if (int.TryParse(index1Str, out int idx1) &&
                    int.TryParse(index2Str, out int idx2))
                {
                    // Check if it's a parameter
                    if (modelManager.Parameters.TryGetValue(name, out var param))
                    {
                        if (param.IsTwoDimensional)
                        {
                            var paramExpr = new IndexedParameterExpression(name, idx1, idx2);
                            return tokenManager.CreateToken(paramExpr, "PARAM");
                        }
                    }
                    
                    // Check if it's a variable
                    if (modelManager.IndexedVariables.TryGetValue(name, out var indexedVar))
                    {
                        if (indexedVar.IsTwoDimensional)
                        {
                            ValidateIndices(name, indexedVar, idx1, idx2, modelManager);
                            return $"{name}{idx1}_{idx2}";
                        }
                    }
                }
                else
                {
                    // Non-numeric indices
                    return $"{name}_idx_{index1Str}_{index2Str}";
                }
                
                return m.Value;
            });
        }
        
        private void ValidateIndices(string name, IndexedVariable var, int idx1, int idx2, ModelManager modelManager)
        {
            var indexSet1 = modelManager.IndexSets[var.IndexSetName];
            var indexSet2 = modelManager.IndexSets[var.SecondIndexSetName!];
            
            if (!indexSet1.Contains(idx1))
                throw new Exception($"First index {idx1} is out of range for variable {name}");
            
            if (!indexSet2.Contains(idx2))
                throw new Exception($"Second index {idx2} is out of range for variable {name}");
        }
    }
    
    /// <summary>
    /// Tokenizes single-dimensional indexed parameters and variables: x[i]
    /// </summary>
    public class SingleDimensionalIndexTokenizer : ITokenizationStrategy
    {
        public int Priority => 4;
        public string Name => "SingleDimensionalIndex";
        
        public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
        {
            string pattern = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z0-9]+)\]";
            
            return Regex.Replace(expression, pattern, m =>
            {
                string name = m.Groups[1].Value;
                string indexStr = m.Groups[2].Value;
                
                if (int.TryParse(indexStr, out int idx))
                {
                    // Check if it's a parameter
                    if (modelManager.Parameters.TryGetValue(name, out var param))
                    {
                        if (param.IsIndexed && !param.IsTwoDimensional)
                        {
                            ValidateParameterIndex(name, param, idx, modelManager);
                            var paramExpr = new IndexedParameterExpression(name, idx);
                            return tokenManager.CreateToken(paramExpr, "PARAM");
                        }
                    }
                    
                    // Check if it's a variable
                    if (modelManager.IndexedVariables.TryGetValue(name, out var indexedVar))
                    {
                        if (!indexedVar.IsScalar && !indexedVar.IsTwoDimensional)
                        {
                            ValidateVariableIndex(name, indexedVar, idx, modelManager);
                            return $"{name}{idx}";
                        }
                    }
                }
                else
                {
                    // Non-numeric index
                    return $"{name}_idx_{indexStr}";
                }
                
                return m.Value;
            });
        }
        
        private void ValidateParameterIndex(string name, Parameter param, int idx, ModelManager modelManager)
        {
            if (modelManager.IndexSets.TryGetValue(param.IndexSetName, out var indexSet))
            {
                if (!indexSet.Contains(idx))
                    throw new Exception($"Index {idx} is out of range for parameter {name}");
            }
        }
        
        private void ValidateVariableIndex(string name, IndexedVariable var, int idx, ModelManager modelManager)
        {
            var indexSet = modelManager.IndexSets[var.IndexSetName];
            if (!indexSet.Contains(idx))
                throw new Exception($"Index {idx} is out of range for variable {name}");
        }
    }
    
   
}