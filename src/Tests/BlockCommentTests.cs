using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class BlockCommentTests : TestBase
    {
        [Fact]
        public void Parse_SimpleBlockComment_ShouldBeIgnored()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                /* This is a block comment */
                int x = 5;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Parameters);
            Assert.Equal(5, manager.Parameters["x"].Value);
        }

        [Fact]
        public void Parse_MultiLineBlockComment_ShouldBeIgnored()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                /*
                 * This is a multi-line
                 * block comment
                 */
                int x = 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Parameters);
        }

        [Fact]
        public void Parse_BlockCommentInMiddle_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int a = 1;
                /* Comment in the middle */
                int b = 2;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.Parameters.Count);
            Assert.Equal(1, manager.Parameters["a"].Value);
            Assert.Equal(2, manager.Parameters["b"].Value);
        }

        [Fact]
        public void Parse_MultipleBlockComments_ShouldAllBeRemoved()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                /* First comment */
                int x = 5;
                /* Second comment */
                int y = 10;
                /* Third comment */
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.Parameters.Count);
        }

        [Fact]
        public void Parse_NestedSlashInBlockComment_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                /* This comment has a / slash and // single-line comment inside */
                int x = 5;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Parameters);
        }

        [Fact]
        public void Parse_BlockCommentWithCode_CodeShouldBeIgnored()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int active = 1;
                /*
                int disabled = 999;
                var float broken;
                */
                int another = 2;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.Parameters.Count);
            Assert.False(manager.Parameters.ContainsKey("disabled"));
            Assert.False(manager.IndexedVariables.ContainsKey("broken"));
        }

        [Fact]
        public void Parse_MixedCommentTypes_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                // Single line comment
                int x = 1;
                /* Block comment */
                int y = 2; // Another single line
                /* Multi-line
                   block comment
                */
                int z = 3;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, manager.Parameters.Count);
        }

        [Fact]
        public void Parse_BlockCommentAtEndOfLine_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int x = 5; /* inline comment */
                int y = 10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, manager.Parameters.Count);
        }

        [Fact]
        public void Parse_UnclosedBlockComment_ShouldIgnoreRestOfFile()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int x = 1;
                /* This comment is never closed
                int y = 2;
                int z = 3;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            // Only x should be parsed
            Assert.Single(manager.Parameters);
            Assert.Equal(1, manager.Parameters["x"].Value);
        }

        [Fact]
        public void Parse_BlockCommentInExpression_ShouldNotBreakParsing()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                dvar float x[I];
                /* Cost constraint */
                constraint: sum(i in I) x[i] <= 100;
            ";

            // Act
            var result = parser.Parse(input);
            parser.ExpandIndexedEquations(result);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Equations);
        }

        [Fact]
        public void Parse_EmptyBlockComment_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                /**/
                int x = 5;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.Parameters);
        }
    }
}