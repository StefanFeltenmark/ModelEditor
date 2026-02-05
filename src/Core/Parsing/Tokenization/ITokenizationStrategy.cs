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
                
                // Parse key values into List<Expression>
                var keyExpressions = ParseItemKeyExpressions(keyValuesStr, modelManager, out string parseError);
                if (keyExpressions == null)
                {
                    throw new Exception($"Error parsing item() key values: {parseError}");
                }
                
                // Validate tuple set exists
                if (!modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
                {
                    throw new Exception($"Tuple set '{setName}' not found");
                }
                
                // Create composite key or single expression
                Expression keyExpression;
                if (keyExpressions.Count == 1)
                {
                    keyExpression = keyExpressions[0];
                }
                else
                {
                    keyExpression = new CompositeKeyExpression(keyExpressions);
                }
                
                // Create item expression
                var itemExpr = new ItemFunctionExpression(setName, keyExpression);
                
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
        
        private List<Expression>? ParseItemKeyExpressions(string keyValuesStr, ModelManager modelManager, out string error)
        {
            error = string.Empty;
            var keyExpressions = new List<Expression>();
            
            var values = SplitByCommaRespectingQuotes(keyValuesStr);
            
            foreach (var valueStr in values)
            {
                string trimmed = valueStr.Trim();
                
                if (string.IsNullOrEmpty(trimmed))
                    continue;
                
                // Check if it's a string literal
                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                {
                    string stringValue = trimmed.Substring(1, trimmed.Length - 2);
                    // Create a string constant expression (we'll need to handle strings specially)
                    keyExpressions.Add(new StringConstantExpression(stringValue));
                }
                // Check if it's a boolean
                else if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    keyExpressions.Add(new ConstantExpression(bool.Parse(trimmed) ? 1.0 : 0.0));
                }
                // Check if it's a number
                else if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
                {
                    keyExpressions.Add(new ConstantExpression(doubleVal));
                }
                // Check if it's a field access (e.g., s.id)
                else if (trimmed.Contains('.'))
                {
                    var parts = trimmed.Split('.');
                    if (parts.Length == 2)
                    {
                        keyExpressions.Add(new TupleFieldAccessExpression(parts[0], parts[1]));
                    }
                    else
                    {
                        error = $"Invalid field access: '{trimmed}'";
                        return null;
                    }
                }
                // Otherwise, treat as parameter or variable reference
                else
                {
                    keyExpressions.Add(new ParameterExpression(trimmed));
                }
            }
            
            if (keyExpressions.Count == 0)
            {
                error = "No key values found";
                return null;
            }
            
            return keyExpressions;
        }

        private List<string> SplitByCommaRespectingQuotes(string input)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == ',' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }
    }

    /// <summary>
/// Tokenizes tuple field access: tupleVar.fieldName or tupleSet[index].fieldName
/// </summary>
public class TupleFieldAccessTokenizer : ITokenizationStrategy
{
    private ITokenizationStrategy tokenizationStrategyImplementation;

