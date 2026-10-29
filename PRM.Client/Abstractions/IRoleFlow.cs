namespace PRM.Client.Abstractions;

/// <summary>Console menu loop for a logged-in role (Admin, Manager, Employee).</summary>
public interface IRoleFlow
{
    Task RunAsync(CancellationToken ct);
}
