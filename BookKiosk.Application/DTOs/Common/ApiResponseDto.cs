namespace BookKiosk.Application.DTOs.Common;

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    // Factory method for Success
    public static ApiResponseDto<T> Ok(T data, string message = "Thành công")
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Code = "SUCCESS",
            Message = message,
            Data = data
        };
    }

    // Factory method for Error
    public static ApiResponseDto<T> Error(string code, string message)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Data = default
        };
    }
}
