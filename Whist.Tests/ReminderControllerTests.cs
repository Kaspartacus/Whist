using Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ServerAPI.Controllers;
using ServerAPI.Repositories.Calendars;
using ServerAPI.Services.Reminders;

namespace Whist.Tests;

public sealed class ReminderControllerTests
{
    [Fact]
    public async Task Run_RejectsMissingSecret()
    {
        var service = new FakeReminderMailService();
        var controller = CreateController(service, configuredSecret: "secret", providedSecret: null);

        var result = await controller.Run(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(0, service.RunCount);
    }

    [Fact]
    public async Task Run_AcceptsCorrectSecret()
    {
        var service = new FakeReminderMailService();
        var controller = CreateController(service, configuredSecret: "secret", providedSecret: "secret");

        var result = await controller.Run(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ReminderRunResult>(ok.Value);
        Assert.Equal(1, service.RunCount);
    }

    [Fact]
    public async Task HasEventToday_ReturnsNoContentWhenThereIsNoEventToday()
    {
        var controller = CreateController(
            new FakeReminderMailService(),
            configuredSecret: "secret",
            providedSecret: "secret",
            calendarRepository: new FakeCalendarRepository([]));

        var result = await controller.HasEventToday();

        Assert.IsType<NoContentResult>(result);
    }

    private static ReminderController CreateController(
        IReminderMailService reminderMailService,
        string configuredSecret,
        string? providedSecret,
        ICalendarRepository? calendarRepository = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reminders:RunSecret"] = configuredSecret
            })
            .Build();

        var controller = new ReminderController(
            reminderMailService,
            calendarRepository ?? new FakeCalendarRepository([]),
            configuration,
            NullLogger<ReminderController>.Instance);

        var httpContext = new DefaultHttpContext();
        if (providedSecret is not null)
            httpContext.Request.Headers["X-Reminder-Secret"] = providedSecret;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private sealed class FakeReminderMailService : IReminderMailService
    {
        public int RunCount { get; private set; }

        public Task<ReminderRunResult> RunOnce(CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new ReminderRunResult(0, 0, 0, 0));
        }
    }

    private sealed class FakeCalendarRepository : ICalendarRepository
    {
        private readonly List<Calendar> _events;

        public FakeCalendarRepository(IEnumerable<Calendar> events)
        {
            _events = events.ToList();
        }

        public Task<List<Calendar>> GetAll() => Task.FromResult(_events);
        public Task<Calendar?> GetByDate(DateTime date) => Task.FromResult(_events.FirstOrDefault(x => x.Date.Date == date.Date));
        public Task<bool> AddOrUpdate(Calendar evt) => throw new NotImplementedException();
        public Task<Calendar?> GetById(int id) => Task.FromResult(_events.FirstOrDefault(x => x.Id == id));
        public Task<bool> Delete(int id) => throw new NotImplementedException();
        public Task<List<Calendar>> FindPendingReminders(int reminderDaysAhead) => Task.FromResult(_events);
        public Task MarkReminderSent(int calendarId) => Task.CompletedTask;
        public Task MarkRemindersSent(IEnumerable<int> calendarIds) => Task.CompletedTask;
    }
}
