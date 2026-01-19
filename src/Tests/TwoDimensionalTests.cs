using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for two-dimensional variable and equation parsing
    /// </summary>
    public class TwoDimensionalTests : TestBase
    {
        [Fact]
        public void Parse_TwoDimensionalVariable_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                range J = 1..2;
                var float x[I,J];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.IndexedVariables["x"];
            Assert.NotNull(variable);
            Assert.Equal("x", variable.BaseName);
            Assert.Equal("I", variable.IndexSetName);
            Assert.Equal("J", variable.SecondIndexSetName);
            Assert.True(variable.IsTwoDimensional);
            Assert.Equal(2, variable.Dimensionality);
            Assert.Equal(VariableType.Float, variable.Type);
        }

        [Fact]
        public void Parse_TwoDimensionalVariableWithType_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range Rows = 1..5;
                range Cols = 1..4;
                var int matrix[Rows,Cols];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var variable = manager.IndexedVariables["matrix"];
            Assert.Equal(VariableType.Integer, variable.Type);
            Assert.True(variable.IsTwoDimensional);
        }

        [Fact]
        public void Parse_TwoDimensionalVariable_FirstIndexSetUndeclared_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                range J = 1..2;
                var float x[I,J];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "First index set 'I' is not declared");
        }

        [Fact]
        public void Parse_TwoDimensionalVariable_SecondIndexSetUndeclared_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                range I = 1..3;
                var float x[I,J];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Second index set 'J' is not declared");
        }

        [Fact]
        public void Parse_TwoDimensionalIndexedEquation_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                range J = 1..3;
                var float x[I,J];
                
                constraint[i in I, j in J]: x[i,j] >= 0;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(6, manager.Equations.Count); // 2 * 3 = 6 equations
            
            foreach (var eq in manager.Equations)
            {
                Assert.Equal("constraint", eq.BaseName);
                Assert.NotNull(eq.Index);
                Assert.NotNull(eq.SecondIndex);
                Assert.Equal(RelationalOperator.GreaterThanOrEqual, eq.Operator);
                Assert.Equal(0.0, eq.Constant.Evaluate(manager));
            }
        }

        [Fact]
        public void Parse_TwoDimensionalEquationWithMultipleVariables_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                range J = 1..2;
                var float x[I,J];
                var float y[I,J];
                balance[i in I,j in J]: x[i,j] + 2*y[i,j] == 10;
            ";

            // Act
            var result = parser.Parse(input);

            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(4, manager.Equations.Count);
            
            // Check equation for i=1, j=2: x[1,2] + 2*y[1,2] == 10
            var eq = manager.Equations.First(e => e.Index == 1 && e.SecondIndex == 2);
            Assert.Equal(2, eq.Coefficients.Count);
            Assert.Equal(1, eq.Coefficients["x1_2"].Evaluate(manager));
            Assert.Equal(2, eq.Coefficients["y1_2"].Evaluate(manager));
            Assert.Equal(10, eq.Constant.Evaluate(manager));
        }

        [Fact]
        public void Parse_TwoDimensionalEquationWithMixedVariables_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..2;
                range J = 1..2;
                var float x[I,J];
                var float total[I];
                var float constant;
                sum[i in I,j in J]: x[i,j] + total[i] + constant == 100;
            ";

            // Act
            var result = parser.Parse(input);

            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            
            // Check equation for i=1, j=1
            var eq = manager.Equations.First(e => e.Index == 1 && e.SecondIndex == 1);
            Assert.Equal(3, eq.Coefficients.Count);
            Assert.Contains("x1_1", eq.Coefficients.Keys);
            Assert.Contains("total1", eq.Coefficients.Keys);
            Assert.Contains("constant", eq.Coefficients.Keys);
        }

        [Theory]
        [InlineData("var x[I,];")]
        [InlineData("var x[,J];")]
        [InlineData("var x[,];")]
        [InlineData("var x[I,,J];")]
        public void Parse_InvalidTwoDimensionalSyntax_ShouldFail(string input)
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_TwoDimensionalVariable_WithSpacing_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                range J = 1..2;
                var float x[I,J];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            if (result.HasErrors)
            {
                // Print errors for debugging
                foreach (var error in result.Errors)
                {
                    Console.WriteLine(error.Message);
                }
            }
            
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.IndexedVariables["x"];
            Assert.NotNull(variable);
            Assert.Equal("x", variable.BaseName);
            Assert.Equal("I", variable.IndexSetName);
            Assert.Equal("J", variable.SecondIndexSetName);
            Assert.True(variable.IsTwoDimensional);
        }

        [Theory]
        [InlineData("var float x[I,J];")]
        [InlineData("var float x[I, J];")]
        [InlineData("var float x[ I , J ];")]
        [InlineData("var float x[ I, J ];")]
        public void Parse_TwoDimensionalVariable_VariousSpacing_ShouldSucceed(string varDeclaration)
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = $@"
                range I = 1..3;
                range J = 1..2;
                {varDeclaration}
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.True(manager.IndexedVariables.ContainsKey("x"));
            Assert.True(manager.IndexedVariables["x"].IsTwoDimensional);
        }
    }
}