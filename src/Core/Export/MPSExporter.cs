using System.Text;
using Core.Models;

namespace Core.Export
{
    /// <summary>
    /// Exports optimization models to MPS (Mathematical Programming System) format
    /// </summary>
    public class MPSExporter
    {
        private readonly ModelManager modelManager;
        private Dictionary<LinearEquation, string> rowNameCache = new Dictionary<LinearEquation, string>();
        const int MAX_NAME_LENGTH = 24;

        public MPSExporter(ModelManager manager)
        {
            modelManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }
        
        /// <summary>
        /// Exports the model to MPS format
        /// </summary>
        /// <param name="problemName">Name of the problem (max 8 chars for compatibility)</param>
        /// <returns>MPS format string</returns>
        public string Export(string problemName = "PROBLEM")
        {
            var sb = new StringBuilder();
            
            // **Warn if templates exist but aren't expanded**
            if (modelManager.IndexedEquationTemplates.Count > 0 || 
                modelManager.ForallStatements.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot export: Model has unexpanded templates. " +
                    "Call ExpandAllTemplates() after loading external data.");
            }
    
            // Validate model
            if (modelManager.Objective == null)
            {
                throw new InvalidOperationException("Cannot export: No objective function defined");
            }
            
            // Sanitize problem name (MPS standard: max 8 chars, no spaces)
            problemName = SanitizeName(problemName, MAX_NAME_LENGTH);
            
            // Build unique row names BEFORE generating sections
            BuildUniqueRowNames();
            
            // NAME section
            sb.AppendLine($"NAME          {problemName}");
            
            // ROWS section
            AppendRowsSection(sb);
            
            // COLUMNS section
            AppendColumnsSection(sb);
            
            // RHS section
            AppendRhsSection(sb);
            
            // BOUNDS section
            AppendBoundsSection(sb);
            
            // ENDATA marker
            sb.AppendLine("ENDATA");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Prepares the model for export by expanding all templates
        /// </summary>
        private void PrepareModelForExport()
        {
            // Expand forall statements if not already done
            if (modelManager.ForallStatements.Count > 0)
            {
                modelManager.ExpandForallStatements();
            }
            
            // Expand indexed equation templates if not already done
            if (modelManager.IndexedEquationTemplates.Count > 0)
            {
                var parser = new EquationParser(modelManager);
                var result = new ParseSessionResult();
                parser.ExpandIndexedEquations(result);
            }
        }
        
        private void BuildUniqueRowNames()
        {
            rowNameCache.Clear();
            var usedNames = new HashSet<string>();
            
            foreach (var equation in modelManager.Equations)
            {
                string baseName = equation.Label ?? equation.BaseName ?? "R";
                
                if (equation.Index.HasValue)
                {
                    if (equation.SecondIndex.HasValue)
                    {
                        baseName = $"{baseName}_{equation.Index}_{equation.SecondIndex}";
                    }
                    else
                    {
                        baseName = $"{baseName}_{equation.Index}";
                    }
                }
                
                string sanitized = SanitizeName(baseName, MAX_NAME_LENGTH);
                string uniqueName = sanitized;
                int counter = 1;
                
                // Ensure uniqueness by appending counter if needed
                while (usedNames.Contains(uniqueName))
                {
                    string suffix = $"_{counter}";
                    int maxBase = MAX_NAME_LENGTH - suffix.Length;
                    uniqueName = sanitized.Substring(0, Math.Min(sanitized.Length, maxBase)) + suffix;
                    counter++;
                }
                
                usedNames.Add(uniqueName);
                rowNameCache[equation] = uniqueName;
            }
        }
        
        private void AppendRowsSection(StringBuilder sb)
        {
            sb.AppendLine("ROWS");
            
            // Objective row (type N = free)
            string objName = modelManager.Objective?.Name ?? "OBJ";
            objName = SanitizeName(objName, MAX_NAME_LENGTH);
            sb.AppendLine($" N  {objName}");
            
            // Constraint rows
            foreach (var equation in modelManager.Equations)
            {
                string rowType = equation.Operator switch
                {
                    RelationalOperator.LessThanOrEqual => "L",
                    RelationalOperator.GreaterThanOrEqual => "G",
                    RelationalOperator.Equal => "E",
                    RelationalOperator.LessThan => "L",  // Treat < as <=
                    RelationalOperator.GreaterThan => "G", // Treat > as >=
                    _ => "E"
                };
                
                string rowName = GetRowName(equation);
                sb.AppendLine($" {rowType}  {rowName}");
            }
        }
        
        private void AppendColumnsSection(StringBuilder sb)
        {
            sb.AppendLine("COLUMNS");
            
            // Get all variables
            var allVariables = GetAllVariableNames();
            
            foreach (var varName in allVariables.OrderBy(v => v))
            {
                string colName = SanitizeName(varName, MAX_NAME_LENGTH);

                // Objective coefficient
                if (modelManager.Objective != null)
                {
                    double objCoeff = GetObjectiveCoefficient(varName);
                    if (Math.Abs(objCoeff) > 1e-10)
                    {
                        string objName = SanitizeName(modelManager.Objective.Name ?? "OBJ", MAX_NAME_LENGTH);
                        // Negate for minimization (MPS standard is minimization)
                        double mpsCoeff = modelManager.Objective.Sense == ObjectiveSense.Minimize 
                            ? objCoeff 
                            : -objCoeff;
                        sb.AppendLine($"    {colName,-10} {objName,-10} {mpsCoeff,12:G}");
                    }
                }
                
                // Constraint coefficients
                for (int i = 0; i < modelManager.Equations.Count; i++)
                {
                    var equation = modelManager.Equations[i];
                    double coeff = GetConstraintCoefficient(equation, varName);
                    
                    if (Math.Abs(coeff) > 1e-10)
                    {
                        string rowName = GetRowName(equation);
                        sb.AppendLine($"    {colName,-10} {rowName,-10} {coeff,12:G}");
                    }
                }
            }
        }
        
