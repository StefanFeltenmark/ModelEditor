using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    /// <summary>
    /// Represents a forall statement with optional filters
    /// Examples:
    ///   forall(i in stations: i.isRunOfRiver == 1, n in nodes)
    ///   forall(i in I, j in J: i != j)
    /// </summary>
    public class ForallStatement
    {
        public List<ForallIterator> Iterators { get; set; } = new List<ForallIterator>();
        public Expression? Condition { get; set; }
        public ConstraintTemplate? ConstraintTemplate { get; set; }

        public string? Label { get; set; }


        /// <summary>
        /// Expands the forall into concrete constraints
        /// </summary>
        public List<LinearEquation> Expand(ModelManager manager)
        {
            var constraints = new List<LinearEquation>();

            // Generate all combinations of iterator values
            ExpandRecursive(manager, 0, new Dictionary<string, object>(), constraints);

            return constraints;
        }

        private void ExpandRecursive(ModelManager manager, int iteratorIndex, 
            Dictionary<string, object> context, List<LinearEquation> constraints)
        {
            if (iteratorIndex >= Iterators.Count)
            {
                // All iterators bound - check global condition and generate constraint
                if (EvaluateGlobalCondition(manager, context))
                {
                    var constraint = GenerateConstraint(manager, context);
                    if (constraint != null)
                    {
                        constraints.Add(constraint);
                    }
                }
                return;
            }

            var iterator = Iterators[iteratorIndex];
            var range = GetIteratorRange(manager, iterator);

            foreach (var value in range)
            {
                // Set context for this iteration
                context[iterator.VariableName] = value;
                SetTemporaryParameter(manager, iterator.VariableName, value);

                try
                {
                    // Check iterator-specific filter
                    if (iterator.Filter != null)
                    {
                        if (!EvaluateFilter(manager, iterator.Filter, context))
                        {
                            // Skip this iteration
                            continue;
                        }
                    }

                    // Recurse to next iterator
                    ExpandRecursive(manager, iteratorIndex + 1, context, constraints);
                }
                finally
                {
                    // Clean up temporary parameter
                    manager.Parameters.Remove(iterator.VariableName);
                }
            }
        }

        private bool EvaluateGlobalCondition(ModelManager manager, Dictionary<string, object> context)
        {
            if (Condition == null)
                return true;

            return EvaluateFilter(manager, Condition, context);
        }

        private bool EvaluateFilter(ModelManager manager, Expression filter, Dictionary<string, object> context)
        {
            try
            {
                if (filter is ComparisonExpression comparison)
                {
                    return EvaluateComparison(manager, comparison, context);
                }
                else if (filter is BinaryExpression binary)
                {
                    // Handle logical AND (&&)
                    if (binary.Operator == BinaryOperator.Multiply) // Sometimes && is parsed as *
                    {
                        bool left = EvaluateFilter(manager, binary.Left, context);
                        bool right = EvaluateFilter(manager, binary.Right, context);
                        return left && right;
                    }
                }

                double result = filter.Evaluate(manager);
                return Math.Abs(result - 1.0) < 1e-10; // 1.0 = true
            }
            catch
            {
                return false;
            }
        }

        private bool EvaluateComparison(ModelManager manager, ComparisonExpression comparison, 
            Dictionary<string, object> context)
        {
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
                if (context.TryGetValue(fieldAccess.VariableName, out var obj))
                {
                    if (obj is TupleInstance tuple)
                    {
                        return tuple.GetValue(fieldAccess.FieldName);
                    }
                }
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
            else if (expr is ConstantExpression constExpr)
            {
                return constExpr.Value;
            }
            else if (expr is StringConstantExpression strConst)
            {
                return strConst.Value;
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

            if (double.TryParse(v1.ToString(), out double d1) &&
                double.TryParse(v2.ToString(), out double d2))
            {
                return d1.CompareTo(d2);
            }

            return string.Compare(v1.ToString(), v2.ToString(), StringComparison.Ordinal);
        }

        private LinearEquation? GenerateConstraint(ModelManager manager, Dictionary<string, object> context)
        {
            if (ConstraintTemplate == null)
                return null;

            // Substitute all iterator variables in the constraint
            string leftExpr = SubstituteIterators(ConstraintTemplate.LeftSide.ToString(), context, manager);
            string rightExpr = SubstituteIterators(ConstraintTemplate.RightSide.ToString(), context, manager);

            // Parse as equation
            var parser = new EquationParser(manager);
            string equationStr = $"{leftExpr} {OperatorToString(ConstraintTemplate.Operator)} {rightExpr}";

            if (parser.TryParseEquation(equationStr, out var equation, out var error))
            {
                if (!string.IsNullOrEmpty(Label) && equation != null)
                {
                    // Generate indexed label: capacity_1, capacity_2, etc.
                    string indexSuffix = GenerateIndexSuffix(context);
                    equation.Label = string.IsNullOrEmpty(indexSuffix) 
                        ? Label 
                        : $"{Label}_{indexSuffix}";
                    equation.BaseName = Label;
                }

                return equation;
            }

            return null;
        }

        /// <summary>
        /// Generates an index suffix from the context (e.g., "1" or "1_2" for multi-dimensional)
        /// </summary>
        private string GenerateIndexSuffix(Dictionary<string, object> context)
        {
            var indices = new List<string>();
    
            foreach (var iterator in Iterators)
            {
                if (context.TryGetValue(iterator.VariableName, out var value))
                {
                    if (value is int intVal)
                    {
                        indices.Add(intVal.ToString());
                    }
                    else if (value is TupleInstance tuple)
                    {
                        // For tuple iterators, we might want to use a specific key field
                        // For now, use a counter or omit
                        continue;
                    }
                }
            }
    
            return string.Join("_", indices);
        }

        private string SubstituteIterators(string template, Dictionary<string, object> context, ModelManager manager)
        {
            string result = template;

            foreach (var kvp in context)
            {
                string iteratorName = kvp.Key;
                object value = kvp.Value;

                if (value is TupleInstance tuple)
                {
                    // For tuple iterators, keep as-is (already bound in parameters)
                    // Field access will work through the temporary parameter
                }
                else if (value is int intValue)
                {
                    // Simple integer substitution
                    result = System.Text.RegularExpressions.Regex.Replace(
                        result,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(iteratorName)}\b",
                        intValue.ToString()
                    );
                }
            }

            return result;
        }

        private string OperatorToString(RelationalOperator op)
        {
            return op switch
            {
                RelationalOperator.Equal => "==",
                RelationalOperator.LessThanOrEqual => "<=",
                RelationalOperator.GreaterThanOrEqual => ">=",
                RelationalOperator.LessThan => "<",
                RelationalOperator.GreaterThan => ">",
                _ => "=="
            };
        }

        private List<object> GetIteratorRange(ModelManager manager, ForallIterator iterator)
        {
            var range = new List<object>();

            // Check various sources
            if (manager.TupleSets.TryGetValue(iterator.Range.SetName ?? "", out var tupleSet))
            {
                range.AddRange(tupleSet.Instances.Cast<object>());
            }
            else if (manager.ComputedSets.TryGetValue(iterator.Range.SetName ?? "", out var computedSet))
            {
                var computed = computedSet.Evaluate(manager);
                if (computed is IEnumerable<object> enumerable)
                {
                    range.AddRange(enumerable);
                }
            }
            else if (manager.IndexSets.TryGetValue(iterator.Range.SetName ?? "", out var indexSet))
            {
                range.AddRange(indexSet.GetIndices().Cast<object>());
            }
            else if (manager.Ranges.TryGetValue(iterator.Range.SetName ?? "", out var oplRange))
            {
                range.AddRange(oplRange.GetValues(manager).Cast<object>());
            }
            else if (manager.PrimitiveSets.TryGetValue(iterator.Range.SetName ?? "", out var primitiveSet))
            {
                range.AddRange(primitiveSet.GetAllValues());
            }

            return range;
        }

        private void SetTemporaryParameter(ModelManager manager, string name, object value)
        {
            ParameterType type = value switch
            {
                int => ParameterType.Integer,
                double => ParameterType.Float,
                string => ParameterType.String,
                bool => ParameterType.Boolean,
                _ => ParameterType.String
            };

            var param = new Parameter(name, type, value);
            manager.Parameters[name] = param;
        }
    }

    /// <summary>
    /// Represents a single iterator in a forall statement
    /// </summary>
    public class ForallIterator
    {
        public string VariableName { get; set; } = "";
        public RangeExpression Range { get; set; } = new RangeExpression();
        public Expression? Filter { get; set; }

        public ForallIterator()
        {
        }

        public ForallIterator(string varName, string setName)
        {
            VariableName = varName;
            Range = new RangeExpression { SetName = setName };
        }

        public ForallIterator(string varName, string setName, Expression filter)
        {
            VariableName = varName;
            Range = new RangeExpression { SetName = setName };
            Filter = filter;
        }
    }

    public class ConstraintTemplate
    {
        public Expression LeftSide { get; set; } = new ConstantExpression(0);
        public RelationalOperator Operator { get; set; }
        public Expression RightSide { get; set; } = new ConstantExpression(0);
    }

    public class RangeExpression
    {
        public Expression? Start { get; set; }
        public Expression? End { get; set; }
        public string? SetName { get; set; }
    }
}