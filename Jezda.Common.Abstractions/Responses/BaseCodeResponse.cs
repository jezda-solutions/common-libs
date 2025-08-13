namespace Jezda.Common.Abstractions.Responses;

public record BaseCodeResponse<T>(T Id, string Name, string? Code)
        : IBaseResponse;