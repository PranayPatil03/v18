using System.Text.Json;
using CRISP.Application.DTOs;
using CRISP.Application.Interfaces;

namespace CRISP.Application.Services;

public sealed class FrameworkService(
    IFrameworkRepository repository,
    IWorkflowJsonProvider jsonProvider) : IFrameworkService
{
    public Task<IReadOnlyList<WorkflowDto>> GetWorkflowsAsync(CancellationToken ct = default) => repository.GetWorkflowsAsync(ct);

    public async Task<WorkflowDto> CreateWorkflowAsync(
        CreateWorkflowRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowCode))
            throw new InvalidOperationException("Workflow code is required.");

        if (string.IsNullOrWhiteSpace(request.WorkflowName))
            throw new InvalidOperationException("Workflow name is required.");

        var definition = new WorkflowDefinitionDto
        {
            WorkflowName = request.WorkflowName.Trim()
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        };

        var skeletonJson = JsonSerializer.Serialize(
            new[] { definition },
            jsonOptions);

        var workflow = await repository.CreateWorkflowAsync(
            request,
            skeletonJson,
            ct);

        jsonProvider.SetDefinitionJson(
            request.WorkflowCode.Trim(),
            1,
            skeletonJson);

        return workflow;
    }

    public Task<WorkflowContextDto?> GetWorkflowContextAsync(string workflowCode, CancellationToken ct = default) => repository.GetWorkflowContextAsync(workflowCode, ct);
    public Task<IReadOnlyList<EntityDto>> SearchEntitiesAsync(string? search, CancellationToken ct = default) => repository.SearchEntitiesAsync(search, ct);
    public Task<IReadOnlyList<FrameworkLookupDto>> GetFrameworkOptionsAsync(int entityId, CancellationToken ct = default) => repository.GetFrameworkOptionsAsync(entityId, ct);
    public Task<IReadOnlyList<IndustrySegmentDto>> GetIndustrySegmentsAsync(string workflowCode, CancellationToken ct = default) => repository.GetIndustrySegmentsAsync(workflowCode, ct);
    public Task<IReadOnlyList<FrameworkStepDto>> GetStepsAsync(string workflowCode, CancellationToken ct = default) => repository.GetStepsAsync(workflowCode, ct);
    public Task<IReadOnlyList<FrameworkListDto>> GetFrameworksAsync(CancellationToken ct = default) => repository.GetFrameworksAsync(ct);
    public Task<IReadOnlyList<ParameterDto>> GetParametersAsync(string workflowCode, CancellationToken ct = default) => repository.GetParametersAsync(workflowCode, ct);
    public Task<IReadOnlyList<VersionDto>> GetVersionsAsync(string workflowCode, CancellationToken ct = default) => repository.GetVersionsAsync(workflowCode, ct);
    public Task<CRISP.Domain.Entities.WorkflowJsonVersion?> GetVersionAsync(string workflowCode, int versionNo, CancellationToken ct = default) => repository.GetVersionAsync(workflowCode, versionNo, ct);

    public async Task<FrameworkDto?> GetFrameworkAsync(int id, int? versionNo = null, CancellationToken ct = default)
    {
        // Edit/View is JSON-first. We intentionally do NOT load FrameworkParameterValue here.
        var framework = await repository.GetFrameworkShellAsync(id, ct);
        if (framework is null)
            return null;

        // The latest published version is the default. A requested older version is loaded only
        // when the user explicitly selects it in the version dropdown.
        var versions = await repository.GetVersionsAsync(framework.WorkflowCode, ct);
        var targetVersion = versionNo
            ?? versions.Where(x => x.IsPublished).OrderByDescending(x => x.VersionNo).Select(x => (int?)x.VersionNo).FirstOrDefault()
            ?? framework.WorkflowVersionNo;
        framework.WorkflowVersionNo = targetVersion;

        var valueJson = await repository.GetFrameworkValueJsonAsync(id, targetVersion, ct);
        var version = await repository.GetVersionAsync(framework.WorkflowCode, targetVersion, ct);
        var definitionJson = version?.JsonContent ?? await jsonProvider.GetDefinitionJsonAsync(
            framework.WorkflowCode,
            targetVersion,
            ct);

        // Use only parameter definitions present in the selected version's JSON. This prevents
        // parameters added later from appearing when an older version is retrieved.
        ApplyVersionParameters(framework, definitionJson);

        if (TryBuildValuesFromJson(framework, definitionJson, valueJson, out var jsonValues, out var error))
        {
            framework.ParameterValues = jsonValues;
            framework.ValueSource = "JSON Cache";
            framework.JsonStatus = "Valid";
            return framework;
        }

        // JSON is unavailable, invalid or inconsistent. Only now read the relational values.
        // Do not fall back to the live relational values for an older version: those values may
        // belong to a different configuration. Database fallback is safe only for the framework's
        // currently stored version.
        framework.ParameterValues = targetVersion == (await repository.GetFrameworkShellAsync(id, ct))?.WorkflowVersionNo
            ? (await repository.GetFrameworkParameterValuesAsync(id, ct)).Where(v => framework.Parameters.Any(p => p.Id == v.ParameterId)).ToList()
            : framework.Parameters.Select(p => new FrameworkParameterDto(p.Id, p.ParameterName, p.Weight, null, 0)).ToList();
        framework.ValueSource = targetVersion == (await repository.GetFrameworkShellAsync(id, ct))?.WorkflowVersionNo ? "Database Fallback" : "No saved values for selected version";
        framework.JsonStatus = string.IsNullOrWhiteSpace(error)
            ? "JSON failed"
            : $"JSON failed: {error}";
        return framework;
    }

    private static void ApplyVersionParameters(FrameworkDto framework, string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().FirstOrDefault()
                : doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array) return;
            var definitions = JsonSerializer.Deserialize<List<WorkflowJsonParameterDto>>(parameters.GetRawText()) ?? [];
            var current = framework.Parameters.ToDictionary(x => x.ParameterCode, StringComparer.OrdinalIgnoreCase);
            var versionParameters = new List<ParameterDto>();
            foreach (var definition in definitions)
            {
                if (!current.TryGetValue(definition.ParameterCode, out var master)) continue;
                var scores = definition.Scores.Select(x => new ScoreDto(x.Score, x.Definition, x.ThresholdValue, x.Unit, x.MinValue, x.MaxValue, x.MinInclusive, x.MaxInclusive)).ToList();
                versionParameters.Add(new ParameterDto(master.Id, definition.ParameterCode, definition.ParameterName, definition.WhyItMatters, definition.HowDoWeMeasure, definition.Weight, definition.DisplayOrder, definition.IsValueDriven, scores));
            }
            framework.Parameters = versionParameters;
        }
        catch (JsonException) { /* Keep the existing parameter list if an old JSON schema cannot be parsed. */ }
    }

    public async Task<CalculateScoreResponse?> CalculateScoreAsync(int parameterId, decimal? value, CancellationToken ct = default)
    {
        var parameter = await repository.GetParameterAsync(parameterId, ct);
        if (parameter is null) return null;
        if (!parameter.IsValueDriven || value is null)
        {
            var first = parameter.Scores.OrderBy(x => x.Score).FirstOrDefault();
            return first is null ? null : new CalculateScoreResponse(parameter.Id, first.Score, first.Definition);
        }

        var match = parameter.Scores
            .Where(x => (!x.MinValue.HasValue || (x.MinInclusive ? value.Value >= x.MinValue.Value : value.Value > x.MinValue.Value))
                     && (!x.MaxValue.HasValue || (x.MaxInclusive ? value.Value <= x.MaxValue.Value : value.Value < x.MaxValue.Value)))
            .OrderBy(x => x.Score)
            .FirstOrDefault();

        return match is null ? null : new CalculateScoreResponse(parameter.Id, match.Score, match.Definition);
    }

    public async Task<FrameworkDto> SaveFrameworkAsync(FrameworkDto framework, CancellationToken ct = default)
    {
        // Score using the selected version's score definitions, not the latest master scores.
        var parameterMap = framework.Parameters.ToDictionary(x => x.Id);

        framework.ParameterValues = framework.ParameterValues.Select(item =>
        {
            if (!parameterMap.TryGetValue(item.ParameterId, out var definition) || !definition.IsValueDriven || !item.Value.HasValue)
                return item;

            var match = definition.Scores
                .Where(x => (!x.MinValue.HasValue || (x.MinInclusive ? item.Value.Value >= x.MinValue.Value : item.Value.Value > x.MinValue.Value))
                         && (!x.MaxValue.HasValue || (x.MaxInclusive ? item.Value.Value <= x.MaxValue.Value : item.Value.Value < x.MaxValue.Value)))
                .OrderBy(x => x.Score)
                .FirstOrDefault();

            return match is null
                ? new FrameworkParameterDto(item.ParameterId, item.ParameterName, item.Weight, item.Value, 0)
                : new FrameworkParameterDto(item.ParameterId, item.ParameterName, item.Weight, item.Value, match.Score);
        }).ToList();

        var saved = await repository.SaveFrameworkAsync(framework, ct);
        // Repository relational rows represent the framework's default version. Preserve the
        // user's selected-version payload here so the version-specific JSON snapshot gets the
        // values they just saved, even when an older version was selected.
        saved.WorkflowVersionNo = framework.WorkflowVersionNo;
        saved.ParameterValues = framework.ParameterValues.ToList();
        saved.Parameters = framework.Parameters.ToList();

        var hasValues = saved.ParameterValues.Any(x => x.Value.HasValue || x.Score != 0);
        // Save even an all-blank value set. Otherwise a previous snapshot for this version could
        // reappear when the user retrieves it later.
        var valueJson = BuildValueJson(saved);
        await repository.SaveFrameworkValueJsonAsync(saved.Id, saved.WorkflowVersionNo, valueJson, ct);
        jsonProvider.InvalidateValue(saved.Id);

        saved.ValueSource = hasValues ? "Versioned JSON Snapshot" : "Versioned JSON Snapshot (blank values)";
        saved.JsonStatus = "Valid";
        return saved;
    }

    public async Task<VersionDto> SaveVersionAsync(SaveVersionRequest request, CancellationToken ct = default)
    {
        var version = await repository.SaveVersionAsync(request, ct);
        jsonProvider.InvalidateDefinition(request.WorkflowCode, version.VersionNo);
        return version;
    }

    public Task<WorkflowDefinitionDto?> GetWorkflowDefinitionAsync(string workflowCode, CancellationToken ct = default) =>
        repository.GetWorkflowDefinitionAsync(workflowCode, ct);

    public async Task<VersionDto> SaveWorkflowDefinitionAsync(SaveWorkflowDefinitionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowCode))
            throw new InvalidOperationException("Workflow code is required.");

        var weight = request.Definition.Parameters.Sum(x => x.Weight);
        if (request.Definition.Parameters.Count > 0 && weight <= 0)
            throw new InvalidOperationException("Parameter weights must be greater than zero.");

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = null };
        var json = JsonSerializer.Serialize(new[] { request.Definition }, jsonOptions);

        request.VersionName = string.IsNullOrWhiteSpace(request.VersionName)
            ? $"{DateTime.Today:dd-MMM-yyyy} - Draft"
            : request.VersionName.Trim();

        var version = await repository.SaveWorkflowDefinitionAsync(request, json, ct);
        jsonProvider.SetDefinitionJson(request.WorkflowCode, version.VersionNo, json);
        return version;
    }

    private static string BuildValueJson(FrameworkDto framework)
    {
        var snapshot = new FrameworkValueSnapshotJson
        {
            FrameworkId = framework.Id,
            WorkflowCode = framework.WorkflowCode,
            WorkflowVersionNo = framework.WorkflowVersionNo,
            EntityId = framework.EntityId,
            EntityName = framework.EntityName,
            Values = framework.ParameterValues.Select(x => new FrameworkValueJsonItem
            {
                ParameterId = x.ParameterId,
                ParameterName = x.ParameterName,
                Weight = x.Weight,
                Value = x.Value,
                Score = x.Score
            }).ToList()
        };

        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool TryBuildValuesFromJson(
        FrameworkDto framework,
        string? definitionJson,
        string? valueJson,
        out List<FrameworkParameterDto> values,
        out string error)
    {
        values = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            error = "definition JSON not found";
            return false;
        }

        if (string.IsNullOrWhiteSpace(valueJson))
        {
            error = "value JSON not found";
            return false;
        }

        try
        {
            var definitions = ParseDefinition(definitionJson);
            var snapshot = JsonSerializer.Deserialize<FrameworkValueSnapshotJson>(valueJson);

            if (snapshot is null)
            {
                error = "value JSON could not be parsed";
                return false;
            }

            if (!string.Equals(snapshot.WorkflowCode, framework.WorkflowCode, StringComparison.OrdinalIgnoreCase)
                || snapshot.FrameworkId != framework.Id
                || snapshot.WorkflowVersionNo != framework.WorkflowVersionNo)
            {
                error = "value JSON does not match the selected framework/version";
                return false;
            }

            var snapshotValues = snapshot.Values
                .Where(x => !string.IsNullOrWhiteSpace(x.ParameterName) || x.ParameterId > 0)
                .ToList();

            if (snapshotValues.Count != definitions.Parameters.Count)
            {
                error = "value JSON and definition JSON contain different parameter counts";
                return false;
            }

            foreach (var item in snapshotValues)
            {
                var savedParameter = framework.Parameters.FirstOrDefault(p =>
                    (item.ParameterId > 0 && p.Id == item.ParameterId)
                    || p.ParameterName.Equals(item.ParameterName, StringComparison.OrdinalIgnoreCase));
                var definition = definitions.Parameters.FirstOrDefault(x =>
                    (savedParameter is not null && !string.IsNullOrWhiteSpace(x.ParameterCode)
                        && x.ParameterCode.Equals(savedParameter.ParameterCode, StringComparison.OrdinalIgnoreCase))
                    || x.ParameterName.Equals(item.ParameterName, StringComparison.OrdinalIgnoreCase)
                    || (item.ParameterId > 0 && x.ParameterId == item.ParameterId));

                if (definition is null)
                {
                    error = $"parameter '{item.ParameterName}' is missing from definition JSON";
                    return false;
                }

                var frameworkParameter = framework.Parameters.FirstOrDefault(p =>
                    (!string.IsNullOrWhiteSpace(definition.ParameterCode) && p.ParameterCode.Equals(definition.ParameterCode, StringComparison.OrdinalIgnoreCase))
                    || p.ParameterName.Equals(definition.ParameterName, StringComparison.OrdinalIgnoreCase));
                values.Add(new FrameworkParameterDto(
                    frameworkParameter?.Id ?? definition.ParameterId,
                    definition.ParameterName,
                    definition.Weight,
                    item.Value,
                    item.Score));
            }

            foreach (var definition in definitions.Parameters)
            {
                if (values.All(x => !x.ParameterName.Equals(definition.ParameterName, StringComparison.OrdinalIgnoreCase)))
                {
                    error = $"parameter '{definition.ParameterName}' is missing from value JSON";
                    values.Clear();
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            values.Clear();
            error = ex.Message;
            return false;
        }
    }

    private static WorkflowDefinitionForCompare ParseDefinition(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().FirstOrDefault()
            : document.RootElement;

        var parameters = root.TryGetProperty("Parameters", out var parameterElement)
            ? parameterElement.Deserialize<List<ParameterForCompare>>() ?? []
            : [];

        return new WorkflowDefinitionForCompare(parameters);
    }

    private sealed record WorkflowDefinitionForCompare(List<ParameterForCompare> Parameters);

    private sealed class ParameterForCompare
    {
        public int ParameterId { get; set; }
        public string ParameterCode { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public decimal Weight { get; set; }
    }

    private sealed class FrameworkValueSnapshotJson
    {
        public int FrameworkId { get; set; }
        public string WorkflowCode { get; set; } = string.Empty;
        public int WorkflowVersionNo { get; set; }
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public List<FrameworkValueJsonItem> Values { get; set; } = [];
    }

    private sealed class FrameworkValueJsonItem
    {
        public int ParameterId { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public decimal? Value { get; set; }
        public int Score { get; set; }
    }
}
