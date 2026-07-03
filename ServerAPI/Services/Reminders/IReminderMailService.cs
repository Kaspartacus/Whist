namespace ServerAPI.Services.Reminders;

public interface IReminderMailService
{
    Task<ReminderRunResult> RunOnce(CancellationToken cancellationToken);
}

public sealed record ReminderRunResult(
    int SentTotal,
    int BirthdayMessagesSent,
    int EventMessagesSent,
    int EventsReminded);
