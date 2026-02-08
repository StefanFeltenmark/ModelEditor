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
                        if (Math.Abs(filterResult - 1.0) > 1e-10) // Not true
                        {
                            return; // Skip this combination
                        }
                    }
                    catch
                    {
                        return; // Filter failed, skip
                    }
                }
                
                // Evaluate body and add to sum
                sum += Body.Evaluate(manager);
                return;
            }

            var (varName, setName) = Iterators[iteratorIndex];
            var range = GetRange(manager, setName);

            foreach (var value in range)
            {
                context[varName] = value;
                manager.SetParameter(varName, value is int i ? i : 0);
                
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
            if (manager.Ranges.TryGetValue(setName, out var range))
            {
                return range.GetValues(manager).ToList();
            }
            if (manager.IndexSets.TryGetValue(setName, out var indexSet))
            {
                return indexSet.GetIndices().ToList();
            }
            return new List<int>();
        }

        public override string ToString()
        {
            var iters = string.Join(", ", Iterators.Select(i => $"{i.varName} in {i.setName}"));
            var filterStr = Filter != null ? $": {Filter}" : "";
            return $"sum({iters}{filterStr}) {Body}";
        }

        public override bool IsConstant => false;
    }
}