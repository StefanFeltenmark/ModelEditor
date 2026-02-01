/*********************************************
 * OPL 12.7.0.0-22.1.1 Model
 *
 * Author: stfe
 * 
 * Creation Date: 19 dec 2016 at 10:00:00
 * Change Date: 13 mar 2023
 * 
 * POM is a stochastic model for midterm planning of a hydro-power system 
 *
 *********************************************/
// Constants
float M = 10e12; // a big number
float HoursToSeconds = 3600;
float VolumeScale = 1e-6; // convert m^3 to Mm^3 
float inflowGlobalScaler = 1.0; // for debugging
float loadScaler = 1.00;
float priceScaler = 1.0;
float spotBuySpread = 0.0;
int WriteOutputFiles = 0;
 
/////////////////////////////////////////////////////////////////////////////////////
// Definitions
/////////////////////////////////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////////////////////////////////
//Reservoir data
/////////////////////////////////////////////////////////////////////////////////////
tuple Reservoir
{
  key string id;
  string HydroNodeindex;  
  float minLevel;
  float initialLevel;
  float initialContent;  
}

tuple ReservoirTData
{
  key string id; 
  key int t;  
  float contentMin;
  float contentMax;  
  float tacticalMinContent;
  float tacticalMaxContent;
  float tacticalMinContentCost;
  float tacticalMaxContentCost;
}


tuple Station
{
  key string id;
  string arcindex;   
  float energyEquivalent; 
  float tailWaterLevel;
  float refhead;
  int isRunOfRiver;
  string reservoirId;
}

tuple StationTData
{
  key string id;
  key int t;  
  float releaseMin;
  float releaseMax;
  float productionMin;  
  float productionMax;    
  float productionSoftMin;
  float productionSoftMax;
  float productionSoftMinCost;
  float productionSoftMaxCost;  
}

tuple Pump
{
  key string id;  
  string arcindex;   
  float energyEquivalent; 
  float referenceLevel; 
  string reservoirId;
}

tuple PumpTData
{
  key string id;
  key int t;  
  float pumpPowerMin;
  float pumpPowerMax;
  float pumpVolumeMin;
  float pumpVolumeMax;  
}

tuple Junction
{
  key string id;
  string HydroNodeindex;    
}

tuple Waterway
{
  key string id; 
  string arcindex; 
}

tuple ReservoirSegment
{  
  key string id; 
  key float Area;
  key float Height;
}

tuple HydroNode
{
  key string id;
}

tuple Arc
{
   key string id;
   string fromHydroNode;
   string toHydroNode;
}

tuple ArcTData
{
   key string id;
   key int t;
   float flowMin;
   float flowMax;
   float flowMinSoft;
   float flowMaxSoft;
   float flowMinSoftCost;
   float flowMaxSoftCost;   
}

tuple PowerPoint
{ 
  string stationId; 
  float flow;
  float head;
  float power;
}

tuple ScenarioTreeNode
{
  key int id;
  float prob;
  int stage;
  int pred;
}

tuple EnergyContract
{
  key string id;  
  string type;
  int startPeriod;
  int endPeriod;
  float minEnergy;
  float maxEnergy;
  float minPower;
  float maxPower;
  float price;
}

tuple ContractTData
{
  key string id;  
  key int t;   
  float minPower;
  float maxPower;
  float price;
}

tuple stationGroup {
  key string id;  
  key string stationId;  
};

tuple stationGroupTData {
  key string group;  
  key int t;
  float groupProdMin;
  float groupProdMax;
  float groupReleaseMin;
  float groupReleaseMax;
};

tuple MarketTData
{
   key int t;
   float maxBuy;
   float maxSell;
}

tuple Reserve
{
  key string id;
  int direction; // 0 means up, 1 means down
}

tuple ReserveT
{
  key string id;
  key int t;
  float price; // income from providing reserve
  float maxVolume;
  float minVolume; // could be an obligation
  float deficitPenalty; // for not meeting the minVolume
}

tuple StationReserve
{ 
  key string id; 
  string reserveId;
  string stationId;      
}

tuple SymmetricPair
 {
   string ReserveProvider1;
   string ReserveProvider2; 
 }

 tuple StationReserveT
 {   	
 	key string id; 	
    key int t;                 
    float reserveMin;
    float reserveMax;         
    int availability;                  
 }

/////////////////////////////////////////////////////////////////////////////////////
//External data
/////////////////////////////////////////////////////////////////////////////////////
int nT = ...;                  // number of planning periods
range T  = 1..nT;              // indices of planning periods
int nPriceSegments = ...; // for now
int UseAverageHeadForProduction = ...;

range priceSegment = 1..nPriceSegments;

{HydroNode} HydroNodes = ...;

