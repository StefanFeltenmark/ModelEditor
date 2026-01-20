namespace Core.Models
{
    public enum ObjectiveSense
    {
        Minimize,
        Maximize
    }

    public class Objective
    {
        public string? Name { get; set; }
        public ObjectiveSense Sense { get; set; }
        public Dictionary<string, Expression> Coefficients { get; set; }
        public Expression Constant { get; set; }

        public Objective(
            ObjectiveSense sense,
            Dictionary<string, Expression> coefficients,
            Expression constant,
            string? name = null)
        {
            Sense = sense;
            Coefficients = coefficients;
            Constant = constant;
            Name = name;
        }

        public override string ToString()
        {
            string senseStr = Sense == ObjectiveSense.Minimize ? "minimize" : "maximize";
            var terms = Coefficients.OrderBy(k => k.Key)
                .Select(kvp => $"{kvp.Value}*{kvp.Key}");
            
            string expr = string.Join(" + ", terms).Replace("+ -", "- ");
            
            if (Constant is ConstantExpression constExpr && Math.Abs(constExpr.Value) > 1e-10)
            {
                expr += $" + {Constant}";
            }

            string nameStr = !string.IsNullOrEmpty(Name) ? $"{Name}: " : "";
            return $"{nameStr}{senseStr} {expr};";
        }
    }
}