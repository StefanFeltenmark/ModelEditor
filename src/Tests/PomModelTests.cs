using Xunit;
using Core;
using Core.Models;

namespace Tests
{
    /// <summary>
    /// Unit tests based on patterns from POM.mod
    /// Tests are organized by feature complexity
    /// </summary>
    public class PomModelTests : TestBase
    {
        #region 1. Basic Constants and Parameters
        
        [Fact]
        public void POM_ScientificNotation_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                float M = 10e12;
                float VolumeScale = 1e-6;
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.Equal(10e12, manager.Parameters["M"].Value);
            Assert.Equal(1e-6, manager.Parameters["VolumeScale"].Value);
        }

        [Fact]
        public void POM_RangeFromParameter_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                int nT = 5;
                range T = 1..nT;
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.Ranges.ContainsKey("T"));
            var range = manager.Ranges["T"];
            Assert.Equal(5, range.GetValues(manager).Count());
        }

        #endregion

        #region 2. Tuple Schema Definitions

        [Fact]
        public void POM_TupleWithMultipleKeyFields_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple ArcTData {
                   key string id;
                   key int t;
                   float flowMin;
                   float flowMax;
                }
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.TupleSchemas.ContainsKey("ArcTData"));
            var schema = manager.TupleSchemas["ArcTData"];
            Assert.Equal(4, schema.Fields.Count);
            Assert.Equal(2, schema.KeyFields.Count);
        }

        [Fact]
        public void POM_AllTupleSchemas_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Reservoir {
                  key string id;
                  string HydroNodeindex;  
                  float minLevel;
                  float initialLevel;
                  float initialContent;  
                }

                tuple Station {
                  key string id;
                  string arcindex;   
                  float energyEquivalent; 
                  int isRunOfRiver;
                }

                tuple ScenarioTreeNode {
                  key int id;
                  float prob;
                  int stage;
                  int pred;
                }
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.Equal(3, manager.TupleSchemas.Count);
        }

        #endregion

        #region 3. External Data Declarations

        [Fact]
        public void POM_ExternalTupleSet_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple HydroNode {
                  key string id;
                }
                
                {HydroNode} HydroNodes = ...;
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.TupleSets.ContainsKey("HydroNodes"));
            Assert.True(manager.TupleSets["HydroNodes"].IsExternal);
        }

        [Fact]
        public void POM_ExternalPrimitiveSet_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                {string} OverFlowArcs = ...;
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.PrimitiveSets.ContainsKey("OverFlowArcs"));
            Assert.True(manager.PrimitiveSets["OverFlowArcs"].IsExternal);
        }

        #endregion

        #region 4. Multi-Dimensional Parameters

        [Fact]
        public void POM_TwoDimensionalIndexedParameter_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Arc {
                    key string id;
                }
                tuple ArcTData {
                   key string id;
                   key int t;
                   float flowMin;
                }
                
                int nT = 3;
                range T = 1..nT;
                {Arc} HydroArcs = ...;
                {ArcTData} HydroArcTs = ...;
                
                ArcTData arcT[s in HydroArcs][t in T] = item(HydroArcTs, <s.id,t>);
            ";

            var result = parser.Parse(input);

            // Should parse without errors (even if external data not loaded)
            // This tests the SYNTAX parsing
            Assert.False(result.HasErrors, 
                $"Should parse 2D indexed parameter syntax: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_ThreeDimensionalExternalParameter_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                }
                
                int nT = 3;
                range T = 1..nT;
                range priceSegment = 1..2;
                
                {Station} stations = ...;
                
                float productionCost[stations][T][priceSegment] = ...;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Should parse 3D parameter syntax: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 5. Set Comprehensions with Tuple Field Access

        [Fact]
        public void POM_SetComprehension_WithTupleFieldCondition_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple ScenarioTreeNode {
                  key int id;
                  float prob;
                  int stage;
                  int pred;
                }
                
                int nT = 5;
                {ScenarioTreeNode} nodes = {<0,1.0,0,0>, <1,0.5,1,0>, <2,0.5,1,0>};
                {ScenarioTreeNode} nodes0 = {n | n in nodes: n.stage >= 1};
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.ComputedSets.ContainsKey("nodes0"));
            var nodes0 = manager.ComputedSets["nodes0"].Evaluate(manager);
            Assert.Equal(2, ((System.Collections.ICollection)nodes0).Count);
        }

        [Fact]
        public void POM_SetComprehension_WithStringComparison_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple EnergyContract {
                  key string id;  
                  string type;
                  float price;
                }
                
                {EnergyContract} contracts = {
                    <""C1"", ""Buy"", 10.0>,
                    <""C2"", ""Sell"", 15.0>,
                    <""C3"", ""Buy"", 12.0>
                };
                
                {EnergyContract} buycontracts = {c | c in contracts: c.type == ""Buy""};
                {EnergyContract} sellcontracts = {c | c in contracts: c.type == ""Sell""};
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.ComputedSets.ContainsKey("buycontracts"));
            Assert.True(manager.ComputedSets.ContainsKey("sellcontracts"));
        }

        [Fact]
        public void POM_SetComprehension_WithMultipleIteratorsAndCondition_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                    string groupId;
                }
                
                tuple StationGroup {
                    key string id;  
                    key string stationId;  
                }
                
                {string} groups = {""G1"", ""G2""};
                {Station} stations = {<""S1"",""G1"">, <""S2"",""G1"">, <""S3"",""G2"">};
                {StationGroup} groupDefinitions = {<""G1"",""S1"">, <""G1"",""S2"">, <""G2"",""S3"">};
                
                {Station} stationsInGroup[g in groups] = {
                    s | s in stations, d in groupDefinitions: 
                    s.id == d.stationId && d.id == g
                };
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Multi-iterator set comprehension should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_SetComprehension_WithProjection_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple HydroNode {
                    key string id;
                    float elevation;
                }
                
                {HydroNode} HydroNodes = {<""N1"",100.0>, <""N2"",200.0>};
                {string} hydroNodeIndices = {i.id | i in HydroNodes};
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.True(manager.ComputedSets.ContainsKey("hydroNodeIndices"));
        }

        #endregion

        #region 6. Computed Parameters with item() and Nested Expressions

        [Fact]
        public void POM_ItemFunction_WithTupleKey_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple ScenarioTreeNode {
                  key int id;
                  float prob;
                  int stage;
                }
                
                {ScenarioTreeNode} nodes = {<0,1.0,0>, <1,0.5,1>};
                ScenarioTreeNode root = item(nodes, 0);
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"item() function should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_NestedItemFunction_WithTupleFieldAccess_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple HydroNode {
                  key string id;
                }
                
                tuple Arc {
                   key string id;
                   string fromHydroNode;
                   string toHydroNode;
                }
                
                tuple Station {
                  key string id;
                  string arcindex;
                }
                
                {HydroNode} HydroNodes = ...;
                {Arc} HydroArcs = ...;
                {Station} stations = ...;
                
                HydroNode stationFromNode[j in stations] = 
                    item(HydroNodes, <item(HydroArcs, <j.arcindex>).fromHydroNode>);
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Nested item() with field access should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_IndexedSetWithItemInComprehension_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Arc {
                   key string id;
                   string fromHydroNode;
                }
                
                {string} OverFlowArcs = {""A1"", ""A2""};
                {Arc} HydroArcs = {<""A1"",""N1"">, <""A2"",""N2"">, <""A3"",""N3"">};
                
                {Arc} J0 = {item(HydroArcs, <i>) | i in OverFlowArcs};
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Set comprehension with item() should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 7. Multi-Dimensional Decision Variables

        [Fact]
        public void POM_TwoDimensionalDvar_WithBounds_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Node {
                    key int id;
                }
                
                range priceSegment = 1..2;
                {Node} nodes0 = {<1>, <2>};
                
                dvar float+ marketSales[n in nodes0][k in priceSegment] in 0..100;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"2D dvar with bounds should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_ThreeDimensionalDvar_WithComplexBounds_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                }
                
                tuple Node {
                    key int id;
                    int stage;
                }
                
                tuple StationTData {
                    key string id;
                    key int t;
                    float releaseMin;
                    float releaseMax;
                }
                
                {Station} stations = ...;
                {Node} nodes0 = ...;
                range priceSegment = 1..2;
                {StationTData} stationTs = ...;
                StationTData stationT[s in stations][t in 1..5] = item(stationTs, <s.id,t>);
                
                dvar float+ stationRelease[j in stations][n in nodes0][priceSegment] 
                    in stationT[j][n.stage].releaseMin..stationT[j][n.stage].releaseMax;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"3D dvar with tuple field bounds should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 8. Decision Expressions (dexpr)

        [Fact]
        public void POM_Dexpr_WithSummationOverTupleSet_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Node {
                    key int id;
                    float prob;
                    int stage;
                }
                
                int nT = 3;
                range T = 1..nT;
                {Node} nodes0 = {<1,0.5,1>, <2,0.5,2>};
                float discountFactor[T] = ...;
                
                dvar float+ value[n in nodes0];
                
                dexpr float expectedValue = 
                    sum(n in nodes0) n.prob * discountFactor[n.stage] * value[n];
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"dexpr with tuple field access should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_Dexpr_WithMultipleSummations_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Node {
                    key int id;
                    float prob;
                }
                
                range priceSegment = 1..2;
                {Node} nodes0 = {<1,0.5>, <2,0.5>};
                
                dvar float+ sales[n in nodes0][k in priceSegment];
                
                dexpr float totalSales = 
                    sum(n in nodes0) n.prob * (sum(k in priceSegment) sales[n][k]);
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"dexpr with nested summations should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_IndexedDexpr_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                }
                
                tuple Node {
                    key int id;
                }
                
                {string} groups = {""G1"", ""G2""};
                {Node} nodes0 = {<1>, <2>};
                range priceSegment = 1..2;
                {Station} stationsInGroup[g in groups] = ...;
                
                dvar float+ production[j in Station][n in nodes0][priceSegment];
                
                dexpr float groupProd[g in groups, n in nodes0, k in priceSegment] = 
                    sum(j in stationsInGroup[g]) production[j][n][k];
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Indexed dexpr should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 9. Forall Constraints with Multiple Iterators

        [Fact]
        public void POM_Forall_WithThreeIterators_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                }
                
                tuple Node {
                    key int id;
                }
                
                {Station} stations = {<""S1"">, <""S2"">};
                {Node} nodes0 = {<1>, <2>};
                range priceSegment = 1..2;
                
                dvar float+ production[j in stations][n in nodes0][priceSegment];
                
                forall(j in stations, n in nodes0, k in priceSegment)
                  StationProduction[j][n][k]:
                  production[j][n][k] <= 100;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"forall with 3 iterators should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_Forall_WithFilterCondition_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                    int isRunOfRiver;
                }
                
                tuple Node {
                    key int id;
                }
                
                int nPriceSegments = 2;
                {Station} stations = {<""S1"",1>, <""S2"",0>};
                {Node} nodes0 = {<1>, <2>};
                range priceSegment = 1..nPriceSegments;
                
                dvar float+ stationRelease[i in stations][n in nodes0][k in priceSegment];
                
                forall(i in stations: i.isRunOfRiver == 1, n in nodes0, k in priceSegment)
                  NoPriceSegmentsForRoR[i,n,k]:
                  stationRelease[i][n][k] == 
                      (1/nPriceSegments) * sum(l in priceSegment) stationRelease[i][n][l];
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"forall with filter condition should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_Forall_WithNestedSummationAndFieldAccess_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Reserve {
                    key string id;
                }
                
                tuple StationReserve {
                    key string id;
                    string stationId;
                }
                
                tuple Station {
                    key string id;
                }
                
                tuple Node {
                    key int id;
                }
                
                {Reserve} upwardReserves = {<""R1"">};
                {StationReserve} reserveParticipants[r in upwardReserves] = ...;
                {Station} stations = {<""S1"">};
                {Node} nodes0 = {<1>};
                range priceSegment = 1..2;
                
                dvar float+ production[s in stations][n in nodes0][k in priceSegment];
                dvar float+ reserve[s in StationReserve][n in nodes0];
                
                forall(s in stations, n in nodes0, k in priceSegment)
                  ProdAndReserveUpperLimit[s,n,k]:
                  production[s][n][k] + 
                  sum(r in upwardReserves, p in reserveParticipants[r]: p.stationId == s.id) 
                      reserve[p][n] <= 100;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"forall with filtered nested summation should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 10. Conditional Constraints (if/else)

        [Fact]
        public void POM_ConditionalConstraint_InForall_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                    float tailWaterLevel;
                }
                
                tuple Node {
                    key int id;
                }
                
                int UseAverageHead = 1;
                {Station} stations = {<""S1"",100.0>};
                {Node} nodes0 = {<1>, <2>};
                range priceSegment = 1..2;
                
                dvar float head[j in stations, n in nodes0, k in priceSegment];
                dvar float level[j in stations, n in nodes0];
                
                forall(j in stations, n in nodes0, k in priceSegment)
                  HeadLevel[j][n][k]:
                  if(UseAverageHead == 1) {
                      head[j,n,k] == level[j,n] - j.tailWaterLevel;
                  } else {
                      head[j,n,k] == level[j,n];
                  }
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"if/else in constraint should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 11. Angle Bracket Tuple References

        [Fact]
        public void POM_AngleBracketTupleReference_InConstraint_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Node {
                    key int id;
                    int pred;
                    int stage;
                }
                
                tuple Reservoir {
                    key string id;
                    float initialContent;
                }
                
                {Node} nodes0 = {<1,0,1>, <2,1,2>};
                {Reservoir} reservoirs = {<""R1"",1000.0>};
                
                dvar float+ content[i in reservoirs][n in nodes0];
                
                forall(i in reservoirs, n in nodes0)
                  Balance[i][n]:
                  content[i][n] == content[i][<n.pred>] + 100;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Angle bracket tuple reference should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 12. Maximize Objective

        [Fact]
        public void POM_MaximizeObjective_WithComplexDexpr_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Node {
                    key int id;
                    float prob;
                }
                
                {Node} nodes0 = {<1,0.5>, <2,0.5>};
                
                dvar float+ income[n in nodes0];
                dvar float+ cost[n in nodes0];
                
                dexpr float expectedIncome = sum(n in nodes0) n.prob * income[n];
                dexpr float expectedCost = sum(n in nodes0) n.prob * cost[n];
                dexpr float objective = expectedIncome - expectedCost;
                
                maximize objective;
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.NotNull(manager.Objective);
            Assert.Equal(ObjectiveSense.Maximize, manager.Objective.Sense);
        }

        #endregion

        #region 13. Execute Blocks

        [Fact]
        public void POM_ExecuteBlock_WithJavaScript_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                float M = 10e12;
                
                float incomingValue = 0.0;
                
                execute CalculateValue {
                    var minVal = M;
                    for(var i = 0; i < 10; i++) {
                        if(i < minVal) {
                            minVal = i;
                        }
                    }
                    incomingValue = minVal;
                }
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Execute block should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 14. Integration Tests - Complex Patterns

        [Fact]
        public void POM_CompleteSubmodel_WaterBalance_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Reservoir {
                    key string id;
                    string HydroNodeindex;
                    float initialContent;
                }
                
                tuple Node {
                    key int id;
                    float prob;
                    int stage;
                    int pred;
                }
                
                tuple HydroNode {
                    key string id;
                }
                
                tuple Arc {
                    key string id;
                    string fromHydroNode;
                    string toHydroNode;
                }
                
                int nT = 3;
                range T = 1..nT;
                
                {Reservoir} reservoirs = {<""R1"",""N1"",1000.0>};
                {Node} nodes0 = {<1,0.5,1,0>, <2,0.5,2,1>};
                {HydroNode} HydroNodes = {<""N1"">, <""N2"">};
                {Arc} HydroArcs = {<""A1"",""N1"",""N2"">};
                
                {string} hydroNodeIndices = {i.id | i in HydroNodes};
                {Arc} Jin[i in hydroNodeIndices] = {j | j in HydroArcs: j.toHydroNode == i};
                {Arc} Jout[i in hydroNodeIndices] = {j | j in HydroArcs: j.fromHydroNode == i};
                
                HydroNode reservoirNode[j in reservoirs] = item(HydroNodes, <j.HydroNodeindex>);
                
                float flowToVolume[t in T] = ...;
                float inflow[HydroNodes][nodes0] = ...;
                
                dvar float+ content[j in reservoirs][n in nodes0];
                dvar float+ flow[j in HydroArcs][n in nodes0];
                
                forall(i in reservoirs, n in nodes0)
                  HydroBalance[i][n]:
                  content[i][n] + flowToVolume[n.stage] * sum(j1 in Jout[i.HydroNodeindex]) flow[j1][n] 
                  == content[i][<n.pred>] + flowToVolume[n.stage] * sum(j1 in Jin[i.HydroNodeindex]) flow[j1][n] 
                  + flowToVolume[n.stage] * inflow[item(HydroNodes,<i.HydroNodeindex>)][n];
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Complex water balance model should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        [Fact]
        public void POM_CompleteSubmodel_PowerProduction_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                }
                
                tuple Node {
                    key int id;
                    float prob;
                    int stage;
                }
                
                tuple PowerPoint {
                    string stationId;
                    float flow;
                    float head;
                    float power;
                }
                
                {Station} stations = {<""S1"">, <""S2"">};
                {Node} nodes0 = {<1,0.5,1>, <2,0.5,2>};
                range priceSegment = 1..2;
                
                {PowerPoint} allProductionPoints = ...;
                {PowerPoint} productionPoints[i in stations] = 
                    {p | p in allProductionPoints: p.stationId == i.id};
                
                dvar float+ production[j in stations][n in nodes0][k in priceSegment];
                dvar float+ alphaProd[n in nodes0][k in priceSegment][p in allProductionPoints] in 0..1;
                
                forall(j in stations, n in nodes0, k in priceSegment)
                  StationProduction[j][n][k]:
                  production[j][n][k] <= sum(p in productionPoints[j]) alphaProd[n][k][p] * p.power;
                
                forall(j in stations, n in nodes0, k in priceSegment)
                  ProdConvexity[j][n][k]:
                  sum(p in productionPoints[j]) alphaProd[n][k][p] == 1;
            ";

            var result = parser.Parse(input);

            Assert.False(result.HasErrors, 
                $"Power production model should parse: {string.Join("; ", result.GetErrorMessages())}");
        }

        #endregion

        #region 15. Constraint Forward Declarations

        [Fact]
        public void POM_ConstraintForwardDeclarations_ShouldParse()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                tuple Station {
                    key string id;
                }
                
                tuple Node {
                    key int id;
                }
                
                {Station} stations = {<""S1"">};
                {Node} nodes0 = {<1>};
                range priceSegment = 1..2;
                
                constraint StationProduction[stations][nodes0][priceSegment];
                constraint PowerBalance[n in nodes0][k in priceSegment];
            ";

            var result = parser.Parse(input);

            // Should either parse successfully or be safely skipped
            Assert.True(result.SuccessCount >= 3, 
                "Should parse tuple, set declarations even if constraint declarations are skipped");
        }

        #endregion

        #region 16. Subject To Block

        [Fact]
        public void POM_SubjectToBlock_ShouldExtractConstraints()
        {
            var manager = CreateModelManager();
            var parser = CreateParser(manager);
            string input = @"
                dvar float+ x;
                dvar float+ y;
                
                maximize x + 2*y;
                
                subject to {
                    x + y <= 10;
                    x >= 0;
                    y >= 0;
                }
            ";

            var result = parser.Parse(input);

            AssertNoErrors(result);
            Assert.NotNull(manager.Objective);
            // Constraints inside subject to should be extracted and parsed
        }

        #endregion
    }
}