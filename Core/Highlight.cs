using System.ComponentModel.DataAnnotations;

namespace Core;

/// <summary>
/// Et "highlight" (minde/hændelse) knyttet til en bruger.
/// Kan have titel, beskrivelse og evt. et billede.
/// </summary>
public class Highlight
{
    /// <summary>Unikt ID for highlightet.</summary>
    public int Id { get; set; }

    /// <summary>Kort titel (maks 80 tegn).</summary>
    [Required]
    [MaxLength(80)]
    public string Title { get; set; } = "";

    /// <summary>Beskrivelse (maks 400 tegn).</summary>
    [Required]
    [MaxLength(400)]
    public string Description { get; set; } = "";

    /// <summary>Dato for highlightet (default: i dag).</summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>URL til billede (hvis der er uploadet et).</summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>ID på brugeren som highlightet tilhører.</summary>
    [Range(0, int.MaxValue)]
    public int UserId { get; set; }

    /// <summary>
    /// Om highlightet er privat.
    /// Private highlights skal kun kunne ses af ejeren.
    /// </summary>
    public bool IsPrivate { get; set; } = false;
}
