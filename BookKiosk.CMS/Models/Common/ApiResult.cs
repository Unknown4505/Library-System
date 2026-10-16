namespace BookKiosk.CMS.Models.Common
{
    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public int StatusCode { get; set; } = 200;
        public bool IsEndpointUnavailable { get; set; }

        public static ApiResult<T> Ok(T data, string? message = null)
        {
            return new ApiResult<T>
            {
                Success = true,
                Data = data,
                Message = message,
                StatusCode = 200
            };
        }

        public static ApiResult<T> Fail(string message, List<string>? errors = null, int statusCode = 400)
        {
            return new ApiResult<T>
            {
                Success = false,
                Message = message,
                Errors = errors,
                StatusCode = statusCode
            };
        }

        public static ApiResult<T> Fail(string message, int statusCode)
        {
            return new ApiResult<T>
            {
                Success = false,
                Message = message,
                StatusCode = statusCode
            };
        }

        public static ApiResult<T> EndpointUnavailable(string endpoint, string method, int statusCode = 404)
        {
            return new ApiResult<T>
            {
                Success = false,
                IsEndpointUnavailable = true,
                StatusCode = statusCode,
                Message = $"Endpoint chưa sẵn sàng trên Backend ({method.ToUpperInvariant()} {endpoint} - {statusCode})"
            };
        }
    }

    public class ApiResult : ApiResult<object>
    {
        public static ApiResult SuccessResult(string? message = null)
        {
            return new ApiResult
            {
                Success = true,
                Message = message
            };
        }

        public static ApiResult ErrorResult(string message, List<string>? errors = null)
        {
            return new ApiResult
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }
}
