namespace Core.Models
{
    /// <summary>
    /// Represents a symbolic expression that can be evaluated later
    /// </summary>
    public abstract class Expression
    {
        /// <summary>
        /// Evaluates the expression given a model manager context
        /// </summary>
        public abstract double Evaluate(ModelManager modelManager);
        
        /// <summary>
        /// Returns a string representation of the expression
        /// </summary>
        public abstract override string ToString();
        
        /// <summary>
        /// Checks if this is a constant expression
        /// </summary>
        public abstract bool IsConstant { get; }
        
        /// <summary>
        /// Simplifies the expression by evaluating constant sub-expressions
        /// </summary>
        public virtual Expression Simplify(ModelManager? modelManager = null)
        {
            // Default: return as-is, override in derived classes
            return this;
        }
    }

    /// <summary>
    /// Constant numeric value
    /// </summary>
    public class ConstantExpression : Expression
    {
        public double Value { get; }
        
        public ConstantExpression(double value)
        {
            Value = value;
        }
        
        public override double Evaluate(ModelManager modelManager) => Value;
        
        public override string ToString() => Value.ToString("G");
        
        public override bool IsConstant => true;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            return this; // Already simplified
        }
    }

    /// <summary>
    /// Reference to a scalar parameter
    /// </summary>
    public class ParameterExpression : Expression
    {
        public string ParameterName { get; }
        
        public ParameterExpression(string parameterName)
        {
            ParameterName = parameterName;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            if (!modelManager.Parameters.TryGetValue(ParameterName, out var param))
            {
                throw new InvalidOperationException($"Parameter '{ParameterName}' not found");
            }
            
            if (!param.IsScalar)
            {
                throw new InvalidOperationException($"Parameter '{ParameterName}' is indexed, cannot evaluate as scalar");
            }
            
            return Convert.ToDouble(param.Value);
        }
        
        public override string ToString() => ParameterName;
        
        public override bool IsConstant => false;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            // If we have a model manager, try to evaluate the parameter
            if (modelManager != null && modelManager.Parameters.TryGetValue(ParameterName, out var param))
            {
                if (param.IsScalar && param.HasValue)
                {
                    return new ConstantExpression(Convert.ToDouble(param.Value));
                }
            }
            return this; // Keep as parameter reference
        }
    }

    /// <summary>
    /// Reference to an indexed parameter
    /// </summary>
    public class IndexedParameterExpression : Expression
    {
        public string ParameterName { get; }
        public int Index1 { get; }
        public int? Index2 { get; }
        
        public IndexedParameterExpression(string parameterName, int index1, int? index2 = null)
        {
            ParameterName = parameterName;
            Index1 = index1;
            Index2 = index2;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            if (!modelManager.Parameters.TryGetValue(ParameterName, out var param))
            {
                throw new InvalidOperationException($"Parameter '{ParameterName}' not found");
            }
            
            object? value;
            if (Index2.HasValue)
            {
                value = param.GetIndexedValue(Index1, Index2.Value);
            }
            else
            {
                value = param.GetIndexedValue(Index1);
            }
            
            if (value == null)
            {
                throw new InvalidOperationException($"Parameter '{this}' has no value assigned");
            }
            
            return Convert.ToDouble(value);
        }
        
        public override string ToString() => Index2.HasValue 
            ? $"{ParameterName}[{Index1},{Index2}]" 
            : $"{ParameterName}[{Index1}]";
        
        public override bool IsConstant => false;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            // If we have a model manager, try to evaluate the parameter
            if (modelManager != null && modelManager.Parameters.TryGetValue(ParameterName, out var param))
            {
                object? value = Index2.HasValue 
                    ? param.GetIndexedValue(Index1, Index2.Value)
                    : param.GetIndexedValue(Index1);
                
                if (value != null)
                {
                    return new ConstantExpression(Convert.ToDouble(value));
                }
            }
            return this; // Keep as parameter reference
        }
    }

    /// <summary>
    /// Binary operation expression (addition, subtraction, multiplication, division)
    /// </summary>
    public class BinaryExpression : Expression
    {
        public Expression Left { get; }
        public Expression Right { get; }
        public BinaryOperator Operator { get; }
        
        public BinaryExpression(Expression left, BinaryOperator op, Expression right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            double leftValue = Left.Evaluate(modelManager);
            double rightValue = Right.Evaluate(modelManager);
            
            return Operator switch
            {
                BinaryOperator.Add => leftValue + rightValue,
                BinaryOperator.Subtract => leftValue - rightValue,
                BinaryOperator.Multiply => leftValue * rightValue,
                BinaryOperator.Divide => leftValue / rightValue,
                _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
            };
        }
        
        public override string ToString()
        {
            string opStr = Operator switch
            {
                BinaryOperator.Add => "+",
                BinaryOperator.Subtract => "-",
                BinaryOperator.Multiply => "*",
                BinaryOperator.Divide => "/",
                _ => "?"
            };
            
            return $"({Left} {opStr} {Right})";
        }
        
        public override bool IsConstant => Left.IsConstant && Right.IsConstant;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            // Recursively simplify operands first
            var simplifiedLeft = Left.Simplify(modelManager);
            var simplifiedRight = Right.Simplify(modelManager);
            
            // If both operands are constants, evaluate immediately
            if (simplifiedLeft is ConstantExpression leftConst && 
                simplifiedRight is ConstantExpression rightConst)
            {
                double result = Operator switch
                {
                    BinaryOperator.Add => leftConst.Value + rightConst.Value,
                    BinaryOperator.Subtract => leftConst.Value - rightConst.Value,
                    BinaryOperator.Multiply => leftConst.Value * rightConst.Value,
                    BinaryOperator.Divide => leftConst.Value / rightConst.Value,
                    _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
                };
                return new ConstantExpression(result);
            }
            
            // Special simplifications
            if (simplifiedLeft is ConstantExpression left)
            {
                // 0 + x = x
                if (Operator == BinaryOperator.Add && Math.Abs(left.Value) < 1e-10)
                    return simplifiedRight;
                
                // 0 * x = 0
                if (Operator == BinaryOperator.Multiply && Math.Abs(left.Value) < 1e-10)
                    return new ConstantExpression(0);
                
                // 1 * x = x
                if (Operator == BinaryOperator.Multiply && Math.Abs(left.Value - 1.0) < 1e-10)
                    return simplifiedRight;
            }
            
            if (simplifiedRight is ConstantExpression right)
            {
                // x + 0 = x
                if (Operator == BinaryOperator.Add && Math.Abs(right.Value) < 1e-10)
                    return simplifiedLeft;
                
                // x * 0 = 0
                if (Operator == BinaryOperator.Multiply && Math.Abs(right.Value) < 1e-10)
                    return new ConstantExpression(0);
                
                // x * 1 = x
                if (Operator == BinaryOperator.Multiply && Math.Abs(right.Value - 1.0) < 1e-10)
                    return simplifiedLeft;
                
                // x - 0 = x
                if (Operator == BinaryOperator.Subtract && Math.Abs(right.Value) < 1e-10)
                    return simplifiedLeft;
            }
            
            // Return simplified binary expression if we can't reduce further
            if (simplifiedLeft != Left || simplifiedRight != Right)
            {
                return new BinaryExpression(simplifiedLeft, Operator, simplifiedRight);
            }
            
            return this;
        }
    }

    /// <summary>
    /// Unary operation (negation)
    /// </summary>
    public class UnaryExpression : Expression
    {
        public Expression Operand { get; }
        public UnaryOperator Operator { get; }
        
        public UnaryExpression(UnaryOperator op, Expression operand)
        {
            Operator = op;
            Operand = operand;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            double value = Operand.Evaluate(modelManager);
            
            return Operator switch
            {
                UnaryOperator.Negate => -value,
                _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
            };
        }
        
        public override string ToString() => Operator switch
        {
            UnaryOperator.Negate => $"-{Operand}",
            _ => $"?{Operand}"
        };
        
        public override bool IsConstant => Operand.IsConstant;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            var simplifiedOperand = Operand.Simplify(modelManager);
            
            // If operand is constant, evaluate immediately
            if (simplifiedOperand is ConstantExpression constExpr)
            {
                double result = Operator switch
                {
                    UnaryOperator.Negate => -constExpr.Value,
                    _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
                };
                return new ConstantExpression(result);
            }
            
            // Return simplified unary expression
            if (simplifiedOperand != Operand)
            {
                return new UnaryExpression(Operator, simplifiedOperand);
            }
            
            return this;
        }
    }

    /// <summary>
    /// Represents accessing a field from a tuple instance
    /// Example: productData[i].cost
    /// </summary>
    public class TupleFieldAccessExpression : Expression
    {
        public string TupleSetName { get; }
        public int Index { get; }
        public string FieldName { get; }

        public TupleFieldAccessExpression(string tupleSetName, int index, string fieldName)
        {
            TupleSetName = tupleSetName;
            Index = index;
            FieldName = fieldName;
        }

        public override double Evaluate(ModelManager modelManager)
        {
            if (!modelManager.TupleSets.TryGetValue(TupleSetName, out var tupleSet))
            {
                throw new InvalidOperationException($"Tuple set '{TupleSetName}' not found");
            }

            if (Index < 1 || Index > tupleSet.Instances.Count)
            {
                throw new InvalidOperationException($"Tuple index {Index} out of range for set '{TupleSetName}' (size: {tupleSet.Instances.Count})");
            }

            var instance = tupleSet.Instances[Index - 1]; // 1-based indexing
            var value = instance.GetValue(FieldName);

            if (value == null)
            {
                throw new InvalidOperationException($"Field '{FieldName}' not found in tuple '{TupleSetName}'");
            }

            return Convert.ToDouble(value);
        }

        public override string ToString() => $"{TupleSetName}[{Index}].{FieldName}";

        public override bool IsConstant => true; // Tuple data is constant

        public override Expression Simplify(ModelManager? modelManager = null)
        {
            if (modelManager != null)
            {
                try
                {
                    return new ConstantExpression(Evaluate(modelManager));
                }
                catch
                {
                    // If evaluation fails, return as-is
                }
            }
            return this;
        }
    }

    /// <summary>
    /// Represents an item() lookup expression for tuples
    /// Example: item(Products, <1, "Widget">)
    /// </summary>
    public class ItemExpression : Expression
    {
        public string TupleSetName { get; }
        public List<object> KeyValues { get; }
        
        public ItemExpression(string tupleSetName, List<object> keyValues)
        {
            TupleSetName = tupleSetName;
            KeyValues = keyValues ?? new List<object>();
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            throw new InvalidOperationException(
                "item() expressions return tuple instances, not numeric values. Use field access instead.");
        }
        
        /// <summary>
        /// Gets the tuple instance referenced by this item expression
        /// </summary>
        public TupleInstance? GetTupleInstance(ModelManager modelManager)
        {
            if (!modelManager.TupleSets.TryGetValue(TupleSetName, out var tupleSet))
                throw new InvalidOperationException($"Tuple set '{TupleSetName}' not found");
            
            if (!modelManager.TupleSchemas.TryGetValue(tupleSet.SchemaName, out var schema))
                throw new InvalidOperationException($"Tuple schema '{tupleSet.SchemaName}' not found");
            
            return tupleSet.FindByKey(schema, KeyValues.ToArray());
        }
        
        public override string ToString() => 
            $"item({TupleSetName}, <{string.Join(", ", KeyValues)}>)";
        
        public override bool IsConstant => true;
        
        public override Expression Simplify(ModelManager? modelManager = null) => this;
    }

    /// <summary>
    /// Represents accessing a field from an item() expression
    /// Example: item(Products, <1>).price
    /// </summary>
    public class ItemFieldAccessExpression : Expression
    {
        public ItemExpression ItemExpression { get; }
        public string FieldName { get; }
        
        public ItemFieldAccessExpression(ItemExpression itemExpr, string fieldName)
        {
            ItemExpression = itemExpr;
            FieldName = fieldName;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            var instance = ItemExpression.GetTupleInstance(modelManager);
            
            if (instance == null)
                throw new InvalidOperationException(
                    $"No tuple found for {ItemExpression}");
            
            var value = instance.GetValue(FieldName);
            
            if (value == null)
                throw new InvalidOperationException(
                    $"Field '{FieldName}' not found in tuple");
            
            return Convert.ToDouble(value);
        }
        
        public override string ToString() => $"{ItemExpression}.{FieldName}";
        
        public override bool IsConstant => true;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            if (modelManager != null)
            {
                try
                {
                    return new ConstantExpression(Evaluate(modelManager));
                }
                catch
                {
                    // If evaluation fails, return as-is
                }
            }
            return this;
        }
    }

    public enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual
    }

    public enum UnaryOperator
    {
        Negate
    }

    /// <summary>
    /// Comparison expression (binary)
    /// </summary>
    public class ComparisonExpression : Expression
    {
        public Expression Left { get; set; }
        public BinaryOperator Operator { get; set; }
        public Expression Right { get; set; }
        
        public ComparisonExpression(Expression left, BinaryOperator op, Expression right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            double leftValue = Left.Evaluate(modelManager);
            double rightValue = Right.Evaluate(modelManager);
            
            bool result = Operator switch
            {
                BinaryOperator.Equal => Math.Abs(leftValue - rightValue) < 1e-10,
                BinaryOperator.NotEqual => Math.Abs(leftValue - rightValue) >= 1e-10,
                BinaryOperator.LessThan => leftValue < rightValue,
                BinaryOperator.LessThanOrEqual => leftValue <= rightValue,
                BinaryOperator.GreaterThan => leftValue > rightValue,
                BinaryOperator.GreaterThanOrEqual => leftValue >= rightValue,
                _ => throw new InvalidOperationException($"Invalid comparison operator: {Operator}")
            };
            
            return result ? 1.0 : 0.0;
        }
        
        public override string ToString() => $"({Left} {Operator} {Right})";
        
        public override bool IsConstant => Left.IsConstant && Right.IsConstant;
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            // Recursively simplify operands first
            var simplifiedLeft = Left.Simplify(modelManager);
            var simplifiedRight = Right.Simplify(modelManager);
            
            // If both operands are constants, evaluate immediately
            if (simplifiedLeft is ConstantExpression leftConst && 
                simplifiedRight is ConstantExpression rightConst)
            {
                double result = Operator switch
                {
                    BinaryOperator.Equal => leftConst.Value == rightConst.Value ? 1.0 : 0.0,
                    BinaryOperator.NotEqual => leftConst.Value != rightConst.Value ? 1.0 : 0.0,
                    BinaryOperator.LessThan => leftConst.Value < rightConst.Value ? 1.0 : 0.0,
                    BinaryOperator.LessThanOrEqual => leftConst.Value <= rightConst.Value ? 1.0 : 0.0,
                    BinaryOperator.GreaterThan => leftConst.Value > rightConst.Value ? 1.0 : 0.0,
                    BinaryOperator.GreaterThanOrEqual => leftConst.Value >= rightConst.Value ? 1.0 : 0.0,
                    _ => throw new InvalidOperationException($"Invalid comparison operator: {Operator}")
                };
                return new ConstantExpression(result);
            }
            
            // Return simplified comparison expression if we can't reduce further
            if (simplifiedLeft != Left || simplifiedRight != Right)
            {
                return new ComparisonExpression(simplifiedLeft, Operator, simplifiedRight);
            }
            
            return this;
        }
    }

    /// <summary>
    /// Represents a summation expression: sum(i in Set) expression[i]
    /// </summary>
    public class SummationExpression : Expression
    {
        public string IndexVariable { get; set; }
        public string SetName { get; set; }
        public Expression Body { get; set; }
        
        public SummationExpression(string indexVariable, string setName, Expression body)
        {
            IndexVariable = indexVariable;
            SetName = setName;
            Body = body;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            double sum = 0.0;
            
            // Get the set to iterate over
            IEnumerable<int> range = GetRange(modelManager);
            
            // Store original parameter value if exists
            bool hadOriginal = modelManager.Parameters.TryGetValue(IndexVariable, out var originalParam);
            double originalValue = hadOriginal ? Convert.ToDouble(originalParam.Value) : 0;
            
            try
            {
                // Evaluate body for each value in the range
                foreach (int value in range)
                {
                    // Set the index variable as a temporary parameter
                    modelManager.SetParameter(IndexVariable, value);
                    
                    // Evaluate the body
                    sum += Body.Evaluate(modelManager);
                }
            }
            finally
            {
                // Restore original parameter or remove temporary one
                if (hadOriginal)
                {
                    modelManager.SetParameter(IndexVariable, originalValue);
                }
                else
                {
                    modelManager.Parameters.Remove(IndexVariable);
                }
            }
            
            return sum;
        }
        
        public override string ToString() => $"sum({IndexVariable} in {SetName}) {Body}";
        
        public override bool IsConstant => false; // Summations depend on runtime evaluation
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            // Simplify the body
            var simplifiedBody = Body.Simplify(modelManager);
            
            // If we have a model manager, we could potentially evaluate the entire summation
            if (modelManager != null)
            {
                try
                {
                    return new ConstantExpression(Evaluate(modelManager));
                }
                catch
                {
                    // If evaluation fails, return with simplified body
                }
            }
            
            // Return with simplified body if changed
            if (simplifiedBody != Body)
            {
                return new SummationExpression(IndexVariable, SetName, simplifiedBody);
            }
            
            return this;
        }
        
        private IEnumerable<int> GetRange(ModelManager modelManager)
        {
            // Try to get from Sets dictionary
            if (modelManager.Sets.TryGetValue(SetName, out var set))
            {
                return set;
            }
            
            // Try to get from IndexSets
            if (modelManager.IndexSets.TryGetValue(SetName, out var indexSet))
            {
                return indexSet.GetIndices();
            }
            
            throw new InvalidOperationException($"Set or IndexSet '{SetName}' not found");
        }
        
        /// <summary>
        /// Expands the summation into individual terms
        /// </summary>
        public List<Expression> ExpandTerms(ModelManager modelManager)
        {
            var terms = new List<Expression>();
            IEnumerable<int> range = GetRange(modelManager);
            
            foreach (int value in range)
            {
                // Create context for index substitution
                var context = new IndexSubstitutionContext(new Dictionary<string, int> 
                { 
                    { IndexVariable, value } 
                });
                
                // Substitute the index in the body
                var substitutedBody = SubstituteIndex(Body, context);
                terms.Add(substitutedBody);
            }
            
            return terms;
        }
        
        private Expression SubstituteIndex(Expression expr, IndexSubstitutionContext context)
        {
            return expr switch
            {
                ConstantExpression constExpr => constExpr,
                
                ParameterExpression paramExpr => 
                    context.TryGetIndex(paramExpr.ParameterName, out int value) 
                        ? new ConstantExpression(value) 
                        : paramExpr,
                
                VariableExpression varExpr => varExpr,
                
                IndexedVariableExpression idxVarExpr => new IndexedVariableExpression(
                    idxVarExpr.BaseName,
                    SubstituteIndex(idxVarExpr.Index1, context),
                    idxVarExpr.Index2 != null ? SubstituteIndex(idxVarExpr.Index2, context) : null
                ),
                
                BinaryExpression binExpr => new BinaryExpression(
                    SubstituteIndex(binExpr.Left, context),
                    binExpr.Operator,
                    SubstituteIndex(binExpr.Right, context)
                ),
                
                UnaryExpression unaryExpr => new UnaryExpression(
                    unaryExpr.Operator,
                    SubstituteIndex(unaryExpr.Operand, context)
                ),
                
                SummationExpression sumExpr => sumExpr, // Keep nested summations as-is for now
                
                _ => expr
            };
        }
    }

    /// <summary>
    /// Represents a variable with dynamic indices (e.g., x[i], flow[i][j])
    /// Used in forall templates where indices are iterator variables
    /// </summary>
    public class IndexedVariableExpression : Expression
    {
        public string BaseName { get; set; }
        public Expression Index1 { get; set; }
        public Expression? Index2 { get; set; }
        
        public IndexedVariableExpression(string baseName, Expression index1, Expression? index2 = null)
        {
            BaseName = baseName;
            Index1 = index1;
            Index2 = index2;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            // Evaluate the indices to get concrete values
            int idx1 = (int)Math.Round(Index1.Evaluate(modelManager));
            
            string varName;
            if (Index2 != null)
            {
                int idx2 = (int)Math.Round(Index2.Evaluate(modelManager));
                varName = $"{BaseName}{idx1}_{idx2}";
            }
            else
            {
                varName = $"{BaseName}{idx1}";
            }
            
            // For templates, we don't actually evaluate the variable value
            // This is used for building the constraint structure
            return 0.0;
        }
        
        public override string ToString()
        {
            if (Index2 != null)
            {
                return $"{BaseName}[{Index1}][{Index2}]";
            }
            else
            {
                return $"{BaseName}[{Index1}]";
            }
        }
        
        public override bool IsConstant => false; // Variable references are not constant
        
        public override Expression Simplify(ModelManager? modelManager = null)
        {
            // Simplify indices
            var simplifiedIndex1 = Index1.Simplify(modelManager);
            var simplifiedIndex2 = Index2?.Simplify(modelManager);
            
            // If indices changed, return new expression
            if (simplifiedIndex1 != Index1 || simplifiedIndex2 != Index2)
            {
                return new IndexedVariableExpression(BaseName, simplifiedIndex1, simplifiedIndex2);
            }
            
            return this;
        }
        
        /// <summary>
        /// Gets the full variable name by evaluating indices
        /// </summary>
        public string GetFullName(ModelManager modelManager)
        {
            int idx1 = (int)Math.Round(Index1.Evaluate(modelManager));
            
            if (Index2 != null)
            {
                int idx2 = (int)Math.Round(Index2.Evaluate(modelManager));
                return $"{BaseName}{idx1}_{idx2}";
            }
            else
            {
                return $"{BaseName}{idx1}";
            }
        }
    }

    /// <summary>
    /// Represents a reference to a decision variable
    /// </summary>
    public class VariableExpression : Expression
    {
        public string VariableName { get; }
        
        public VariableExpression(string variableName)
        {
            VariableName = variableName;
        }
        
        public override double Evaluate(ModelManager modelManager)
        {
            // Variables don't have runtime values during model building
            // This is used for structure, not evaluation
            return 0.0;
        }
        
        public override string ToString() => VariableName;
        
        public override bool IsConstant => false;
        
        public override Expression Simplify(ModelManager? modelManager = null) => this;
        
        /// <summary>
        /// Gets the full variable name
        /// </summary>
        public string GetFullName() => VariableName;
    }

   
}