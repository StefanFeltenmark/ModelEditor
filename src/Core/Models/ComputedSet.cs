using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Parsing;

namespace Core.Models
{
    /// <summary>
    /// Represents a set comprehension with advanced features
    /// </summary>
    public class ComputedSet
    {
        public string Name { get; set; }
        public string ElementType { get; set; }
        public List<SetIterator> Iterators { get; set; }
        public string OutputExpression { get; set; }
        public Expression? Condition { get; set; }
        public bool IsProjection { get; set; }

        public ComputedSet(string name, string elementType, List<SetIterator> iterators, 
            string outputExpr, Expression? condition, bool isProjection)
        {
            Name = name;
            ElementType = elementType;
            Iterators = iterators;
            OutputExpression = outputExpr;
            Condition = condition;
            IsProjection = isProjection;
        }

        public object Evaluate(ModelManager manager)
        {
            var results = new List<object>();

            // Get all iterator ranges
            var iteratorRanges = GetIteratorRanges(manager);

            // Generate all combinations of iterator values
            EvaluateRecursive(manager, 0, new Dictionary<string, object>(), iteratorRanges, results);

            // Return appropriate type
            if (IsProjection)
            {
                // Projection returns primitive values (strings, ints, etc.)
                return results;
            }
            else
            {
                // Filter returns tuple instances or other complex objects
                return results;
            }
        }

        private void EvaluateRecursive(ModelManager manager, int iteratorIndex, 
            Dictionary<string, object> context, List<(string name, List<object> values)> ranges, 
            List<object> results)
        {
            if (iteratorIndex >= Iterators.Count)
            {
                // All iterators bound - evaluate condition and output
                if (EvaluateCondition(manager, context))
                {
                    var output = EvaluateOutput(manager, context);
                    if (output != null)
                    {
                        results.Add(output);
                    }
                }
                return;
            }

            // Iterate over current iterator's range
            var (iterName, values) = ranges[iteratorIndex];

            foreach (var value in values)
            {
                context[iterName] = value;

                // Set temporary parameter for expression evaluation
                SetContextParameter(manager, iterName, value);

                try
                {
                    // Recurse to next iterator
                    EvaluateRecursive(manager, iteratorIndex + 1, context, ranges, results);
                }
                finally
                {
                    // Clean up temporary parameter
                    manager.Parameters.Remove(iterName);
                }
            }
        }

        private bool EvaluateCondition(ModelManager manager, Dictionary<string, object> context)
        {
            if (Condition == null)
                return true;

            try
            {
                // Handle string comparisons
                if (Condition is ComparisonExpression comparison)
                {
                    return EvaluateComparison(manager, comparison, context);
                }

                // Handle logical AND
                if (Condition is BinaryExpression binary && binary.Operator == BinaryOperator.Equal)
                {
                    // This might be part of a compound condition
                    double result = Condition.Evaluate(manager);
                    return Math.Abs(result - 1.0) < 1e-10; // 1.0 = true
                }

                double condResult = Condition.Evaluate(manager);
                return Math.Abs(condResult - 1.0) < 1e-10;
            }
            catch
            {
                return false;
            }
        }

        private bool EvaluateComparison(ModelManager manager, ComparisonExpression comparison, 
            Dictionary<string, object> context)
        {
            // Special handling for string and tuple field comparisons
            object? leftValue = GetExpressionValue(comparison.Left, manager, context);
            object? rightValue = GetExpressionValue(comparison.Right, manager, context);

            if (leftValue == null || rightValue == null)
                return false;

            switch (comparison.Operator)
            {
                case BinaryOperator.Equal:
                    return AreValuesEqual(leftValue, rightValue);
                
                case BinaryOperator.NotEqual:
                    return !AreValuesEqual(leftValue, rightValue);
                
                case BinaryOperator.LessThan:
                    return CompareValues(leftValue, rightValue) < 0;
                
                case BinaryOperator.LessThanOrEqual:
                    return CompareValues(leftValue, rightValue) <= 0;
                
                case BinaryOperator.GreaterThan:
                    return CompareValues(leftValue, rightValue) > 0;
                
                case BinaryOperator.GreaterThanOrEqual:
                    return CompareValues(leftValue, rightValue) >= 0;
                
                default:
                    return false;
            }
        }