{Arc} HydroArcs = ...;
{ArcTData} HydroArcTs = ...;
ArcTData arcT[s in HydroArcs][t in T] = item(HydroArcTs, <s.id,t>);


{string} OverFlowArcs = ...;

// To time series
float OverflowCost = ...;
float energyImbalanceCost = ...; 

{Junction} junctions = ...;
{Reservoir} reservoirs = ...;

{ReservoirSegment} allReservoirSegments = ...;
{ReservoirSegment} reservoirSegments[r in reservoirs] = { s | s in allReservoirSegments: s.id == r.id};

{ReservoirTData} reservoirTs = ...;
ReservoirTData reservoirT[r in reservoirs][t in T] = item(reservoirTs, <r.id,t>);

{Station} stations = ...;
{StationTData} stationTs = ...;
StationTData stationT[s in stations][t in T] = item(stationTs, <s.id,t>);

int nProductionPoints = ...;
{PowerPoint} allProductionPoints = ...; //

{Waterway} waterways = ...;

{Pump} pumps = ...;
{PumpTData} pumpTs = ...;
PumpTData pumpT[s in pumps][t in T] = item(pumpTs, <s.id,t>);

int nConsumptionPoints = ...;
{PowerPoint} allConsumptionPoints = ...; //

{EnergyContract} contracts = ...;
{ContractTData} contractTs = ...;
ContractTData contractT[c in contracts][t in T] = item(contractTs, <c.id,t>);

{EnergyContract} buycontracts = { c | c in contracts: c.type == "Buy"};
{EnergyContract} sellcontracts = { c | c in contracts: c.type == "Sell"};

{string} groups = ...;
{stationGroup} groupDefinitions = ...;
{Station} stationsInGroup[g in groups] = { s | s in stations, d in groupDefinitions: s.id == d.stationId && d.id == g};
{stationGroupTData} stationGroupTs = ...;
stationGroupTData stationGroupT[g in groups][t in T] = item(stationGroupTs, <g,t>);

{PowerPoint} productionPoints[i in stations] = { p | p in allProductionPoints: p.stationId == i.id};
{PowerPoint} consumptionPoints[i in pumps] = { p | p in allConsumptionPoints: p.stationId == i.id};

//float maxBuy[T] = ...;
//float maxSell[T] = ...;
float powerLoad[T][priceSegment] = ...;  // net obligations

float productionCost[stations][T][priceSegment] = ...;
float pumpCost[pumps][T][priceSegment] = ...;

// End water value cut description
 int nCuts = ...;
 range Cuts = 1..nCuts;
 float cuts[k in Cuts][j in reservoirs] = ...;
 float cutRHS[k in Cuts] = ...;
 
 {MarketTData} marketTs = ...;
 MarketTData marketT[t in T] = item(marketTs, <t>);
 
 int perLengthHours[T] = ...;   // length of each planning period in hours
 int nHoursPerPriceSegment[T][priceSegment] = ...;
 
 {ScenarioTreeNode} nodes = ...;
 float marketPrice[nodes][priceSegment] = ...; // spot price
 float inflow[HydroNodes][nodes] = ...;
 float discountFactor[T] = ...; // discount factor
 
 {Reserve} reserves = ...;
 {ReserveT} reserveT = ...;
 
 {StationReserve} stationReserves = ...;
 {StationReserveT} stationReservesT = ...;
 
 {SymmetricPair} symmetricReserves = ...; 
  
/////////////////////////////////////////////////////////////////////////////////////
// Internal data
/////////////////////////////////////////////////////////////////////////////////////
float segmentFlowToVolume[t in T][k in priceSegment] = HoursToSeconds*VolumeScale*nHoursPerPriceSegment[t][k];
ScenarioTreeNode root = item(nodes,0);
{ScenarioTreeNode} leaves = {n | n in nodes: n.stage == nT};
{ScenarioTreeNode} nodes0 = {n | n in nodes: n.stage >= 1};
{string} hydroNodeIndices = { i.id | i in HydroNodes};
{Arc} Jout[i in hydroNodeIndices] = {j | j in HydroArcs: j.fromHydroNode == i};
{Arc} Jin[i in hydroNodeIndices] = {j | j in HydroArcs: j.toHydroNode == i};  
{Arc} J0 = {item(HydroArcs,<i>) | i in OverFlowArcs};


// rescale period length to seconds
float perLengthSeconds[t in T] = perLengthHours[t]*HoursToSeconds;

// scale flow over period to Mm^3
float flowToVolume[t in T] = VolumeScale*perLengthSeconds[t]; 

HydroNode stationFromNode[j in stations] = item(HydroNodes,<item(HydroArcs,<j.arcindex>).fromHydroNode>);
HydroNode reservoirNode[j in reservoirs] = item(HydroNodes, <j.HydroNodeindex>);
HydroNode pumpFromNode[j in pumps] = item(HydroNodes,<item(HydroArcs,<j.arcindex>).fromHydroNode>);

