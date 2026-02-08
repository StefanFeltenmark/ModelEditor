using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// Represents an indexed collection of computed sets
    /// Example: {Station} stationsInGroup[g in groups] = {...}
    /// </summary>
    public class IndexedComputedSet : ComputedSet
    {
        public string IndexVariable { get; set; }
        public string IndexSetName { get; set; }

        public IndexedComputedSet(string name, string elementType, string indexVar, string indexSet,
            ComputedSet baseComprehension)
            : base(name, elementType, baseComprehension.Iterators, baseComprehension.OutputExpression,
                  baseComprehension.Condition, baseComprehension.IsProjection)
        {
            IndexVariable = indexVar;
            IndexSetName = indexSet;
        }

        /// <summary>
        /// Evaluates the indexed set for a specific index value
        /// </summary>
        public object EvaluateForIndex(ModelManager manager, object indexValue)
        {
            // Set the index variable as a parameter
            var indexParam = new Parameter(IndexVariable, ParameterType.String, indexValue);
            var hadOriginal = manager.Parameters.TryGetValue(IndexVariable, out var originalParam);

            try
            {
                manager.Parameters[IndexVariable] = indexParam;
                return Evaluate(manager);
            }
            finally
            {
                if (hadOriginal && originalParam != null)
                {
                    manager.Parameters[IndexVariable] = originalParam;
                }
                else
                {
                    manager.Parameters.Remove(IndexVariable);
                }
            }
        }
    }
}