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
            if (keyExpr is CompositeKeyExpression compositeKey)
            {
                // Multi-part key: <field1, field2, ...>
                var resolvedParts = new List<object>();
                
                foreach (var part in compositeKey.KeyParts)
                {
                    var resolvedPart = ResolveKeyWithContext(part, manager, context);
                    resolvedParts.Add(resolvedPart);
                }
                
                return resolvedParts;
            }
            else if (keyExpr is ItemFunctionExpression nestedItem)
            {
                // Nested item() call
                return nestedItem.EvaluateWithContext(manager, context);
            }
            else if (keyExpr is ItemFieldAccessExpression itemField)
            {
                // item(...).field
                var tuple = ((ItemFunctionExpression)itemField.ItemExpression).EvaluateWithContext(manager, context) as TupleInstance;
                return tuple?.GetValue(itemField.FieldName) 
                    ?? throw new InvalidOperationException($"Field '{itemField.FieldName}' not found");
            }
            else if (keyExpr is DynamicTupleFieldAccessExpression dynamicField)
            {
                // variable.field where variable is an iterator
                if (context.TryGetIterator(dynamicField.VariableName, out int iterValue))
                {
                    // Get tuple from temporary parameter
                    if (manager.Parameters.TryGetValue(dynamicField.VariableName, out var param) && 
                        param.Value is TupleInstance tupleInstance)
                    {
                        var fieldValue = tupleInstance.GetValue(dynamicField.FieldName);
                        return fieldValue ?? throw new InvalidOperationException(
                            $"Field '{dynamicField.FieldName}' not found in tuple '{dynamicField.VariableName}'");
                    }
                    
                    // ✅ NEW: Try to look up in tuple sets by index
                    // This handles cases where iterator is numeric but refers to a tuple set position
                    foreach (var tupleSet in manager.TupleSets.Values)
                    {
                        if (iterValue >= 1 && iterValue <= tupleSet.Instances.Count)
                        {
                            var tuple = tupleSet.Instances[iterValue - 1]; // 1-based
                            var fieldValue = tuple.GetValue(dynamicField.FieldName);
                            if (fieldValue != null)
                            {
                                return fieldValue;
                            }
                        }
                    }
                }
                
                // Try to evaluate normally
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
            else if (keyExpr is ParameterExpression paramExpr)
            {
                // Check if it's an iterator variable
                if (context.TryGetIterator(paramExpr.ParameterName, out int iterValue))
                {
                    return iterValue;
                }
                
                // Regular parameter
                if (manager.Parameters.TryGetValue(paramExpr.ParameterName, out var param))
                {
                    return param.Value ?? throw new InvalidOperationException(
                        $"Parameter '{paramExpr.ParameterName}' has no value");
                }
                
                throw new InvalidOperationException($"Parameter '{paramExpr.ParameterName}' not found");
            }
            else if (keyExpr is ConstantExpression constExpr)
            {
                return constExpr.Value;
            }
            else if (keyExpr is StringConstantExpression strConst)
            {
                return strConst.Value;
            }
            
            // Fallback: try to evaluate as numeric
            return keyExpr.Evaluate(manager);
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