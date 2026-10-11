/*
    CRISP 2.0 - JSON value snapshot support
    Run this script against the existing CRISP database.

    Purpose:
      1. Keep framework values in a second JSON snapshot.
      2. Edit/View reads JSON first (memory cache -> DB snapshot on cache miss).
      3. If JSON is invalid/inconsistent, application falls back to FrameworkParameterValue.
*/

IF OBJECT_ID('dbo.FrameworkJsonValueSnapshot', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FrameworkJsonValueSnapshot
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FrameworkInstanceId INT NOT NULL,
        WorkflowVersionNo INT NOT NULL,
        JsonContent NVARCHAR(MAX) NOT NULL,
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_FrameworkJsonValueSnapshot_CreatedOn DEFAULT SYSUTCDATETIME(),
        IsCurrent BIT NOT NULL CONSTRAINT DF_FrameworkJsonValueSnapshot_IsCurrent DEFAULT 1,
        CONSTRAINT FK_FrameworkJsonValueSnapshot_Framework
            FOREIGN KEY (FrameworkInstanceId) REFERENCES dbo.FrameworkInstance(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_FrameworkJsonValueSnapshot_Framework_Current
        ON dbo.FrameworkJsonValueSnapshot(FrameworkInstanceId, IsCurrent);
END;
GO

/* Create a current value snapshot for existing framework rows that already have values. */
INSERT INTO dbo.FrameworkJsonValueSnapshot
(
    FrameworkInstanceId,
    WorkflowVersionNo,
    JsonContent,
    CreatedOn,
    IsCurrent
)
SELECT
    f.Id,
    f.WorkflowVersionNo,
    (
        SELECT
            f.Id AS FrameworkId,
            w.WorkflowCode,
            f.WorkflowVersionNo,
            f.EntityId,
            f.EntityName,
            JSON_QUERY((
                SELECT
                    fpv.ParameterDefinitionId AS ParameterId,
                    pd.ParameterName,
                    pd.Weight,
                    fpv.Value,
                    fpv.Score
                FROM dbo.FrameworkParameterValue fpv
                INNER JOIN dbo.ParameterDefinition pd
                    ON pd.Id = fpv.ParameterDefinitionId
                WHERE fpv.FrameworkInstanceId = f.Id
                ORDER BY pd.DisplayOrder
                FOR JSON PATH
            )) AS [Values]
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
    ),
    SYSUTCDATETIME(),
    1
FROM dbo.FrameworkInstance f
INNER JOIN dbo.WorkflowMaster w
    ON w.Id = f.WorkflowMasterId
WHERE EXISTS
(
    SELECT 1
    FROM dbo.FrameworkParameterValue fpv
    WHERE fpv.FrameworkInstanceId = f.Id
)
AND NOT EXISTS
(
    SELECT 1
    FROM dbo.FrameworkJsonValueSnapshot s
    WHERE s.FrameworkInstanceId = f.Id
    AND s.IsCurrent = 1
);
GO