        private void AppendRhsSection(StringBuilder sb)
        {
            sb.AppendLine("RHS");
            
            // Use a single RHS vector name
            string rhsName = "RHS1";
            
            foreach (var equation in modelManager.Equations)
            {
                double rhsValue = equation.Constant.Evaluate(modelManager);
                
                if (Math.Abs(rhsValue) > 1e-10)
                {
                    string rowName = GetRowName(equation);
                    sb.AppendLine($"    {rhsName,-10} {rowName,-10} {rhsValue,12:G}");
                }
            }
        }
        
        private void AppendBoundsSection(StringBuilder sb)
        {
            sb.AppendLine("BOUNDS");
            
            string boundName = "BOUND1";
            var allVariables = GetAllVariableNames();
            
            foreach (var varName in allVariables.OrderBy(v => v))
            {
                string colName = SanitizeName(varName, MAX_NAME_LENGTH);
                var varInfo = GetVariableInfo(varName);
                
                if (varInfo == null)
                    continue;
                
                bool hasLower = varInfo.LowerBound.HasValue;
                bool hasUpper = varInfo.UpperBound.HasValue;
                
                if (!hasLower && !hasUpper)
                {
                    // Free variable (unbounded both ways)
                    sb.AppendLine($" FR {boundName,-10} {colName}");
                }
                else if (hasLower && !hasUpper)
                {
                    if (varInfo.LowerBound == 0)
                    {
                        // Default lower bound is 0, so PL (positive, unbounded above)
                        sb.AppendLine($" PL {boundName,-10} {colName}");
                    }
                    else
                    {
                        // Custom lower bound
                        sb.AppendLine($" LO {boundName,-10} {colName,-10} {varInfo.LowerBound,12:G}");
                    }
                }
                else if (!hasLower && hasUpper)
                {
                    if (varInfo.UpperBound == 0)
                    {
                        // Upper bound of 0
                        sb.AppendLine($" UP {boundName,-10} {colName,-10} {0,12:G}");
                        sb.AppendLine($" MI {boundName,-10} {colName}");
                    }
                    else
                    {
                        // Upper bound only (implies lower = -inf)
                        sb.AppendLine($" MI {boundName,-10} {colName}");
                        sb.AppendLine($" UP {boundName,-10} {colName,-10} {varInfo.UpperBound,12:G}");
                    }
                }
                else
                {
                    // Both bounds specified
                    sb.AppendLine($" LO {boundName,-10} {colName,-10} {varInfo.LowerBound,12:G}");
                    sb.AppendLine($" UP {boundName,-10} {colName,-10} {varInfo.UpperBound,12:G}");
                }
                
                // Integer variables
                if (varInfo.Type == VariableType.Integer)
                {
                    sb.AppendLine($" LI {boundName,-10} {colName}");
                }
            }
        }
        
        private HashSet<string> GetAllVariableNames()
        {
            var variables = new HashSet<string>();
            
            // From objective
            if (modelManager.Objective != null)
            {
                foreach (var varName in modelManager.Objective.Coefficients.Keys)
                {
                    variables.Add(varName);
                }
            }
            
            // From constraints
            foreach (var equation in modelManager.Equations)
            {
                foreach (var varName in equation.Coefficients.Keys)
                {
                    variables.Add(varName);
                }
            }
            
            return variables;
        }
        
        private double GetObjectiveCoefficient(string varName)
        {
            if (modelManager.Objective == null)
                return 0.0;
            
            if (modelManager.Objective.Coefficients.TryGetValue(varName, out var expr))
            {
                return expr.Evaluate(modelManager);
            }
            
            return 0.0;
        }
        
        private double GetConstraintCoefficient(LinearEquation equation, string varName)
        {
            if (equation.Coefficients.TryGetValue(varName, out var expr))
            {
                return expr.Evaluate(modelManager);
            }
            
            return 0.0;
        }
        
        private IndexedVariable? GetVariableInfo(string expandedName)
        {
            // Try to find the base variable
            // expandedName could be "x1", "flow1_2", etc.
            
            foreach (var variable in modelManager.IndexedVariables.Values)
            {
                if (variable.IsScalar && variable.BaseName == expandedName)
                {
                    return variable;
                }
                
                // Check if expandedName matches this variable's pattern
                if (expandedName.StartsWith(variable.BaseName))
                {
                    return variable;
                }
            }
            
            return null;
        }
        
        private string GetRowName(LinearEquation equation)
        {
            if (rowNameCache.TryGetValue(equation, out string? cachedName))
            {
                return cachedName;
            }
            
            // Fallback (shouldn't happen if BuildUniqueRowNames was called)
            string baseName = equation.Label ?? equation.BaseName ?? $"R{modelManager.Equations.IndexOf(equation)}";
            
            if (equation.Index.HasValue)
            {
                if (equation.SecondIndex.HasValue)
                {
                    baseName = $"{baseName}_{equation.Index}_{equation.SecondIndex}";
                }
                else
                {
                    baseName = $"{baseName}_{equation.Index}";
                }
            }

            return SanitizeName(baseName, MAX_NAME_LENGTH);
        }
        
        private string SanitizeName(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name))
                name = "UNNAMED";
            
            // Remove invalid characters
            name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            
            // Ensure it starts with a letter
            if (!char.IsLetter(name[0]))
                name = "V" + name;
            
            // Truncate to max length
            if (name.Length > maxLength)
                name = name.Substring(0, maxLength);
            
            return name.ToUpper();
        }
    }
}