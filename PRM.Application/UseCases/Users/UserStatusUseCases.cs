using PRM.Application.Interfaces.Repository;
using PRM.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PRM.Application.UseCases.Users
{
    public class DeactivateUserUseCase(IUnitOfWork unitOfWork)
    {
        public async Task<Result> ExecuteAsync(string username, CancellationToken ct = default)
        {
            var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user is null)
                return Result.Failure("User not found.");

            if (!user.IsActive)
                return Result.Failure("User is already inactive.");

            user.IsActive = false;

            unitOfWork.Users.Update(user);
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }

    public class ReactivateUserUseCase(IUnitOfWork unitOfWork)
    {
        public async Task<Result> ExecuteAsync(string username, CancellationToken ct = default)
        {
            var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user is null)
                return Result.Failure("User not found.");

            if (user.IsActive)
                return Result.Failure("User is already active.");

            user.IsActive = true;

            unitOfWork.Users.Update(user);
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }
}
