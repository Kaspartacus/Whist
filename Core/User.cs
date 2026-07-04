using System.ComponentModel.DataAnnotations;

namespace Core;

/// <summary>
/// Bruger i systemet.
/// Indeholder profil-data. Fines findes kun for bagudkompatibilitet med gamle user-dokumenter.
/// </summary>
public class User
{
    /// <summary>Unikt ID for brugeren.</summary>
    public int Id { get; set; }

    /// <summary>Fulde navn (maks 40 tegn).</summary>
    [Required]
    [MaxLength(40)]
    public string Name { get; set; } = "";

    /// <summary>Kaldenavn/visningsnavn (maks 30 tegn).</summary>
    [Required]
    [MaxLength(30)]
    public string NickName { get; set; } = "";

    /// <summary>Email (maks 100 tegn).</summary>
    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = "";

    /// <summary>Adresse (maks 200 tegn).</summary>
    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = "";

    /// <summary>
    /// Telefonnummer (8 cifre) – bruges fx til MobilePay.
    /// </summary>
    [Required]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "Telefonnummer skal være 8 cifre.")]
    public string PhoneNumber { get; set; } = "";

    /// <summary>Fødselsdato (valgfri).</summary>
    public DateOnly? BirthDate { get; set; }

    /// <summary>Beskrivelse/biografi (maks 500 tegn).</summary>
    [MaxLength(500)]
    public string Description { get; set; } = "";

    /// <summary>Fun fact (maks 200 tegn).</summary>
    [MaxLength(200)]
    public string FunFact { get; set; } = "";

    /// <summary>URL til profilbillede.</summary>
    [MaxLength(500)]
    public string ImageUrl { get; set; } = "";

    /// <summary>Legacy embedded bøder. Nye bøder ligger i fines-containeren.</summary>
    public ICollection<Fine> Fines { get; set; } = new List<Fine>();
}
