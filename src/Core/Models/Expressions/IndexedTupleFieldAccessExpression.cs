namespace Core.Models
{
    /// <summary>
    /// Represents indexed tuple field access: tupleSet[index].field
    /// Example: products[1].price
    /// </summary>
    public class IndexedTupleFieldAccessExpression : Expression
    {
        public string TupleSetName { get; set; }
        public int Index { get; set; }
        public string FieldName { get; set; }

        public IndexedTupleFieldAccessExpression(string tupleSetName, int index, string fieldName)
        {
            TupleSetName = tupleSetName;
            Index = index;
            FieldName = fieldName;
        }

        public override double Evaluate(ModelManager manager)
        {
            // Get the tuple set
            if (!manager.TupleSets.TryGetValue(TupleSetName, out var tupleSet))
            {
                throw new InvalidOperationException($"Tuple set '{TupleSetName}' not found");
            }

            // Get the tuple instance at the specified index
            if (Index < 0 || Index >= tupleSet.Instances.Count)
            {
                throw new InvalidOperationException(
                    $"Index {Index} is out of range for tuple set '{TupleSetName}' (count: {tupleSet.Instances.Count})");
            }

            var instance = tupleSet.Instances[Index];

            // Get the field value
            var value = instance.GetValue(FieldName);
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"Field '{FieldName}' not found in tuple instance at index {Index}");
            }

            // Convert to double
            if (value is int i) return i;
            if (value is float f) return f;
            if (value is double d) return d;
            if (value is long l) return l;

            throw new InvalidOperationException(
                $"Field '{FieldName}' has non-numeric value of type {value.GetType().Name}");
        }

        public object EvaluateToValue(ModelManager manager)
        {
            var tupleSet = manager.TupleSets[TupleSetName];
            var instance = tupleSet.Instances[Index];
            return instance.GetValue(FieldName) 
                ?? throw new InvalidOperationException($"Field '{FieldName}' not found");
        }

        public override string ToString()
        {
            return $"{TupleSetName}[{Index}].{FieldName}";
        }

        public override bool IsConstant { get; }
    }
}