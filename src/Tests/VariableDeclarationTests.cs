using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for variable declaration parsing
    /// </summary>
    public class VariableDeclarationTests : TestBase
    {
        [Fact]
        public void Parse_ScalarVariableWithType_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            Assert.Single(manager.IndexedVariables);
            
            var variable = manager.IndexedVariables["x"];
            Assert.NotNull(variable);
            Assert.Equal("x", variable.BaseName);
            Assert.Equal(string.Empty, variable.IndexSetName);
            Assert.Equal(VariableType.Float, variable.Type);
        }

        [Fact]
        public void Parse_ScalarVariableWithoutType_DefaultsToFloat()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var x;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var variable = manager.IndexedVariables["x"];
            Assert.NotNull(variable);
            Assert.Equal(VariableType.Float, variable.Type);
        }

        [Theory]
        [InlineData("var float x;", VariableType.Float)]
        [InlineData("var int y;", VariableType.Integer)]
        [InlineData("var bool z;", VariableType.Boolean)]
        public void Parse_ScalarVariableWithDifferentTypes_ShouldHaveCorrectType(string input, VariableType expectedType)
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var variable = manager.IndexedVariables.Values.First();
            Assert.Equal(expectedType, variable.Type);
        }

        [Fact]
        public void Parse_IndexedVariableWithType_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..10;
                var float x[I];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount);
            
            var variable = manager.IndexedVariables["x"];
            Assert.NotNull(variable);
            Assert.Equal("x", variable.BaseName);
            Assert.Equal("I", variable.IndexSetName);
            Assert.Equal(VariableType.Float, variable.Type);
        }

        [Fact]
        public void Parse_IndexedVariableWithoutType_DefaultsToFloat()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                var x[I];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var variable = manager.IndexedVariables["x"];
            Assert.NotNull(variable);
            Assert.Equal(VariableType.Float, variable.Type);
        }

        [Fact]
        public void Parse_IndexedVariableWithUndeclaredIndexSet_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x[I];";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "not declared");
        }

        [Fact]
        public void Parse_MultipleVariables_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..10;
                var float x;
                var int y[I];
                var bool z;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(4, result.SuccessCount);
            Assert.Equal(3, manager.IndexedVariables.Count);
            
            Assert.Equal(VariableType.Float, manager.IndexedVariables["x"].Type);
            Assert.Equal(VariableType.Integer, manager.IndexedVariables["y"].Type);
            Assert.Equal(VariableType.Boolean, manager.IndexedVariables["z"].Type);
        }

        [Theory]
        [InlineData("var;")]
        [InlineData("var [I];")]
        [InlineData("var x[];")]
        [InlineData("float x;")]
        public void Parse_InvalidVariableSyntax_ShouldFail(string input)
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void GetVariablesByType_ShouldFilterCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                var float x;
                var int y;
                var float z;
                var bool w;
            ";

            // Act
            parser.Parse(input);
            var floatVars = manager.GetVariablesByType(VariableType.Float);
            var intVars = manager.GetVariablesByType(VariableType.Integer);
            var boolVars = manager.GetVariablesByType(VariableType.Boolean);

            // Assert
            Assert.Equal(2, floatVars.Count);
            Assert.Single(intVars);
            Assert.Single(boolVars);
        }
    }
}