using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Repositories.Rules;

namespace ServerAPI.Controllers;

/// <summary>
/// API-controller for regler.
/// 
/// Princip:
/// - Controller er "tynd": validerer input og kalder repository.
/// - Ingen database/logik her.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RuleController : ControllerBase
{
    private readonly IRuleRepository _repo;
    private readonly ILogger<RuleController> _logger;

    public RuleController(IRuleRepository repo, ILogger<RuleController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Henter alle regler.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Rule>>> GetAll()
    {
        var rules = await _repo.GetAll();
        return Ok(rules);
    }

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Opretter en ny regel.
    /// Returnerer 400 hvis tekst mangler.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Rule>> Add([FromBody] SaveRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Regeltekst mangler." });

        var rule = new Rule { Text = request.Text.Trim() };
        var added = await _repo.Add(rule);
        _logger.LogInformation(
            "Rule {RuleId} was created by user {ActorUserId}.",
            added.Id,
            GetCurrentUserId());

        return CreatedAtAction(nameof(GetAll), new { id = added.Id }, added);
    }

    /// <summary>
    /// Opdaterer en regel.
    /// Returnerer 400 hvis id i route ikke matcher rule.Id.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] SaveRuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Regeltekst mangler." });

        var rule = new Rule { Id = id, Text = request.Text.Trim() };
        await _repo.Update(rule);
        _logger.LogInformation(
            "Rule {RuleId} was updated by user {ActorUserId}.",
            id,
            GetCurrentUserId());
        return NoContent();
    }

    /// <summary>
    /// Sletter en regel ud fra id.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        await _repo.Delete(id);
        _logger.LogInformation(
            "Rule {RuleId} was deleted by user {ActorUserId}.",
            id,
            GetCurrentUserId());
        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }
}
