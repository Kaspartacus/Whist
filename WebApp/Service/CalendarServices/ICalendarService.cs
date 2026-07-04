using Core;

namespace WebApp.Service.CalendarServices;

/// <summary>
/// Frontend service-kontrakt for kalender.
/// 
/// Formål:
/// - Give UI et simpelt API til kalender-CRUD
/// - Skjule HTTP-detaljer fra pages/components
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Henter alle kalender-events.
    /// UI sorterer/filtrerer selv (fordi datasættet er lille).
    /// </summary>
    Task<List<Calendar>> GetAll();

    /// <summary>
    /// Gemmer et kalender-event (opret/ret).
    /// Backend håndterer selv om det er add eller update.
    /// </summary>
    Task<Calendar> Save(Calendar calendar);

    /// <summary>
    /// Sletter et kalender-event ud fra id.
    /// </summary>
    Task Delete(int id);
}
