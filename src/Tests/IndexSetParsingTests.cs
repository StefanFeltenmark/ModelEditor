using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for index set (range) declaration parsing
    /// </summary>
    public class IndexSetParsingTests : TestBase
    {
        [Fact]
        public void Parse_SimpleRange_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "range I = 1..10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            Assert.Single(manager.IndexSets);
            
            var indexSet = manager.IndexSets["I"];
            Assert.NotNull(indexSet);
            Assert.Equal("I", indexSet.Name);
            Assert.Equal(1, indexSet.StartIndex);
            Assert.Equal(10, indexSet.EndIndex);
        }

        [Fact]
        public void Parse_RangeWithParameter_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int T = 5;
                range I = 1..T;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount);
            
            var indexSet = manager.IndexSets["I"];
            Assert.NotNull(indexSet);
            Assert.Equal(1, indexSet.StartIndex);
            Assert.Equal(5, indexSet.EndIndex);
        }

        [Fact]
        public void Parse_RangeWithParameterExpression_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int N = 10;
                range I = 1..N;
                range J = 2..N;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            Assert.Equal(2, manager.IndexSets.Count);
        }

        [Fact]
        public void Parse_RangeWithInvalidStart_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "range I = invalid..10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_RangeWithInvalidEnd_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "range I = 1..invalid;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_RangeStartGreaterThanEnd_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "range I = 10..5;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "greater than");
        }

        [Fact]
        public void Parse_RangeWithUndeclaredParameter_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "range I = 1..T;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Theory]
        [InlineData("range I 1..10")]
        [InlineData("range = 1..10")]
        [InlineData("range I = 1-10")]
        [InlineData("I = 1..10")]
        public void Parse_InvalidRangeSyntax_ShouldFail(string input)
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }
    }
}