using System.Text;

namespace Core.Models
{
    /// <summary>
    /// Represents an OPL decision expression (dexpr)
    /// Example: dexpr float totalCost = sum(p in Products) cost[p] * production[p];
    /// </summary>
    public class DecisionExpression
    {
        // Track dependencies
        public HashSet<string> DependsOn { get; private set; } = new HashSet<string>();

        public string Name { get; set; }
        public VariableType Type { get; set; }
        public Expression Expression { get; set; }
        public string? IndexSetName { get; set; }  // For indexed dexpr: dexpr float cost[i in Products]
        public bool IsIndexed => !string.IsNullOrEmpty(IndexSetName);
        
        // For non-indexed (scalar) dexpr
        public DecisionExpression(string name, VariableType type, Expression expression)
        {
            Name = name;
            Type = type;
            Expression = expression;
        }
        
        // For indexed dexpr
        public DecisionExpression(string name, VariableType type, string indexSetName, Expression expression)
        {
            Name = name;
            Type = type;
            IndexSetName = indexSetName;
            Expression = expression;
        }
        
        /// <summary>
        /// Evaluates the decision expression for scalar (non-indexed) case
        /// </summary>
        public double Evaluate(ModelManager modelManager)
        {
            if (IsIndexed)
            {
                throw new InvalidOperationException($"Decision expression '{Name}' is indexed. Use Evaluate(modelManager, index) instead.");
            }
            
            return Expression.Evaluate(modelManager);
        }

        /// <summary>
        /// Analyzes the expression to find dependencies on other dexprs
        /// </summary>
        public void AnalyzeDependencies(ModelManager modelManager)
        {
            DependsOn.Clear();
            FindDexprReferences(Expression, DependsOn);
        }
        
        private void FindDexprReferences(Expression expr, HashSet<string> dependencies)
        {
            switch (expr)
            {
                case DecisionExpressionExpression dexprExpr:
                    dependencies.Add(dexprExpr.Name);
                    break;
                
                case BinaryExpression binExpr:
                    FindDexprReferences(binExpr.Left, dependencies);
                    FindDexprReferences(binExpr.Right, dependencies);
                    break;
                
                case UnaryExpression unaryExpr:
                    FindDexprReferences(unaryExpr.Operand, dependencies);
                    break;
                
                case SummationExpression sumExpr:
                    FindDexprReferences(sumExpr.Body, dependencies);
                    break;
                
                case IndexedVariableExpression idxVarExpr:
                    FindDexprReferences(idxVarExpr.Index1, dependencies);
                    if (idxVarExpr.Index2 != null)
                        FindDexprReferences(idxVarExpr.Index2, dependencies);
                    break;
            }
        }
        
        /// <summary>
        /// Evaluates the decision expression for a specific index
        /// </summary>
        public double Evaluate(ModelManager modelManager, int index)
        {
            if (!IsIndexed)
            {
                throw new InvalidOperationException($"Decision expression '{Name}' is not indexed. Use Evaluate(modelManager) instead.");
            }
            
            // Create a temporary context with the index variable
            // Store original parameter if it exists
            bool hadOriginal = modelManager.Parameters.TryGetValue(IndexSetName!, out var originalParam);
            double originalValue = hadOriginal ? Convert.ToDouble(originalParam!.Value) : 0;
            
            try
            {
                // Set the index variable
                modelManager.SetParameter(IndexSetName!, index);
                
                // Evaluate the expression
                return Expression.Evaluate(modelManager);
            }
            finally
            {
                // Restore original parameter or remove temporary one
                if (hadOriginal)
                {
                    modelManager.SetParameter(IndexSetName!, originalValue);
                }
                else
                {
                    modelManager.Parameters.Remove(IndexSetName!);
                }
            }
        }
        
        /// <summary>
        /// Expands an indexed dexpr into individual expressions
        /// </summary>
        public Dictionary<int, Expression> ExpandIndexed(ModelManager modelManager)
        {
            if (!IsIndexed)
            {
                throw new InvalidOperationException($"Decision expression '{Name}' is not indexed.");
            }
            
            var expanded = new Dictionary<int, Expression>();
            
            // Get the index set
            IEnumerable<int> indices = GetIndices(modelManager);
            
            foreach (int index in indices)
            {
                // Store original parameter if it exists
                bool hadOriginal = modelManager.Parameters.TryGetValue(IndexSetName!, out var originalParam);
                double originalValue = hadOriginal ? Convert.ToDouble(originalParam!.Value) : 0;
                
                try
                {
                    // Set the index variable
                    modelManager.SetParameter(IndexSetName!, index);
                    
                    // Clone and substitute the expression (simplified version)
                    expanded[index] = Expression; // In reality, should substitute index
                }
                finally
                {
                    // Restore original parameter or remove temporary one
                    if (hadOriginal)
                    {
                        modelManager.SetParameter(IndexSetName!, originalValue);
                    }
                    else
                    {
                        modelManager.Parameters.Remove(IndexSetName!);
                    }
                }
            }
            
            return expanded;
        }
        
        private IEnumerable<int> GetIndices(ModelManager modelManager)
        {
            // Try index sets first
            if (modelManager.IndexSets.TryGetValue(IndexSetName!, out var indexSet))
            {
                return indexSet.GetIndices();
            }
            
            throw new InvalidOperationException($"Index set '{IndexSetName}' not found");
        }
        
        public override string ToString()
        {
            if (IsIndexed)
            {
                return $"dexpr {Type.ToString().ToLower()} {Name}[{IndexSetName}] = {Expression}";
            }
            else
            {
                return $"dexpr {Type.ToString().ToLower()} {Name} = {Expression}";
            }
        }
        
        /// <summary>
        /// Returns a detailed string representation for debugging
        /// </summary>
        public string ToDetailedString(ModelManager modelManager, bool includeValue = false)
        {
            var sb = new StringBuilder();
            sb.Append($"dexpr {Type.ToString().ToLower()} {Name}");
            
            // FIXED: Use IndexSetName (singular)
            if (IsIndexed)
            {
                sb.Append($"[{IndexSetName}]");
            }
            
            sb.Append($" = {Expression}");
            
            if (includeValue && !IsIndexed)
            {
                try
                {
                    double value = Evaluate(modelManager);
                    sb.Append($"  // evaluates to: {value}");
                }
                catch (Exception ex)
                {
                    sb.Append($"  // error: {ex.Message}");
                }
            }
            
            if (DependsOn.Count > 0)
            {
                sb.Append($"\n  // depends on: {string.Join(", ", DependsOn)}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Prints all values for indexed dexpr
        /// </summary>
        public string PrintAllValues(ModelManager modelManager)
        {
            if (!IsIndexed)
            {
                return $"{Name} = {Evaluate(modelManager)}";
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"{Name}:");
            
            // FIXED: Get single-dimensional indices
            var indices = GetIndices(modelManager);
            
            foreach (int index in indices)
            {
                try
                {
                    // FIXED: Call Evaluate with single index
                    double value = Evaluate(modelManager, index);
                    sb.AppendLine($"  [{index}] = {value}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  [{index}] = ERROR: {ex.Message}");
                }
            }
            
            return sb.ToString();
        }
    }
}