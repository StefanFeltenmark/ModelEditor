using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class TupleSetTests : TestBase
    {
        [Fact]
        public void Parse_ExplicitTwoDimensionalTupleSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "tupleset Routes = {(1,2), (1,3), (2,3), (2,4)};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            Assert.Single(manager.TupleSets);
            
            var tupleSet = manager.TupleSets["Routes"];
            Assert.Equal("Routes", tupleSet.Name);
            Assert.Equal(2, tupleSet.Dimension);
            Assert.Equal(4, tupleSet.Count);
            Assert.True(tupleSet.Contains(1, 2));
            Assert.True(tupleSet.Contains(2, 4));
            Assert.False(tupleSet.Contains(3, 4));
        }

        [Fact]
        public void Parse_ExplicitThreeDimensionalTupleSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "tupleset Connections = {(1,2,3), (1,3,2), (2,1,3)};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["Connections"];
            Assert.Equal(3, tupleSet.Dimension);
            Assert.Equal(3, tupleSet.Count);
            Assert.True(tupleSet.Contains(1, 2, 3));
            Assert.False(tupleSet.Contains(1, 1, 1));
        }

        [Fact]
        public void Parse_ComputedTupleSetWithFilter_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..4;
                range J = 1..4;
                tupleset ValidPairs = {(i,j) | i in I, j in J, i < j};
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["ValidPairs"];
            Assert.Equal(2, tupleSet.Dimension);
            
            // Should contain (1,2), (1,3), (1,4), (2,3), (2,4), (3,4)
            Assert.Equal(6, tupleSet.Count);
            Assert.True(tupleSet.Contains(1, 2));
            Assert.True(tupleSet.Contains(1, 3));
            Assert.True(tupleSet.Contains(3, 4));
            Assert.False(tupleSet.Contains(2, 1)); // i < j filter
            Assert.False(tupleSet.Contains(2, 2)); // i < j filter
        }

        [Fact]
        public void Parse_ComputedTupleSetWithMultipleFilters_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                range J = 1..5;
                tupleset SpecialPairs = {(i,j) | i in I, j in J, i < j, i + j > 5};
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["SpecialPairs"];
            
            // i < j and i + j > 5
            // Valid: (1,5), (2,4), (2,5), (3,4), (3,5), (4,5)
            Assert.True(tupleSet.Contains(2, 4));
            Assert.True(tupleSet.Contains(3, 4));
            Assert.False(tupleSet.Contains(1, 2)); // sum = 3, not > 5
            Assert.False(tupleSet.Contains(2, 2)); // i not < j
        }

        [Fact]
        public void Parse_ExternalTupleSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "tupleset Arcs = ...;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["Arcs"];
            Assert.True(tupleSet.IsExternal);
            Assert.Equal(0, tupleSet.Count);
        }

        [Fact]
        public void TupleSet_ToString_ShouldFormatCorrectly()
        {
            // Arrange
            var tuples = new List<Tuple<int, int>>
            {
                Tuple.Create(1, 2),
                Tuple.Create(3, 4)
            };
            var tupleSet = new TupleSet("Test", tuples);

            // Act
            string str = tupleSet.ToString();

            // Assert
            Assert.Contains("Test", str);
            Assert.Contains("(1,2)", str);
            Assert.Contains("(3,4)", str);
        }

        [Fact]
        public void Parse_InvalidTupleFormat_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "tupleset Bad = {1,2, 3,4};"; // Missing parentheses

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Invalid tuple format");
        }

        [Fact]
        public void Parse_MixedDimensions_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "tupleset Mixed = {(1,2), (3,4,5)};"; // 2D and 3D mixed

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Invalid");
        }
    }
}