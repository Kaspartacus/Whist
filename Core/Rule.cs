using System.ComponentModel.DataAnnotations;

namespace Core;

/// <summary>
/// En regeltekst.
/// Regler ændres sjældent og vises typisk som en liste.
/// </summary>
public class Rule
{
    /// <summary>Unikt ID for reglen.</summary>
    public int Id { get; set; }

    /// <summary>Selve reglen (maks 500 tegn).</summary>
    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;
}
