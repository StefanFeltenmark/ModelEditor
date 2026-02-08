using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    /// <summary>
    /// Wraps an expression (typically item()) with multi-dimensional parameter context
    /// Handles iterator variable substitution during evaluation
    /// </summary>
    public class MultiDimParameterExpression : Expression
    {
        public Expression InnerExpression { get; set; }
        public List<(string? IteratorVar, string IndexSet)> Dimensions { get; set; }

        public MultiDimParameterExpression(Expression inner, List<(string?, string)> dimensions)
        {
            InnerExpression = inner;
            Dimensions = dimensions;
        }

        public override double Evaluate(ModelManager manager)
        {
            // This will be called during constraint expansion
            // The iterator variables will be substituted before evaluation
            return InnerExpression.Evaluate(manager);
        }

        /// <summary>
        /// Evaluates with specific index values
        /// </summary>
        public object EvaluateWithIndices(ModelManager manager, int[] indices)
        {
            if (indices.Length != Dimensions.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {Dimensions.Count} indices, got {indices.Length}");
            }

            // Create evaluation context with iterator bindings
            var context = new EvaluationContext();
            for (int i = 0; i < indices.Length; i++)
            {
                if (Dimensions[i].IteratorVar != null)
                {
                    context.SetIterator(Dimensions[i].IteratorVar, indices[i]);
                }
            }

            // Evaluate inner expression with context
            // This requires expressions to support EvaluationContext
            // For ItemFunctionExpression, we'll need to implement this
            
            if (InnerExpression is ItemFunctionExpression itemExpr)
            {
                return itemExpr.EvaluateWithContext(manager, context);
                        
            }

            throw new NotImplementedException(
                $"Multi-dimensional evaluation not supported for {InnerExpression.GetType().Name}");
        }

        public override string ToString()
        {
            return InnerExpression.ToString();
        }

        public override bool IsConstant => false;
    }
}