using Core;

namespace ServerAPI.Repositories.Fines;

public static class FineArchivePolicy
{
    public static bool Apply(Fine fine, DateTime utcNow)
    {
        var originalIsArchived = fine.IsArchived;
        var originalArchivedAt = fine.ArchivedAt;

        if (!fine.IsPaid || fine.PaidAt is null)
        {
            fine.IsArchived = false;
            fine.ArchivedAt = null;
        }
        else
        {
            var archiveDate = fine.PaidAt.Value.ToUniversalTime().AddYears(1);
            var shouldArchive = utcNow >= archiveDate;

            fine.IsArchived = shouldArchive;
            fine.ArchivedAt = shouldArchive ? archiveDate : null;
        }

        return fine.IsArchived != originalIsArchived || fine.ArchivedAt != originalArchivedAt;
    }
}
