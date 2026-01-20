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
    
        public Dictionary<string, TupleSet> TupleSets { get; } = 
            new Dictionary<string, TupleSet>();

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
            IndexedVariables.Clear();
            IndexedEquationTemplates.Clear();
            LabeledEquations.Clear();
            Equations.Clear();
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
            var result = new System.Text.StringBuilder();
            
            result.AppendLine($"=== Parse Results ===\n");
            
            // Show parameters
            if (Parameters.Count > 0)
            {
                result.AppendLine($"Parameters ({Parameters.Count}):");
                foreach (var param in Parameters.Values)
                {
                    result.AppendLine($"  {param}");
                }
                result.AppendLine();
            }

            // Show index sets
            if (IndexSets.Count > 0)
            {
                result.AppendLine($"Index Sets ({IndexSets.Count}):");
                foreach (var indexSet in IndexSets.Values)
                {
                    result.AppendLine($"  range {indexSet}");
                }
                result.AppendLine();
            }

            // Show variable declarations
            if (IndexedVariables.Count > 0)
            {
                result.AppendLine($"Indexed Variables ({IndexedVariables.Count}):");
                foreach (var variable in IndexedVariables.Values)
                {
                    string typeStr = variable.Type switch
                    {
                        VariableType.Float => "float",
                        VariableType.Integer => "int",
                        VariableType.Boolean => "bool",
                        _ => "float"
                    };

                    // Check if this is a scalar variable (no index set)
                    if (variable.IsScalar)
                    {
                        result.AppendLine($"  var {typeStr} {variable.BaseName} → Scalar variable");
                    }
                    else if (variable.IsTwoDimensional)
                    {
                        var indexSet1 = IndexSets[variable.IndexSetName];
                        var indexSet2 = IndexSets[variable.SecondIndexSetName!];
                        result.AppendLine($"  {variable} → Type: {typeStr}, Expands to: {variable.BaseName}[{indexSet1.StartIndex}..{indexSet1.EndIndex},{indexSet2.StartIndex}..{indexSet2.EndIndex}]");
                    }
                    else
                    {
                        var indexSet = IndexSets[variable.IndexSetName];
                        result.AppendLine($"  {variable} → Type: {typeStr}, Expands to: {variable.BaseName}[{indexSet.StartIndex}]..{variable.BaseName}[{indexSet.EndIndex}]");
                    }
                }
                result.AppendLine();
            }

            // Show indexed equation templates
            if (IndexedEquationTemplates.Count > 0)
            {
                result.AppendLine($"Indexed Equation Templates ({IndexedEquationTemplates.Count}):");
                foreach (var template in IndexedEquationTemplates.Values)
                {
                    if (template.IsTwoDimensional)
                    {
                        var indexSet1 = IndexSets[template.IndexSetName];
                        var indexSet2 = IndexSets[template.SecondIndexSetName!];
                        result.AppendLine($"  {template}");
                        result.AppendLine($"    → Expands to {indexSet1.Count * indexSet2.Count} equations");
                    }
                    else
                    {
                        var indexSet = IndexSets[template.IndexSetName];
                        result.AppendLine($"  {template}");
                        result.AppendLine($"    → Expands to {indexSet.Count} equations");
                    }
                }
                result.AppendLine();
            }

            // Show labeled equations
            if (LabeledEquations.Count > 0)
            {
                result.AppendLine($"Labeled Equations ({LabeledEquations.Count}):");
                foreach (var kvp in LabeledEquations)
                {
                    result.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
                result.AppendLine();
            }

            // Show equations
            if (Equations.Count > 0)
            {
                int equationCount = Equations.Count(eq => !eq.IsInequality());
                int inequalityCount = Equations.Count(eq => eq.IsInequality());
                int labeledCount = Equations.Count(eq => !string.IsNullOrEmpty(eq.Label));
                int indexedCount = Equations.Count(eq => eq.Index.HasValue);
                
                result.AppendLine($"All Equations & Inequalities ({Equations.Count}):");
                result.AppendLine($"  Equations: {equationCount}");
                result.AppendLine($"  Inequalities: {inequalityCount}");
                result.AppendLine($"  Labeled: {labeledCount}");
                result.AppendLine($"  Indexed: {indexedCount}\n");

                for (int i = 0; i < Equations.Count; i++)
                {
                    var eq = Equations[i];
                    result.AppendLine($"{i + 1}. {eq}");
                    result.AppendLine($"   Type: {(eq.IsInequality() ? "Inequality" : "Equation")}");
                    result.AppendLine($"   Operator: {eq.GetOperatorSymbol()}");
                    if (!string.IsNullOrEmpty(eq.Label))
                    {
                        result.AppendLine($"   Label: {eq.Label}");
                    }
                    if (eq.Index.HasValue)
                    {
                        if (eq.SecondIndex.HasValue)
                        {
                            result.AppendLine($"   Indices: [{eq.Index.Value},{eq.SecondIndex.Value}] (Base: {eq.BaseName})");
                        }
                        else
                        {
                            result.AppendLine($"   Index: {eq.Index.Value} (Base: {eq.BaseName})");
                        }
                    }
                    result.AppendLine($"   Variables: {string.Join(", ", eq.GetVariables())}");
                    result.AppendLine($"   Constant: {eq.Constant}");
                    result.AppendLine();
                }
            }

            return result.ToString();
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
            if (!TupleSchemas.ContainsKey(tupleSet.SchemaName))
            {
                throw new InvalidOperationException($"Tuple schema '{tupleSet.SchemaName}' is not defined");
            }
        
            if (TupleSets.ContainsKey(tupleSet.Name))
            {
                throw new InvalidOperationException($"Tuple set '{tupleSet.Name}' is already defined");
            }
        
            TupleSets[tupleSet.Name] = tupleSet;
        }
    }
}