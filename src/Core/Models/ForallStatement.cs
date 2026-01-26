namespace Core.Models
{
    /// <summary>
    /// Represents a forall loop for generating multiple constraints
    /// Example: forall(i in 1..n) constraint[i] <= capacity[i];
    /// </summary>
    public class ForallStatement
    {
        public List<ForallIterator> Iterators { get; set; } = new List<ForallIterator>();
        public Expression? Condition { get; set; }
        public ConstraintTemplate ConstraintTemplate { get; set; } = null!;
        
        public ForallStatement() { }
        
        /// <summary>
        /// Expands the forall statement into concrete constraints
        /// </summary>
        public List<LinearEquation> Expand(ModelManager modelManager)
        {
            var constraints = new List<LinearEquation>();
            var context = new Dictionary<string, int>();
            
            ExpandRecursive(modelManager, 0, context, constraints);
            
            return constraints;
        }
        
        private void ExpandRecursive(
            ModelManager modelManager, 
            int iteratorIndex, 
            Dictionary<string, int> context,
            List<LinearEquation> constraints)
        {
            if (iteratorIndex >= Iterators.Count)
            {
                // All iterators processed - check condition and generate constraint
                if (Condition == null || EvaluateCondition(context, modelManager))
                {
                    var constraint = ConstraintTemplate.Instantiate(context, modelManager);
                    if (constraint != null)
                    {
                        constraints.Add(constraint);
                    }
                }
                return;
            }
            
            var iterator = Iterators[iteratorIndex];
            var range = iterator.GetRange(modelManager);
            
            foreach (var value in range)
            {
                context[iterator.VariableName] = value;
                ExpandRecursive(modelManager, iteratorIndex + 1, context, constraints);
            }
            
            context.Remove(iterator.VariableName);
        }
        
        private bool EvaluateCondition(Dictionary<string, int> context, ModelManager modelManager)
        {
            if (Condition == null)
                return true;
            
            // Create temporary parameters for condition evaluation
            var tempParams = new Dictionary<string, double>();
            foreach (var kvp in context)
            {
                tempParams[kvp.Key] = kvp.Value;
            }
            
            // Store original parameters
            var originalParams = new Dictionary<string, double>();
            foreach (var kvp in tempParams)
            {
                if (modelManager.Parameters.TryGetValue(kvp.Key, out var param))
                {
                    originalParams[kvp.Key] = Convert.ToDouble(param.Value);
                }
            }
            
            // Set temporary values
            foreach (var kvp in tempParams)
            {
                modelManager.SetParameter(kvp.Key, kvp.Value);
            }
            
            // Evaluate condition
            double result = Condition.Evaluate(modelManager);
            
            // Restore original parameters
            foreach (var kvp in originalParams)
            {
                modelManager.SetParameter(kvp.Key, kvp.Value);
            }
            foreach (var kvp in tempParams)
            {
                if (!originalParams.ContainsKey(kvp.Key))
                {
                    modelManager.Parameters.Remove(kvp.Key);
                }
            }
            
            return Math.Abs(result - 1.0) < 1e-10; // True if result is 1.0
        }
    }
    
    /// <summary>
    /// Represents an iterator in a forall statement
    /// Example: "i in 1..n" or "j in Cities"
    /// </summary>
    public class ForallIterator
    {
        public string VariableName { get; set; } = "";
        public RangeExpression Range { get; set; } = null!;
        
        public IEnumerable<int> GetRange(ModelManager modelManager)
        {
            return Range.GetValues(modelManager);
        }
    }
    
    /// <summary>
    /// Represents a range expression (e.g., 1..n, Cities)
    /// </summary>
    public class RangeExpression
    {
        public Expression? Start { get; set; }
        public Expression? End { get; set; }
        public string? SetName { get; set; }
        
        public IEnumerable<int> GetValues(ModelManager modelManager)
        {
            if (SetName != null)
            {
                // Look up a predefined set
                if (modelManager.Sets.TryGetValue(SetName, out var set))
                {
                    return set;
                }
                throw new InvalidOperationException($"Set '{SetName}' not found");
            }
            
            if (Start != null && End != null)
            {
                int start = (int)Start.Evaluate(modelManager);
                int end = (int)End.Evaluate(modelManager);
                
                return Enumerable.Range(start, end - start + 1);
            }
            
            throw new InvalidOperationException("Invalid range expression");
        }
    }
    
    /// <summary>
    /// Template for generating constraints with index substitution
    /// </summary>
    public class ConstraintTemplate
    {
        public string? Label { get; set; }
        public Expression LeftSide { get; set; } = null!;
        public RelationalOperator Operator { get; set; }
        public Expression RightSide { get; set; } = null!;
        
        public LinearEquation? Instantiate(Dictionary<string, int> indices, ModelManager modelManager)
        {
            try
            {
                // Create context for index substitution
                var context = new IndexSubstitutionContext(indices);
                
                // Substitute indices in expressions
                var leftWithIndices = SubstituteIndices(LeftSide, context);
                var rightWithIndices = SubstituteIndices(RightSide, context);
                
                // Create the equation
                var equation = new LinearEquation
                {
                    Label = Label,
                    BaseName = Label ?? "constraint",
                    Operator = Operator
                };
                
                // Set index if single index
                if (indices.Count == 1)
                {
                    equation.Index = indices.Values.First();
                }
                else if (indices.Count == 2)
                {
                    var values = indices.Values.ToList();
                    equation.Index = values[0];
                    equation.SecondIndex = values[1];
                }
                
                // Evaluate and build the linear equation
                BuildLinearEquation(equation, leftWithIndices, rightWithIndices, modelManager);
                
                return equation;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error instantiating constraint: {ex.Message}");
                return null;
            }
        }
        
        private Expression SubstituteIndices(Expression expr, IndexSubstitutionContext context)
        {
            return expr switch
            {
                ConstantExpression constExpr => constExpr, // No substitution needed
                
                ParameterExpression paramExpr => 
                    context.TryGetIndex(paramExpr.ParameterName, out int value) 
                        ? new ConstantExpression(value) 
                        : paramExpr,
                
                VariableExpression varExpr => varExpr, // Keep as-is
                
                IndexedVariableExpression idxVarExpr => new IndexedVariableExpression(
                    idxVarExpr.BaseName,
                    SubstituteIndices(idxVarExpr.Index1, context),
                    idxVarExpr.Index2 != null ? SubstituteIndices(idxVarExpr.Index2, context) : null
                ),
                
                BinaryExpression binExpr => new BinaryExpression(
                    SubstituteIndices(binExpr.Left, context),
                    binExpr.Operator,
                    SubstituteIndices(binExpr.Right, context)
                ),
                
                UnaryExpression unaryExpr => new UnaryExpression(
                    unaryExpr.Operator,
                    SubstituteIndices(unaryExpr.Operand, context)
                ),
                
                SummationExpression sumExpr => SubstituteSummation(sumExpr, context),
                
                _ => expr // Unknown type, return as-is
            };
        }

        private Expression SubstituteSummation(SummationExpression sumExpr, IndexSubstitutionContext context)
        {
            // This is complex - for now, return as-is
            // Full implementation would need to evaluate the summation with substituted indices
            return sumExpr;
        }
        
        private void BuildLinearEquation(
            LinearEquation equation, 
            Expression left, 
            Expression right,
            ModelManager modelManager)
        {
            // Move all terms to left side: left - right = 0
            // Then extract coefficients and constant
            
            var coefficients = new Dictionary<string, Expression>();
            double constant = 0.0;
            
            ExtractTerms(left, coefficients, ref constant, 1.0, modelManager);
            ExtractTerms(right, coefficients, ref constant, -1.0, modelManager);
            
            equation.Coefficients = coefficients;
            equation.Constant = new ConstantExpression(-constant);
        }
        
        private void ExtractTerms(
            Expression expr,
            Dictionary<string, Expression> coefficients,
            ref double constant,
            double sign,
            ModelManager modelManager)
        {
            if (expr is VariableExpression varExpr)
            {
                string varName = varExpr.GetFullName();
                if (!coefficients.ContainsKey(varName))
                {
                    coefficients[varName] = new ConstantExpression(0);
                }
                
                double currentCoeff = coefficients[varName].Evaluate(modelManager);
                coefficients[varName] = new ConstantExpression(currentCoeff + sign);
            }
            else if (expr is ConstantExpression constExpr)
            {
                constant += sign * constExpr.Value;
            }
            else if (expr is BinaryExpression binExpr)
            {
                if (binExpr.Operator == BinaryOperator.Add)
                {
                    ExtractTerms(binExpr.Left, coefficients, ref constant, sign, modelManager);
                    ExtractTerms(binExpr.Right, coefficients, ref constant, sign, modelManager);
                }
                else if (binExpr.Operator == BinaryOperator.Subtract)
                {
                    ExtractTerms(binExpr.Left, coefficients, ref constant, sign, modelManager);
                    ExtractTerms(binExpr.Right, coefficients, ref constant, -sign, modelManager);
                }
                else if (binExpr.Operator == BinaryOperator.Multiply)
                {
                    // Check if it's coefficient * variable
                    if (binExpr.Left is ConstantExpression leftConst && binExpr.Right is VariableExpression rightVar)
                    {
                        string varName = rightVar.GetFullName();
                        if (!coefficients.ContainsKey(varName))
                        {
                            coefficients[varName] = new ConstantExpression(0);
                        }
                        
                        double currentCoeff = coefficients[varName].Evaluate(modelManager);
                        coefficients[varName] = new ConstantExpression(currentCoeff + sign * leftConst.Value);
                    }
                    else if (binExpr.Right is ConstantExpression rightConst && binExpr.Left is VariableExpression leftVar)
                    {
                        string varName = leftVar.GetFullName();
                        if (!coefficients.ContainsKey(varName))
                        {
                            coefficients[varName] = new ConstantExpression(0);
                        }
                        
                        double currentCoeff = coefficients[varName].Evaluate(modelManager);
                        coefficients[varName] = new ConstantExpression(currentCoeff + sign * rightConst.Value);
                    }
                    else
                    {
                        // Complex multiplication - try to evaluate
                        double value = expr.Evaluate(modelManager);
                        constant += sign * value;
                    }
                }
                else
                {
                    // Other operators - evaluate the whole expression
                    double value = expr.Evaluate(modelManager);
                    constant += sign * value;
                }
            }
            else if (expr is SummationExpression sumExpr)
            {
                // Expand summation and extract terms
                var expandedTerms = sumExpr.ExpandTerms(modelManager);
                foreach (var term in expandedTerms)
                {
                    ExtractTerms(term, coefficients, ref constant, sign, modelManager);
                }
            }
            else
            {
                // Try to evaluate as constant
                try
                {
                    double value = expr.Evaluate(modelManager);
                    constant += sign * value;
                }
                catch
                {
                    // Cannot evaluate - skip
                }
            }
        }
    }
    
    /// <summary>
    /// Context for substituting index variables in expressions
    /// </summary>
    public class IndexSubstitutionContext
    {
        public Dictionary<string, int> Indices { get; }
        
        public IndexSubstitutionContext(Dictionary<string, int> indices)
        {
            Indices = new Dictionary<string, int>(indices);
        }
        
        public bool TryGetIndex(string name, out int value)
        {
            return Indices.TryGetValue(name, out value);
        }
    }
}