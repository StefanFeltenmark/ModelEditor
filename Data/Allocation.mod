// Parameters
int n = 5;
int m = 3;

// Index sets
range Products = 1..n;
range Resources = 1..m;

// External data
float cost[Products] = ...;
float resourceUsage[Products, Resources] = ...;
float resourceLimit[Resources] = ...;

// Decision variables (OPL style)
dvar float+ production[Products];  // Non-negative by default

// Objective
minimize totalCost: sum(p in Products) cost[p] * production[p];

// Constraints (OPL style with forall)
forall(r in Resources)
  resourceConstraint: 
    sum(p in Products) resourceUsage[p,r] * production[p] <= resourceLimit[r];

forall(p in Products)
  minProduction: 
    production[p] >= 10;