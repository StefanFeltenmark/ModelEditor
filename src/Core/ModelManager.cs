using Core.Models;

namespace Core
{
    /// <summary>
    /// Manages all parsed model data
    /// </summary>
    public class ModelManager
    {
        public List<LinearEquation> ParsedEquations { get; private set; } = new List<LinearEquation>();
        public Dictionary<string, LinearEquation> LabeledEquations { get; private set; } = new Dictionary<string, LinearEquation>();
        public Dictionary<string, IndexSet> IndexSets { get; private set; } = new Dictionary<string, IndexSet>();
        public Dictionary<string, IndexedVariable> IndexedVariables { get; private set; } = new Dictionary<string, IndexedVariable>();
        public Dictionary<string, IndexedEquation> IndexedEquationTemplates { get; private set; } = new Dictionary<string, IndexedEquation>();
        public Dictionary<string, Parameter> Parameters { get; private set; } = new Dictionary<string, Parameter>();

        public void Clear()
        {
            ParsedEquations.Clear();
            LabeledEquations.Clear();
            IndexSets.Clear();
            IndexedVariables.Clear();
            IndexedEquationTemplates.Clear();
            Parameters.Clear();
        }

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

        public LinearEquation? GetEquationByLabel(string label)
        {
            return LabeledEquations.TryGetValue(label, out var equation) ? equation : null;
        }

        public Parameter? GetParameter(string name)
        {
            return Parameters.TryGetValue(name, out var param) ? param : null;
        }

        public List<LinearEquation> GetEquationsByBaseName(string baseName)
        {
            return ParsedEquations.Where(eq => eq.BaseName == baseName).OrderBy(eq => eq.Index).ToList();
        }

        public LinearEquation? GetIndexedEquation(string baseName, int index)
        {
            return ParsedEquations.FirstOrDefault(eq => eq.BaseName == baseName && eq.Index == index);
        }

        public List<IndexedVariable> GetVariablesByType(VariableType type)
        {
            return IndexedVariables.Values.Where(v => v.Type == type).ToList();
        }

        public VariableType? GetVariableType(string variableName)
        {
            return IndexedVariables.TryGetValue(variableName, out var variable) ? variable.Type : null;
        }

        public double GetIndexedVariableCoefficient(LinearEquation equation, string variableName, int index)
        {
            string expandedName = $"{variableName}{index}";
            return equation.GetCoefficient(expandedName);
        }

        public List<int> GetUsedIndices(LinearEquation equation, string variableName)
        {
            var indices = new List<int>();
            
            foreach (var varKey in equation.GetVariables())
            {
                if (varKey.StartsWith(variableName))
                {
                    string indexPart = varKey.Substring(variableName.Length);
                    if (int.TryParse(indexPart, out int index))
                    {
                        indices.Add(index);
                    }
                }
            }
            
            return indices.OrderBy(i => i).ToList();
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
                    var indexSet = IndexSets[variable.IndexSetName];
                    string typeStr = variable.Type switch
                    {
                        VariableType.Float => "float",
                        VariableType.Integer => "int",
                        VariableType.Boolean => "bool",
                        _ => "float"
                    };
                    result.AppendLine($"  {variable} → Type: {typeStr}, Expands to: {variable.BaseName}[{indexSet.StartIndex}]..{variable.BaseName}[{indexSet.EndIndex}]");
                }
                result.AppendLine();
            }

            // Show indexed equation templates
            if (IndexedEquationTemplates.Count > 0)
            {
                result.AppendLine($"Indexed Equation Templates ({IndexedEquationTemplates.Count}):");
                foreach (var template in IndexedEquationTemplates.Values)
                {
                    var indexSet = IndexSets[template.IndexSetName];
                    result.AppendLine($"  {template}");
                    result.AppendLine($"    → Expands to {indexSet.Count} equations");
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
                        result.AppendLine($"   Index: {eq.Index.Value} (Base: {eq.BaseName})");
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