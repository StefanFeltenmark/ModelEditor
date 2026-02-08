using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    /// <summary>
    /// Represents the item() function: item(setName, keyExpression)
    /// Example: item(nodes, 0) or item(HydroArcTs, <s.id, t>)
    /// </summary>
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
            // For now, item() typically returns a tuple, not a numeric value
            // This will be called when item() result is used in numeric context
            throw new InvalidOperationException(
                $"item({SetName}, ...) returns a tuple. Use field access: item(...).fieldName");
        }

        /// <summary>
        /// Evaluates item() with iterator context
        /// Substitutes iterator variables in the key expression before lookup
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

        /// <summary>
        /// Evaluates the item() function and returns the tuple instance
        /// </summary>
        public TupleInstance EvaluateToTuple(ModelManager manager)
        {
            // Simple evaluation without context (for non-iterator keys)
            return EvaluateWithContext(manager, new EvaluationContext()) as TupleInstance
                ?? throw new InvalidOperationException($"item({SetName}, ...) did not return a tuple");
        }

        /// <summary>
        /// Resolves a key expression by substituting iterator variables
        /// </summary>
        private object ResolveKeyWithContext(Expression keyExpr, ModelManager manager, EvaluationContext context)
        {
            // Handle different key expression types
            
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
            else if (keyExpr is TupleFieldAccessExpression fieldAccess)
            {
                // Field access: variable.field
                // Extract the variable name from the base expression
                string? variableName = null;
                
                if (fieldAccess.BaseExpression is TupleVariableExpression tupleVar)
                {
                    variableName = tupleVar.VariableName;
                }
                else if (fieldAccess.BaseExpression is ParameterExpression paramExpr)
                {
                    variableName = paramExpr.ParameterName;
                }
                
                // Check if it's an iterator variable
                if (variableName != null && context.TryGetIterator(variableName, out int iterValue))
                {
                    // The iterator refers to a tuple set - we need to get the tuple instance
                    // and then access the field
                    
                    // First, try to find which tuple set this iterator belongs to
                    // This is complex - for now, we need the tuple instance to be in the context
                    
                    // Try to get the tuple from a temporary parameter set by the iteration
                    if (manager.Parameters.TryGetValue(variableName, out var param) && 
                        param.Value is TupleInstance tupleInstance)
                    {
                        // Get the field value from the tuple
                        var fieldValue = tupleInstance.GetValue(fieldAccess.FieldName);
                        return fieldValue ?? throw new InvalidOperationException(
                            $"Field '{fieldAccess.FieldName}' not found in tuple '{variableName}'");
                    }
                    
                    throw new InvalidOperationException(
                        $"Iterator variable '{variableName}' does not resolve to a tuple instance");
                }
                
                // Not an iterator, evaluate normally
                return fieldAccess.Evaluate(manager);
            }
            else if (keyExpr is DynamicTupleFieldAccessExpression dynamicFieldAccess)
            {
                // Dynamic field access: iterator.field (where iterator is from forall/sum context)
                if (context.TryGetIterator(dynamicFieldAccess.VariableName, out int iterValue))
                {
                    // Try to get the tuple from a temporary parameter
                    if (manager.Parameters.TryGetValue(dynamicFieldAccess.VariableName, out var param) && 
                        param.Value is TupleInstance tupleInstance)
                    {
                        var fieldValue = tupleInstance.GetValue(dynamicFieldAccess.FieldName);
                        return fieldValue ?? throw new InvalidOperationException(
                            $"Field '{dynamicFieldAccess.FieldName}' not found in tuple '{dynamicFieldAccess.VariableName}'");
                    }
                    
                    throw new InvalidOperationException(
                        $"Iterator variable '{dynamicFieldAccess.VariableName}' does not resolve to a tuple instance");
                }
                
                // Evaluate normally
                return dynamicFieldAccess.Evaluate(manager);
            }
            else if (keyExpr is ParameterExpression paramExpr)
            {
                // Check if it's an iterator variable
                if (context.TryGetIterator(paramExpr.ParameterName, out int iterValue))
                {
                    return iterValue;
                }
                
                // It's a regular parameter
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

        /// <summary>
        /// Finds a tuple instance by matching its key fields
        /// </summary>
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

        /// <summary>
        /// Compares two values for equality, handling type conversions
        /// </summary>
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

        /// <summary>
        /// Formats a key for error messages
        /// </summary>
        private string FormatKey(object key)
        {
            if (key is List<object> list)
            {
                return $"<{string.Join(", ", list)}>";
            }
            return key.ToString() ?? "";
        }

        public override bool IsConstant => false;

        public override string ToString()
        {
            return $"item({SetName}, {KeyExpression})";
        }
    }
}