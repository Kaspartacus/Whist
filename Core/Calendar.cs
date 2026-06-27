using System.ComponentModel.DataAnnotations;

namespace Core;

/// <summary>
/// Kalender-begivenhed.
/// Bruges til at vise kommende events og til at styre mail-påmindelser (ReminderSent).
/// </summary>
public class Calendar
{
    /// <summary>Unikt ID for begivenheden.</summary>
    public int Id { get; set; }

    /// <summary>Dato/tid for begivenheden.</summary>
    public DateTime Date { get; set; }

    /// <summary>Tekst/nota for begivenheden.</summary>
    [Required]
    [MaxLength(100)]
    public string Note { get; set; } = "";

    /// <summary>
    /// Om der allerede er sendt påmindelse for denne begivenhed.
    /// Bruges af worker, så man ikke sender flere mails for samme event.
    /// </summary>
    public bool ReminderSent { get; set; } = false;
}
