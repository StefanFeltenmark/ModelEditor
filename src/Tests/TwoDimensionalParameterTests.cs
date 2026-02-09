using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class TwoDimensionalParameterTests : TestBase
    {
        [Fact]
        public void TwoDimensionalParameter_MatrixAssignment_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..3;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            var modelResult = parser.Parse(modelText);
            Assert.False(modelResult.HasErrors, 
                $"Model parsing failed: {string.Join(", ", modelResult.GetErrorMessages())}");
            
            // Act
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[10, 20],
                          [30, 40],
                          [50, 60]];
            ";
            
            var dataResult = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(dataResult.HasErrors,
                $"Data parsing failed: {string.Join(", ", dataResult.GetErrorMessages())}");
            
            var param = manager.Parameters["matrix"];
            Assert.True(param.IsTwoDimensional);
            
            // Verify all values
            Assert.Equal(10.0, Convert.ToDouble(param.GetIndexedValue(1, 1)));
            Assert.Equal(20.0, Convert.ToDouble(param.GetIndexedValue(1, 2)));
            Assert.Equal(30.0, Convert.ToDouble(param.GetIndexedValue(2, 1)));
            Assert.Equal(40.0, Convert.ToDouble(param.GetIndexedValue(2, 2)));
            Assert.Equal(50.0, Convert.ToDouble(param.GetIndexedValue(3, 1)));
            Assert.Equal(60.0, Convert.ToDouble(param.GetIndexedValue(3, 2)));
        }

        [Fact]
        public void TwoDimensionalParameter_CompactMatrixFormat_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..3;
                int data[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Compact format on single line
            var dataParser = new DataFileParser(manager);
            string dataText = "data = [[1, 2, 3], [4, 5, 6]];";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["data"];
            Assert.Equal(1, param.GetIndexedValue(1, 1));
            Assert.Equal(2, param.GetIndexedValue(1, 2));
            Assert.Equal(3, param.GetIndexedValue(1, 3));
            Assert.Equal(4, param.GetIndexedValue(2, 1));
            Assert.Equal(5, param.GetIndexedValue(2, 2));
            Assert.Equal(6, param.GetIndexedValue(2, 3));
        }

        [Fact]
        public void TwoDimensionalParameter_IndividualAssignment_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float costs[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Individual assignments
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                costs[1,1] = 10.5;
                costs[1,2] = 20.3;
                costs[2,1] = 15.7;
                costs[2,2] = 25.9;
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["costs"];
            Assert.Equal(10.5, Convert.ToDouble(param.GetIndexedValue(1, 1)));
            Assert.Equal(20.3, Convert.ToDouble(param.GetIndexedValue(1, 2)));
            Assert.Equal(15.7, Convert.ToDouble(param.GetIndexedValue(2, 1)));
            Assert.Equal(25.9, Convert.ToDouble(param.GetIndexedValue(2, 2)));
        }

        [Fact]
        public void TwoDimensionalParameter_WrongRowCount_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..3;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Only 2 rows provided, but 3 expected
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[10, 20],
                          [30, 40]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = result.GetErrorMessages();
            Assert.Contains("expects 3 rows", string.Join(" ", errors));
        }

        [Fact]
        public void TwoDimensionalParameter_WrongColumnCount_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..3;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Second row has only 2 columns instead of 3
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[10, 20, 30],
                          [40, 50]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = result.GetErrorMessages();
            Assert.Contains("Expected 3 values", string.Join(" ", errors));
        }

        [Fact]
        public void TwoDimensionalParameter_TypeMismatch_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                int matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Providing float values for int parameter
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[10.5, 20.3],
                          [30.7, 40.9]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = result.GetErrorMessages();
            Assert.Contains("Cannot parse", string.Join(" ", errors));
        }

        [Fact]
        public void TwoDimensionalParameter_IndexOutOfRange_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float costs[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Index [3,1] is out of range (I only goes to 2)
            var dataParser = new DataFileParser(manager);
            string dataText = "costs[3,1] = 10.5;";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = result.GetErrorMessages();
            Assert.Contains("out of range", string.Join(" ", errors));
        }

        [Fact]
        public void TwoDimensionalParameter_MixedAssignmentStyles_Works()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Initialize with matrix, then override some values
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[10, 20],
                          [30, 40]];
                matrix[1,1] = 99.9;
                matrix[2,2] = 88.8;
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["matrix"];
            Assert.Equal(99.9, Convert.ToDouble(param.GetIndexedValue(1, 1))); // Overridden
            Assert.Equal(20.0, Convert.ToDouble(param.GetIndexedValue(1, 2))); // Original
            Assert.Equal(30.0, Convert.ToDouble(param.GetIndexedValue(2, 1))); // Original
            Assert.Equal(88.8, Convert.ToDouble(param.GetIndexedValue(2, 2))); // Overridden
        }

        [Fact]
        public void TwoDimensionalParameter_LargeMatrix_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..5;
                range J = 1..4;
                int matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[1,  2,  3,  4],
                          [5,  6,  7,  8],
                          [9,  10, 11, 12],
                          [13, 14, 15, 16],
                          [17, 18, 19, 20]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["matrix"];
            
            // Spot check some values
            Assert.Equal(1, param.GetIndexedValue(1, 1));
            Assert.Equal(8, param.GetIndexedValue(2, 4));
            Assert.Equal(11, param.GetIndexedValue(3, 3));
            Assert.Equal(20, param.GetIndexedValue(5, 4));
        }

        [Fact]
        public void TwoDimensionalParameter_EmptyMatrix_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act
            var dataParser = new DataFileParser(manager);
            string dataText = "matrix = [[]];";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
        }

        [Fact]
        public void TwoDimensionalParameter_NonRectangularMatrix_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..3;
                range J = 1..3;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Rows have different lengths
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[1, 2, 3],
                          [4, 5],
                          [6, 7, 8]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = result.GetErrorMessages();
            Assert.Contains("Expected 3 values", string.Join(" ", errors));
        }

        [Fact]
        public void TwoDimensionalParameter_WithComments_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                // This is a cost matrix
                matrix = [[10, 20],  // First row
                          [30, 40]]; // Second row
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["matrix"];
            Assert.Equal(10.0, Convert.ToDouble(param.GetIndexedValue(1, 1)));
            Assert.Equal(40.0, Convert.ToDouble(param.GetIndexedValue(2, 2)));
        }

        [Fact]
        public void TwoDimensionalParameter_NegativeValues_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[-10.5, 20.3],
                          [15.7, -25.9]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["matrix"];
            Assert.Equal(-10.5, Convert.ToDouble(param.GetIndexedValue(1, 1)));
            Assert.Equal(20.3, Convert.ToDouble(param.GetIndexedValue(1, 2)));
            Assert.Equal(15.7, Convert.ToDouble(param.GetIndexedValue(2, 1)));
            Assert.Equal(-25.9, Convert.ToDouble(param.GetIndexedValue(2, 2)));
        }

        [Fact]
        public void TwoDimensionalParameter_ScientificNotation_ParsesCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..2;
                range J = 1..2;
                float matrix[I][J] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                matrix = [[1.5e2, 2.3e-1],
                          [3.7e1, 4.9e0]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.False(result.HasErrors);
            
            var param = manager.Parameters["matrix"];
            Assert.Equal(150.0, Convert.ToDouble(param.GetIndexedValue(1, 1)));
            Assert.Equal(0.23, Convert.ToDouble(param.GetIndexedValue(1, 2)), 5);
            Assert.Equal(37.0, Convert.ToDouble(param.GetIndexedValue(2, 1)));
            Assert.Equal(4.9, Convert.ToDouble(param.GetIndexedValue(2, 2)), 5);
        }

        [Fact]
        public void TwoDimensionalParameter_AssignTo1DParameter_ReturnsError()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            
            string modelText = @"
                range I = 1..3;
                float vector[I] = ...;
            ";
            
            parser.Parse(modelText);
            
            // Act - Trying to assign 2D matrix to 1D parameter
            var dataParser = new DataFileParser(manager);
            string dataText = @"
                vector = [[10, 20, 30]];
            ";
            
            var result = dataParser.Parse(dataText);
            
            // Assert
            Assert.True(result.HasErrors);
            var errors = result.GetErrorMessages();
            Assert.Contains("one-dimensional", string.Join(" ", errors));
        }
    }
}