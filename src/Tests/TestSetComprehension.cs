using Xunit;
using Core;
using Core.Models;

namespace Tests
{

    public class TestSetComprehension : TestBase
    {



        [Fact]
        public void TupleSetInitialization_ShouldNotBeConfusedWithSetComprehension()
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
        
        // This is tuple initialization, NOT a set comprehension
        {Edge} edges = {<1,2,10.5>, <2,3,20.0>};
    ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.True(manager.TupleSets.ContainsKey("edges"));
            Assert.Equal(2, manager.TupleSets["edges"].Instances.Count);
        }

        [Fact]
        public void SetComprehension_ShouldBeParsedCorrectly()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
        tuple Arc {
            string from;
            string to;
        }
        
        {Arc} allArcs = {<""A"",""B"">, <""B"",""C"">, <""C"",""A"">};
        {string} nodes = {""A"", ""B"", ""C""};
        
        // This IS a set comprehension
        {Arc} outgoingFromA = {a | a in allArcs: a.from == ""A""};
    ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.True(manager.ComputedSets.ContainsKey("outgoingFromA"));
        }

    }
}
