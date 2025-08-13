namespace Jezda.Common.Abstractions.Responses;

public record CodeResponse<T>(T Id, string Code)
        : ICodeResponse;