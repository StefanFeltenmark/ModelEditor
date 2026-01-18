using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for indexed equation parsing and expansion
    /// </summary>
    public class IndexedEquationTests : TestBase
    {
        [Fact]
        public void Parse_IndexedEquation_ShouldExpand()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                var float x[I];
                var float y[I];
                
                constraint[i in I]: x[i] + y[i] <= 10;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            
            // Should create 3 expanded equations
            Assert.Equal(3, manager.Equations.Count);
            
            for (int i = 1; i <= 3; i++)
            {
                var eq = manager.Equations[i - 1];
                Assert.Equal("constraint", eq.BaseName);
                Assert.Equal(i, eq.Index);
                Assert.Equal(RelationalOperator.LessThanOrEqual, eq.Operator);
                Assert.Equal(10.0, eq.Constant.Evaluate(manager));
            }
        }

        [Fact]
        public void Parse_IndexedEquationWithCoefficients_ShouldExpandCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                var float x[I];
                var float y[I];
                
                eq[i in I]: 2*x[i] + 3*y[i] == 15;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);
            
            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.Equations.Count);
            
            var eq1 = manager.Equations[0];
            Assert.Equal(2.0, eq1.Coefficients["x1"].Evaluate(manager));
            Assert.Equal(3.0, eq1.Coefficients["y1"].Evaluate(manager));        

            var eq2 = manager.Equations[1];
            Assert.Equal(2.0, eq2.Coefficients["x2"].Evaluate(manager));
            Assert.Equal(3.0, eq2.Coefficients["y2"].Evaluate(manager));
        }

        [Fact]
        public void Parse_IndexedEquationWithUndeclaredIndexSet_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                var float x;
                constraint[i in I]: x <= 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
            var errors = string.Join(", ", result.GetErrorMessages());
            Assert.Contains("I", errors);
        }

        [Fact]
        public void Parse_MultipleIndexedEquations_ShouldExpandAll()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                range J = 1..2;
                var float x[I];
                var float y[J];
                
                c1[i in I]: x[i] <= 10;
                c2[j in J]: y[j] >= 5;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(4, manager.Equations.Count); // 2 + 2
            
            // Check c1 equations
            Assert.Equal(2, manager.Equations.Count(e => e.BaseName == "c1"));
            // Check c2 equations
            Assert.Equal(2, manager.Equations.Count(e => e.BaseName == "c2"));
        }

        [Fact]
        public void Parse_IndexedEquationWithParameters_ShouldSubstituteCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            
            // Add parameter data
            var costParam = new Parameter("cost", ParameterType.Float, "I", isExternal: true);
            costParam.SetIndexedValue(1, 10.0);
            costParam.SetIndexedValue(2, 20.0);
            manager.Parameters.Add("cost", costParam);
            
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;                
                var float x[I];
                
                budget[i in I]: cost[i]*x[i] <= 100;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.Equations.Count);

            Assert.Equal(10.0, manager.Equations[0].Coefficients["x1"].Evaluate(manager));
            Assert.Equal(20.0, manager.Equations[1].Coefficients["x2"].Evaluate(manager));
        }
        
        [Fact]
        public void Parse_TwoDimensionalIndexedEquation_ShouldExpand()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                range J = 1..2;
                var float flow[I,J];
                
                capacity[i in I, j in J]: flow[i,j] <= 50;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(4, manager.Equations.Count); // 2x2 = 4
            
            foreach (var eq in manager.Equations)
            {
                Assert.Equal("capacity", eq.BaseName);
                Assert.NotNull(eq.Index);
                Assert.NotNull(eq.SecondIndex);
            }
        }
    }
}