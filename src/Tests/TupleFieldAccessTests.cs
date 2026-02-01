using Xunit;
using Core;
using Core.Models;
using Core.Parsing;

namespace Tests
{
    public class TupleFieldAccessTests : TestBase
    {
        [Fact]
        public void ParseTupleFieldAccess_InSetComprehension_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Node {
                    key int id;
                    int stage;
                }
                
                {Node} nodes = {<0,0>, <1,1>, <2,1>, <3,2>};
                int maxStage = 2;
                
                {Node} nodes0 = {n | n in nodes: n.stage >= 1};
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.True(manager.ComputedSets.ContainsKey("nodes0"));
        }
        
        [Fact]
        public void ParseTupleFieldAccess_InDexpr_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Product {
                    key int id;
                    float cost;
                    float price;
                }
                
                {Product} products = {<1, 10.0, 25.0>, <2, 15.0, 35.0>};
                
                dexpr float totalMargin = sum(p in products) (p.price - p.cost);
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.True(manager.DecisionExpressions.ContainsKey("totalMargin"));
        }
        
        [Fact]
        public void TupleFieldAccessParser_ShouldDetectPattern()
        {
            // Arrange & Act
            bool isAccess1 = TupleFieldAccessParser.IsTupleFieldAccess("n.prob");
            bool isAccess2 = TupleFieldAccessParser.IsTupleFieldAccess("j.arcindex");
            bool isNotAccess = TupleFieldAccessParser.IsTupleFieldAccess("123.456");
            
            // Assert
            Assert.True(isAccess1);
            Assert.True(isAccess2);
            Assert.False(isNotAccess);
        }
        
        [Fact]
        public void TupleFieldAccess_InComplexCondition_ShouldWork()
        {
            // Arrange
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Contract {
                    key string id;
                    string type;
                    float price;
                }
                
                {Contract} contracts = {
                    <""C1"", ""Buy"", 10.0>,
                    <""C2"", ""Sell"", 15.0>,
                    <""C3"", ""Buy"", 12.0>
                };
                
                {Contract} buyContracts = {c | c in contracts: c.type == ""Buy""};
                {Contract} sellContracts = {c | c in contracts: c.type == ""Sell""};
            ";

            // Act
            var result = parser.Parse(input);

            // Assert
            AssertNoErrors(result);
            Assert.True(manager.ComputedSets.ContainsKey("buyContracts"));
            Assert.True(manager.ComputedSets.ContainsKey("sellContracts"));
        }
    }
}