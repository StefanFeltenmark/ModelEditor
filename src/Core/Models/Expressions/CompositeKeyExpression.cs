using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    /// <summary>
    /// Represents a composite key expression: <field1, field2, field3>
    /// Used in item() function calls with multiple key parts
    /// Example: item(HydroArcTs, <s.id, t>)
    /// </summary>
    public class CompositeKeyExpression : Expression
    {
        public List<Expression> KeyParts { get; set; }

        public CompositeKeyExpression(List<Expression> keyParts)
        {
            KeyParts = keyParts;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException(
                "CompositeKeyExpression cannot be evaluated to a numeric value. " +
                "It should be used within item() function context.");
        }

        /// <summary>
        /// Evaluates all key parts and returns them as a list
        /// </summary>
        public List<object> EvaluateKeyParts(ModelManager manager)
        {
            var results = new List<object>();
            
            foreach (var part in KeyParts)
            {
                if (part is StringConstantExpression strExpr)
                {
                    results.Add(strExpr.Value);
                }
                else if (part is ConstantExpression constExpr)
                {
                    results.Add(constExpr.Value);
                }
                else if (part is DynamicTupleFieldAccessExpression fieldAccess)
                {
                    // This will be resolved in context
                    results.Add(part); // Return the expression itself
                }
                else
                {
                    // Try to evaluate as numeric
                    results.Add(part.Evaluate(manager));
                }
            }
            
            return results;
        }

        public override string ToString()
        {
            return $"<{string.Join(", ", KeyParts.Select(p => p.ToString()))}>";
        }

        public override bool IsConstant => KeyParts.All(p => p.IsConstant);
    }
}