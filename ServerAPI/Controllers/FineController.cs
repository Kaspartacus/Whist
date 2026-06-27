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
    public ActionResult<Fine[]> GetAll()
    {
        return _fineRepository.GetAll();
    }

    /// <summary>
    /// Henter alle bøder for en bestemt bruger.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("user/{userId}")]
    public ActionResult<Fine[]> GetByUserId(int userId)
    {
        return _fineRepository.GetByUserId(userId);
    }

    /// <summary>
    /// Henter bøder pagineret (server-side paging).
    /// Fordel: UI kan vise tabel uden at hente alt.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("paged")]
    public ActionResult<PagedResult<Fine>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? userId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] bool? isPaid = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = _fineRepository.GetPaged(page, pageSize, userId, searchTerm, fromDate, toDate, minAmount, maxAmount, isPaid);
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
    public IActionResult Add(SaveFineRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest(new { message = "Bruger er påkrævet." });

        var fine = new Fine
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Comment = request.Comment?.Trim() ?? "",
            IsPaid = request.IsPaid
        };

        _fineRepository.AddFine(fine);
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
    public IActionResult Update(SaveFineRequest request)
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
            IsPaid = request.IsPaid
        };

        _fineRepository.Update(fine);
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
    public IActionResult Delete(int userId, int id)
    {
        _fineRepository.Delete(userId, id);
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