Reservoir upReservoir[i in J0] = first({j | j in reservoirs: j.HydroNodeindex == i.fromHydroNode});

execute{
    upReservoir;
}

// Compute incoming reservoir value:

//sum(j in reservoirs) cuts[k][j]*initialcontent[j] + cutRHS[k];
float incomingReservoirValue = 0.0;
execute CalulateIncomingReservoirValue
{
   var minVal = M;
   for(var k in Cuts)
   {
     var cutVal = 0.0;   
     for(var j in reservoirs)
     {
         cutVal = cutVal + cuts[k][j]*j.initialContent + cutRHS[k];
     }
     if( cutVal < minVal)
     {
       minVal = cutVal;     
     }
   }     
   incomingReservoirValue = minVal;
     
}

ReserveT reserveTs[r in reserves][t in T] = item(reserveT,<r.id, t>);
StationReserveT stationReserveTs[r in stationReserves][t in T] = item(stationReservesT,<r.id, t>);

{StationReserve} reserveParticipants[r in reserves] = {s | s in stationReserves: s.reserveId == r.id};

{Reserve} upwardReserves = {r | r in reserves: r.direction == 1};
{Reserve} downwardReserves = {r | r in reserves: r.direction == 0};

float maxLevel[i in reservoirs] = i.minLevel + sum(s in reservoirSegments[i]) s.Height; 


/////////////////////////////////////////////////////////////////////////////////////
// Variables
/////////////////////////////////////////////////////////////////////////////////////
dvar float+ marketSales[n in nodes0][k in priceSegment] in 0..marketT[n.stage].maxSell;
dvar float+ marketPurchase[n in nodes0][k in priceSegment] in 0..marketT[n.stage].maxBuy;
dvar float+ powerSurplus[n in nodes0][k in priceSegment];
dvar float+ powerDeficit[n in nodes0][k in priceSegment];
dvar float+ flow[j in HydroArcs][n in nodes0] in arcT[j][n.stage].flowMin..arcT[j][n.stage].flowMax;
dvar float+ stationRelease[j in stations][n in nodes0][priceSegment]  in stationT[j][n.stage].releaseMin..stationT[j][n.stage].releaseMax;
dvar float+ production[j in stations][n in nodes0][priceSegment]; //  in stationT[j][n.stage].productionMin..stationT[j][n.stage].productionMax;
dvar float+ pumpPower[j in pumps][n in nodes0][priceSegment]  in pumpT[j][n.stage].pumpPowerMin..pumpT[j][n.stage].pumpPowerMax;
dvar float+ pumpVolume[j in pumps][n in nodes0][priceSegment]  in pumpT[j][n.stage].pumpVolumeMin..pumpT[j][n.stage].pumpVolumeMax;
dvar float+ content[j in reservoirs][n in nodes];
dvar float+ level[i in HydroNodes][n in nodes];
dvar float head[i in stations][n in nodes0][priceSegment];
dvar float+ pumpinghead_pos[i in pumps][n in nodes0][priceSegment];
//dvar float+ pumpinghead_neg[i in pumps][n in nodes0][priceSegment];
dvar float+ segLevel[s in allReservoirSegments][n in nodes0];
dvar float+ tacticalUpperViolation[i in reservoirs][n in nodes0];
dvar float+ tacticalLowerViolation[i in reservoirs][n in nodes0];
dvar float+ softLimitLowerViolation[i in HydroArcs][n in nodes0];
dvar float+ softLimitUpperViolation[i in HydroArcs][n in nodes0];
dvar float+ softUpperLimitViolationProd[i in stations][n in nodes0][priceSegment];
dvar float+ softLowerLimitViolationProd[i in stations][n in nodes0][priceSegment];
dvar float endWaterValue[n in leaves];
dvar float+ alphaProd[n in nodes0][k in priceSegment][p in allProductionPoints] in 0..1;
dvar float+ alphaPump[n in nodes0][k in priceSegment][p in allConsumptionPoints] in 0..1;
dvar float+ contractUse[c in contracts][n in nodes0][k in priceSegment] in c.minPower..c.maxPower;
dvar float+ contractPosition[c in contracts][n in nodes];
dvar float+ resbalsurplus[i in reservoirs][n in nodes] in 0..10000;
dvar float+ resbaldeficit[i in reservoirs,n in nodes] in 0..10000;
dvar float+ reserve[stationReserves][n in nodes];
dvar float+ reserveDeficit[reserves][n in nodes]; 

/////////////////////////////////////////////////////////////////////////////////////
// Decision expressions
/////////////////////////////////////////////////////////////////////////////////////

