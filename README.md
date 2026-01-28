# CobolToMySql Studio

A Windows desktop WPF (.NET 8, C# 12) application that parses COBOL copybooks and generates MySQL schemas (DDL), ingests fixed-width data into staging tables, and translates simple COBOL-like logic into SQL transformations for curated tables/views.

This app interprets the copybook structure and emits SQL that reproduces COBOL data semantics without executing COBOL inside MySQL.

## Tech Stack
- .NET 8 (C# 12)
- WPF (MVVM via CommunityToolkit.Mvvm)
- MySQL 8 via MySqlConnector
- Clean Architecture: Domain / Application / Infrastructure / UI / Tests

## Project Layout
- CobolToMySqlStudio.Domain — Copybook AST models
- CobolToMySqlStudio.Application — Services (parser, layout calculation, SQL generation, import, transformations)
- CobolToMySqlStudio.Infrastructure — MySQL executor (MySqlConnector)
- CobolToMySqlStudio.UI — WPF app (MVVM), DI/hosting, views
- CobolToMySqlStudio.Tests — xUnit tests
- Samples — sample copybook and data

## Features Implemented
- Import Copybook (.cpy/.txt)
  - Internal parser builds an AST with levels, PICTURE, USAGE, OCCURS, REDEFINES (structure captured), offsets estimated.
- Preview Layout
  - Tree view of groups/fields with offset and storage length. Toggle fillers.
- Generate Schema (MySQL)
  - Staging table DDL generation with simple type mappings.
  - Apply DDL to MySQL.
- Import Data (ETL)
  - Fixed-width ASCII import to staging via batched parameterized INSERT.
  - Import log field `ImportFileName` stored per row.
- Transform/Execute
  - Minimal DSL to build curated view SQL: MOVE, COMPUTE, IF/THEN/ELSE, DATE8, COMP3 placeholder.

Notes:
- EBCDIC is stub-ready (ASCII implemented). COMP-3 normalization kept minimal; extend TransformEngine for full COMP-3 decode.
- XML metadata import is stubbed via UI with a placeholder (designed to be replaced by a CB2XML-style importer later).

Related reading: https://stackoverflow.com/questions/35846800/dynamically-reading-cobol-redefines-with-c-sharp/35977421#35977421

## Prerequisites
- .NET 8 SDK
- MySQL 8 server (local or remote)

## Configure Connection
Update UI/appsettings.json:

```
{
  "ConnectionStrings": {
    "MySql": "Server=localhost;Port=3306;Database=cobol_studio;User Id=root;Password=your_password;Allow User Variables=true;"
  }
}
```

Create the database if it does not exist.

## Build and Run
- Build: `dotnet build CobolToMySqlStudio.sln`
- Run UI: `dotnet run --project CobolToMySqlStudio.UI`

## Using the App
1) Import Copybook
- Open Samples/sample.cpy (or your copybook). Parser computes structure and offsets.

2) Preview Layout
- Browse the tree, verify offsets and lengths. Toggle fillers.

3) Generate Schema
- Set staging table name (e.g., `staging_customer`).
- Preview DDL, then Apply to DB.

4) Import Data
- Choose Samples/sample.dat (ensure it matches copybook layout).
- Run Import. Check status and errors list.

5) Transform/Execute
- Write DSL rules, e.g.:
```
MOVE CUST-ID -> CUSTOMER_ID
DATE8 BIRTHDATE = BIRTH-YYYYMMDD
COMPUTE TOTAL = AMOUNT + 100
IF FLAG-A = 'Y' THEN ACTIVE = 1 ELSE ACTIVE = 0
```
- Generate SQL to create or replace a curated view.
- Apply to DB.

## Tests
Run: `dotnet test`

Included tests cover:
- Parser basics
- Offset computation estimates
- Type mapping
- DSL to SQL generation

## Extensibility Notes
- XML Importer: Add an `IXmlMetadataImporter` in Application and a concrete implementation. Bind to UI `Import XML` button.
- OCCURS normalization: Extend SQL generator to emit child tables or flattened columns; UI should capture chosen mode.
- REDEFINES discriminator: Extend TransformEngine to emit CASE logic based on user-provided rule.
- EBCDIC: Implement a reader in Infrastructure with code page handling.

## Security & Operations
- Parameterized inserts for bulk loads; DDL creation done via generated scripts.
- Logging via Microsoft.Extensions.Logging; logs surface in the UI Logs tab.

## Limitations
- Copybook parsing and length computations are simplified; enhance for full COBOL coverage (COMP/COMP-3 exact bytes, sign nibbles, alignment).
- Import currently expects line-based records; adjust for true fixed-length binary/EBCDIC as needed.
