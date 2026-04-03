namespace Core.Models
{
    public enum LogicalConstraintType
    {
        /// <summary>(C1) || (C2) — at least one must hold</summary>
        Disjunctive,
        /// <summary>condition => constraint — if condition holds, constraint must hold</summary>
        Implication,
        /// <summary>bVar == 1 => constraint — indicator constraint</summary>
        Indicator
    }

    /// <summary>
    /// Represents a logical constraint that cannot be expressed as a single LinearEquation.
    /// Examples:
    ///   (x >= 10) || (y >= 5)           — Disjunctive
    ///   (demand[i] > 0) => supply >= 0  — Implication
    ///   b == 1 => x <= capacity         — Indicator
    /// </summary>
    public class LogicalConstraint
    {
        public LogicalConstraintType Type { get; }

        /// <summary>Left-hand constraint (or indicator variable expression for Indicator type).</summary>
        public LinearEquation Left { get; }

        /// <summary>Right-hand constraint. For Implication/Indicator, the constraint that must hold when Left is satisfied.</summary>
        public LinearEquation Right { get; }

        /// <summary>Optional label.</summary>
        public string? Label { get; set; }

        public LogicalConstraint(LogicalConstraintType type, LinearEquation left, LinearEquation right, string? label = null)
        {
            Type = type;
            Left = left;
            Right = right;
            Label = label;
        }

        public override string ToString()
        {
            return Type switch
            {
                LogicalConstraintType.Disjunctive => $"({Left}) || ({Right})",
                LogicalConstraintType.Implication => $"({Left}) => ({Right})",
                LogicalConstraintType.Indicator   => $"({Left}) => ({Right})",
                _ => $"{Left} ? {Right}"
            };
        }
    }
}
