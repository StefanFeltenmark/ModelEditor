using System.Diagnostics;
using Core.Models;
using Volue.Optimal.Cplex;

namespace Core.Solving
{
    /// <summary>
    /// Orchestrates solving a fully-expanded ModelManager using the CPLEX solver.
    /// </summary>
    public class ModelSolver
    {
        public SolveResult Solve(ModelManager manager)
        {
            var sw = Stopwatch.StartNew();

            var builder = new ModelManagerCplexBuilder(manager);
            var logger = new CplexAdapterLogger();
            var extractor = new CplexModelSolutionExtractor(logger);
            var parameters = new CplexParameters
            {
                MaxSolutionTime = TimeSpan.FromMinutes(10),
                ResultOutput = false,
                ProgressOutput = false,
                ErrorOutput = true,
            };

            try
            {
                using var solver = new CplexSolver(builder, extractor, parameters, logger);
                solver.Setup();
                solver.Solve();
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new SolveResult
                {
                    Status = SolveStatus.Error,
                    StatusMessage = ex.Message,
                    SolveTime = sw.Elapsed
                };
            }

            sw.Stop();
            return BuildResult(extractor, builder, sw.Elapsed);
        }

        private static SolveResult BuildResult(
            ICplexModelSolutionExtractor ext,
            ModelManagerCplexBuilder builder,
            TimeSpan elapsed)
        {
            var status = ext.SolutionStatus switch
            {
                1 or 101 => SolveStatus.Optimal,
                2 or 102 => SolveStatus.Infeasible,
                3 => SolveStatus.Unbounded,
                103 or 105 or 107 => SolveStatus.Feasible,
                _ => SolveStatus.Error
            };

            var vars = new Dictionary<string, double>();
            if (ext.X != null)
                for (int i = 0; i < ext.X.Length; i++)
                    vars[builder.GetVariableName(i)] = ext.X[i];

            var slacks = new Dictionary<string, double>();
            if (ext.Slack != null)
                for (int i = 0; i < ext.Slack.Length; i++)
                    slacks[builder.GetConstraintName(i)] = ext.Slack[i];

            return new SolveResult
            {
                Status = status,
                ObjectiveValue = ext.ObjVal,
                MipGap = ext.MipRelGap,
                VariableValues = vars,
                ConstraintSlacks = slacks,
                SolveTime = elapsed,
                StatusMessage = $"CPLEX status {ext.SolutionStatus}"
            };
        }
    }
}
