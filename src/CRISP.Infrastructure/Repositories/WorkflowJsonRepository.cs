using CRISP.Infrastructure.Persistence;

namespace CRISP.Infrastructure.Repositories;

public sealed class WorkflowJsonRepository
{
    private readonly AppDbContext _db;

    public WorkflowJsonRepository(AppDbContext db)
    {
        _db = db;
    }
}
