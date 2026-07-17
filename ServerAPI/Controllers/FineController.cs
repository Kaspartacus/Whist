using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Repositories.Fines;

namespace ServerAPI.Controllers;

/// <summary>
/// API for bøder.
/// Controlleren er "tynd" og videresender logik til repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FineController : ControllerBase
{
    private readonly IFineRepository _fineRepository;
    private readonly ILogger<FineController> _logger;

    /// <summary>
    /// Repository injiceres via DI.
    /// </summary>
    public FineController(IFineRepository fineRepository, ILogger<FineController> logger)
    {
        _fineRepository = fineRepository;
        _logger = logger;
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Henter alle bøder (på tværs af brugere).
    /// Bruges primært til overblik/summary i UI.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<Fine[]>> GetAll([FromQuery] bool includeArchived = false)
    {
        return await _fineRepository.GetAll(includeArchived);
    }

    /// <summary>
    /// Henter alle bøder for en bestemt bruger.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<Fine[]>> GetByUserId(int userId, [FromQuery] bool includeArchived = false)
    {
        return await _fineRepository.GetByUserId(userId, includeArchived);
    }

    /// <summary>
    /// Henter bøder pagineret (server-side paging).
    /// Fordel: UI kan vise tabel uden at hente alt.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<Fine>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? userId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] bool? isPaid = null,
        [FromQuery] bool? isArchived = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _fineRepository.GetPaged(page, pageSize, userId, searchTerm, fromDate, toDate, minAmount, maxAmount, isPaid, isArchived);
        return Ok(result);
    }

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Opretter en ny bøde.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add(SaveFineRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest(new { message = "Bruger er påkrævet." });

        var fine = new Fine
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Comment = request.Comment?.Trim() ?? "",
            IsPaid = request.IsPaid,
            PaidAt = request.IsPaid ? DateTime.UtcNow : null
        };

        var created = await _fineRepository.AddFine(fine);
        if (!created)
            return BadRequest(new { message = "Brugeren findes ikke." });

        _logger.LogInformation(
            "Fine {FineId} was created for user {TargetUserId} by user {ActorUserId}. Amount: {Amount}.",
            fine.Id,
            fine.UserId,
            GetCurrentUserId(),
            fine.Amount);
        return Ok();
    }

    /// <summary>
    /// Opdaterer en eksisterende bøde (fx IsPaid/kommentar/beløb).
    /// </summary>
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Update(SaveFineRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest(new { message = "Bruger er påkrævet." });

        if (request.Id <= 0)
            return BadRequest(new { message = "Bøde-id er ugyldigt." });

        if (request.Date is null || request.Date.Value == default)
            return BadRequest(new { message = "Dato er ugyldig." });

        var fine = new Fine
        {
            Id = request.Id,
            UserId = request.UserId,
            Amount = request.Amount,
            Comment = request.Comment?.Trim() ?? "",
            Date = request.Date.Value,
            IsPaid = request.IsPaid,
            PaidAt = request.PaidAt
        };

        var updated = await _fineRepository.Update(fine);
        if (!updated)
            return NotFound(new { message = "Bøden blev ikke fundet." });

        _logger.LogInformation(
            "Fine {FineId} for user {TargetUserId} was updated by user {ActorUserId}. Paid: {IsPaid}. Amount: {Amount}.",
            fine.Id,
            fine.UserId,
            GetCurrentUserId(),
            fine.IsPaid,
            fine.Amount);
        return Ok();
    }

    /// <summary>
    /// Sletter en bøde for en given bruger.
    /// </summary>
    [HttpDelete("user/{userId}/{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int userId, int id)
    {
        var deleted = await _fineRepository.Delete(userId, id);
        if (!deleted)
            return NotFound(new { message = "Bøden blev ikke fundet." });

        _logger.LogInformation(
            "Fine {FineId} for user {TargetUserId} was deleted by user {ActorUserId}.",
            id,
            userId,
            GetCurrentUserId());
        return Ok();
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }
}
