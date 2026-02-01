using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for parameter declaration parsing
    /// </summary>
    public class ParameterParsingTests : TestBase
    {
        [Fact]
        public void Parse_IntParameter_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "int T = 10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            Assert.Single(manager.Parameters);
            
            var param = manager.GetParameter("T");
            Assert.NotNull(param);
            Assert.Equal("T", param.Name);
            Assert.Equal(ParameterType.Integer, param.Type);
            Assert.Equal(10, param.Value);
        }

        [Fact]
        public void Parse_FloatParameter_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "float pi = 3.14;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var param = manager.GetParameter("pi");
            Assert.NotNull(param);
            Assert.Equal(ParameterType.Float, param.Type);
            Assert.Equal(3.14, (double)param.Value, 2);
        }

        [Fact]
        public void Parse_StringParameter_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "string name = \"test\";";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var param = manager.GetParameter("name");
            Assert.NotNull(param);
            Assert.Equal(ParameterType.String, param.Type);
            Assert.Equal("test", param.Value);
        }

        [Fact]
        public void Parse_IntExpression_ShouldEvaluate()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "int result = 5 + 3;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var param = manager.GetParameter("result");
            Assert.NotNull(param);
            Assert.Equal(8, param.Value);
        }

        [Fact]
        public void Parse_FloatExpression_ShouldEvaluate()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "float result = 2.5 * 4;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var param = manager.GetParameter("result");
            Assert.NotNull(param);
            Assert.Equal(10.0, (double)param.Value, 2);
        }

        [Fact]
        public void Parse_FloatParameterWithExpression_ShouldNotParseAsEquation()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "float result = 2.5 * 4;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            
            // Should be parsed as a parameter, not an equation
            Assert.Single(manager.Parameters);
            Assert.Empty(manager.Equations);
            
            var param = manager.GetParameter("result");
            Assert.NotNull(param);
            Assert.Equal(ParameterType.Float, param.Type);
            Assert.Equal(10.0, (double)param.Value, 2);
        }

        [Fact]
        public void Parse_IntParameterWithExpression_ShouldNotParseAsEquation()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "int count = 5 + 3 * 2;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Parameters);
            Assert.Empty(manager.Equations);
            
            var param = manager.GetParameter("count");
            Assert.Equal(11, param.Value); // 5 + (3 * 2) = 11
        }

        [Fact]
        public void Parse_ParameterVsEquation_ShouldDistinguish()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                float capacity = 100.5;
                var float x;
                var float y;
                constraint: x + y == capacity;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            // Should have 1 parameter and 1 equation
            Assert.Single(manager.Parameters);
            Assert.Single(manager.Equations);
            
            var param = manager.GetParameter("capacity");
            Assert.NotNull(param);
            Assert.Equal(100.5, (double)param.Value, 2);
            
            var equation = manager.Equations[0];
            Assert.Equal(RelationalOperator.Equal, equation.Operator);
        }

        [Fact]
        public void Parse_SingleEqualsInEquation_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            var manager = CreateModelManager();
            string input = @"
                var float x;
                var float y;
                x + y = 5;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "No relational operator (==, <=, >=, <, >) found in equation");
        }

        [Theory]
        [InlineData("int x = 10;", true)]
        [InlineData("float y = 5.5;", true)]
        [InlineData("string s = \"text\";", true)]
        [InlineData("x + y == 10;", false)]
        [InlineData("2*x + 3*y <= 5;", false)]
        public void Parse_StatementType_ShouldBeCorrect(string input, bool shouldBeParameter)
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            if (!shouldBeParameter)
            {
                // Need to declare variables for equations
                parser.Parse("var float x; var float y;");
                manager = parser.GetModelManager(); // Get updated manager
            }

            // Act
            var result = parser.Parse(input);

            // Assert
            if (shouldBeParameter)
            {
                AssertNoErrors(result);
                Assert.NotEmpty(manager.Parameters);
                Assert.Empty(manager.Equations);
            }
            else
            {
                // Should be equation
                AssertNoErrors(result);
                Assert.NotEmpty(manager.Equations);
            }
        }

        [Theory]
        [InlineData("int x = ;")]
        [InlineData("float y")]
        [InlineData("string z = unquoted")]
        [InlineData("invalid T = 10")]
        public void Parse_InvalidParameter_ShouldFail(string input)
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_MultipleParameters_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int T = 10;
                float rate = 0.5;
                string label = ""test"";
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            Assert.Equal(3, manager.Parameters.Count);
        }
    }
}