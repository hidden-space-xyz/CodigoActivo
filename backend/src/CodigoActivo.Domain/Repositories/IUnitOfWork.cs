namespace CodigoActivo.Domain.Repositories;

public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
