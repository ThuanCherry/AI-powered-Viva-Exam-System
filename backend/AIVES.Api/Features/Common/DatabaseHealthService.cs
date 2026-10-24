using AIVES.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AIVES.Api.Features.Common;

public class DatabaseHealthService(AivesDbContext db)
{
    public async Task<bool> CheckAsync(CancellationToken ct)
    {
        try { return await db.Database.CanConnectAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; }
    }
}
