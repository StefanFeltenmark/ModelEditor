using Core;

namespace Tests
{
    /// <summary>
    /// Tests for comment handling
    /// </summary>
    public class CommentHandlingTests : TestBase
    {
        [Fact]
        public void Parse_SingleLineComment_ShouldIgnore()
        {
            // Arrange
            var parser = CreateParser();
            string input = "// This is a comment";

            // Act
            var result = parser.Parse(input);

            // Assert
            // Should have error because there's no actual content
            AssertHasError(result);
        }

        [Fact]
        public void Parse_InlineComment_ShouldParseBeforeComment()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "int T = 10; // This is the value";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(10, manager.GetParameter("T")?.Value);
        }

        [Fact]
        public void Parse_MultipleCommentsAndCode_ShouldParseCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                // Parameter definitions
                int T = 10; // Time periods
                // Index set
                range I = 1..T; // Period indices
                // Variables
                var x; // Decision variable
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
        }

        [Fact]
        public void Parse_CommentWithSpecialCharacters_ShouldNotInterfere()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "int T = 10; // Special chars: = < > [ ] ; {}";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
        }
    }
}