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
            
            // **Phase 1: Parse model structure**
            var modelText = @"
                range I = 1..5;
                float a[I] = ...;
                var float x[I];
                test[i in I]: x[i] >= a[i];
            ";
            
            var parser = new EquationParser(manager);
            var result = parser.Parse(modelText);
            
            // Verify model parsing (NO expansion yet!)
            Assert.False(result.HasErrors, 
                $"Model parsing failed: {string.Join(", ", result.GetErrorMessages())}");
            Assert.True(manager.Parameters.ContainsKey("a"), "Parameter 'a' not found");
            
            // At this point, equations are still TEMPLATES
            Assert.Equal(0, manager.Equations.Count); // No concrete equations yet
            Assert.Equal(1, manager.IndexedEquationTemplates.Count); // But we have a template!
            
            // **Phase 2: Load external data**
            var dataText = "a = [10, 20, 30, 40, 50];";
            var dataParser = new DataFileParser(manager);
            var dataResult = dataParser.Parse(dataText);
            
            Assert.False(dataResult.HasErrors,
                $"Data parsing failed: {string.Join(", ", dataResult.GetErrorMessages())}");
            
            // Verify data was loaded
            var param = manager.Parameters["a"];
            Assert.Equal(10.0, Convert.ToDouble(param.GetIndexedValue(1)));
            
            // **Phase 3: Expand templates NOW (after data is loaded)**
            parser.ExpandAllTemplates(result);
            
            // NOW we have concrete equations with actual parameter values
            Assert.Equal(5, manager.Equations.Count);
            
            // Check first equation: x[1] >= 10
            var eq1 = manager.Equations[0];
            Assert.Equal(10.0, eq1.Constant.Evaluate(manager));
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
                cost = [100,200,300];
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

