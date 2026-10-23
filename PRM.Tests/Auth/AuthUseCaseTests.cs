using PRM.Application.DTOs.Auth;
using PRM.Application.Interfaces.Service;
using PRM.Application.UseCases.Auth;
using PRM.Domain.Enums;
using PRM.Tests.Helpers;

namespace PRM.Tests.Auth;

public class LoginUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsToken_WhenCredentialsValid()
    {
        var uow = new InMemoryUnitOfWork();
        var user = TestDataBuilder.CreateUser(1, UserRole.Manager);
        uow.Users.Seed(user);
        await uow.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("Password1", user.PasswordHash)).Returns(true);

        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.GenerateToken(1, user.Username, "Manager")).Returns("jwt-token");

        var useCase = new LoginUseCase(uow, hasher.Object, tokenService.Object);
        var result = await useCase.ExecuteAsync(new LoginRequest(user.Username, "Password1"));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Token.Should().Be("jwt-token");
        result.Data.Role.Should().Be("Manager");
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenPasswordInvalid()
    {
        var uow = new InMemoryUnitOfWork();
        var user = TestDataBuilder.CreateUser(1, UserRole.Employee);
        uow.Users.Seed(user);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var useCase = new LoginUseCase(uow, hasher.Object, Mock.Of<ITokenService>());
        var result = await useCase.ExecuteAsync(new LoginRequest(user.Username, "wrong"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenAccountDeactivated()
    {
        var uow = new InMemoryUnitOfWork();
        var user = TestDataBuilder.CreateUser(1, UserRole.Employee, isActive: false);
        uow.Users.Seed(user);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var useCase = new LoginUseCase(uow, hasher.Object, Mock.Of<ITokenService>());
        var result = await useCase.ExecuteAsync(new LoginRequest(user.Username, "Password1"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("deactivated");
    }
}

public class ChangePasswordUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesPassword_WhenRequestValid()
    {
        var uow = new InMemoryUnitOfWork();
        uow.Users.Seed(TestDataBuilder.CreateUser(1, UserRole.Employee));

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash("NewPass1")).Returns("new-hash");

        var useCase = new ChangePasswordUseCase(uow, hasher.Object);
        var result = await useCase.ExecuteAsync(1, new ChangePasswordRequest("NewPass1", "NewPass1"));

        result.IsSuccess.Should().BeTrue();
        uow.Users.Items[0].PasswordHash.Should().Be("new-hash");
        uow.Users.Items[0].ForcePasswordChange.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenPasswordsDoNotMatch()
    {
        var useCase = new ChangePasswordUseCase(new InMemoryUnitOfWork(), Mock.Of<IPasswordHasher>());
        var result = await useCase.ExecuteAsync(1, new ChangePasswordRequest("NewPass1", "OtherPass1"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("do not match");
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenPasswordWeak()
    {
        var useCase = new ChangePasswordUseCase(new InMemoryUnitOfWork(), Mock.Of<IPasswordHasher>());
        var result = await useCase.ExecuteAsync(1, new ChangePasswordRequest("weak", "weak"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("8 characters");
    }
}
