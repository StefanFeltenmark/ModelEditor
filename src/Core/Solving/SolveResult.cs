namespace Core.Solving
{
    public enum SolveStatus
    {
        Optimal,
        Feasible,
        Infeasible,
        Unbounded,
        Error
    }

    public class SolveResult
    {
        public SolveStatus Status { get; init; }
        public double? ObjectiveValue { get; init; }
        public Dictionary<string, double> VariableValues { get; init; } = new();
        public Dictionary<string, double> ConstraintSlacks { get; init; } = new();
        public double? MipGap { get; init; }
        public TimeSpan SolveTime { get; init; }
        public string? StatusMessage { get; init; }
    }
}
