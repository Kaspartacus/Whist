using Core;
using ServerAPI.Repositories.Fines;

namespace Whist.Tests;

public sealed class FineArchivePolicyTests
{
    [Fact]
    public void Apply_DoesNotArchiveUnpaidFine()
    {
        var fine = new Fine
        {
            IsPaid = false,
            PaidAt = DateTime.UtcNow.AddYears(-2),
            IsArchived = true,
            ArchivedAt = DateTime.UtcNow.AddYears(-1)
        };

        var changed = FineArchivePolicy.Apply(fine, DateTime.UtcNow);

        Assert.True(changed);
        Assert.False(fine.IsArchived);
        Assert.Null(fine.ArchivedAt);
    }

    [Fact]
    public void Apply_DoesNotArchivePaidFineBeforeOneYear()
    {
        var paidAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var fine = new Fine { IsPaid = true, PaidAt = paidAt };

        var changed = FineArchivePolicy.Apply(fine, paidAt.AddYears(1).AddTicks(-1));

        Assert.False(changed);
        Assert.False(fine.IsArchived);
        Assert.Null(fine.ArchivedAt);
    }

    [Fact]
    public void Apply_ArchivesPaidFineAfterOneYear()
    {
        var paidAt = new DateTime(2025, 7, 15, 10, 30, 0, DateTimeKind.Utc);
        var fine = new Fine { IsPaid = true, PaidAt = paidAt };

        var changed = FineArchivePolicy.Apply(fine, paidAt.AddYears(1));

        Assert.True(changed);
        Assert.True(fine.IsArchived);
        Assert.Equal(paidAt.AddYears(1), fine.ArchivedAt);
    }
}
