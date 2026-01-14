using Core;

namespace Tests
{
    /// <summary>
    /// Tests for error handling and edge cases
    /// </summary>
    public class ErrorHandlingTests : TestBase
    {
        [Fact]
        public void Parse_EmptyString_ShouldReturnError()
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse("");

            // Assert
            AssertHasError(result, "No text");
        }

        [Fact]
        public void Parse_WhitespaceOnly_ShouldReturnError()
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse("   \n\t  ");

            // Assert
            AssertHasError(result, "No text");
        }

        [Fact]
        public void Parse_CommentsOnly_ShouldReturnError()
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse("// Just comments\n// Nothing else");

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_InvalidStatement_ShouldReportError()
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse("invalid statement here;");

            // Assert
            AssertHasError(result);
            Assert.Contains("Line 1", result.Errors[0].Message);
        }

        [Fact]
        public void Parse_PartiallyValidInput_ShouldReportBothSuccessAndErrors()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int T = 10;
                invalid statement;
                range I = 1..T;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            Assert.True(result.HasSuccess);
            Assert.True(result.HasErrors);
            Assert.Equal(2, result.SuccessCount);
            Assert.Single(result.Errors);
        }

        [Fact]
        public void Parse_ForwardReferenceToUndeclaredParameter_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                range I = 1..T;
                int T = 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_DuplicateParameterNames_ShouldOverwrite()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int T = 5;
                int T = 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(10, manager.Parameters["T"]?.Value);
        }

        [Fact]
        public void Parse_DuplicateIndexSetNames_ShouldOverwrite()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                range I = 1..10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(10, manager.IndexSets["I"].EndIndex);
        }

        [Fact]
        public void Parse_MultipleSemicolons_ShouldIgnoreEmpty()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "int T = 10;;; range I = 1..5;;;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount);
        }

        [Fact]
        public void Parse_MissingSemicolon_ShouldTreatAsOneStatement()
        {
            // Arrange
            var parser = CreateParser();
            string input = "int T = 10 range I = 1..5;";

            // Act
            var result = parser.Parse(input);

            // Assert
            // This should fail because it's treated as one invalid statement
            AssertHasError(result);
        }
    }
}