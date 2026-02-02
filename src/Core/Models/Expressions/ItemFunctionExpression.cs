namespace Core.Models
{
    /// <summary>
    /// Represents item(setName, key) function call
    /// Returns a tuple instance from a tuple set based on key matching
    /// </summary>
    public class ItemFunctionExpression : Expression
    {
        public string SetName { get; set; }
        public Expression KeyExpression { get; set; }  // Can be composite key
        
        public ItemFunctionExpression(string setName, Expression keyExpression)
        {
            SetName = setName;
            KeyExpression = keyExpression;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException(
                "item() returns a tuple, not a numeric value. Use field access like item(set, key).field");
        }

        /// <summary>
        /// Evaluates and returns the tuple instance
        /// </summary>
        public TupleInstance EvaluateToTuple(ModelManager manager)
        {
            // Get the tuple set
            if (!manager.TupleSets.TryGetValue(SetName, out var tupleSet))
            {
                throw new InvalidOperationException($"Tuple set '{SetName}' not found");
            }

            // Evaluate the key expression to get key values
            var keyValues = EvaluateKeyExpression(KeyExpression, manager);

            // Find matching tuple instance
            var match = FindMatchingInstance(tupleSet, keyValues, manager);
            
            if (match == null)
            {
                throw new InvalidOperationException(
                    $"No tuple found in '{SetName}' matching key {string.Join(",", keyValues)}");
            }

            return match;
        }

        private List<object> EvaluateKeyExpression(Expression expr, ModelManager manager)
        {
            if (expr is CompositeKeyExpression composite)
            {
                // Multiple key values: <s.id, t>
                return composite.KeyExpressions
                    .Select(e => EvaluateSingleKey(e, manager))
                    .ToList();
            }
            else
            {
                // Single key value
                return new List<object> { EvaluateSingleKey(expr, manager) };
            }
        }

        private object EvaluateSingleKey(Expression expr, ModelManager manager)
        {
            if (expr is TupleFieldAccessExpression fieldAccess)
            {
                // Evaluate field access: s.id
                return fieldAccess.Evaluate(manager);
            }
            else if (expr is ConstantExpression constant)
            {
                return constant.Value;
            }
            else if (expr is ParameterExpression param)
            {
                var p = manager.Parameters[param.ParameterName];
                return p.Value ?? throw new InvalidOperationException($"Parameter '{param.ParameterName}' has no value");
            }
            else
            {
                // Try numeric evaluation
                return expr.Evaluate(manager);
            }
        }

        private TupleInstance? FindMatchingInstance(TupleSet tupleSet, List<object> keyValues, ModelManager manager)
        {
            var schema = manager.TupleSchemas[tupleSet.SchemaName];
            var keyFields = schema.KeyFields;

            if (keyValues.Count != keyFields.Count)
            {
                throw new InvalidOperationException(
                    $"Key count mismatch: provided {keyValues.Count} keys but schema has {keyFields.Count} key fields");
            }

            foreach (var instance in tupleSet.Instances)
            {
                bool matches = true;
                for (int i = 0; i < keyFields.Count; i++)
                {
                    string keyField = keyFields[i];
                    object expectedValue = keyValues[i];
                    object actualValue = instance.GetValue(keyField);

                    if (!AreEqual(expectedValue, actualValue))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return instance;
            }

            return null;
        }

        private bool AreEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // Handle numeric conversions
            if (IsNumeric(a) && IsNumeric(b))
            {
                return Convert.ToDouble(a).Equals(Convert.ToDouble(b));
            }

            return a.Equals(b);
        }

        private bool IsNumeric(object value)
        {
            return value is int || value is long || value is float || value is double || value is decimal;
        }

        public override string ToString()
        {
            return $"item({SetName}, {KeyExpression})";
        }

        public override bool IsConstant { get; }
    }

    /// <summary>
    /// Represents a composite key in angle brackets: <expr1, expr2, ...>
    /// </summary>
    public class CompositeKeyExpression : Expression
    {
        public List<Expression> KeyExpressions { get; set; }

        public CompositeKeyExpression(List<Expression> keyExpressions)
        {
            KeyExpressions = keyExpressions;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException("Composite key cannot be evaluated to a number");
        }

        public override string ToString()
        {
            return $"<{string.Join(", ", KeyExpressions)}>";
        }

        public override bool IsConstant { get; }
    }
}