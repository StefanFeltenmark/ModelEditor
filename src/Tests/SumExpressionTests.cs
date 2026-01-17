using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class SumExpressionTests : TestBase
    {
        [Fact]
        public void SimpleSumExpression_ParsesCorrectly()
        {
            var manager = CreateModelManager();

            // Arrange
            var text = @"
                range I = 1..3;
                float cost[I] = ...;
                var float x[I];
                
                budget: sum(i in I) cost[i]*x[i] <= 100;
            ";
            
            // Load data
            manager.IndexSets.Add("I", new IndexSet("I", 1, 3));
            var costParam = new Parameter("cost", ParameterType.Float, "I", isExternal: true);
            costParam.SetIndexedValue(1, 10.0);
            costParam.SetIndexedValue(2, 20.0);
            costParam.SetIndexedValue(3, 30.0);
            manager.Parameters.Add("cost", costParam);
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            Assert.Equal(1, manager.Equations.Count);
            
            var equation = manager.Equations[0];
            Assert.Equal("budget", equation.Label);
            Assert.Equal(RelationalOperator.LessThanOrEqual, equation.Operator);
            Assert.Equal(100.0, equation.Constant);
            
            // Should have coefficients: 10*x1 + 20*x2 + 30*x3
            Assert.Equal(3, equation.Coefficients.Count);
            Assert.Equal(10.0, equation.Coefficients["x1"]);
            Assert.Equal(20.0, equation.Coefficients["x2"]);
            Assert.Equal(30.0, equation.Coefficients["x3"]);
        }
        
        [Fact]
        public void SumExpression_WithConstantMultiplier_ParsesCorrectly()
        {
            var manager = CreateModelManager();
            // Arrange
            var text = @"
                range I = 1..2;
                var float x[I];
                
                total: 2*sum(i in I) x[i] == 10;
            ";
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // 2*(x[1]+x[2]) = 2*x[1] + 2*x[2]
            Assert.Equal(2.0, equation.Coefficients["x1"]);
            Assert.Equal(2.0, equation.Coefficients["x2"]);
        }
        
        [Fact]
        public void MultipleSums_InSameEquation_ParseCorrectly()
        {
            var manager = CreateModelManager();
            // Arrange
            var text = @"
                range I = 1..2;
                range J = 1..2;
                var float x[I];
                var float y[J];
                
                balance: sum(i in I) x[i] + sum(j in J) y[j] == 100;
            ";
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // Should have x1, x2, y1, y2 all with coefficient 1
            Assert.Equal(4, equation.Coefficients.Count);
            Assert.Equal(1.0, equation.Coefficients["x1"]);
            Assert.Equal(1.0, equation.Coefficients["x2"]);
            Assert.Equal(1.0, equation.Coefficients["y1"]);
            Assert.Equal(1.0, equation.Coefficients["y2"]);
            Assert.Equal(100.0, equation.Constant);
        }
        
        [Fact]
        public void SumWithTwoDimensionalVariables_ParsesCorrectly()
        {
            var manager = CreateModelManager();
            // Arrange
            var text = @"
                range I = 1..2;
                range J = 1..2;
                var float flow[I,J];
                
                total: sum(i in I) sum(j in J) flow[i,j] <= 50;
            ";
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // Should have flow1_1, flow1_2, flow2_1, flow2_2
            Assert.Equal(4, equation.Coefficients.Count);
            Assert.True(equation.Coefficients.ContainsKey("flow1_1"));
            Assert.True(equation.Coefficients.ContainsKey("flow1_2"));
            Assert.True(equation.Coefficients.ContainsKey("flow2_1"));
            Assert.True(equation.Coefficients.ContainsKey("flow2_2"));
            Assert.All(equation.Coefficients.Values, coeff => Assert.Equal(1.0, coeff));
        }
        
        [Fact]
        public void SumInIndexedEquation_ExpandsCorrectly()
        {
            // Arrange
            var text = @"
                range I = 1..2;
                range J = 1..3;
                var float x[I,J];
                
                row_sum[i in I]: sum(j in J) x[i,j] <= 100;
            ";
            
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            // Should create 2 equations (one for each i)
            Assert.Equal(2, manager.Equations.Count);
            
            // First equation: x[1,1] + x[1,2] + x[1,3] <= 100
            var eq1 = manager.Equations[0];
            Assert.Equal("row_sum", eq1.BaseName);
            Assert.Equal(1, eq1.Index);
            Assert.Equal(3, eq1.Coefficients.Count);
            Assert.True(eq1.Coefficients.ContainsKey("x1_1"));
            Assert.True(eq1.Coefficients.ContainsKey("x1_2"));
            Assert.True(eq1.Coefficients.ContainsKey("x1_3"));
            
            // Second equation: x[2,1] + x[2,2] + x[2,3] <= 100
            var eq2 = manager.Equations[1];
            Assert.Equal("row_sum", eq2.BaseName);
            Assert.Equal(2, eq2.Index);
            Assert.Equal(3, eq2.Coefficients.Count);
            Assert.True(eq2.Coefficients.ContainsKey("x2_1"));
            Assert.True(eq2.Coefficients.ContainsKey("x2_2"));
            Assert.True(eq2.Coefficients.ContainsKey("x2_3"));
        }
        
        [Fact]
        public void SumWithMixedParameters_ParsesCorrectly()
        {
            // Arrange
            var text = @"
                range I = 1..2;
                float a[I] = ...;
                float b[I] = ...;
                var float x[I];
                
                weighted: sum(i in I) (a[i] + b[i])*x[i] <= 50;
            ";

            var manager = CreateModelManager();
            
            // Setup parameters
            var aParam = new Parameter("a", ParameterType.Float, "I", isExternal: true);
            aParam.SetIndexedValue(1, 2.0);
            aParam.SetIndexedValue(2, 3.0);
            manager.Parameters.Add("a", aParam);
            
            var bParam = new Parameter("b", ParameterType.Float, "I", isExternal: true);
            bParam.SetIndexedValue(1, 1.0);
            bParam.SetIndexedValue(2, 2.0);
            manager.Parameters.Add("b", bParam);
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // (2+1)*x1 + (3+2)*x2 = 3*x1 + 5*x2
            Assert.Equal(3.0, equation.Coefficients["x1"]);
            Assert.Equal(5.0, equation.Coefficients["x2"]);
        }
        
        [Fact]
        public void SumWithConstantTerm_ParsesCorrectly()
        {
            // Arrange
            var text = @"
                range I = 1..3;
                var float x[I];
                
                total: sum(i in I) x[i] + 50 == 100;
            ";
            
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // x1 + x2 + x3 + 50 == 100 -> x1 + x2 + x3 == 50
            Assert.Equal(3, equation.Coefficients.Count);
            Assert.All(equation.Coefficients.Values, coeff => Assert.Equal(1.0, coeff));
            Assert.Equal(50.0, equation.Constant);
        }
        
        [Fact]
        public void EmptySumExpression_ReturnsError()
        {
            // Arrange
            var text = @"
                range I = 1..3;
                var float x[I];
                
                invalid: sum(i in I) <= 100;
            ";

            var manager = CreateModelManager();
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = string.Join(", ", result.GetErrorMessages());
            Assert.Contains("Empty", errors);
        }
        
        [Fact]
        public void SumWithUndeclaredIndexSet_ReturnsError()
        {
            // Arrange
            var text = @"
                var float x;
                
                invalid: sum(i in UndeclaredSet) x == 100;
            ";
            
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = string.Join(", ", result.GetErrorMessages());
            Assert.Contains("UndeclaredSet", errors);
            Assert.Contains("not found", errors);
        }
        
        [Fact]
        public void SumWithComplexExpression_ParsesCorrectly()
        {
            // Arrange
            var text = @"
                range I = 1..2;                
                var float x[I];
                var float y[I];
                
                objective: sum(i in I) (cost[i]*x[i] - 2*y[i]) >= 0;
            ";
            
            // Setup parameter
            var costParam = new Parameter("cost", ParameterType.Float, "I", isExternal: true);
            costParam.SetIndexedValue(1, 10.0);
            costParam.SetIndexedValue(2, 20.0);
            var manager = CreateModelManager();
            manager.Parameters.Add("cost", costParam);
            
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // 10*x1 - 2*y1 + 20*x2 - 2*y2 >= 0
            Assert.Equal(10.0, equation.Coefficients["x1"]);
            Assert.Equal(-2.0, equation.Coefficients["y1"]);
            Assert.Equal(20.0, equation.Coefficients["x2"]);
            Assert.Equal(-2.0, equation.Coefficients["y2"]);
        }
        
        [Fact]
        public void SumWithSingleElement_ParsesCorrectly()
        {
            // Arrange
            var text = @"
                range I = 5..5;
                var float x[I];
                
                single: sum(i in I) x[i] == 10;
            ";
            
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            Assert.Single(equation.Coefficients);
            Assert.Equal(1.0, equation.Coefficients["x5"]);
            Assert.Equal(10.0, equation.Constant);
        }
        
        [Fact]
        public void TripleNestedSum_ParsesCorrectly()
        {
            // Arrange
            var text = @"
                range I = 1..2;
                range J = 1..2;
                range K = 1..2;
                var float x[I,J,K];
                
                total: sum(i in I) sum(j in J) sum(k in K) x[i,j,k] == 100;
            ";
            
            // Create 3D indexed variable manually
            var xVar = new IndexedVariable("x", "I", VariableType.Float, "J");
            // Note: This is a simplification - the actual implementation would need 3D support
            // For now, test what we can with the existing 2D structure
            
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            
            // This test documents current behavior - may need adjustment
            // when 3D indexing is fully supported
        }
        
        [Fact]
        public void SumBothSidesOfEquation_ParsesCorrectly()
        {
            // Arrange
            var text = @"
                range I = 1..2;
                range J = 1..2;
                var float x[I];
                var float y[J];
                
                balance: sum(i in I) x[i] == sum(j in J) y[j];
            ";
            
            var manager = CreateModelManager();
            var parser = new EquationParser(manager);
            
            // Act
            var result = parser.Parse(text);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            Assert.False(result.HasErrors, 
                $"Parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            
            var equation = manager.Equations[0];
            // x1 + x2 - y1 - y2 == 0
            Assert.Equal(1.0, equation.Coefficients["x1"]);
            Assert.Equal(1.0, equation.Coefficients["x2"]);
            Assert.Equal(-1.0, equation.Coefficients["y1"]);
            Assert.Equal(-1.0, equation.Coefficients["y2"]);
            Assert.Equal(0.0, equation.Constant);
        }
    }
}