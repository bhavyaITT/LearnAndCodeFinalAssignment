namespace PRM.Application.Interfaces.Service;

public interface IProjectHealthService
{
    Task RecalculateHealthAsync(int projectId, CancellationToken ct = default);
}
