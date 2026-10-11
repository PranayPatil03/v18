# CRISP_2.0

ICRA-style Credit Rating Framework demo built with .NET 10, Blazor Server, MudBlazor, Clean Architecture, EF Core + SQL Server LocalDB and a JSON-driven Rules Engine.

## Architecture
UI (CRISP.Web) -> API (CRISP.Api) -> Application (Services/DTOs) -> Infrastructure (Repositories/EF Core) -> SQL Server

Rules Engine is a separate class library under Libraries/RulesEngine and is called by the Application evaluation flow.

## Data-driven behaviour
- CustomerMaster drives entity lookup.
- FrameworkInstance.EntityId drives the Create/View Framework list.
- Published/latest workflow JSON is the source definition for parameters, score definitions and calculation rules.
- Admin Parameters and Rules pages modify the JSON definition and save a new version.
- Saving a JSON version synchronizes ParameterDefinition/ScoreDefinition tables so existing framework value rows remain usable.
- Framework screen reads the synchronized definition; no parameter names, weights or scoring rules are hardcoded in Razor.

## Database
Use Database/01_Create_CRISP_2.0.sql against `(localdb)\MSSQLLocalDB`. It creates CRISP_2_0 and seeds the 20 Microns example and CorporateBusiness JSON.

## Run
API: http://localhost:5101/swagger
Web: http://localhost:4200/crf/corporate

The solution intentionally uses no EF migrations.

## Final UI folder structure

CRISP.Web/Pages is organized by feature instead of keeping all Razor pages in one folder:

- `Pages/CRF/Corporate.razor`
- `Pages/WorkflowJson/ManageJson.razor`
- `Pages/WorkflowJson/Parameters.razor`
- `Pages/WorkflowJson/Rules.razor`
- `Pages/WorkflowJson/Evaluate.razor`
- `Pages/Information/FinancialInformation.razor`
- `Pages/Reports/ReportGeneration.razor`
- `Pages/Home.razor`
- `Pages/Error.razor`

Corporate dialogs are under `Components/Corporate/Dialogs`.

## Corporate flow

1. Open Corporate. The eight workflow steps are loaded immediately from the workflow context.
2. Search/select an entity. Frameworks are loaded only for that entity.
3. If the entity has a Draft framework, only the existing frameworks are shown; `Create New Framework` is hidden.
4. If the entity has no Draft, existing saved frameworks plus `Create New Framework` are shown.
5. Selecting a framework loads the framework, industry segments, parameter values and scores in one framework request.
6. `Edit/View Score` opens the Business Score dialog.
7. Run Framework sends the selected published Workflow JSON and inputs through the separate `Libraries/RulesEngine` library.
8. Workflow JSON changes are saved as a new version and synchronized transactionally to ParameterDefinition, ScoreDefinition, RuleDefinition and WorkflowStep.

## JSON-first framework value loading

When Edit/View Score is opened, framework values are loaded JSON-first. The published workflow definition JSON and the framework value JSON are kept in `IMemoryCache`. A cache miss loads the JSON snapshot from SQL once and then keeps it in memory. The application compares the value snapshot with the selected workflow definition/version. If the JSON is valid and consistent, those JSON values are used. If JSON is missing, invalid, or inconsistent, the service reports `JSON failed` and falls back to `FrameworkParameterValue` in SQL. Saving a framework writes the relational values first and creates/refreshes the second value JSON snapshot and its memory-cache entry.

The Microsoft RulesEngine library remains the execution layer for rule/calculation expressions. JSON defines the rules; the RulesEngine executes them with the framework inputs. 

## Final PoC behavior – workflow masters and JSON fallback

The final v8 flow keeps Parameters, Rules and Validations as database-backed master data.

- `Admin -> Workflow JSON -> New Workflow` creates a new `WorkflowMaster` record and an initial skeleton JSON Version 1.
- Parameters, Rules and Validations pages read their master definitions from SQL Server through the Repository and Service layers.
- Add/Edit/Delete on the master pages persists the relational definitions and creates a new JSON version snapshot.
- Framework Edit/View is JSON-first: the published definition/value JSON is resolved through `IMemoryCache`; on cache miss the JSON is loaded once from the database and cached.
- The application compares the definition/value JSON before using it for runtime evaluation.
- If JSON is missing, invalid or inconsistent, the application reports `JSON failed` and falls back to persisted framework values from SQL Server.
- Microsoft RulesEngine executes the rules/calculation expressions represented by the selected JSON workflow.

The source files are intentionally formatted as normal multi-line C# and Razor files for maintainability and review.
