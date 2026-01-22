using Core.Models;

namespace Core
{
    /// <summary>
    /// Manages all parsed model data
    /// </summary>
    public class ModelManager
    {
        public Dictionary<string, Parameter> Parameters { get; } = new Dictionary<string, Parameter>();
        public Dictionary<string, IndexSet> IndexSets { get; } = new Dictionary<string, IndexSet>();
        public Dictionary<string, IndexedVariable> IndexedVariables { get; } = new Dictionary<string, IndexedVariable>();
        public Dictionary<string, IndexedEquation> IndexedEquationTemplates { get; } = new Dictionary<string, IndexedEquation>();
        public Dictionary<string, LinearEquation> LabeledEquations { get; } = new Dictionary<string, LinearEquation>();
        public List<LinearEquation> Equations { get; } = new List<LinearEquation>();

        public Objective? Objective { get; set; }

        public Dictionary<string, DecisionExpression> DecisionExpressions { get; } = 
            new Dictionary<string, DecisionExpression>();

        public List<AssertStatement> Assertions { get; } = new List<AssertStatement>();

        public Dictionary<string, TupleSchema> TupleSchemas { get; } = 
            new Dictionary<string, TupleSchema>();
    
        public Dictionary<string, TupleSet> TupleSets { get; private set; } = new Dictionary<string, TupleSet>();

        public void AddParameter(Parameter parameter)
        {
            if (Parameters.ContainsKey(parameter.Name))
            {
                throw new InvalidOperationException($"Parameter '{parameter.Name}' is already defined");
            }
            
            // For indexed parameters, validate that the index sets exist
            if (parameter.IsIndexed)
            {
                if (!IndexSets.ContainsKey(parameter.IndexSetName))
                {
                    throw new InvalidOperationException($"Index set '{parameter.IndexSetName}' is not defined for parameter '{parameter.Name}'");
                }
                
                if (parameter.IsTwoDimensional && !IndexSets.ContainsKey(parameter.SecondIndexSetName!))
                {
                    throw new InvalidOperationException($"Index set '{parameter.SecondIndexSetName}' is not defined for parameter '{parameter.Name}'");
                }
            }
            
            Parameters[parameter.Name] = parameter;
        }

        public void AddIndexSet(IndexSet indexSet)
        {
            IndexSets[indexSet.Name] = indexSet;
        }

        public void AddIndexedVariable(IndexedVariable variable)
        {
            IndexedVariables[variable.BaseName] = variable;
        }

        public void AddIndexedEquationTemplate(IndexedEquation equation)
        {
            IndexedEquationTemplates[equation.BaseName] = equation;
        }

        public void AddEquation(LinearEquation equation)
        {
            Equations.Add(equation);
            
            if (!string.IsNullOrEmpty(equation.Label))
            {
                LabeledEquations[equation.Label] = equation;
            }
        }

        public void SetObjective(Objective objective)
        {
            Objective = objective;
        }

        public void AddDecisionExpression(DecisionExpression dexpr)
        {
            if (DecisionExpressions.ContainsKey(dexpr.Name))
            {
                throw new InvalidOperationException($"Decision expression '{dexpr.Name}' is already defined");
            }
            
            DecisionExpressions[dexpr.Name] = dexpr;
        }

        public void AddAssertion(AssertStatement assertion)
        {
            Assertions.Add(assertion);
        }

        public void ValidateAssertions()
        {
            foreach (var assertion in Assertions)
            {
                if (!assertion.Validate(this, out string error))
                {
                    throw new InvalidOperationException($"Assertion failed: {error}");
                }
            }
        }

        public void Clear()
        {
            Parameters.Clear();
            IndexSets.Clear();
            TupleSets.Clear();
            IndexedVariables.Clear();
            Equations.Clear();
            LabeledEquations.Clear();
            IndexedEquationTemplates.Clear();
            Objective = null; 
            DecisionExpressions.Clear();
            Assertions.Clear(); 
            TupleSchemas.Clear();
            TupleSets.Clear();
            TupleSchemas.Clear();
            TupleSets.Clear();
        }