    public string Tokenize(string expression, TokenManager tokenManager, ModelManager modelManager)
    {
        // Pattern 1: Indexed tuple with NUMERIC index: tupleSet[123].field
        string numericPattern = @"([a-zA-Z][a-zA-Z0-9_]*)\[([0-9]+)\]\.([a-zA-Z][a-zA-Z0-9_]*)";
        
        expression = Regex.Replace(expression, numericPattern, m =>
        {
            string setName = m.Groups[1].Value;
            string indexStr = m.Groups[2].Value;
            string fieldName = m.Groups[3].Value;
            
            if (!int.TryParse(indexStr, out int index))
                return m.Value;
            
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
                
                // FIXED: Validate against index set, not instance count
                if (tupleSet.IsIndexed)
                {
                    // Tuple set is indexed - validate against index set
                    if (!modelManager.IndexSets.TryGetValue(tupleSet.IndexSetName!, out var indexSet))
                    {
                        throw new Exception($"Index set '{tupleSet.IndexSetName}' not found for tuple set '{setName}'");
                    }
                
                    if (!indexSet.Contains(index))
                    {
                        throw new Exception(
                            $"Index {index} is out of range for tuple set '{setName}' " +
                            $"(valid range: {indexSet.StartIndex}..{indexSet.EndIndex})");
                    }
                
                    // Map index set value to tuple instance position
                    int instanceIndex = indexSet.GetPosition(index);
                    if (instanceIndex < 0 || instanceIndex >= tupleSet.Instances.Count)
                    {
                        throw new Exception(
                            $"Index {index} maps to position {instanceIndex} which is out of bounds " +
                            $"for tuple set '{setName}' (instance count: {tupleSet.Instances.Count})");
                    }
                
                    var tupleExpr = new IndexedTupleFieldAccessExpression(setName, instanceIndex, fieldName);
                    return tokenManager.CreateToken(tupleExpr, "TUPLE");
                }
                else
                {
                    // No index set - use direct indexing (0-based)
                    if (index < 0 || index >= tupleSet.Instances.Count)
                    {
                        throw new Exception(
                            $"Index {index} is out of range for tuple set '{setName}' " +
                            $"(count: {tupleSet.Instances.Count})");
                    }
                
                    var tupleExpr = new IndexedTupleFieldAccessExpression(setName, index, fieldName);
                    return tokenManager.CreateToken(tupleExpr, "TUPLE");
                }
                
            }
            
            return m.Value;
        });
        
        // Pattern 2: NEW - Iterator variable index: tupleSet[varName].field
        string iteratorPattern = @"([a-zA-Z][a-zA-Z0-9_]*)\[([a-zA-Z][a-zA-Z0-9_]*)\]\.([a-zA-Z][a-zA-Z0-9_]*)";
        
        expression = Regex.Replace(expression, iteratorPattern, m =>
        {
            string setName = m.Groups[1].Value;
            string iteratorVar = m.Groups[2].Value;
            string fieldName = m.Groups[3].Value;
            
            if (modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
            {
                // Validate field exists
                if (modelManager.TupleSchemas.TryGetValue(tupleSet.SchemaName, out var schema))
                {
                    if (!schema.Fields.ContainsKey(fieldName))
                    {
                        throw new Exception($"Field '{fieldName}' not found in schema '{schema.Name}'");
                    }
                }
                
                // Create iterator-based expression
                var iterExpr = new IteratorIndexedTupleFieldAccessExpression(setName, iteratorVar, fieldName);
                return tokenManager.CreateToken(iterExpr, "TUPLE_ITER");
            }
            
            return m.Value;
        });
        
        return expression;
    }

    public int Priority { get; }
    public string Name { get; }
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
                            var index1Expr = new ConstantExpression(idx1);
                            var index2Expr = new ConstantExpression(idx2);
                            var paramExpr = new IndexedParameterExpression(name, index1Expr, index2Expr);
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
                    // Non-numeric indices - create expressions
                    Expression idx1Expr = new ParameterExpression(index1Str);
                    Expression idx2Expr = new ParameterExpression(index2Str);
                    
                    if (modelManager.Parameters.ContainsKey(name))
                    {
                        var paramExpr = new IndexedParameterExpression(name, idx1Expr, idx2Expr);
                        return tokenManager.CreateToken(paramExpr, "PARAM");
                    }
                    
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
                            var indexExpr = new ConstantExpression(idx);
                            var paramExpr = new IndexedParameterExpression(name, indexExpr);
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
                    if (modelManager.Parameters.ContainsKey(name))
                    {
                        Expression indexExpr = new ParameterExpression(indexStr);
                        var paramExpr = new IndexedParameterExpression(name, indexExpr);
                        return tokenManager.CreateToken(paramExpr, "PARAM");
                    }
                    
                    return $"{name}_idx_{indexStr}";
                }
                
                return m.Value;
            });
        }
        
        private void ValidateParameterIndex(string name, Parameter param, int idx, ModelManager modelManager)
        {
            if (param.IndexSetName != null && modelManager.IndexSets.TryGetValue(param.IndexSetName, out var indexSet))
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