namespace Core.Models
{
    /// <summary>
    /// Represents an angle bracket tuple key reference: <expr>
    /// Used for tuple lookups like: content[i][<n.pred>]
    /// </summary>
    public class TupleKeyExpression : Expression
    {
        public Expression InnerExpression { get; set; }

        public TupleKeyExpression(Expression inner)
        {
            InnerExpression = inner;
        }

        public override double Evaluate(ModelManager manager)
        {
            // Evaluate the inner expression to get the actual index
            return InnerExpression.Evaluate(manager);
        }

        public override string ToString()
        {
            return $"<{InnerExpression}>";
        }

        public override bool IsConstant => InnerExpression.IsConstant;
    }
}