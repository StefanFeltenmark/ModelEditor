namespace Core.Models
{
    /// <summary>
    /// Represents an OPL decision expression (dexpr)
    /// Example: dexpr float totalCost = sum(p in Products) cost[p] * production[p];
    /// </summary>
    public class DecisionExpression
    {
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
            // Try ranges first
            if (modelManager.IndexSets.TryGetValue(IndexSetName!, out var range))
            {
                
                return range.GetIndices();
            }
            
            // Try index sets
            if (modelManager.IndexSets.TryGetValue(IndexSetName!, out var indexSet))
            {
                return indexSet.GetIndices();
            }
            
            throw new InvalidOperationException($"Index set or range '{IndexSetName}' not found");
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
    }
}