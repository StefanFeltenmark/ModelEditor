namespace Core.Models
{
    /// <summary>
    /// Represents indexed parameter access: param[i] or param[i][j]
    /// </summary>
    public class IndexedParameterExpression : Expression
    {
        public string ParameterName { get; set; }
        public List<Expression> Indices { get; set; }

        public IndexedParameterExpression(string paramName, Expression index)
        {
            ParameterName = paramName;
            Indices = new List<Expression> { index };
        }

        public IndexedParameterExpression(string paramName, Expression index1, Expression index2)
        {
            ParameterName = paramName;
            Indices = new List<Expression> { index1, index2 };
        }

        public IndexedParameterExpression(string paramName, List<Expression> indices)
        {
            ParameterName = paramName;
            Indices = indices;
        }

        public override double Evaluate(ModelManager manager)
        {
            var param = manager.Parameters[ParameterName];
            
            if (Indices.Count == 1)
            {
                int index = (int)Indices[0].Evaluate(manager);
                var value = param.GetIndexedValue(index);
                return Convert.ToDouble(value);
            }
            else if (Indices.Count == 2)
            {
                int index1 = (int)Indices[0].Evaluate(manager);
                int index2 = (int)Indices[1].Evaluate(manager);
                var value = param.GetIndexedValue(index1, index2);
                return Convert.ToDouble(value);
            }
            else
            {
                var indexValues = Indices.Select(i => (int)i.Evaluate(manager)).ToArray();
                var value = param.GetIndexedValue(indexValues);
                return Convert.ToDouble(value);
            }
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }

        public override bool IsConstant { get; }

        public object EvaluateToObject(ModelManager manager)
        {
            var param = manager.Parameters[ParameterName];
            
            if (Indices.Count == 1)
            {
                int index = (int)Indices[0].Evaluate(manager);
                return param.GetIndexedValue(index);
            }
            else if (Indices.Count == 2)
            {
                int index1 = (int)Indices[0].Evaluate(manager);
                int index2 = (int)Indices[1].Evaluate(manager);
                return param.GetIndexedValue(index1, index2);
            }
            else
            {
                var indexValues = Indices.Select(i => (int)i.Evaluate(manager)).ToArray();
                return param.GetIndexedValue(indexValues);
            }
        }
    }

    /// <summary>
    /// Represents indexed tuple parameter access: tupleParam[i][j]
    /// </summary>
    public class IndexedTupleParameterExpression : Expression
    {
        public string ParameterName { get; set; }
        public List<Expression> Indices { get; set; }

        public IndexedTupleParameterExpression(string paramName, List<Expression> indices)
        {
            ParameterName = paramName;
            Indices = indices;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException(
                $"Indexed tuple parameter '{ParameterName}' returns a tuple, not a number");
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }

        public override bool IsConstant { get; }

        public TupleInstance EvaluateToTuple(ModelManager manager)
        {
            var tupleParam = manager.TupleParameters[ParameterName];
            var indexValues = Indices.Select(i => (int)i.Evaluate(manager)).ToArray();
            return tupleParam.GetIndexedValue(indexValues);
        }
    }

    /// <summary>
    /// Represents a reference to a tuple variable (iterator or tuple parameter)
    /// </summary>
    public class TupleVariableExpression : Expression
    {
        public string VariableName { get; set; }

        public TupleVariableExpression(string varName)
        {
            VariableName = varName;
        }

        public override double Evaluate(ModelManager manager)
        {
            throw new InvalidOperationException(
                $"Tuple variable '{VariableName}' cannot be evaluated to a number");
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }

        public override bool IsConstant { get; }
    }
}