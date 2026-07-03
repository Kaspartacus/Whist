using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Repositories.Calendars;

namespace ServerAPI.Services.Reminders;

public sealed class ReminderMailService : IReminderMailService
{
    private static readonly TimeZoneInfo DanishTimeZone = FindDanishTimeZone();
    private const int ReminderDaysAhead = 2;

    private readonly CosmosDbContext _cosmos;
    private readonly ICalendarRepository _calendarRepository;
    private readonly IConfiguration _config;
    private readonly ILogger<ReminderMailService> _log;

    public ReminderMailService(
        CosmosDbContext cosmos,
        ICalendarRepository calendarRepository,
        IConfiguration config,
        ILogger<ReminderMailService> log)
    {
        _cosmos = cosmos;
        _calendarRepository = calendarRepository;
        _config = config;
        _log = log;
    }

    public async Task<ReminderRunResult> RunOnce(CancellationToken cancellationToken)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DanishTimeZone);
        var today = DateOnly.FromDateTime(now.DateTime);
        var currentYear = today.Year;

        var users = (await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken))
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .ToList();

        if (users.Count == 0)
        {
            _log.LogWarning("Ingen modtagere fundet.");
            return new ReminderRunResult(0, 0, 0, 0);
        }

        using var client = CreateSmtpClient();
        var from = _config["Smtp:From"] ?? _config["Smtp:User"]!;

        var birthdayMessagesSent = await SendBirthdayGreetings(
            users,
            today,
            currentYear,
            from,
            client,
            cancellationToken);

        var eventMessagesSent = await SendEventReminders(
            users,
            today,
            from,
            client,
            cancellationToken);

        var result = new ReminderRunResult(
            birthdayMessagesSent + eventMessagesSent.MessagesSent,
            birthdayMessagesSent,
            eventMessagesSent.MessagesSent,
            eventMessagesSent.EventsReminded);

        _log.LogInformation(
            "Reminder run completed. Sent total: {SentTotal}. Birthday messages: {BirthdayMessages}. Event messages: {EventMessages}. Events reminded: {EventsReminded}.",
            result.SentTotal,
            result.BirthdayMessagesSent,
            result.EventMessagesSent,
            result.EventsReminded);

        return result;
    }

    private async Task<int> SendBirthdayGreetings(
        IEnumerable<ApplicationUser> users,
        DateOnly today,
        int currentYear,
        string from,
        SmtpClient client,
        CancellationToken cancellationToken)
    {
        static bool IsBirthdayToday(DateOnly? birthDate, DateOnly todayLocal)
            => birthDate.HasValue && birthDate.Value.Day == todayLocal.Day && birthDate.Value.Month == todayLocal.Month;

        var birthdayUsers = users
            .Where(user => IsBirthdayToday(user.BirthDate, today))
            .Where(user => user.LastBirthdayGreetingSentYear != currentYear)
            .ToList();

        var sent = 0;
        foreach (var user in birthdayUsers)
        {
            var to = user.Email!.Trim();
            var name = !string.IsNullOrWhiteSpace(user.Name) ? user.Name : user.NickName;

            var subject = "Tillykke med fødselsdagen 🎉";

            var text = $@"Kære {name}

Hjerteligt tillykke med fødselsdagen 🇩🇰
Du ønskes en god dag fra hele Whist holdet

/ Whist holdet";

            var html = $@"
<p>Kære <strong>{WebUtility.HtmlEncode(name)}</strong></p>
<p>Hjerteligt tillykke med fødselsdagen 🇩🇰</p>
<p>Du ønskes en god dag fra hele Whist holdet</p>
<br/>
<p>/ Whist holdet</p>";

            using var msg = CreateMessage(from, to, subject, text, html);

            try
            {
                await client.SendMailAsync(msg, cancellationToken);
                user.LastBirthdayGreetingSentYear = currentYear;
                await CosmosDbContext.UpsertAsync(_cosmos.Users, user.Id.ToString(), user, cancellationToken);
                sent++;
                _log.LogInformation("Sendte fødselsdagshilsen til {To} for år {Year}.", to, currentYear);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Kunne ikke sende fødselsdagshilsen til {To}", to);
            }
        }

        return sent;
    }

    private async Task<(int MessagesSent, int EventsReminded)> SendEventReminders(
        List<ApplicationUser> users,
        DateOnly today,
        string from,
        SmtpClient client,
        CancellationToken cancellationToken)
    {
        var eventsToRemind = await _calendarRepository.FindPendingReminders(ReminderDaysAhead);
        if (eventsToRemind.Count == 0)
        {
            _log.LogInformation("Ingen kommende events uden reminder inden for {ReminderDaysAhead} dage.", ReminderDaysAhead);
            return (0, 0);
        }

        var recipients = users
            .Select(user => user.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            _log.LogWarning("Ingen modtagere fundet til event reminders.");
            return (0, 0);
        }

        var sent = 0;
        var remindedEventIds = new List<int>();

        foreach (var ev in eventsToRemind)
        {
            var dateStr = ev.Date.ToString("dd-MM-yyyy");
            var daysUntil = (ev.Date.Date - today.ToDateTime(TimeOnly.MinValue)).Days;
            var whenText = daysUntil switch
            {
                0 => "i dag",
                1 => "i morgen",
                2 => "om 2 dage",
                _ => $"om {daysUntil} dage"
            };

            var subject = $"Påmindelse: Whist-holdet {dateStr}";
            var text =
                $"Husk, du har en begivenhed med Whist-holdet d. {dateStr} {whenText}."
                + (string.IsNullOrWhiteSpace(ev.Note) ? "" : $"\n\nNote: {ev.Note}")
                + "\n\nVi ses!\nWhist-holdet";

            var html = $@"
<p>Husk, du har en begivenhed med Whist-holdet d. <strong>{dateStr}</strong> <em>{whenText}</em>.</p>
{(string.IsNullOrWhiteSpace(ev.Note) ? "" : $"<p><strong>Note:</strong> {WebUtility.HtmlEncode(ev.Note)}</p>")}
<hr/>
<p>Vi ses!<br/>Whist-holdet</p>";

            var sentForEvent = 0;
            foreach (var to in recipients)
            {
                using var msg = CreateMessage(from, to, subject, text, html);

                try
                {
                    await client.SendMailAsync(msg, cancellationToken);
                    sent++;
                    sentForEvent++;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Kunne ikke sende event reminder til {To}", to);
                }
            }

            if (sentForEvent > 0)
                remindedEventIds.Add(ev.Id);
        }

        await _calendarRepository.MarkRemindersSent(remindedEventIds);
        return (sent, remindedEventIds.Count);
    }

    private SmtpClient CreateSmtpClient()
    {
        var host = _config["Smtp:Host"]!;
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var enableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true");
        var smtpUser = _config["Smtp:User"]!;
        var smtpPass = _config["Smtp:Password"]!;

        return new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            Timeout = 1000 * 30
        };
    }

    private static MailMessage CreateMessage(string from, string to, string subject, string text, string html)
    {
        var msg = new MailMessage
        {
            From = new MailAddress(from, "Whist holdet"),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8
        };

        msg.To.Add(to);
        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, Encoding.UTF8, MediaTypeNames.Text.Plain));
        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html));
        return msg;
    }

    private static TimeZoneInfo FindDanishTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}