//objective terms
dexpr float expectedMarketIncome = sum(n in nodes0) n.prob*discountFactor[n.stage]*(sum(k in priceSegment) nHoursPerPriceSegment[n.stage][k]*priceScaler*marketPrice[n][k]*marketSales[n][k]);
dexpr float expectedMarketCost = sum(n in nodes0) n.prob*discountFactor[n.stage]*(sum(k in priceSegment) nHoursPerPriceSegment[n.stage][k]*(priceScaler*marketPrice[n][k]+spotBuySpread)*marketPurchase[n][k]);
dexpr float TotalTLpenalty = sum(i in reservoirs, n in nodes0) n.prob*discountFactor[n.stage]*(reservoirT[i][n.stage].tacticalMaxContentCost*tacticalUpperViolation[i][n] + reservoirT[i][n.stage].tacticalMinContentCost*tacticalLowerViolation[i][n]);
dexpr float TotalLowerSoftLimitPenalty = sum(i in HydroArcs, n in nodes0) n.prob*discountFactor[n.stage]*arcT[i][n.stage].flowMinSoftCost*softLimitLowerViolation[i][n];
dexpr float TotalUpperSoftLimitPenalty = sum(i in HydroArcs, n in nodes0) n.prob*discountFactor[n.stage]*arcT[i][n.stage].flowMaxSoftCost*softLimitUpperViolation[i][n];
//dexpr float TotalSoftProdLimitPenalty = sum(i in stations, n in nodes0, k in priceSegment) n.prob*discountFactor[n.stage]*(productionSoftMinCost[i][n.stage]*softLowerLimitViolationProd[i][n][k] + productionSoftMaxCost[i][n.stage]*softUpperLimitViolationProd[i][n][k]);
dexpr float TotalOFpenalty = sum(j in J0, n in nodes0) n.prob*discountFactor[n.stage]*OverflowCost*flow[j][n]; 
dexpr float ExpectedEndWaterValue = sum(n in leaves) n.prob*discountFactor[n.stage]*endWaterValue[n];
dexpr float expectedImbalanceCost = sum(n in nodes0) n.prob*discountFactor[n.stage]*(sum(k in priceSegment) energyImbalanceCost*nHoursPerPriceSegment[n.stage][k]*(powerSurplus[n][k]+powerDeficit[n][k]));
dexpr float totalBuyContractCost = sum(c in buycontracts) sum(n in nodes0) n.prob*discountFactor[n.stage]*contractT[c][n.stage].price*(sum(k in priceSegment) nHoursPerPriceSegment[n.stage][k]*contractUse[c][n][k]);
dexpr float totalSellContractIncome = sum(c in sellcontracts) sum(n in nodes0) n.prob*discountFactor[n.stage]*contractT[c][n.stage].price*(sum(k in priceSegment) nHoursPerPriceSegment[n.stage][k]*contractUse[c][n][k]);
dexpr float rbaldiffpenalty = sum(i in reservoirs, n in nodes) n.prob*10000000*(resbalsurplus[i][n] + resbaldeficit[i][n]);
dexpr float totalPenaltyCost = TotalTLpenalty + TotalLowerSoftLimitPenalty + TotalUpperSoftLimitPenalty + TotalOFpenalty; // + TotalSoftProdLimitPenalty + rbaldiffpenalty;
dexpr float groupProd[g in groups,n in nodes0, k in priceSegment] = sum(j in stationsInGroup[g]) production[j][n][k];
dexpr float groupRelease[g in groups,n in nodes0, k in priceSegment] = sum(j in stationsInGroup[g]) stationRelease[j][n][k];
dexpr float totalProductionCost = sum(j in stations, n in nodes0) n.prob*discountFactor[n.stage]*(sum(k in priceSegment) nHoursPerPriceSegment[n.stage][k]*productionCost[j][n.stage][k]*production[j][n][k]);
dexpr float totalPumpCost = sum(j in pumps, n in nodes0) n.prob*discountFactor[n.stage]*(sum(k in priceSegment) nHoursPerPriceSegment[n.stage][k]*pumpCost[j][n.stage][k]*pumpPower[j][n][k]);
dexpr float totalReserve[r in reserves, n in nodes0] = sum(s in reserveParticipants[r]) reserve[s][n];
dexpr float expectedReserveIncome = sum(r in reserves, n in nodes0) n.prob*discountFactor[n.stage]*reserveTs[r][n.stage].price*totalReserve[r,n];
dexpr float expectedReserveDeficitCost = sum(r in reserves, n in nodes0) n.prob*discountFactor[n.stage]*reserveTs[r][n.stage].deficitPenalty*reserveDeficit[r][n];
dexpr float objective = expectedMarketIncome +  expectedReserveIncome - expectedReserveDeficitCost - expectedMarketCost - totalPenaltyCost - totalProductionCost - totalPumpCost + ExpectedEndWaterValue - expectedImbalanceCost + totalSellContractIncome - totalBuyContractCost - incomingReservoirValue -rbaldiffpenalty;

