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

    public enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public enum UnaryOperator
    {
        Negate
    }
}