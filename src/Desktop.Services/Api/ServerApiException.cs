using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.Services.Api;

/// <summary>Thrown when the server returns a structured ApiErrorResponse.</summary>
public sealed class ServerApiException : Exception
{
    public ServerApiException(ApiError error) : base(error.Message)
    {
        Error = error;
    }

    public ApiError Error { get; }
}
