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

    public RuleController(IRuleRepository repo)
    {
        _repo = repo;
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Henter alle regler.
    /// </summary>
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
    public async Task<ActionResult<Rule>> Add([FromBody] Rule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Text))
            return BadRequest("Regeltekst mangler.");

        var added = await _repo.Add(rule);
        
        return CreatedAtAction(nameof(GetAll), new { id = added.Id }, added);
    }

    /// <summary>
    /// Opdaterer en regel.
    /// Returnerer 400 hvis id i route ikke matcher rule.Id.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] Rule rule)
    {
        if (id != rule.Id)
            return BadRequest();

        await _repo.Update(rule);
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
        return NoContent();
    }
}
