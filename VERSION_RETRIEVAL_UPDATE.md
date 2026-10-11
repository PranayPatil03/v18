# CRISP_2.0 – Workflow version retrieval update

## Behavior
- Opening an existing framework selects the highest-numbered **published** Workflow JSON version by default.
- The framework toolbar now includes a **Workflow version** dropdown and **Retrieve** button.
- Selecting an older published version and clicking Retrieve loads that version's JSON definition and its own saved value snapshot.
- Parameter fields are filtered using the selected version's JSON so parameters added in later versions do not appear in an older version.
- Framework value snapshots are retrieved by `(FrameworkInstanceId, WorkflowVersionNo)`, not only by the `IsCurrent` flag.
- Saving values for a selected older version updates that version's snapshot and does not replace the framework's default version pointer or its relational values for the default version.
- Saving a blank value set stores a blank snapshot for that version so an older saved value set does not unexpectedly reappear.

## Database
No new table or column is required. The implementation uses the existing `FrameworkJsonValueSnapshot` table and its `WorkflowVersionNo` column. No EF migrations are introduced.

## Important notes
- The workflow versions offered in the dropdown are published versions. Draft versions are not shown there.
- The project was updated from the supplied ZIP. This environment does not have the .NET SDK installed, so a `dotnet build` could not be run here. Please build the solution in Visual Studio with the .NET SDK specified in `global.json` before deploying to IIS.
