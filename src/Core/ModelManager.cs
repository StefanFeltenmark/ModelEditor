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
        public List<LinearEquation> ParsedEquations { get; } = new List<LinearEquation>();

        public void AddParameter(Parameter parameter)
        {
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
            ParsedEquations.Add(equation);
            
            if (!string.IsNullOrEmpty(equation.Label))
            {
                LabeledEquations[equation.Label] = equation;
            }
        }

        public void Clear()
        {
            Parameters.Clear();
            IndexSets.Clear();
            IndexedVariables.Clear();
            IndexedEquationTemplates.Clear();
            LabeledEquations.Clear();
            ParsedEquations.Clear();
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
            if (ParsedEquations.Count > 0)
            {
                int equationCount = ParsedEquations.Count(eq => !eq.IsInequality());
                int inequalityCount = ParsedEquations.Count(eq => eq.IsInequality());
                int labeledCount = ParsedEquations.Count(eq => !string.IsNullOrEmpty(eq.Label));
                int indexedCount = ParsedEquations.Count(eq => eq.Index.HasValue);
                
                result.AppendLine($"All Equations & Inequalities ({ParsedEquations.Count}):");
                result.AppendLine($"  Equations: {equationCount}");
                result.AppendLine($"  Inequalities: {inequalityCount}");
                result.AppendLine($"  Labeled: {labeledCount}");
                result.AppendLine($"  Indexed: {indexedCount}\n");

                for (int i = 0; i < ParsedEquations.Count; i++)
                {
                    var eq = ParsedEquations[i];
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
    }
}