        private object? GetExpressionValue(Expression expr, ModelManager manager, Dictionary<string, object> context)
        {
            if (expr is DynamicTupleFieldAccessExpression fieldAccess)
            {
                if (context.TryGetValue(fieldAccess.VariableName, out var obj) && obj is TupleInstance tuple)
                {
                    return tuple.GetValue(fieldAccess.FieldName);
                }
            }
            else if (expr is StringConstantExpression strConst)
            {
                return strConst.Value;
            }
            else if (expr is ConstantExpression constExpr)
            {
                return constExpr.Value;
            }
            else if (expr is ParameterExpression paramExpr)
            {
                if (context.TryGetValue(paramExpr.ParameterName, out var value))
                {
                    return value;
                }
                if (manager.Parameters.TryGetValue(paramExpr.ParameterName, out var param))
                {
                    return param.Value;
                }
            }

            try
            {
                return expr.Evaluate(manager);
            }
            catch
            {
                return null;
            }
        }

        private bool AreValuesEqual(object v1, object v2)
        {
            if (v1.Equals(v2))
                return true;

            string s1 = v1.ToString() ?? "";
            string s2 = v2.ToString() ?? "";

            return s1.Equals(s2, StringComparison.OrdinalIgnoreCase);
        }

        private int CompareValues(object v1, object v2)
        {
            if (v1 is IComparable c1 && v2.GetType() == v1.GetType())
            {
                return c1.CompareTo(v2);
            }

            // Try numeric comparison
            if (double.TryParse(v1.ToString(), out double d1) &&
                double.TryParse(v2.ToString(), out double d2))
            {
                return d1.CompareTo(d2);
            }

            // String comparison
            return string.Compare(v1.ToString(), v2.ToString(), StringComparison.Ordinal);
        }

        private object? EvaluateOutput(ModelManager manager, Dictionary<string, object> context)
        {
            if (IsProjection)
            {
                // Projection: extract field value
                var match = System.Text.RegularExpressions.Regex.Match(OutputExpression, 
                    @"^([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)$");
                
                if (match.Success)
                {
                    string varName = match.Groups[1].Value;
                    string fieldName = match.Groups[2].Value;

                    if (context.TryGetValue(varName, out var obj) && obj is TupleInstance tuple)
                    {
                        return tuple.GetValue(fieldName);
                    }
                }
            }
            else
            {
                // Filter: return the iterator variable (tuple instance)
                if (context.TryGetValue(OutputExpression, out var value))
                {
                    return value;
                }

                // Check for item() expression
                if (OutputExpression.StartsWith("item(", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse and evaluate item()
                    if (ItemFunctionParser.TryParse(OutputExpression, manager, out var itemExpr, out _))
                    {
                        if (itemExpr is ItemFunctionExpression itemFunc)
                        {
                            var evalContext = new EvaluationContext();
                            foreach (var kvp in context)
                            {
                                if (kvp.Value is int intVal)
                                {
                                    evalContext.SetIterator(kvp.Key, intVal);
                                }
                            }
                            return itemFunc.EvaluateWithContext(manager, evalContext);
                        }
                    }
                }
            }

            return null;
        }

        private List<(string name, List<object> values)> GetIteratorRanges(ModelManager manager)
        {
            var ranges = new List<(string, List<object>)>();

            foreach (var iterator in Iterators)
            {
                var values = new List<object>();

                // Try tuple set
                if (manager.TupleSets.TryGetValue(iterator.SetName, out var tupleSet))
                {
                    values.AddRange(tupleSet.Instances.Cast<object>());
                }
                // Try computed set
                else if (manager.ComputedSets.TryGetValue(iterator.SetName, out var computedSet))
                {
                    var computed = computedSet.Evaluate(manager);
                    if (computed is IEnumerable enumerable)
                    {
                        values.AddRange(enumerable.Cast<object>());
                    }
                }
                // Try primitive set
                else if (manager.PrimitiveSets.TryGetValue(iterator.SetName, out var primitiveSet))
                {
                    values.AddRange(primitiveSet.GetAllValues());
                }
                // Try index set
                else if (manager.IndexSets.TryGetValue(iterator.SetName, out var indexSet))
                {
                    values.AddRange(indexSet.GetIndices().Cast<object>());
                }
                // Try range
                else if (manager.Ranges.TryGetValue(iterator.SetName, out var range))
                {
                    values.AddRange(range.GetValues(manager).Cast<object>());
                }
                else
                {
                    throw new InvalidOperationException($"Set '{iterator.SetName}' not found");
                }
                
                ranges.Add((iterator.VariableName, values));
            }

            return ranges;
        }

        private void SetContextParameter(ModelManager manager, string name, object value)
        {
            var param = new Parameter(name, ParameterType.String, value);
            manager.Parameters[name] = param;
        }
    }

    //public class SetIterator
    //{
    //    public string IteratorVariable { get; set; }
    //    public string SetName { get; set; }

    //    public SetIterator(string iteratorVar, string setName)
    //    {
    //        IteratorVariable = iteratorVar;
    //        SetName = setName;
    //    }
    //}
}