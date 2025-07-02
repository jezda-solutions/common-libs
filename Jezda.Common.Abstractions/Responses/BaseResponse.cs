namespace Jezda.Common.Abstractions.Responses;

public record BaseResponse<T>(T Id, string Name)
        : IBaseResponse;