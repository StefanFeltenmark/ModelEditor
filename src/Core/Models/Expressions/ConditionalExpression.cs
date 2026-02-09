using System;

namespace Core.Models
{
    /// <summary>
    /// Represents a conditional expression: if(condition) { trueValue } else { falseValue }
    /// Also supports ternary operator: (condition) ? trueValue : falseValue
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
            
            // Treat non-zero as true, zero as false
            bool isTrue = Math.Abs(conditionResult - 1.0) < 1e-10 || conditionResult != 0.0;

            return isTrue
                ? TrueValue.Evaluate(manager)
                : FalseValue.Evaluate(manager);
        }

        public override string ToString()
        {
            return $"if({Condition}) {{ {TrueValue} }} else {{ {FalseValue} }}";
        }

        public override bool IsConstant => 
            Condition.IsConstant && TrueValue.IsConstant && FalseValue.IsConstant;

        public override Expression Simplify(ModelManager? modelManager = null)
        {
            var simplifiedCondition = Condition.Simplify(modelManager);
            var simplifiedTrue = TrueValue.Simplify(modelManager);
            var simplifiedFalse = FalseValue.Simplify(modelManager);

            // If condition is constant, we can evaluate at compile time
            if (simplifiedCondition.IsConstant && modelManager != null)
            {
                double condValue = simplifiedCondition.Evaluate(modelManager);
                bool isTrue = Math.Abs(condValue - 1.0) < 1e-10 || condValue != 0.0;
                return isTrue ? simplifiedTrue : simplifiedFalse;
            }

            return new ConditionalExpression(simplifiedCondition, simplifiedTrue, simplifiedFalse);
        }
    }
}