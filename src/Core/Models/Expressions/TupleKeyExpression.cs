using System;

namespace Core.Models
{
    /// <summary>
    /// Represents an angle bracket tuple key reference: <expr>
    /// Used for tuple lookups and index resolution like: content[i][<n.pred>]
    /// </summary>
    public class TupleKeyExpression : Expression
    {
        public Expression InnerExpression { get; set; }

        public TupleKeyExpression(Expression inner)
        {
            InnerExpression = inner;
        }

        /// <summary>
        /// Evaluates the inner expression to resolve the actual index/key value
        /// </summary>
        public override double Evaluate(ModelManager manager)
        {
            // Evaluate inner expression to get the actual value
            // This resolves tuple field access like n.pred -> 1
            return InnerExpression.Evaluate(manager);
        }

        /// <summary>
        /// Gets the resolved value as an integer index
        /// </summary>
        public int GetIndexValue(ModelManager manager)
        {
            return (int)Math.Round(Evaluate(manager));
        }

        public override string ToString()
        {
            return $"<{InnerExpression}>";
        }

        public override bool IsConstant => InnerExpression.IsConstant;

        public override Expression Simplify(ModelManager? modelManager = null)
        {
            var simplified = InnerExpression.Simplify(modelManager);
            
            // If inner expression is constant, we can evaluate it
            if (simplified.IsConstant && modelManager != null)
            {
                double value = simplified.Evaluate(modelManager);
                return new ConstantExpression(value);
            }
            
            if (simplified != InnerExpression)
            {
                return new TupleKeyExpression(simplified);
            }
            
            return this;
        }
    }
}