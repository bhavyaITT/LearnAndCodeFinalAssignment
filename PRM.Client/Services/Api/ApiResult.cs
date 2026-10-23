namespace PRM.Client.Services.Api;

public class ApiResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }

    public static ApiResult Ok() => new() { IsSuccess = true };
    public static ApiResult Fail(string error) => new() { IsSuccess = false, Error = error };
}

public sealed class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T data) => new() { IsSuccess = true, Data = data };
    public static new ApiResult<T> Fail(string error) => new() { IsSuccess = false, Error = error };
}
