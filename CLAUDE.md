# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ModelEditor** is a C# / .NET 10 Windows Forms application for editing, parsing, and validating optimization models in an OPL (CPLEX Optimization Programming Language)-inspired syntax. It parses `.mod` model files and `.dat` data files, expands indexed constraints, and exports to **MPS format** for LP/MIP solvers.

## Solution Structure

Multi-project solution at `src/ModelEditor.slnx`:

| Project | Type | Purpose |
|---------|------|---------|
| `src/Core/` | Class library (`net10.0`) | Parser, model state, expression evaluation, MPS export |
| `src/NetWorks/` | WinForms (`net10.0-windows7.0`) | Primary production GUI ("Optimization Modeler") |
| `src/ModelEditorGUI/` | WinForms (`net10.0-windows`) | Legacy GUI |
| `src/Tests/` | xUnit (`net10.0`) | Unit and integration tests |

## Commands

```bash
# Build
dotnet build

# Run the GUI
dotnet run --project src/NetWorks/ModelEditorApp.csproj

# Run all tests
dotnet test src/Tests/Tests.csproj

# Run a single test class
dotnet test src/Tests/Tests.csproj --filter "FullyQualifiedName~EquationParsingTests"

# Run with code coverage
dotnet test src/Tests/Tests.csproj --collect:"XPlat Code Coverage"
```

## Architecture

### Parsing Pipeline

1. User opens a **RunConfiguration** (pairing a `.mod` model file with a `.dat` data file)
2. **`EquationParser`** orchestrates parsing — delegates to 24+ specialized parsers in `src/Core/Parsing/`
3. **`DataFileParser`** populates external parameters from `.dat` files
4. **`EquationParser.ExpandIndexedEquations()`** expands forall/indexed templates into concrete constraints
5. **`MPSExporter`** serializes the expanded model to MPS format

### Key Classes

**`EquationParser`** (`src/Core/EquationParser.cs`) — Central orchestrator. Drives the full parse of model text through all specialized parsers, manages evaluation via `ExpressionEvaluator` and `JavaScriptEvaluator` (Jint), and expands indexed equations.

**`ModelManager`** (`src/Core/ModelManager.cs`) — Central state container holding all parsed model elements: `Parameters`, `IndexSets`, `IndexedVariables`, `IndexedEquationTemplates`, `Equations`, `Objective`, `ForallStatements`, `TupleSets`, `TupleParameters`.

**Expression system** (`src/Core/Models/Expression.cs`) — Abstract `Expression` base with subclasses: `ConstantExpression`, `ParameterExpression`, `IndexedParameterExpression`, `BinaryOperatorExpression`, `SummationExpression`, `ConditionalExpression`. Supports `Evaluate()` and `Simplify()` for symbolic computation.

**`LinearEquation`** (`src/Core/Models/LinearEquation.cs`) — Represents `a₁*v₁ + a₂*v₂ + ... ≤|=|≥ c`. Stores variable→expression coefficient mapping, RHS constant, relational operator, and optional label.

**`MPSExporter`** (`src/Core/Export/MPSExporter.cs`) — Converts the fully-expanded model to MPS format (NAME, ROWS, COLUMNS, RHS, BOUNDS, ENDATA sections).

**`MainForm`** (`src/NetWorks/MainForm.cs`) — Primary GUI window managing editor tabs, file explorer, results/error panel, and invoking Core parsing services.

### Two-Phase Model Processing

Indexed statements are **not evaluated during parsing** — they are stored as templates. Expansion only happens after data files are loaded:
- `IndexedEquationTemplates` → concrete `Equations` via `ExpandIndexedEquations()`
- `ForallStatements` → concrete constraints via the same expansion pass

This means `MPSExporter` must only be called after expansion is complete.

### JavaScript Evaluator

The `JavaScriptEvaluator` uses **Jint** to evaluate arithmetic expressions that are complex to handle symbolically (e.g., conditional logic). This is an intentional design choice, not a workaround.

## Sample Data

Model and data file examples in `/Data/`:
- `Transport.mod/.dat` — transportation problem
- `Allocation.mod/.dat` — resource allocation
- `Sudoku.mod/.dat` — Sudoku solver
- `POM.mod/.dat` — complex multi-dimensional model

## OPL Syntax Subset Supported

```opl
int N = 5;
float capacity[Products] = ...;   // "..." = external, loaded from .dat

range I = 1..N;
{string} Products = {"A", "B", "C"};

var float+ x[I, J];               // non-negative continuous variable
var int y[I] in 0..100;

minimize sum(i in I) cost[i] * x[i];

subject to {
  forall(i in I) sum(j in J) x[i,j] <= capacity[i];
  myLabel: sum(i in I) x[i] >= 50;
}
```
