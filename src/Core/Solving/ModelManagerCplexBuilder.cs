using Core.Models;
using Volue.Optimal.Cplex;

namespace Core.Solving
{
    /// <summary>
    /// Translates a fully-expanded ModelManager into a CplexModel for the CPLEX solver.
    /// </summary>
    internal class ModelManagerCplexBuilder : ICplexModelBuilder
    {
        private const double CplexInfinity = 1e30;

        private readonly ModelManager _manager;
        private List<string> _colToVarName = new();
        private List<string> _rowToName = new();

        public ModelManagerCplexBuilder(ModelManager manager)
        {
            _manager = manager;
        }

        public string GetVariableName(int colIndex) =>
            colIndex >= 0 && colIndex < _colToVarName.Count ? _colToVarName[colIndex] : $"var_{colIndex}";

        public string GetConstraintName(int rowIndex) =>
            rowIndex >= 0 && rowIndex < _rowToName.Count ? _rowToName[rowIndex] : $"c_{rowIndex}";

        public CplexModel? BuildModel()
        {
            var objective = _manager.Objective;
            if (objective == null)
                return null;

            var equations = _manager.Equations;

            // Collect all variable names from objective + all constraints (sorted for determinism)
            var varSet = new HashSet<string>(objective.Coefficients.Keys);
            foreach (var eq in equations)
                foreach (var k in eq.Coefficients.Keys)
                    varSet.Add(k);

            _colToVarName = varSet.OrderBy(x => x).ToList();
            var varNameToCol = _colToVarName
                .Select((name, idx) => (name, idx))
                .ToDictionary(t => t.name, t => t.idx);

            var model = new CplexModel();
            model.OptSense = objective.Sense == ObjectiveSense.Maximize
                ? CplexOptSense.Max
                : CplexOptSense.Min;

            // Add rows (all constraints must be added before columns in CSC format)
            _rowToName = new List<string>(equations.Count);
            for (int r = 0; r < equations.Count; r++)
            {
                var eq = equations[r];
                char sense = eq.Operator switch
                {
                    RelationalOperator.LessThanOrEqual => 'L',
                    RelationalOperator.LessThan => 'L',
                    RelationalOperator.GreaterThanOrEqual => 'G',
                    RelationalOperator.GreaterThan => 'G',
                    _ => 'E'
                };
                double rhs = eq.Constant.Evaluate(_manager);
                string name = eq.Label ?? eq.BaseName ?? $"c{r}";
                _rowToName.Add(name);
                model.Data.AddRow(sense, rhs, name);
            }

            // Add columns — CSC format: BeginColumn → AddColumn(rowIndices, values) → AddVariable
            for (int c = 0; c < _colToVarName.Count; c++)
            {
                string varName = _colToVarName[c];

                int col = model.BeginColumn();

                // Gather non-zero constraint coefficients for this variable
                var rowIndices = new List<int>();
                var rowValues = new List<double>();
                for (int r = 0; r < equations.Count; r++)
                {
                    if (equations[r].Coefficients.TryGetValue(varName, out var expr))
                    {
                        double v = expr.Evaluate(_manager);
                        if (Math.Abs(v) > 1e-12)
                        {
                            rowIndices.Add(r);
                            rowValues.Add(v);
                        }
                    }
                }
                model.AddColumn(rowIndices, rowValues);

                double lb = GetLowerBound(varName);
                double ub = GetUpperBound(varName);
                double objCoeff = objective.Coefficients.TryGetValue(varName, out var objExpr)
                    ? objExpr.Evaluate(_manager)
                    : 0.0;
                char type = GetVariableType(varName);

                model.AddVariable(col, lb, ub, objCoeff, type, varName);
            }

            model.UpdateProblemType();
            return model;
        }

        public void UpdateModel() { }

        // --- helpers ---

        private IndexedVariable? FindVariableInfo(string expandedName)
        {
            foreach (var v in _manager.IndexedVariables.Values)
            {
                if (v.IsScalar && v.BaseName == expandedName)
                    return v;
                if (expandedName.StartsWith(v.BaseName))
                    return v;
            }
            return null;
        }

        private double GetLowerBound(string varName)
        {
            var info = FindVariableInfo(varName);
            if (info == null) return 0.0;
            return info.LowerBound ?? 0.0;
        }

        private double GetUpperBound(string varName)
        {
            var info = FindVariableInfo(varName);
            if (info == null) return CplexInfinity;
            return info.UpperBound ?? CplexInfinity;
        }

        private char GetVariableType(string varName)
        {
            var info = FindVariableInfo(varName);
            if (info == null) return 'C';
            return info.Type switch
            {
                VariableType.Integer => 'I',
                VariableType.Boolean => 'B',
                _ => 'C'
            };
        }
    }
}
