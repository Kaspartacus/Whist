using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Repositories.Calendars;

namespace ServerAPI.Workers;

/// <summary>
/// Worker der sender mails:
/// A) Fødselsdage (i dag)
/// B) Kalender events (+2 dage)
///
/// Kører 1 gang dagligt kl. 09:00 lokal tid.
/// </summary>
public class MailReminderWorker : BackgroundService
{
    private static readonly TimeZoneInfo DanishTimeZone = FindDanishTimeZone();

    private readonly ILogger<MailReminderWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public MailReminderWorker(
        ILogger<MailReminderWorker> log,
        IServiceScopeFactory scopeFactory,
        IConfiguration config)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Kør dagligt kl. 09:00 dansk tid.
                // Containeren kan køre i UTC, så vi bruger eksplicit Europe/Copenhagen.
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DanishTimeZone);
                var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, 9, 0, 0, now.Offset);

                if (now >= nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _log.LogInformation("ReminderWorker næste kørsel: {NextRun} (om {Delay})", nextRun, delay);

                await Task.Delay(delay, stoppingToken);
                await RunOnce(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ReminderWorker fejl i loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task RunOnce(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<CosmosDbContext>();
        var calRepo = scope.ServiceProvider.GetRequiredService<ICalendarRepository>();

        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DanishTimeZone);
        var today = DateOnly.FromDateTime(now.DateTime);

        // 1) Hent modtagere (bruges til både fødselsdage og events)
        var users = (await CosmosDbContext.ReadAllAsync<ApplicationUser>(cosmos.Users, ct))
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .ToList();

        if (users.Count == 0)
        {
            _log.LogWarning("Ingen modtagere fundet.");
            return;
        }

        // 2) SMTP config
        var host = _config["Smtp:Host"]!;
        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var enableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "true");
        var smtpUser = _config["Smtp:User"]!;
        var smtpPass = _config["Smtp:Password"]!; // fra secrets / env
        var from = _config["Smtp:From"] ?? smtpUser;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            Timeout = 1000 * 30
        };

        var sentTotal = 0;

        // --------------------------
        // A) Fødselsdage (i dag)
        // --------------------------
        static bool IsBirthdayToday(DateOnly? birthDate, DateOnly todayLocal)
            => birthDate.HasValue && birthDate.Value.Day == todayLocal.Day && birthDate.Value.Month == todayLocal.Month;

        var birthdayUsers = users.Where(u => IsBirthdayToday(u.BirthDate, today)).ToList();

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

            using var msg = new MailMessage
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

            try
            {
                await client.SendMailAsync(msg, ct);
                sentTotal++;
                _log.LogInformation("Sendte fødselsdagshilsen til {To}", to);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Kunne ikke sende fødselsdagshilsen til {To}", to);
            }
        }

        // --------------------------
        // B) Event reminders (+2 dage)
        // --------------------------
        var eventsInTwoDays = await calRepo.FindByExactOffsetDays(2);

        if (eventsInTwoDays.Count == 0)
        {
            _log.LogInformation("Ingen events +2 dage. (Fødselsdage kan stadig være sendt).");
            _log.LogInformation("ReminderWorker sendte {Sent} mails (inkl. evt. fødselsdage).", sentTotal);
            return;
        }

        // Deduplikeret recipient-liste
        var recipients = users
            .Select(u => u.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            _log.LogWarning("Ingen modtagere fundet til event reminders.");
            _log.LogInformation("ReminderWorker sendte {Sent} mails (inkl. evt. fødselsdage).", sentTotal);
            return;
        }

        foreach (var ev in eventsInTwoDays)
        {
            var dateStr = ev.Date.ToString("dd-MM-yyyy");
            var subject = $"Påmindelse: Whist-holdet {dateStr}";

            var text =
                $"Husk, du har en begivenhed med Whist-holdet d. {dateStr} om 2 dage."
                + (string.IsNullOrWhiteSpace(ev.Note) ? "" : $"\n\nNote: {ev.Note}")
                + "\n\nVi ses!\nWhist-holdet";

            var html = $@"
<p>Husk, du har en begivenhed med Whist-holdet d.d. <strong>{dateStr}</strong> <em>om 2 dage</em>.</p>
{(string.IsNullOrWhiteSpace(ev.Note) ? "" : $"<p><strong>Note:</strong> {WebUtility.HtmlEncode(ev.Note)}</p>")}
<hr/>
<p>Vi ses!<br/>Whist-holdet</p>";

            foreach (var to in recipients)
            {
                using var msg = new MailMessage
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

                try
                {
                    await client.SendMailAsync(msg, ct);
                    sentTotal++;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Kunne ikke sende til {To}", to);
                }
            }
        }

        // Markér alle events som sendt i ét batch-kald (bedre performance)
        await calRepo.MarkRemindersSent(eventsInTwoDays.Select(e => e.Id));

        _log.LogInformation(
            "ReminderWorker sendte {Sent} mails for {Events} events. (Inkl. evt. fødselsdage).",
            sentTotal,
            eventsInTwoDays.Count
        );
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
