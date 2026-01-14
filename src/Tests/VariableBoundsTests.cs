using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Tests for variable declaration with bounds parsing
    /// </summary>
    public class VariableBoundsTests : TestBase
    {
        #region Scalar Variable Bounds Tests

        [Fact]
        public void Parse_ScalarVariableWithNumericBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x in 0..100;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.NotNull(variable);
            Assert.True(variable.IsScalar);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(100, variable.UpperBound);
            Assert.True(variable.HasBounds);
        }

        [Fact]
        public void Parse_ScalarVariableWithNegativeBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float temperature in -50..50;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("temperature");
            Assert.NotNull(variable);
            Assert.Equal(-50, variable.LowerBound);
            Assert.Equal(50, variable.UpperBound);
        }

        [Fact]
        public void Parse_ScalarVariableWithDecimalBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float percentage in 0.0..1.0;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("percentage");
            Assert.NotNull(variable);
            Assert.Equal(0.0, variable.LowerBound);
            Assert.Equal(1.0, variable.UpperBound);
        }

        [Fact]
        public void Parse_ScalarVariableWithParameterBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int capacity = 100;
                var float x in 0..capacity;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.NotNull(variable);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(100, variable.UpperBound);
        }

        [Fact]
        public void Parse_ScalarVariableWithFloatParameterBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                float minCost = 10.5;
                float maxCost = 500.75;
                var float cost in minCost..maxCost;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("cost");
            Assert.NotNull(variable);
            Assert.Equal(10.5, variable.LowerBound);
            Assert.Equal(500.75, variable.UpperBound);
        }

        [Fact]
        public void Parse_ScalarIntVariableWithBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var int count in 1..10;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("count");
            Assert.NotNull(variable);
            Assert.Equal(VariableType.Integer, variable.Type);
            Assert.Equal(1, variable.LowerBound);
            Assert.Equal(10, variable.UpperBound);
        }

        [Fact]
        public void Parse_ScalarBoolVariableWithBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var bool active in 0..1;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("active");
            Assert.NotNull(variable);
            Assert.Equal(VariableType.Boolean, variable.Type);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(1, variable.UpperBound);
        }

        [Fact]
        public void Parse_ScalarVariableWithoutBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float unrestricted;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("unrestricted");
            Assert.NotNull(variable);
            Assert.Null(variable.LowerBound);
            Assert.Null(variable.UpperBound);
            Assert.False(variable.HasBounds);
        }

        #endregion

        #region Indexed Variable Bounds Tests

        [Fact]
        public void Parse_IndexedVariableWithNumericBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                var float x[I] in 0..100;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.NotNull(variable);
            Assert.False(variable.IsScalar);
            Assert.Equal("I", variable.IndexSetName);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(100, variable.UpperBound);
        }

        [Fact]
        public void Parse_IndexedVariableWithParameterBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int maxCapacity = 1000;
                range Facilities = 1..5;
                var int capacity[Facilities] in 0..maxCapacity;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("capacity");
            Assert.NotNull(variable);
            Assert.Equal(VariableType.Integer, variable.Type);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(1000, variable.UpperBound);
        }

        [Fact]
        public void Parse_IndexedVariableWithMixedBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                float maxValue = 99.5;
                range Products = 1..10;
                var float price[Products] in 5.5..maxValue;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("price");
            Assert.NotNull(variable);
            Assert.Equal(5.5, variable.LowerBound);
            Assert.Equal(99.5, variable.UpperBound);
        }

        [Fact]
        public void Parse_IndexedVariableWithoutBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                var float y[I];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(2, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("y");
            Assert.NotNull(variable);
            Assert.Null(variable.LowerBound);
            Assert.Null(variable.UpperBound);
            Assert.False(variable.HasBounds);
        }

        #endregion

        #region 2D Indexed Variable Bounds Tests

        [Fact]
        public void Parse_TwoDimensionalVariableWithBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                range J = 1..4;
                var float matrix[I,J] in -10..10;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("matrix");
            Assert.NotNull(variable);
            Assert.True(variable.IsTwoDimensional);
            Assert.Equal("I", variable.IndexSetName);
            Assert.Equal("J", variable.SecondIndexSetName);
            Assert.Equal(-10, variable.LowerBound);
            Assert.Equal(10, variable.UpperBound);
        }

        [Fact]
        public void Parse_TwoDimensionalVariableWithParameterBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int maxAllocation = 100;
                range Facilities = 1..5;
                range Products = 1..10;
                var float allocation[Facilities,Products] in 0..maxAllocation;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(4, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("allocation");
            Assert.NotNull(variable);
            Assert.True(variable.IsTwoDimensional);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(100, variable.UpperBound);
        }

        [Fact]
        public void Parse_TwoDimensionalVariableWithoutBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..3;
                range J = 1..4;
                var int grid[I,J];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(3, result.SuccessCount);
            
            var variable = manager.GetIndexedVariable("grid");
            Assert.NotNull(variable);
            Assert.True(variable.IsTwoDimensional);
            Assert.Null(variable.LowerBound);
            Assert.Null(variable.UpperBound);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void Parse_VariableWithInvalidBoundsOrder_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x in 100..0;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Lower bound");
            AssertHasError(result, "greater than upper bound");
        }

        [Fact]
        public void Parse_VariableWithUndeclaredParameterBound_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x in 0..undeclared;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "undeclared");
            AssertHasError(result, "not a valid number or declared parameter");
        }

        [Fact]
        public void Parse_VariableWithNonNumericParameterBound_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                string name = ""test"";
                var float x in 0..name;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "must be numeric for bounds");
        }

        [Fact]
        public void Parse_VariableWithInvalidBoundsSyntax_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x in 0-100;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Invalid bounds format");
        }

        [Fact]
        public void Parse_VariableWithMissingUpperBound_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x in 0..;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_VariableWithMissingLowerBound_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x in ..100;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_VariableWithOnlyInKeyword_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x in;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result);
        }

        [Fact]
        public void Parse_IndexedVariableWithBoundsButUndeclaredIndexSet_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = "var float x[I] in 0..100;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertHasError(result, "Index set 'I' is not declared");
        }

        #endregion

        #region Multiple Variables Tests

        [Fact]
        public void Parse_MultipleVariablesWithDifferentBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                var float x in 0..100;
                var int y in 1..10;
                var float z[I] in -50..50;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(4, result.SuccessCount);
            
            var x = manager.GetIndexedVariable("x");
            Assert.Equal(0, x.LowerBound);
            Assert.Equal(100, x.UpperBound);
            
            var y = manager.GetIndexedVariable("y");
            Assert.Equal(1, y.LowerBound);
            Assert.Equal(10, y.UpperBound);
            
            var z = manager.GetIndexedVariable("z");
            Assert.Equal(-50, z.LowerBound);
            Assert.Equal(50, z.UpperBound);
        }

        [Fact]
        public void Parse_MixedBoundedAndUnboundedVariables_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                range I = 1..5;
                var float x in 0..100;
                var float y;
                var float z[I] in -10..10;
                var int w[I];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(5, result.SuccessCount);
            
            var x = manager.GetIndexedVariable("x");
            Assert.True(x.HasBounds);
            
            var y = manager.GetIndexedVariable("y");
            Assert.False(y.HasBounds);
            
            var z = manager.GetIndexedVariable("z");
            Assert.True(z.HasBounds);
            
            var w = manager.GetIndexedVariable("w");
            Assert.False(w.HasBounds);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void Parse_VariableWithZeroBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x in 0..0;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.NotNull(variable);
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(0, variable.UpperBound);
        }

        [Fact]
        public void Parse_VariableWithLargeNegativeBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x in -1000000..-1;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.Equal(-1000000, variable.LowerBound);
            Assert.Equal(-1, variable.UpperBound);
        }

        [Fact]
        public void Parse_VariableWithVerySmallDecimalBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x in 0.001..0.999;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.Equal(0.001, variable.LowerBound.Value, 6);
            Assert.Equal(0.999, variable.UpperBound.Value, 6);
        }

        [Fact]
        public void Parse_VariableWithWhitespaceInBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x in  0  ..  100  ;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(100, variable.UpperBound);
        }

        #endregion

        #region Complex Scenarios Tests

        [Fact]
        public void Parse_CompleteModelWithBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                // Parameters
                int maxCapacity = 1000;
                float minCost = 10.5;
                float maxCost = 500.75;
                
                // Index sets
                range Facilities = 1..5;
                range Products = 1..10;
                
                // Scalar variables with bounds
                var float totalCost in 0..10000;
                var int totalUnits in 0..5000;
                
                // Indexed variables with bounds
                var int capacity[Facilities] in 0..maxCapacity;
                var float cost[Products] in minCost..maxCost;
                
                // 2D indexed variable with bounds
                var float allocation[Facilities,Products] in 0..100;
                
                // Variables without bounds
                var float unrestricted[Facilities];
                var bool active[Products];
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Equal(13, result.SuccessCount);
            
            // Verify parameters
            Assert.Equal(3, manager.Parameters.Count);
            
            // Verify index sets
            Assert.Equal(2, manager.IndexSets.Count);
            
            // Verify variables
            Assert.Equal(7, manager.IndexedVariables.Count);
            
            // Verify specific bounds
            var totalCost = manager.GetIndexedVariable("totalCost");
            Assert.Equal(0, totalCost.LowerBound);
            Assert.Equal(10000, totalCost.UpperBound);
            
            var capacityVar = manager.GetIndexedVariable("capacity");
            Assert.Equal(0, capacityVar.LowerBound);
            Assert.Equal(1000, capacityVar.UpperBound);
            
            var allocation = manager.GetIndexedVariable("allocation");
            Assert.Equal(0, allocation.LowerBound);
            Assert.Equal(100, allocation.UpperBound);
            Assert.True(allocation.IsTwoDimensional);
        }

        [Fact]
        public void Parse_VariableWithParameterCalculationInBounds_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int base = 50;
                int multiplier = 2;
                int maxValue = base;
                var float x in 0..maxValue;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            
            var variable = manager.GetIndexedVariable("x");
            Assert.Equal(0, variable.LowerBound);
            Assert.Equal(50, variable.UpperBound);
        }

        [Theory]
        [InlineData("var float x in 0..100", VariableType.Float, 0.0, 100.0)]
        [InlineData("var int y in 1..50", VariableType.Integer, 1.0, 50.0)]
        [InlineData("var bool z in 0..1", VariableType.Boolean, 0.0, 1.0)]
        public void Parse_DifferentVariableTypesWithBounds_ShouldSucceed(
            string declaration, VariableType expectedType, double expectedLower, double expectedUpper)
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);

            // Act
            var result = parser.Parse(declaration + ";");

            // Assert
            AssertNoErrors(result);
            Assert.Equal(1, result.SuccessCount);
            
            var variables = manager.IndexedVariables.Values.First();
            Assert.Equal(expectedType, variables.Type);
            Assert.Equal(expectedLower, variables.LowerBound);
            Assert.Equal(expectedUpper, variables.UpperBound);
        }

        #endregion

        #region ToString and Display Tests

        [Fact]
        public void Variable_ToStringWithBounds_ShouldIncludeBounds()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x in 0..100;";

            // Act
            parser.Parse(input);
            var variable = manager.GetIndexedVariable("x");

            // Assert
            Assert.NotNull(variable);
            string str = variable.ToString();
            Assert.Contains("in", str);
            Assert.Contains("0", str);
            Assert.Contains("100", str);
        }

        [Fact]
        public void Variable_ToStringWithoutBounds_ShouldNotIncludeBounds()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "var float x;";

            // Act
            parser.Parse(input);
            var variable = manager.GetIndexedVariable("x");

            // Assert
            Assert.NotNull(variable);
            string str = variable.ToString();
            Assert.DoesNotContain("in", str);
        }

        #endregion
    }
}