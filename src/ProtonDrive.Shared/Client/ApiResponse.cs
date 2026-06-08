namespace ProtonDrive.Client;

public record ApiResponse
{
    public ResponseCode Code { get; init; }

    public string? Error { get; init; }

    public bool Succeeded => Code is ResponseCode.Success or ResponseCode.MultipleResponses;

    public static ApiResponse Success { get; } = new() { Code = ResponseCode.Success };
}
