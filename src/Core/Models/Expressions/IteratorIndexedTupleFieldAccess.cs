namespace Core.Models
{
    /// <summary>
    /// Represents tuple field access with an iterator variable as index
    /// Example: productData[p].price where p is an iterator
    /// </summary>
    public class IteratorIndexedTupleFieldAccessExpression : Expression
    {
        public string TupleSetName { get; set; }
        public string IteratorVariable { get; set; }
        public string FieldName { get; set; }

        public IteratorIndexedTupleFieldAccessExpression(string tupleSetName, string iteratorVar, string fieldName)
        {
            TupleSetName = tupleSetName;
            IteratorVariable = iteratorVar;
            FieldName = fieldName;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException(
                $"Cannot evaluate {TupleSetName}[{IteratorVariable}].{FieldName} without iterator context. " +
                "This expression must be evaluated during forall/sum expansion.");
        }

        /// <summary>
        /// Evaluates with a specific iterator value
        /// </summary>
        public double EvaluateWithIterator(ModelManager manager, int iteratorValue)
        {
            var tupleSet = manager.TupleSets[TupleSetName];
            
            // Map iterator value to instance index
            int instanceIndex;
            if (tupleSet.IsIndexed)
            {
                var indexSet = manager.IndexSets[tupleSet.IndexSetName!];
                instanceIndex = indexSet.GetPosition(iteratorValue);
            }
            else
            {
                instanceIndex = iteratorValue;
            }

            if (instanceIndex < 0 || instanceIndex >= tupleSet.Instances.Count)
            {
                throw new InvalidOperationException(
                    $"Iterator value {iteratorValue} maps to invalid position {instanceIndex}");
            }

            var instance = tupleSet.Instances[instanceIndex];
            var value = instance.GetValue(FieldName);

            return Convert.ToDouble(value);
        }

        /// <summary>
        /// Substitutes the iterator with a concrete value
        /// Returns a new expression with the iterator replaced
        /// </summary>
        public IndexedTupleFieldAccessExpression Substitute(int iteratorValue)
        {
            return new IndexedTupleFieldAccessExpression(TupleSetName, iteratorValue, FieldName);
        }

        public override string ToString()
        {
            return $"{TupleSetName}[{IteratorVariable}].{FieldName}";
        }

        public override bool IsConstant { get; }
    }
}