execute{
groupProd;
groupRelease;
}
/////////////////////////////////////////////////////////////////////////////////////
// Constraints
/////////////////////////////////////////////////////////////////////////////////////
constraint StationProduction[stations][nodes0][priceSegment];
constraint StationRelease[stations][nodes0][priceSegment];
constraint StationHead[stations][nodes0][priceSegment];
constraint ProdConvexity[stations][nodes0][priceSegment];
constraint PowerBalance[n in nodes0][k in priceSegment];
constraint PumpConsumption[pumps][nodes0][priceSegment];
constraint PumpVolume[pumps][nodes0][priceSegment];
constraint PumpHead[pumps][nodes0][priceSegment];
constraint PumpConvexity[pumps][nodes0][priceSegment];
constraint StationReleaseToArcFlow[stations][nodes0];
constraint PumpVolumeToArcFlow[pumps][nodes0];
constraint HydroBalanceReservoir[reservoirs][nodes0];
constraint HydroBalanceReservoir0[reservoirs];
constraint ReservoirLevel0[reservoirs];
constraint HydroBalanceJunction[junctions][nodes];
constraint TacticalUpperReservoirLimit[reservoirs][nodes];
constraint TacticalLowerReservoirLimit[reservoirs][nodes];
constraint ReservoirModelLevel[reservoirs][nodes];
constraint ReservoirModelLevelBounds[allReservoirSegments][n in nodes];
constraint ReservoirModelContent[reservoirs][nodes];
constraint HeadLevelRelationStation[stations][nodes][priceSegment];
constraint HeadLevelRelationPump[pumps][nodes][priceSegment];
constraint MaxFlowLimitConstraint[HydroArcs][nodes0];
constraint MinFlowLimitConstraint[HydroArcs][nodes0];
constraint MaxProdLimitConstraint[stations][nodes0][priceSegment];
constraint MinProdLimitConstraint[stations][nodes0][priceSegment];
constraint cutConstraint[Cuts][leaves];
constraint ContractBalance[contracts][nodes0];
constraint ContractPowerLimits[contracts][nodes0][priceSegment];
constraint ContractFinalPosition[c in contracts][leaves];
constraint GroupProductionMax[groups,nodes0,priceSegment];
constraint GroupProductionMin[groups,nodes0,priceSegment];
constraint GroupReleaseMax[groups,nodes0,priceSegment];
constraint GroupReleaseMin[groups,nodes0,priceSegment];
constraint NoPriceSegmentsForRoR[stations,nodes0,priceSegment];
constraint ReservoirContentMinLimit[reservoirs][nodes0];
constraint ReservoirContentMaxLimit[reservoirs][nodes0]; 
constraint ReserveDeficit[reserves,nodes0];
constraint UpperReserveLimit[reserves,nodes0];
constraint ProdAndReserveUpperLimit[stations,nodes0,priceSegment];
constraint ProdAndReserveLowerLimit[stations,nodes0,priceSegment];
constraint ReserveUpperLimit[stationReserves,nodes0];
constraint ReserveLowerLimit[stationReserves,nodes0];
constraint SymmetricReserveConstraint[symmetricReserves,nodes0];