        public string GenerateParseResultsReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Parse Results ===\n");
            
            sb.AppendLine($"Parameters: {Parameters.Count}");
            foreach (var param in Parameters.Values)
            {
                sb.AppendLine($"  - {param}");
            }
            
            sb.AppendLine($"\nIndex Sets: {IndexSets.Count}");
            foreach (var indexSet in IndexSets.Values)
            {
                sb.AppendLine($"  - {indexSet}");
            }
            
            // Add tuple sets
            sb.AppendLine($"\nTuple Sets: {TupleSets.Count}");
            foreach (var tupleSet in TupleSets.Values)
            {
                sb.AppendLine($"  - {tupleSet}");
            }
            
            sb.AppendLine($"\nVariables: {IndexedVariables.Count}");
            foreach (var variable in IndexedVariables.Values)
            {
                sb.AppendLine($"  - {variable}");
            }
            
            sb.AppendLine($"\nEquations: {Equations.Count}");
            foreach (var equation in Equations)
            {
                sb.AppendLine($"  - {equation}");
            }
            
            return sb.ToString();
        }

        public Parameter? GetParameter(string name)
        {
            return Parameters.TryGetValue(name, out var parameter) ? parameter : null;
        }

        public LinearEquation? GetEquationByLabel(string label)
        {
            return LabeledEquations.TryGetValue(label, out var equation) ? equation : null;
        }

        public IndexSet? GetIndexSet(string name)
        {
            return IndexSets.TryGetValue(name, out var indexSet) ? indexSet : null;
        }

        public IndexedVariable? GetIndexedVariable(string baseName)
        {
            return IndexedVariables.TryGetValue(baseName, out var variable) ? variable : null;
        }

        public VariableType? GetVariableType(string baseName)
        {
            return IndexedVariables.TryGetValue(baseName, out var variable) ? variable.Type : null;
        }

        public List<IndexedVariable> GetVariablesByType(VariableType type)
        {
            return IndexedVariables.Values
                .Where(v => v.Type == type)
                .ToList();
        }

        public List<LinearEquation> GetEquationsByBaseName(string baseName)
        {
            return Equations
                .Where(eq => eq.BaseName == baseName)
                .ToList();
        }

        public LinearEquation? GetIndexedEquation(string baseName, int index)
        {
            return Equations
                .FirstOrDefault(eq => eq.BaseName == baseName && eq.Index == index);
        }

        public LinearEquation? GetIndexedEquation(string baseName, int index1, int index2)
        {
            return Equations
                .FirstOrDefault(eq => eq.BaseName == baseName && eq.Index == index1 && eq.SecondIndex == index2);
        }

        public double GetIndexedVariableCoefficient(LinearEquation equation, string variableName, int index)
        {
            // For indexed variables, the actual variable name is baseName + index (e.g., "x1", "x2")
            string fullVariableName = $"{variableName}{index}";
    
            // Try to get constant coefficient, otherwise evaluate with current model state
            if (equation.TryGetConstantCoefficient(fullVariableName, out double coeff))
            {
                return coeff;
            }
            else
            {
                // Need to evaluate the expression
                var (coefficients, _) = equation.Evaluate(this);
                return coefficients.TryGetValue(fullVariableName, out double value) ? value : 0.0;
            }
        }

        public void AddTupleSchema(TupleSchema schema)
        {
            if (TupleSchemas.ContainsKey(schema.Name))
            {
                throw new InvalidOperationException($"Tuple schema '{schema.Name}' is already defined");
            }
            TupleSchemas[schema.Name] = schema;
        }
    
        public void AddTupleSet(TupleSet tupleSet)
        {
            if (TupleSets.ContainsKey(tupleSet.Name))
            {
                throw new InvalidOperationException($"Tuple set '{tupleSet.Name}' already exists");
            }
            TupleSets[tupleSet.Name] = tupleSet;
        }
    }
}