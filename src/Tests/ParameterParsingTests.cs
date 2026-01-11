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