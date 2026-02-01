using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class IndexedParameterTests : TestBase
    {
        [Fact]
        public void IndexedParameter_VectorNotation_ParsesCorrectly()
        {
            var manager = new ModelManager();
            // Model file
            var modelText = @"
                range I = 1..5;
                float a[I] = ...;
                var float x[I];
                test[i in I]: x[i] >= a[i];
            ";
            
            var parser = new EquationParser(manager);
            var result = parser.Parse(modelText);
            
            // Verify model parsing
            Assert.False(result.HasErrors, 
                $"Model parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            Assert.True(manager.Parameters.ContainsKey("a"), "Parameter 'a' not found");
            
            var param = manager.Parameters["a"];
            Assert.True(param.IsIndexed, "Parameter 'a' should be indexed");
            Assert.Equal("I", param.IndexSetName);
            Assert.True(param.IsExternal, "Parameter 'a' should be external");
            
            // Data file
            var dataText = "a = [10, 20, 30, 40, 50];";
            var dataParser = new DataFileParser(manager);
            var dataResult = dataParser.Parse(dataText);
            
            // Verify data parsing
            Assert.False(dataResult.HasErrors,
                $"Data parsing failed: {string.Join(", ", dataResult.GetErrorMessages())}");
            
            // Verify values were assigned
            Assert.Equal(10.0, Convert.ToDouble(param.GetIndexedValue(1)));
            Assert.Equal(20.0, Convert.ToDouble(param.GetIndexedValue(2)));
            Assert.Equal(30.0, Convert.ToDouble(param.GetIndexedValue(3)));
            Assert.Equal(40.0, Convert.ToDouble(param.GetIndexedValue(4)));
            Assert.Equal(50.0, Convert.ToDouble(param.GetIndexedValue(5)));

            // Expand indexed equations
            var expansionResult = new ParseSessionResult();
            parser.ExpandIndexedEquations(expansionResult);

            // Verify equations were created with substituted parameter values
            Assert.Equal(5, manager.Equations.Count);
            
            // Check first equation: x[1] >= 10
            var eq1 = manager.Equations[0];
            Assert.Equal(10.0, eq1.Constant.Evaluate(manager)); // The constant should be 10 (from a[1])
        }
        
        [Fact]
        public void IndexedParameter_IndividualAssignment_ParsesCorrectly()
        {
            var manager = new ModelManager();

            var modelText = @"
                range I = 1..3;
                float cost[i in I] = ...;
            ";
            
            var parser = new EquationParser(manager);
            var result = parser.Parse(modelText);
            Assert.False(result.HasErrors);
            
            var dataText = @"
                cost = {100,200,300};
            ";
            
            var dataParser = new DataFileParser(manager);
            var dataResult = dataParser.Parse(dataText);
            
            Assert.False(dataResult.HasErrors,
                $"Data parsing errors: {string.Join(", ", dataResult.GetErrorMessages())}");
            
            var param = manager.Parameters["cost"];
            Assert.Equal(100.0, Convert.ToDouble(param.GetIndexedValue(1)));
            Assert.Equal(200.0, Convert.ToDouble(param.GetIndexedValue(2)));
            Assert.Equal(300.0, Convert.ToDouble(param.GetIndexedValue(3)));
        }
    }
}

