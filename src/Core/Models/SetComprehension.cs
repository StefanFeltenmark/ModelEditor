namespace Core.Models
{
    /// <summary>
    /// Represents a set comprehension: { expr | var in Set : condition }
    /// Examples:
    ///   {Arc} Jout[i] = {j | j in HydroArcs: j.fromHydroNode == i};
    ///   {Station} filtered = {s | s in stations, d in defs: s.id == d.stationId};
    /// </summary>
    public class SetComprehension
    {
        /// <summary>
        /// The expression to evaluate for each element (e.g., "j", "s.id", "item(Set, key)")
        /// </summary>
        public string Expression { get; set; }
        
        /// <summary>
        /// Iterator definitions (e.g., "j in HydroArcs", "s in stations")
        /// </summary>
        public List<SetIterator> Iterators { get; set; }
        
        /// <summary>
        /// Filter condition (optional)
        /// </summary>
        public string? Condition { get; set; }
        
        /// <summary>
        /// The type of elements in the resulting set
        /// </summary>
        public string ElementType { get; set; } // e.g., "Arc", "Station", "string", "int"
        
        public SetComprehension(string elementType, string expression, List<SetIterator> iterators, string? condition = null)
        {
            ElementType = elementType;
            Expression = expression;
            Iterators = iterators;
            Condition = condition;
        }
        
        /// <summary>
        /// Evaluates the set comprehension
        /// </summary>
        public object EvaluateSet(ModelManager modelManager, Dictionary<string, object>? context = null)
        {
            context ??= new Dictionary<string, object>();
            var results = new List<object>();
            
            // Recursively expand all iterators
            EvaluateIterators(0, context, results, modelManager);
            
            return results;
        }
        
        private void EvaluateIterators(
            int iteratorIndex, 
            Dictionary<string, object> context,
            List<object> results,
            ModelManager modelManager)
        {
            if (iteratorIndex >= Iterators.Count)
            {
                // All iterators processed - evaluate condition and expression
                if (string.IsNullOrEmpty(Condition) || EvaluateCondition(Condition, context, modelManager))
                {
                    var value = EvaluateExpression(Expression, context, modelManager);
                    if (value != null)
                    {
                        results.Add(value);
                    }
                }
                return;
            }
            
            var iterator = Iterators[iteratorIndex];
            var collection = GetCollection(iterator.SetName, context, modelManager);
            
            foreach (var item in collection)
            {
                context[iterator.VariableName] = item;
                EvaluateIterators(iteratorIndex + 1, context, results, modelManager);
            }
            
            context.Remove(iterator.VariableName);
        }
        
        private IEnumerable<object> GetCollection(string setName, Dictionary<string, object> context, ModelManager modelManager)
        {
            // Check if it's a context variable (for nested comprehensions)
            if (context.ContainsKey(setName))
            {
                if (context[setName] is IEnumerable<object> enumerable)
                    return enumerable;
            }
            
            // Check tuple sets
            if (modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
            {
                return tupleSet.Instances.Cast<object>();
            }
            
            // Check primitive sets
            if (modelManager.PrimitiveSets.TryGetValue(setName, out var primitiveSet))
            {
                return primitiveSet.GetAllValues();
            }
            
            // Check ranges
            if (modelManager.Ranges.TryGetValue(setName, out var range))
            {
                return range.GetValues(modelManager).Cast<object>();
            }
            
            // Check index sets
            if (modelManager.IndexSets.TryGetValue(setName, out var indexSet))
            {
                return indexSet.GetIndices().Cast<object>();
            }
            
            throw new InvalidOperationException($"Set '{setName}' not found");
        }
        
        private bool EvaluateCondition(string condition, Dictionary<string, object> context, ModelManager modelManager)
        {
            // Parse and evaluate the condition
            // This is simplified - full implementation would parse the condition properly
            
            try
            {
                // Replace context variables in condition
                string evaluableCondition = condition;
                foreach (var kvp in context)
                {
                    // Handle tuple field access: j.fromHydroNode
                    if (kvp.Value is TupleInstance tuple)
                    {
                        // For now, just mark that we have tuple context
                        // Full implementation would parse and replace field accesses
                    }
                }
                
                // For now, use a simple evaluator
                // Full implementation would use expression parser
                var evaluator = new ConditionEvaluator(modelManager, context);
                return evaluator.Evaluate(condition);
            }
            catch
            {
                return false;
            }
        }
        
        private object? EvaluateExpression(string expression, Dictionary<string, object> context, ModelManager modelManager)
        {
            // Simple case: just return the iterator variable
            if (context.ContainsKey(expression))
            {
                return context[expression];
            }
            
            // Handle tuple field access: j.fromHydroNode
            if (expression.Contains('.'))
            {
                var parts = expression.Split('.');
                if (parts.Length == 2 && context.TryGetValue(parts[0], out var obj))
                {
                    if (obj is TupleInstance tuple)
                    {
                        return tuple.GetValue(parts[1]);
                    }
                }
            }
            
            // Handle item() expressions: item(HydroArcs, <i>)
            if (expression.StartsWith("item("))
            {
                return EvaluateItemExpression(expression, context, modelManager);
            }
            
            // Just return the expression as-is (literal)
            return expression;
        }
        
        private object? EvaluateItemExpression(string itemExpr, Dictionary<string, object> context, ModelManager modelManager)
        {
            // Parse: item(SetName, <key1, key2, ...>)
            var match = System.Text.RegularExpressions.Regex.Match(
                itemExpr, 
                @"item\s*\(\s*(\w+)\s*,\s*<([^>]+)>\s*\)"
            );
            
            if (!match.Success)
                return null;
            
            string setName = match.Groups[1].Value;
            string keysStr = match.Groups[2].Value;
            
            // Parse keys
            var keys = keysStr.Split(',').Select(k => k.Trim()).ToList();
            var keyValues = new List<object>();
            
            foreach (var key in keys)
            {
                if (context.TryGetValue(key, out var contextValue))
                {
                    keyValues.Add(contextValue);
                }
                else if (int.TryParse(key, out int intKey))
                {
                    keyValues.Add(intKey);
                }
                else
                {
                    keyValues.Add(key.Trim('"'));
                }
            }
            
            // Look up in tuple set
            if (modelManager.TupleSets.TryGetValue(setName, out var tupleSet))
            {
                var schema = modelManager.TupleSchemas[tupleSet.SchemaName];
                return tupleSet.FindByKey(schema, keyValues.ToArray());
            }
            
            return null;
        }
        
        public override string ToString()
        {
            var iteratorStr = string.Join(", ", Iterators.Select(i => $"{i.VariableName} in {i.SetName}"));
            var conditionStr = string.IsNullOrEmpty(Condition) ? "" : $": {Condition}";
            return $"{{{Expression} | {iteratorStr}{conditionStr}}}";
        }
    }
    
    /// <summary>
    /// Represents an iterator in a set comprehension
    /// </summary>
    public class SetIterator
    {
        public string VariableName { get; set; }
        public string SetName { get; set; }
        
        public SetIterator(string variableName, string setName)
        {
            VariableName = variableName;
            SetName = setName;
        }
        
        public override string ToString() => $"{VariableName} in {SetName}";
    }
    
    /// <summary>
    /// Helper class to evaluate conditions in set comprehensions
    /// </summary>
    public class ConditionEvaluator
    {
        private readonly ModelManager modelManager;
        private readonly Dictionary<string, object> context;
        
        public ConditionEvaluator(ModelManager manager, Dictionary<string, object> ctx)
        {
            modelManager = manager;
            context = ctx;
        }
        
        public bool Evaluate(string condition)
        {
            // Handle logical operators first
            if (condition.Contains("&&"))
            {
                var parts = SplitByOperator(condition, "&&");
                return parts.All(p => Evaluate(p.Trim()));
            }
            
            if (condition.Contains("||"))
            {
                var parts = SplitByOperator(condition, "||");
                return parts.Any(p => Evaluate(p.Trim()));
            }
            
            // Parse comparison operators
            var operators = new[] { "==", "!=", "<=", ">=", "<", ">" };
            
            foreach (var op in operators)
            {
                if (condition.Contains(op))
                {
                    var parts = condition.Split(new[] { op }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var left = EvaluateValue(parts[0].Trim());
                        var right = EvaluateValue(parts[1].Trim());
                        
                        return op switch
                        {
                            "==" => Equals(left, right),
                            "!=" => !Equals(left, right),
                            "<" => Compare(left, right) < 0,
                            ">" => Compare(left, right) > 0,
                            "<=" => Compare(left, right) <= 0,
                            ">=" => Compare(left, right) >= 0,
                            _ => false
                        };
                    }
                }
            }
            
            return false;
        }
        
        private List<string> SplitByOperator(string condition, string op)
        {
            var results = new List<string>();
            var parts = condition.Split(new[] { op }, StringSplitOptions.None);
            return parts.Select(p => p.Trim()).ToList();
        }
        
        private object? EvaluateValue(string expr)
        {
            expr = expr.Trim();
            
            // Handle tuple field access: variable.field
            if (expr.Contains('.'))
            {
                var parts = expr.Split('.');
                if (parts.Length == 2 && context.TryGetValue(parts[0], out var obj))
                {
                    if (obj is TupleInstance tuple)
                    {
                        return tuple.GetValue(parts[1]);
                    }
                }
            }
            
            // Handle context variable
            if (context.TryGetValue(expr, out var value))
            {
                return value;
            }
            
            // Handle literal string
            if (expr.StartsWith("\"") && expr.EndsWith("\""))
            {
                return expr.Trim('"');
            }
            
            // Handle literal number
            if (int.TryParse(expr, out int intVal))
            {
                return intVal;
            }
            
            if (double.TryParse(expr, System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out double doubleVal))
            {
                return doubleVal;
            }
            
            // Check if it's a parameter
            if (modelManager.Parameters.TryGetValue(expr, out var param))
            {
                return param.Value;
            }
            
            return expr;
        }
        
        private int Compare(object? left, object? right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            
            // Handle numeric comparisons
            if (left is IConvertible lc && right is IConvertible rc)
            {
                try
                {
                    double leftNum = Convert.ToDouble(lc);
                    double rightNum = Convert.ToDouble(rc);
                    return leftNum.CompareTo(rightNum);
                }
                catch
                {
                    // Fall through to string comparison
                }
            }
            
            // String comparison
            return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal);
        }
    }
}