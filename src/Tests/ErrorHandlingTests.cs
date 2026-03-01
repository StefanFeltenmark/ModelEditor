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
        public void Parse_InvalidStatement_ShouldReportError()
        {
            // Arrange
            var parser = CreateParser();

            // Act
            var result = parser.Parse("invalid statement here;");

            // Assert
            AssertHasError(result);
            Assert.Equal(1, result.Errors[0].LineNumber);
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

        [Fact]
        public void Parse_ErrorLineNumbers_ShouldAccountForBlankLines()
        {
            // Arrange - mimics sudoku.mod structure with blank lines
            var parser = CreateParser();
            string input =
                "// comment\n" +         // line 1
                "\n" +                    // line 2 (blank)
                "range I = 1..9;\n" +     // line 3
                "range J = 1..9;\n" +     // line 4
                "\n" +                    // line 5 (blank)
                "\n" +                    // line 6 (blank)
                "invalid statement;\n";   // line 7

            // Act
            var result = parser.Parse(input);

            // Assert
            Assert.True(result.HasErrors);
            Assert.Equal(7, result.Errors[0].LineNumber);
        }

        [Fact]
        public void Parse_ErrorLineNumbers_WithConsecutiveBlankLines_ShouldBeAccurate()
        {
            // Arrange
            var parser = CreateParser();
            string input =
                "\n" +                    // line 1 (blank)
                "\n" +                    // line 2 (blank)
                "\n" +                    // line 3 (blank)
                "invalid statement;\n";   // line 4

            // Act
            var result = parser.Parse(input);

            // Assert
            Assert.True(result.HasErrors);
            Assert.Equal(4, result.Errors[0].LineNumber);
        }
    }
}