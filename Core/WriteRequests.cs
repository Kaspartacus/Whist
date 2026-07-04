using System.ComponentModel.DataAnnotations;

namespace Core;

public sealed class SaveFineRequest
{
    public int Id { get; set; }

    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Range(1, 500)]
    public decimal Amount { get; set; }

    [MaxLength(400)]
    public string Comment { get; set; } = "";

    public DateTime? Date { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaidAt { get; set; }
}

public sealed class SaveHighlightRequest
{
    [Required, MaxLength(80)]
    public string Title { get; set; } = "";

    [Required, MaxLength(400)]
    public string Description { get; set; } = "";

    public DateTime? Date { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsPrivate { get; set; }
}

public sealed class SaveRuleRequest
{
    [Required, MaxLength(500)]
    public string Text { get; set; } = "";
}

public sealed class SaveCalendarRequest
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    [Required, MaxLength(100)]
    public string Note { get; set; } = "";
}

public sealed class CreatePointRequest
{
    [Range(1, int.MaxValue)]
    public int PlayerId { get; set; }

    [Range(-3000, 3000)]
    public int Points { get; set; }

    public DateTime Date { get; set; }
}
