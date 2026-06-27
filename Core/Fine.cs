using System.ComponentModel.DataAnnotations;

namespace Core;

/// <summary>
/// Bøde der tilhører en bruger.
/// Bøder kan filtreres/pagineres og kan markeres som betalt.
/// </summary>
public class Fine
{
    /// <summary>Unikt ID for bøden.</summary>
    public int Id { get; set; }

    /// <summary>ID på brugeren som bøden tilhører.</summary>
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    /// <summary>Beløb (1-500).</summary>
    [Range(1, 500)]
    public decimal Amount { get; set; }

    /// <summary>Valgfri kommentar (maks 400 tegn).</summary>
    [MaxLength(400)]
    public string Comment { get; set; } = "";

    /// <summary>Hvornår bøden blev givet.</summary>
    public DateTime Date { get; set; }

    /// <summary>Om bøden er betalt.</summary>
    public bool IsPaid { get; set; } = false;
}
