using CRISP.Infrastructure.Persistence;

namespace CRISP.Infrastructure.Repositories;

public sealed class WorkflowRepository
{
    private readonly AppDbContext _db;

    public WorkflowRepository(AppDbContext db)
    {
        _db = db;
    }
}
