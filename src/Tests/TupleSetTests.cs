using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class TupleSetTests : TestBase
    {
        [Fact]
        public void Parse_TupleSchemaAndSet_ShouldSucceed()
        {
            // Arrange & Act
            var tupleSet = new TupleSet("products", "Product", true);

           
            // Assert
            Assert.True(tupleSet.IsExternal);
            Assert.Equal(0, tupleSet.Count);
        }

        [Fact]
        public void AddInstance_WithNullInstance_ShouldThrow()
        {
            // Arrange
            var tupleSet = new TupleSet("products", "Product", false);
            var instance = new TupleInstance("Product");
            instance.SetValue("id", 1);
            tupleSet.AddInstance(instance);

            // Act
            var retrieved = tupleSet[0];

            // Assert
            Assert.Equal(instance, retrieved);
            Assert.Equal(1, retrieved.GetValue("id"));
        }

        [Fact]
        public void Parse_ExternalTupleSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Arc {
                    int from;
                    int to;
                }
                {Arc} arcs = ...;
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["arcs"];
            Assert.True(tupleSet.IsExternal);
            Assert.Equal(0, tupleSet.Instances.Count);
        }

        [Fact]
        public void Parse_TupleSetWithData_ShouldPopulateFields()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Edge {
                    int from;
                    int to;
                    float cost;
                }
                {Edge} edges = {<1,2,10.5>, <2,3,20.0>};
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["edges"];
            Assert.Equal(2, tupleSet.Instances.Count);
            
            var firstEdge = tupleSet.Instances[0];
            Assert.Equal(1, firstEdge.GetValue("from"));
            Assert.Equal(2, firstEdge.GetValue("to"));
            Assert.Equal(10.5, firstEdge.GetValue("cost"));
        }

        [Fact]
        public void TupleSet_ToString_ShouldFormatCorrectly()
        {
            // Arrange
            var schema = new TupleSchema("TestTuple");
            schema.AddField("x", VariableType.Integer);
            schema.AddField("y", VariableType.Integer);
            
            var tupleSet = new TupleSet("testSet", "TestTuple", false);
            var instance1 = new TupleInstance("TestTuple");
            instance1.SetValue("x", 1);
            instance1.SetValue("y", 2);
            tupleSet.AddInstance(instance1);

            // Act
            string str = tupleSet.ToString();

            // Assert
            Assert.Contains("testSet", str);
            Assert.Contains("TestTuple", str);
        }

        [Fact]
        public void Parse_InvalidTupleFormat_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                tuple Bad {
                    int x;
                    int y;
                }
                {Bad} badSet = {1,2, 3,4};
            "; // Missing angle brackets

            // Act
            var result = parser.Parse(input);

            // Assert
            Assert.True(result.HasErrors);
        }

        [Fact]
        public void Parse_MismatchedFieldCount_ShouldFail()
        {
            // Arrange
            var parser = CreateParser();
            string input = @"
                tuple Pair {
                    int x;
                    int y;
                }
                {Pair} pairs = {<1,2>, <3,4,5>};
            "; // Second tuple has 3 values, schema expects 2

            // Act
            var result = parser.Parse(input);

            // Assert
            Assert.True(result.HasErrors);
        }

        [Fact]
        public void Parse_TupleSetWithStringFields_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple City {
                    string name;
                    int population;
                }
                {City} cities = {<""NYC"", 8000000>, <""LA"", 4000000>};
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var tupleSet = manager.TupleSets["cities"];
            Assert.Equal(2, tupleSet.Instances.Count);
            
            var nyc = tupleSet.Instances[0];
            Assert.Equal("NYC", nyc.GetValue("name"));
            Assert.Equal(8000000, nyc.GetValue("population"));
        }
    }
}