using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    /// <summary>
    /// Summation with multiple iterators and optional filter
    /// Example: sum(i in Set1, j in Set2: i != j) expr[i][j]
    /// </summary>
    public class FilteredSummationExpression : Expression
    {
        public List<(string varName, string setName)> Iterators { get; set; }
        public Expression? Filter { get; set; }
        public Expression Body { get; set; }

        public FilteredSummationExpression(List<(string, string)> iterators, Expression? filter, Expression body)
        {
            Iterators = iterators;
            Filter = filter;
            Body = body;
        }

        public override double Evaluate(ModelManager manager)
        {
            double sum = 0.0;
            ExpandRecursive(manager, 0, new Dictionary<string, object>(), ref sum);
            return sum;
        }

        private void ExpandRecursive(ModelManager manager, int iteratorIndex, 
            Dictionary<string, object> context, ref double sum)
        {
            if (iteratorIndex >= Iterators.Count)
            {
                // All iterators bound - check filter
                if (Filter != null)
                {
                    try
                    {
                        double filterResult = Filter.Evaluate(manager);
                        // 0 = false, non-zero = true
                        if (Math.Abs(filterResult) < 1e-10)
                        {
                            return; // Filter failed, skip this combination
                        }
                    }
                    catch
                    {
                        return; // Filter evaluation failed, skip
                    }
                }
                
                // Evaluate body and add to sum
                try
                {
                    sum += Body.Evaluate(manager);
                }
                catch
                {
                    // Skip invalid evaluations
                }
                return;
            }

            var (varName, setName) = Iterators[iteratorIndex];
            var range = GetRange(manager, setName);

            foreach (var value in range)
            {
                context[varName] = value;
                SetTemporaryParameter(manager, varName, value);
                
                try
                {
                    ExpandRecursive(manager, iteratorIndex + 1, context, ref sum);
                }
                finally
                {
                    manager.Parameters.Remove(varName);
                }
            }
        }

        private List<int> GetRange(ModelManager manager, string setName)
        {
            // Try range first
            if (manager.Ranges.TryGetValue(setName, out var range))
            {
                return range.GetValues(manager).ToList();
            }
            
            // Try index set
            if (manager.IndexSets.TryGetValue(setName, out var indexSet))
            {
                return indexSet.GetIndices().ToList();
            }
            
            // Try primitive set
            if (manager.PrimitiveSets.TryGetValue(setName, out var primitiveSet))
            {
                if (primitiveSet.Type == PrimitiveSetType.Int)
                {
                    return primitiveSet.GetIntValues().ToList();
                }
            }
            
            return new List<int>();
        }

        private void SetTemporaryParameter(ModelManager manager, string name, object value)
        {
            ParameterType type = value switch
            {
                int => ParameterType.Integer,
                double => ParameterType.Float,
                string => ParameterType.String,
                bool => ParameterType.Boolean,
                _ => ParameterType.Integer
            };

            var param = new Parameter(name, type, value);
            manager.Parameters[name] = param;
        }

        public override string ToString()
        {
            var iters = string.Join(", ", Iterators.Select(i => $"{i.varName} in {i.setName}"));
            var filterStr = Filter != null ? $": {Filter}" : "";
            return $"sum({iters}{filterStr}) {Body}";
        }

        public override bool IsConstant => false;

        public override Expression Simplify(ModelManager? modelManager = null)
        {
            var simplifiedBody = Body.Simplify(modelManager);
            var simplifiedFilter = Filter?.Simplify(modelManager);

            return new FilteredSummationExpression(Iterators, simplifiedFilter, simplifiedBody);
        }
    }
}