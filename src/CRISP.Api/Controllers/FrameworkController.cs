using CRISP.Application.DTOs;
using CRISP.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CRISP.Api.Controllers;

[ApiController]
[Route("api/framework")]
public sealed class FrameworkController : ControllerBase
{
    private readonly IFrameworkService _frameworkService;
    private readonly IRuleEvaluationService _evaluationService;

    public FrameworkController(
        IFrameworkService frameworkService,
        IRuleEvaluationService evaluationService)
    {
        _frameworkService = frameworkService;
        _evaluationService = evaluationService;
    }

    [HttpGet("workflows")]
    public async Task<ActionResult<IReadOnlyList<WorkflowDto>>> Workflows(
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetWorkflowsAsync(ct));
    }

    [HttpPost("workflows")]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow(
        CreateWorkflowRequest request,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.CreateWorkflowAsync(request, ct));
    }

    [HttpGet("context/{code}")]
    public async Task<IActionResult> WorkflowContext(
        string code,
        CancellationToken ct)
    {
        var context = await _frameworkService.GetWorkflowContextAsync(code, ct);
        return context is null ? NotFound() : Ok(context);
    }

    [HttpGet("entities")]
    public async Task<ActionResult<IReadOnlyList<EntityDto>>> Entities(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.SearchEntitiesAsync(search, ct));
    }

    [HttpGet("entities/{entityId:int}/frameworks")]
    public async Task<ActionResult<IReadOnlyList<FrameworkLookupDto>>> FrameworkOptions(
        int entityId,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetFrameworkOptionsAsync(entityId, ct));
    }

    [HttpGet("{code}/steps")]
    public async Task<ActionResult<IReadOnlyList<FrameworkStepDto>>> Steps(
        string code,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetStepsAsync(code, ct));
    }

    [HttpGet("{code}/industries")]
    public async Task<ActionResult<IReadOnlyList<IndustrySegmentDto>>> Industries(
        string code,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetIndustrySegmentsAsync(code, ct));
    }

    [HttpGet("instances")]
    public async Task<ActionResult<IReadOnlyList<FrameworkListDto>>> Instances(
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetFrameworksAsync(ct));
    }

    [HttpGet("{code}/parameters")]
    public async Task<ActionResult<IReadOnlyList<ParameterDto>>> Parameters(
        string code,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetParametersAsync(code, ct));
    }

    [HttpGet("{code}/versions")]
    public async Task<ActionResult<IReadOnlyList<VersionDto>>> Versions(
        string code,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.GetVersionsAsync(code, ct));
    }

    [HttpGet("{code}/versions/{versionNo:int}")]
    public async Task<IActionResult> Version(
        string code,
        int versionNo,
        CancellationToken ct)
    {
        var version = await _frameworkService.GetVersionAsync(code, versionNo, ct);

        if (version is null)
            return NotFound();

        return Ok(
            new
            {
                version.Id,
                version.VersionNo,
                version.VersionName,
                version.Status,
                version.IsPublished,
                version.CreatedOn,
                version.CreatedBy,
                version.JsonContent
            });
    }

    [HttpGet("instances/{id:int}")]
    public async Task<IActionResult> Instance(
        int id,
        [FromQuery] int? versionNo,
        CancellationToken ct)
    {
        var framework = await _frameworkService.GetFrameworkAsync(id, versionNo, ct);
        return framework is null ? NotFound() : Ok(framework);
    }

    [HttpPost("instances")]
    public async Task<ActionResult<FrameworkDto>> SaveInstance(
        FrameworkDto model,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.SaveFrameworkAsync(model, ct));
    }

    [HttpPost("calculate-score")]
    public async Task<IActionResult> CalculateScore(
        CalculateScoreRequest request,
        CancellationToken ct)
    {
        var result = await _frameworkService.CalculateScoreAsync(
            request.ParameterId,
            request.Value,
            ct);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<EvaluateResponse>> Evaluate(
        EvaluateRequest request,
        CancellationToken ct)
    {
        return Ok(await _evaluationService.EvaluateAsync(request, ct));
    }

    [HttpPost("versions")]
    public async Task<ActionResult<VersionDto>> SaveVersion(
        SaveVersionRequest request,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.SaveVersionAsync(request, ct));
    }

    [HttpGet("{code}/definition")]
    public async Task<IActionResult> Definition(
        string code,
        CancellationToken ct)
    {
        var definition = await _frameworkService.GetWorkflowDefinitionAsync(code, ct);
        return definition is null ? NotFound() : Ok(definition);
    }

    [HttpPost("definition")]
    public async Task<ActionResult<VersionDto>> SaveDefinition(
        SaveWorkflowDefinitionRequest request,
        CancellationToken ct)
    {
        return Ok(await _frameworkService.SaveWorkflowDefinitionAsync(request, ct));
    }
}
