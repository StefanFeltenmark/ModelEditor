using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for validation of identifiers in equations
    /// </summary>
    public class ValidationTests : TestBase
    {

        [Fact]
        public void Parse_ConsecutiveIdentifiersWithoutOperator_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
        var x;
        var y;
        
        invalid: x y == 10;
    ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Consecutive identifiers");
        }

        [Fact]
        public void Parse_IdentifiersWithOperator_ShouldSucceed()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
        var x;
        var y;
        
        valid: x * y == 10;
    ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
        }

    }
}