/////////////////////////////////////////////////////////////////////////////////////
// Model definition
/////////////////////////////////////////////////////////////////////////////////////
maximize objective;
subject to
{
// Station flow to power
forall(j in stations, n in nodes0, k in priceSegment)
  StationProduction[j][n][k]:
  production[j][n][k] <= sum(p in productionPoints[j]) alphaProd[n][k][p]*p.power;
   
forall(j in stations, n in nodes0, k in priceSegment)
  StationRelease[j][n][k]:
  stationRelease[j][n][k] == sum(p in productionPoints[j]) alphaProd[n][k][p]*p.flow;

forall(j in stations, n in nodes0, k in priceSegment)
  StationHead[j][n][k]:
  head[j][n][k] == sum(p in productionPoints[j]) alphaProd[n][k][p]*p.head;


forall(j in stations, n in nodes0, k in priceSegment)
  ProdConvexity[j][n][k]:
  sum(p in productionPoints[j]) alphaProd[n][k][p] == 1;
     
// Pump power to flow
forall(j in pumps, n in nodes0, k in priceSegment)
  PumpConsumption[j][n][k]:
  pumpPower[j][n][k] == sum(p in consumptionPoints[j]) alphaPump[n][k][p]*p.power;

forall(j in pumps, n in nodes0, k in priceSegment)
  PumpVolume[j][n][k]:
  pumpVolume[j][n][k] == sum(p in consumptionPoints[j]) alphaPump[n][k][p]*p.flow;

forall(j in pumps, n in nodes0, k in priceSegment)
  PumpHead[j][n][k]:
  pumpinghead_pos[j][n][k] == sum(p in consumptionPoints[j]) alphaPump[n][k][p]*p.head;

forall(j in pumps, n in nodes0, k in priceSegment)
  PumpConvexity[j][n][k]:
     sum(p in consumptionPoints[j]) alphaPump[n][k][p] == 1;
     
// Power balance
forall(n in nodes0, k in priceSegment)
  PowerBalance[n][k]:
sum(j in stations) production[j][n][k] - sum(j in pumps) pumpPower[j][n][k] - marketSales[n][k] + marketPurchase[n][k] + sum(c in buycontracts) contractUse[c][n][k] - powerSurplus[n][k] + powerDeficit[n][k] == sum(c in sellcontracts) contractUse[c][n][k] + loadScaler*powerLoad[n.stage][k];

  
// Flow on station arcs
forall(j in stations, n in nodes0) 
  // Flow on station arcs
  StationReleaseToArcFlow[j][n]:
  flowToVolume[n.stage]*flow[item(HydroArcs,<j.arcindex>)][n] == sum(k in priceSegment) segmentFlowToVolume[n.stage][k]*stationRelease[j][n][k];
  
// Flow on pump arcs
forall(j in pumps, n in nodes0)
  PumpVolumeToArcFlow[j][n]:
  flowToVolume[n.stage]*flow[item(HydroArcs,<j.arcindex>)][n] == sum(k in priceSegment) segmentFlowToVolume[n.stage][k]*pumpVolume[j][n][k];
  
forall(i in reservoirs)
  HydroBalanceReservoir0[i]:
  content[i][root] + resbaldeficit[i][root] - resbalsurplus[i][root] == i.initialContent;

forall(i in reservoirs)
  ReservoirLevel0[i]:
  level[reservoirNode[i]][root] == i.initialLevel;
  
forall(i in reservoirs, n in nodes0) 
ReservoirContentMinLimit[i][n]: 
 content[i][n] >=  reservoirT[i][n.stage].contentMin;
  
forall(i in reservoirs, n in nodes0)
ReservoirContentMaxLimit[i][n]:   
 content[i][n] <= reservoirT[i][n.stage].contentMax;
  
// Balance at reservoir, m^3
forall(i in reservoirs, n in nodes0)
 HydroBalanceReservoir[i][n]:
content[i][n] + flowToVolume[n.stage]*sum(j1 in Jout[i.HydroNodeindex]) flow[j1][n]  + resbaldeficit[i][n] - resbalsurplus[i][n] == content[i][<n.pred>] + flowToVolume[n.stage]*sum(j1 in Jin[i.HydroNodeindex]) flow[j1][n] + inflowGlobalScaler*flowToVolume[n.stage]*inflow[item(HydroNodes,<i.HydroNodeindex>)][n];
//content[i][n] + flowToVolume[n.stage]*sum(j1 in Jout[i.HydroNodeindex]) flow[j1][n] == content[i][<n.pred>] + flowToVolume[n.stage]*sum(j1 in Jin[i.HydroNodeindex]) flow[j1][n] + inflowGlobalScaler*flowToVolume[n.stage]*inflow[item(HydroNodes,<i.HydroNodeindex>)][n];
 
// Balance at junction
forall(i in junctions, n in nodes0)
   HydroBalanceJunction[i][n]:
sum(j1 in Jout[i.HydroNodeindex]) flow[j1][n] == sum(j1 in Jin[i.HydroNodeindex]) flow[j1][n] + inflow[item(HydroNodes,<i.HydroNodeindex>)][n];  

// Water valuation
forall( k in Cuts, n in leaves)
  cutConstraint[k][n]:
   endWaterValue[n] <= sum(j in reservoirs) cuts[k][j]*content[j][n] + cutRHS[k];

// Relation between content and head
forall(i in reservoirs, n in nodes0)
  ReservoirModelLevel[i][n]:
  level[reservoirNode[i]][n] == i.minLevel + sum(s in reservoirSegments[i]) segLevel[s][n];  

forall(i in reservoirs, n in nodes0, s in reservoirSegments[i])
  ReservoirModelLevelBounds[s][n]:
  segLevel[s][n] <= s.Height;          

forall(i in reservoirs, n in nodes0)
  ReservoirModelContent[i][n]:
  content[i][n] == sum(s in reservoirSegments[i]) s.Area*segLevel[s][n];
   
   
forall(j in stations, n in nodes0, k in priceSegment)
  HeadLevelRelationStation[j][n][k]:
  if(UseAverageHeadForProduction == 1)
  {     
     head[j,n,k] == (level[stationFromNode[j],n]+level[stationFromNode[j],<n.pred>])/2 - j.tailWaterLevel;
  }
  else
  {   
    head[j,n,k] == level[stationFromNode[j],n] - j.tailWaterLevel;
  }  
   
forall(j in pumps, n in nodes0,k in priceSegment)
  HeadLevelRelationPump[j][n][k]:  
   pumpinghead_pos[j,n,k] == j.referenceLevel - level[pumpFromNode[j],n];
 //  pumpinghead_pos[j,n,k] - pumpinghead_neg[j,n,k] == level[pumpToNode[j],n] - level[pumpFromNode[j],n];

//// Tactical reservoir upper limit
forall(i in reservoirs, n in nodes0)
  TacticalUpperReservoirLimit[i][n]:
  tacticalUpperViolation[i][n] >= content[i][n] - reservoirT[i][n.stage].tacticalMaxContent;
  
////// Tactical reservoir lower limit
forall(i in reservoirs, n in nodes0)
  TacticalLowerReservoirLimit[i][n]:
 tacticalLowerViolation[i][n] >= reservoirT[i][n.stage].tacticalMinContent - content[i][n];
       
// Extra spill limits
if(false)
{
forall(i in J0, n in nodes0)  
  flow[i][n] <=  (arcT[i][n.stage].flowMax/(maxLevel[upReservoir[i]] - upReservoir[i].minLevel))*(level[item(HydroNodes,<upReservoir[i].HydroNodeindex>)][n] - upReservoir[i].minLevel);
}         
// Soft limits on hydroarcs
forall(i in HydroArcs, n in nodes0)
  MaxFlowLimitConstraint[i][n]:
  softLimitUpperViolation[i][n] >= flow[i][n] - arcT[i][n.stage].flowMaxSoft;

forall(i in HydroArcs, n in nodes0)
  MinFlowLimitConstraint[i][n]:
  softLimitLowerViolation[i][n] >=  arcT[i][n.stage].flowMinSoft - flow[i][n];

// Soft limits on production
forall(i in stations, n in nodes0, k in priceSegment)
  MaxProdLimitConstraint[i][n][k]:
  softUpperLimitViolationProd[i][n][k] >= production[i][n][k] - stationT[i][n.stage].productionSoftMax;

forall(i in stations, n in nodes0, k in priceSegment)
  MinProdLimitConstraint[i][n][k]:
  softLowerLimitViolationProd[i][n][k] >=  stationT[i][n.stage].productionSoftMin - production[i][n][k];
  
// Station groups
forall(g in groups, n in nodes0, k in priceSegment)  
 GroupProductionMax[g,n,k]:
  sum(j in stationsInGroup[g]) production[j][n][k] <=  stationGroupT[g][n.stage].groupProdMax;
  
forall(g in groups, n in nodes0, k in priceSegment)
  GroupProductionMin[g,n,k]:  
  sum(j in stationsInGroup[g]) production[j][n][k] >=  stationGroupT[g][n.stage].groupProdMin;

forall(g in groups, n in nodes0, k in priceSegment)
  GroupReleaseMax[g,n,k]:  
  sum(j in stationsInGroup[g]) stationRelease[j][n][k] <=  stationGroupT[g][n.stage].groupReleaseMax;
  
forall(g in groups, n in nodes0, k in priceSegment)
  GroupReleaseMin[g,n,k]:  
  sum(j in stationsInGroup[g]) stationRelease[j][n][k] >=  stationGroupT[g][n.stage].groupReleaseMin;
  
// Contracts  
forall(c in contracts, n in nodes0)
  ContractBalance[c][n]:
  contractPosition[c][n] == contractPosition[c][<n.pred>] + sum(k in priceSegment) contractUse[c][n][k]*nHoursPerPriceSegment[n.stage][k]; 

forall(c in contracts, n in nodes0, k in priceSegment)
  ContractPowerLimits[c][n][k]:
  contractT[c][n.stage].minPower <= contractUse[c][n][k] <= contractT[c][n.stage].maxPower;

  
forall(c in contracts, n in leaves)
  ContractFinalPosition[c][n]:
   c.minEnergy <= contractPosition[c][n] <= c.maxEnergy;
   
   
forall(i in stations: i.isRunOfRiver==1, n in nodes0, k in priceSegment)
  NoPriceSegmentsForRoR[i,n,k]:
  stationRelease[i][n][k] == (1/nPriceSegments)*sum(l in priceSegment) stationRelease[i][n][l];

// Reserve deficit
forall(r in reserves, n in nodes0)
  ReserveDeficit[r,n]:
  totalReserve[r,n] + reserveDeficit[r,n] >= reserveTs[r,n.stage].minVolume;
  
// Reserve limit
forall(r in reserves, n in nodes0)
  UpperReserveLimit[r,n]:
  totalReserve[r,n] <= reserveTs[r,n.stage].maxVolume;
   
// Reserve and production limit, upward
forall(s in stations, n in nodes0, k in priceSegment)
  ProdAndReserveUpperLimit[s,n,k]:
	production[s][n][k] + sum(r in upwardReserves, p in reserveParticipants[r]: p.stationId == s.id) reserve[p][n] <= stationT[s][n.stage].productionMax;
	
// Reserve and production limit, downward

forall(s in stations, n in nodes0, k in priceSegment)
  ProdAndReserveLowerLimit[s,n,k]:
	production[s][n][k] - sum(r in downwardReserves, p in reserveParticipants[r]: p.stationId == s.id) reserve[p][n] >= stationT[s][n.stage].productionMin;     

// Reserve limits
forall(s in stationReserves, n in nodes0)
  ReserveUpperLimit[s,n]:
	reserve[s][n] <= stationReserveTs[s][n.stage].reserveMax;
	
forall(s in stationReserves, n in nodes0)
  ReserveLowerLimit[s,n]:
	reserve[s][n] >= stationReserveTs[s][n.stage].reserveMin;

/**************************************
 * Symmetric reserves
 ***************************************/
 forall(p in symmetricReserves, n in nodes0)
  SymmetricReserveConstraint[p,n]:
   reserve[item(stationReserves,<p.ReserveProvider1>)][n] - reserve[item(stationReserves,<p.ReserveProvider2>)][n] == 0;

}

