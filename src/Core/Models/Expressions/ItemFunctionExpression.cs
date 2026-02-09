using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    public class ItemFunctionExpression : Expression
    {
        public string SetName { get; set; }
        public Expression KeyExpression { get; set; }

        public ItemFunctionExpression(string setName, Expression keyExpression)
        {
            SetName = setName;
            KeyExpression = keyExpression;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException(
                $"item({SetName}, ...) returns a tuple. Use field access: item(...).fieldName");
        }

        public override bool IsConstant => false;

        public override string ToString()
        {
            return $"item({SetName}, {KeyExpression})";
        }

        /// <summary>
        /// Evaluates and returns the tuple instance (no context)
        /// </summary>
        public TupleInstance EvaluateToTuple(ModelManager manager)
        {
            return EvaluateWithContext(manager, new EvaluationContext()) as TupleInstance
                ?? throw new InvalidOperationException($"item({SetName}, ...) did not return a tuple");
        }

        /// <summary>
        /// Evaluates with iterator context
        /// </summary>
        public object EvaluateWithContext(ModelManager manager, EvaluationContext context)
        {
            // Resolve the key expression with context
            var resolvedKey = ResolveKeyWithContext(KeyExpression, manager, context);

            // Look up the tuple set
            if (!manager.TupleSets.TryGetValue(SetName, out var tupleSet))
            {
                throw new InvalidOperationException($"Tuple set '{SetName}' not found");
            }

            // Find the tuple instance matching the key
            var instance = FindTupleByKey(tupleSet, resolvedKey, manager);
            
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"No tuple found in '{SetName}' matching key: {FormatKey(resolvedKey)}");
            }

            return instance;
        }

        private object ResolveKeyWithContext(Expression keyExpr, ModelManager manager, EvaluationContext context)
        {
            // Handle numeric index
            if (keyExpr is ConstantExpression constExpr)
            {
                return constExpr.Value;
            }
            
            // Handle parameter reference
            if (keyExpr is ParameterExpression paramExpr)
            {
                if (manager.Parameters.TryGetValue(paramExpr.ParameterName, out var param))
                {
                    return param.Value ?? throw new InvalidOperationException(
                        $"Parameter '{paramExpr.ParameterName}' has no value");
                }
                
                // Check evaluation context (for iterators)
                if (context.TryGetIterator(paramExpr.ParameterName, out var iterValue))
                {
                    return iterValue;
                }
            }
            
            // **ENHANCED: Handle dynamic tuple field access with iterator context**
            if (keyExpr is DynamicTupleFieldAccessExpression dynamicField)
            {
                // Try iterator context first
                if (context.TryGetIterator(dynamicField.VariableName, out int iterValue))
                {
                    // Look up tuple from sets by position (1-based indexing)
                    foreach (var tupleSet in manager.TupleSets.Values)
                    {
                        if (iterValue >= 1 && iterValue <= tupleSet.Instances.Count)
                        {
                            var tuple = tupleSet.Instances[iterValue - 1]; // Convert to 0-based
                            var fieldValue = tuple.GetValue(dynamicField.FieldName);
                            if (fieldValue != null)
                            {
                                return fieldValue;
                            }
                        }
                    }
                }
                
                // Try temporary parameter (tuple instance)
                if (manager.Parameters.TryGetValue(dynamicField.VariableName, out var param) && 
                    param.Value is TupleInstance tupleInstance)
                {
                    var fieldValue = tupleInstance.GetValue(dynamicField.FieldName);
                    return fieldValue ?? throw new InvalidOperationException(
                        $"Field '{dynamicField.FieldName}' not found in tuple '{dynamicField.VariableName}'");
                }
                
                // Try to evaluate field access with context
                var dict = new Dictionary<string, object>();
                if (manager.Parameters.TryGetValue(dynamicField.VariableName, out var p) && p.Value != null)
                {
                    dict[dynamicField.VariableName] = p.Value;
                }
                
                var resolvedValue = dynamicField.GetFieldValueWithContext(dict);
                if (resolvedValue != null)
                {
                    return resolvedValue;
                }
                
                throw new InvalidOperationException($"Cannot resolve {dynamicField}");
            }
            
            // **NEW: Handle angle bracket tuple key expression**
            if (keyExpr is TupleKeyExpression tupleKey)
            {
                // Evaluate the inner expression to get the key value
                return ResolveKeyWithContext(tupleKey.InnerExpression, manager, context);
            }
            
            // Handle string constant
            if (keyExpr is StringConstantExpression strConst)
            {
                return strConst.Value;
            }
            
            // Fallback: try to evaluate the expression
            try
            {
                return keyExpr.Evaluate(manager);
            }
            catch
            {
                throw new InvalidOperationException($"Cannot resolve key expression: {keyExpr}");
            }
        }

        private TupleInstance? FindTupleByKey(TupleSet tupleSet, object key, ModelManager manager)
        {
            if (!manager.TupleSchemas.TryGetValue(tupleSet.SchemaName, out var schema))
            {
                throw new InvalidOperationException($"Schema '{tupleSet.SchemaName}' not found");
            }

            var keyFields = schema.KeyFields;
            
            if (keyFields.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Tuple schema '{schema.Name}' has no key fields defined");
            }

            // Convert key to list of values
            List<object> keyValues;
            if (key is List<object> list)
            {
                keyValues = list;
            }
            else
            {
                keyValues = new List<object> { key };
            }

            // Validate key count matches
            if (keyValues.Count != keyFields.Count)
            {
                throw new InvalidOperationException(
                    $"Key has {keyValues.Count} values but schema '{schema.Name}' " +
                    $"has {keyFields.Count} key fields: {string.Join(", ", keyFields)}");
            }

            // Find matching instance
            foreach (var instance in tupleSet.Instances)
            {
                bool matches = true;
                
                for (int i = 0; i < keyFields.Count; i++)
                {
                    string keyFieldName = keyFields[i];
                    object keyValue = keyValues[i];
                    object? instanceValue = instance.GetValue(keyFieldName);
                    
                    if (!AreValuesEqual(keyValue, instanceValue))
                    {
                        matches = false;
                        break;
                    }
                }
                
                if (matches)
                {
                    return instance;
                }
            }

            return null;
        }

        private bool AreValuesEqual(object value1, object? value2)
        {
            if (value2 == null)
                return false;

            // Direct equality
            if (value1.Equals(value2))
                return true;

            // Try converting both to strings for comparison
            string str1 = value1.ToString() ?? "";
            string str2 = value2.ToString() ?? "";
            
            if (str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
                return true;

            // Try numeric comparison
            if (double.TryParse(str1, out double d1) && 
                double.TryParse(str2, out double d2))
            {
                return Math.Abs(d1 - d2) < 1e-10;
            }

            return false;
        }

        private string FormatKey(object key)
        {
            if (key is List<object> list)
            {
                return $"<{string.Join(", ", list)}>";
            }
            return key.ToString() ?? "";
        }
    }
}