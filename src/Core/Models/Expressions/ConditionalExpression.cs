namespace Core.Models
{
    /// <summary>
    /// Represents a conditional expression: if(condition) { trueValue } else { falseValue }
    /// Used in OPL-style constraints
    /// </summary>
    public class ConditionalExpression : Expression
    {
        public Expression Condition { get; set; }
        public Expression TrueValue { get; set; }
        public Expression FalseValue { get; set; }

        public ConditionalExpression(Expression condition, Expression trueValue, Expression falseValue)
        {
            Condition = condition;
            TrueValue = trueValue;
            FalseValue = falseValue;
        }

        public override double Evaluate(ModelManager manager)
        {
            double conditionResult = Condition.Evaluate(manager);
            bool isTrue = Math.Abs(conditionResult - 1.0) < 1e-10; // 1.0 = true

            return isTrue
                ? TrueValue.Evaluate(manager)
                : FalseValue.Evaluate(manager);
        }

        public override string ToString()
        {
            return $"if({Condition}) {{ {TrueValue} }} else {{ {FalseValue} }}";
        }

        public override bool IsConstant => Condition.IsConstant && TrueValue.IsConstant && FalseValue.IsConstant;
    }
}