    using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    public class PrimitiveSetTests : TestBase
    {
        [Fact]
        public void Parse_IntegerSet_InlineData_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "{int} nodes = {1, 2, 3, 4, 5};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.Single(manager.PrimitiveSets);
            
            var set = manager.PrimitiveSets["nodes"];
            Assert.Equal("nodes", set.Name);
            Assert.Equal(PrimitiveSetType.Int, set.Type);
            Assert.Equal(5, set.Count);
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(5));
            Assert.False(set.Contains(10));
        }

        [Fact]
        public void Parse_StringSet_InlineData_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"{string} cities = {""NYC"", ""LA"", ""Chicago"", ""Houston""};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["cities"];
            Assert.Equal(PrimitiveSetType.String, set.Type);
            Assert.Equal(4, set.Count);
            Assert.True(set.Contains("NYC"));
            Assert.True(set.Contains("Chicago"));
            Assert.False(set.Contains("Boston"));
        }

        [Fact]
        public void Parse_FloatSet_InlineData_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "{float} rates = {0.05, 0.10, 0.15, 0.20};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["rates"];
            Assert.Equal(PrimitiveSetType.Float, set.Type);
            Assert.Equal(4, set.Count);
            Assert.True(set.Contains(0.05));
            Assert.True(set.Contains(0.20));
            Assert.False(set.Contains(0.25));
        }

        [Fact]
        public void Parse_ExternalIntSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "{int} externalNodes = ...;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["externalNodes"];
            Assert.True(set.IsExternal);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void Parse_ExternalStringSet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "{string} countries = ...;";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["countries"];
            Assert.Equal(PrimitiveSetType.String, set.Type);
            Assert.True(set.IsExternal);
        }

        [Fact]
        public void Parse_EmptySet_ShouldSucceed()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "{int} empty = {};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["empty"];
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void Parse_DuplicateValuesInSet_ShouldStoreSingle()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = "{int} numbers = {1, 2, 2, 3, 3, 3};";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["numbers"];
            Assert.Equal(3, set.Count); // Only unique values
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
        }

        [Fact]
        public void GetIntValues_OnIntegerSet_ShouldReturnValues()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.Int);
            set.Add(3);
            set.Add(1);
            set.Add(2);

            // Act
            var values = set.GetIntValues().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            Assert.Contains(1, values);
            Assert.Contains(2, values);
            Assert.Contains(3, values);
        }

        [Fact]
        public void GetStringValues_OnStringSet_ShouldReturnValues()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.String);
            set.Add("alpha");
            set.Add("beta");
            set.Add("gamma");

            // Act
            var values = set.GetStringValues().ToList();

            // Assert
            Assert.Equal(3, values.Count);
            Assert.Contains("alpha", values);
            Assert.Contains("gamma", values);
        }

        [Fact]
        public void GetIntValues_OnStringSet_ShouldThrow()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.String);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => set.GetIntValues());
        }

        [Fact]
        public void Clear_ShouldRemoveAllValues()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.Int);
            set.Add(1);
            set.Add(2);
            set.Add(3);

            // Act
            set.Clear();

            // Assert
            Assert.Equal(0, set.Count);
            Assert.False(set.Contains(1));
        }

        [Fact]
        public void DataFile_LoadPrimitiveSetData_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var modelParser = CreateParser(manager);
            
            // Parse model with external set
            string model = "{int} nodes = ...;";
            modelParser.Parse(model);
            
            // Parse data file
            var dataParser = new DataFileParser(manager);
            string dataFile = "nodes = {1, 5, 10, 15, 20};";
            
            // Act
            var result = dataParser.Parse(dataFile);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["nodes"];
            Assert.Equal(5, set.Count);
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(20));
        }

        [Fact]
        public void DataFile_LoadStringSetData_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var modelParser = CreateParser(manager);
            
            string model = "{string} cities = ...;";
            modelParser.Parse(model);
            
            var dataParser = new DataFileParser(manager);
            string dataFile = @"cities = {""NYC"", ""LA"", ""SF"", ""Boston""};";
            
            // Act
            var result = dataParser.Parse(dataFile);

            // Assert
            AssertNoErrors(result);
            var set = manager.PrimitiveSets["cities"];
            Assert.Equal(4, set.Count);
            Assert.True(set.Contains("NYC"));
            Assert.True(set.Contains("Boston"));
        }

        [Fact]
        public void ToString_IntSet_ShouldFormat()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.Int);
            set.Add(3);
            set.Add(1);
            set.Add(2);

            // Act
            var str = set.ToString();

            // Assert
            Assert.Contains("{int}", str);
            Assert.Contains("test", str);
            Assert.Contains("1", str);
            Assert.Contains("2", str);
            Assert.Contains("3", str);
        }

        [Fact]
        public void ToString_StringSet_ShouldQuoteValues()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.String);
            set.Add("hello");
            set.Add("world");

            // Act
            var str = set.ToString();

            // Assert
            Assert.Contains("{string}", str);
            Assert.Contains("\"hello\"", str);
            Assert.Contains("\"world\"", str);
        }

        [Fact]
        public void ToString_ExternalEmptySet_ShouldIndicateNotLoaded()
        {
            // Arrange
            var set = new PrimitiveSet("test", PrimitiveSetType.Int, isExternal: true);

            // Act
            var str = set.ToString();

            // Assert
            Assert.Contains("external", str);
            Assert.Contains("not loaded", str);
        }
    }
}