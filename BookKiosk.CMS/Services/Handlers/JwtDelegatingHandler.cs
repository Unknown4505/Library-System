using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace BookKiosk.CMS.Services.Handlers
{
    public class JwtDelegatingHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<JwtDelegatingHandler> _logger;

        public JwtDelegatingHandler(IHttpContextAccessor httpContextAccessor, ILogger<JwtDelegatingHandler> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                // Ưu tiên đọc token lưu trong AuthenticationProperties
                var token = await httpContext.GetTokenAsync("access_token");

                // Nếu không có trong properties, tìm trong claims
                if (string.IsNullOrWhiteSpace(token))
                {
                    token = httpContext.User.FindFirst("access_token")?.Value;
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("API Backend trả về 401 Unauthorized cho endpoint: {RequestUri}", request.RequestUri);
            }

            return response;
        }
    }
}
