using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for equation parsing
    /// </summary>
    public class EquationParsingTests : TestBase
    {
        [Fact]
        public void Parse_SimpleEquation_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "2*x + 3*y = 10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            Assert.Single(manager.ParsedEquations);
            
            var equation = manager.ParsedEquations[0];
            Assert.Equal(RelationalOperator.Equal, equation.Operator);
            Assert.Equal(10, equation.Constant);
            Assert.Equal(2, equation.GetCoefficient("x"));
            Assert.Equal(3, equation.GetCoefficient("y"));
        }

        [Fact]
        public void Parse_EquationWithDecimalCoefficients_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "1.5*x + 2.7*y = 5.3;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var equation = manager.ParsedEquations[0];
            Assert.Equal(1.5, equation.GetCoefficient("x"), 2);
            Assert.Equal(2.7, equation.GetCoefficient("y"), 2);
            Assert.Equal(5.3, equation.Constant, 2);
        }

        [Fact]
        public void Parse_LabeledEquation_ShouldStoreLabel()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "eq1: x + y = 5;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var equation = manager.GetEquationByLabel("eq1");
            Assert.NotNull(equation);
            Assert.Equal("eq1", equation.Label);
        }

        [Theory]
        [InlineData("x + y = 5", RelationalOperator.Equal)]
        [InlineData("x + y <= 5", RelationalOperator.LessThanOrEqual)]
        [InlineData("x + y >= 5", RelationalOperator.GreaterThanOrEqual)]
        [InlineData("x + y < 5", RelationalOperator.LessThan)]
        [InlineData("x + y > 5", RelationalOperator.GreaterThan)]
        public void Parse_DifferentOperators_ShouldParseCorrectly(string input, RelationalOperator expectedOp)
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            // Act
            var result = parser.Parse(input + ";");

            // Assert
            AssertNoErrors(result);
            Assert.Equal(expectedOp, manager.ParsedEquations[0].Operator);
        }

        [Fact]
        public void Parse_EquationWithImplicitCoefficient_ShouldDefaultToOne()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "x + y = 5;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var equation = manager.ParsedEquations[0];
            Assert.Equal(1, equation.GetCoefficient("x"));
            Assert.Equal(1, equation.GetCoefficient("y"));
        }

        [Fact]
        public void Parse_EquationWithNegativeCoefficients_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "-2*x + 3*y = 10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var equation = manager.ParsedEquations[0];
            Assert.Equal(-2, equation.GetCoefficient("x"));
            Assert.Equal(3, equation.GetCoefficient("y"));
        }

        [Fact]
        public void Parse_MultipleEquations_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                eq1: x + y = 5;
                eq2: 2*x - y = 3;
                eq3: x + 2*y <= 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            Assert.Equal(3, manager.ParsedEquations.Count);
            Assert.Equal(3, manager.LabeledEquations.Count);
        }

        [Theory]
        [InlineData("x +")]
        [InlineData("= 5")]
        [InlineData("x y = 5")]
        [InlineData("x + y")]
        public void Parse_InvalidEquationSyntax_ShouldFail(string input)
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse(input + ";");

            // Assert
            AssertHasError(result);
        }
    }
}