/////////////////////////////////////////////////////////////////////////////////////
// END OF MODEL
/////////////////////////////////////////////////////////////////////////////////////

//main
//{
//   
//   thisOplModel.generate();
// 
//   var status = cplex.solve();
//   
//   if( status == 1)
//   {
//   var obj = cplex.getObjValue();
//   
//   writeln("Objective = ",obj);
//   
//   writeln("Postprocessing...");
//   
//   thisOplModel.postProcess();
//     
//   writeln("Done.");
//       
// }   
//} 
 

float WaterValues[reservoirs][nodes];
float PowerShadows[nodes][priceSegment];
 
  
execute FillDuals {
  for(var i in reservoirs) {    
    WaterValues[i][root] = HydroBalanceReservoir0[i].dual;        
    for(var n in nodes0)
    {      
     	WaterValues[i][n] = HydroBalanceReservoir[i][n].dual;           
    }     
  }
                    
    for(var n in nodes0)
    {      
      for(var k in priceSegment)
      {
     	 PowerShadows[n][k] = PowerBalance[n][k].dual;
       }     	           
    }     
  
}  
 
execute WriteContent{
if(WriteOutputFiles==1)
{

	var ofile = new IloOplOutputFile("content_sto.csv");
	for(var n in nodes0)
	  {
	    ofile.write(";");
	    ofile.write(n.stage);
	  }
	  
	
	ofile.writeln();
	for(var res in reservoirs)
	{
	  ofile.write(res.name)
	
	  for(var n in nodes0)
	  {
	   ofile.write(";");    
	   ofile.write(content[res][n]);
	 } 
	 ofile.writeln();  
	}
	ofile.close();
 }
}

