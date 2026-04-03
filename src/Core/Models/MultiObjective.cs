namespace Core.Models
{
    public enum MultiObjectiveType
    {
        /// <summary>Lexicographic: optimize objectives in priority order.</summary>
        Lexicographic,
        /// <summary>Weighted sum: single combined expression (already handled by Objective).</summary>
        WeightedSum
    }

    /// <summary>
    /// Represents a multi-objective directive.
    /// OPL syntax:
    ///   minimize staticLex(cost, time);
    ///   minimize cost, time;
    /// </summary>
    public class MultiObjective
    {
        public MultiObjectiveType Type { get; }
        public ObjectiveSense Sense { get; }
        public List<Objective> Objectives { get; }

        public MultiObjective(MultiObjectiveType type, ObjectiveSense sense, List<Objective> objectives)
        {
            Type = type;
            Sense = sense;
            Objectives = objectives;
        }

        public override string ToString()
        {
            string sense = Sense == ObjectiveSense.Minimize ? "minimize" : "maximize";
            string objs = string.Join(", ", Objectives.Select(o => string.Join(" + ",
                o.Coefficients.Select(kvp => $"{kvp.Value}*{kvp.Key}"))));
            return Type == MultiObjectiveType.Lexicographic
                ? $"{sense} staticLex({objs})"
                : $"{sense} {objs}";
        }
    }
}
