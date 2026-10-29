using PRM.Application.Common;
using PRM.Application.DTOs.Project;
using PRM.Application.Interfaces.Repository;
using PRM.Domain.Entities;
using PRM.Domain.Enums;

namespace PRM.Application.UseCases.Projects;

public class AddMilestoneUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result<MilestoneResponse>> ExecuteAsync(int projectId, AddMilestoneRequest request, CancellationToken ct = default)
    {
        var titleError = ValidationRules.ValidateNonEmptyName(request.Title, "Milestone title");
        if (titleError is not null)
            return Result<MilestoneResponse>.Failure(titleError);

        var project = await unitOfWork.Projects.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<MilestoneResponse>.Failure($"Project with ID {projectId} not found.");

        var dueDateError = ValidationRules.ValidateMilestoneDueDate(request.DueDate, project.EndDate);
        if (dueDateError is not null)
            return Result<MilestoneResponse>.Failure(dueDateError);

        var milestone = new Milestone
        {
            ProjectId = projectId,
            Title = request.Title.Trim(),
            DueDate = request.DueDate,
            Status = MilestoneStatus.NotStarted
        };

        await unitOfWork.Milestones.AddAsync(milestone, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<MilestoneResponse>.Success(new MilestoneResponse(
            milestone.Id, milestone.Title, milestone.DueDate, milestone.Status.ToString()
        ));
    }
}

public class UpdateMilestoneStatusUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result> ExecuteAsync(int milestoneId, UpdateMilestoneStatusRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MilestoneStatus>(request.Status, ignoreCase: true, out var status))
            return Result.Failure($"Invalid status '{request.Status}'. Valid: NotStarted, InProgress, Done.");

        var milestone = await unitOfWork.Milestones.GetByIdAsync(milestoneId, ct);
        if (milestone is null)
            return Result.Failure($"Milestone with ID {milestoneId} not found.");

        milestone.Status = status;
        unitOfWork.Milestones.Update(milestone);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public class UpdateMilestoneDueDateUseCase(IUnitOfWork unitOfWork)
{
    public async Task<Result<MilestoneResponse>> ExecuteAsync(
        int milestoneId, UpdateMilestoneDueDateRequest request, CancellationToken ct = default)
    {
        var milestone = await unitOfWork.Milestones.GetByIdAsync(milestoneId, ct);
        if (milestone is null)
            return Result<MilestoneResponse>.Failure($"Milestone with ID {milestoneId} not found.");

        var project = await unitOfWork.Projects.GetByIdAsync(milestone.ProjectId, ct);
        if (project is null)
            return Result<MilestoneResponse>.Failure($"Project for milestone {milestoneId} not found.");

        var dueDateError = ValidationRules.ValidateMilestoneDueDate(request.DueDate, project.EndDate);
        if (dueDateError is not null)
            return Result<MilestoneResponse>.Failure(dueDateError);

        milestone.DueDate = request.DueDate;
        unitOfWork.Milestones.Update(milestone);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<MilestoneResponse>.Success(new MilestoneResponse(
            milestone.Id, milestone.Title, milestone.DueDate, milestone.Status.ToString()
        ));
    }
}