execute WriteProduction{

if(WriteOutputFiles==1)
{

var ofile = new IloOplOutputFile("production_sto.csv");
for(var n in nodes0)
  {
    ofile.write(";");
    ofile.write(n.stage);
  }
  ofile.writeln();
for(var station in stations)
{
  ofile.write(station.name)

  for(var n in nodes0)
  {
    
  for(var k in priceSegment)
  {
   ofile.write(";");    
   ofile.write(production[station][n][k]);
  }   
 } 
 ofile.writeln();  
}
for(var n in nodes0)
  {
    ofile.write(";");
    ofile.write(marketPrice[n]);
  }
  ofile.writeln();
ofile.close();
}
}

execute WriteStationRelease{

if(WriteOutputFiles==1)
{

var ofile = new IloOplOutputFile("stationrelease_sto.csv");
for(var n in nodes0)
  {
    ofile.write(";");
    ofile.write(n.stage);
  }
  ofile.writeln();
for(var station in stations)
{
  ofile.write(station.name)

  for(var n in nodes0)
  {
   ofile.write(";");    
   ofile.write(stationRelease[station][n]);
 } 
 ofile.writeln();  
}

ofile.close();
}
}

execute WritePumping{
if(WriteOutputFiles==1)
{

var ofile = new IloOplOutputFile("pumping_sto.csv");
for(var n in nodes0)
  {
    ofile.write(";");
    ofile.write(n.stage);
  }
  ofile.writeln();
for(var pump in pumps)
{
  ofile.write(pump.name)

  for(var n in nodes0)
  {
   ofile.write(";");    
   ofile.write(pumpPower[pump][n]);
 } 
 ofile.writeln();  
}
for(var n in nodes0)
  {
    ofile.write(";");
    ofile.write(marketPrice[n]);
  }
  ofile.writeln();
ofile.close();
}

}
 