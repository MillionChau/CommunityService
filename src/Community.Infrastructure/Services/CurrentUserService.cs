using System.Security.Claims;
using Community.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Community.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    public Guid? UserId
    {
        get
        {
            var http = _httpContextAccessor.HttpContext;
            if (http?.User?.Identity?.IsAuthenticated != true) return null;

            // IdentityService đặt userId vào "sub" (được map sang NameIdentifier) và "user_id"
            var claim = http.User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? http.User.FindFirst("sub")
                        ?? http.User.FindFirst("user_id");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }
    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    /// <summary>Role đầu tiên trong token (IdentityService ghi ClaimTypes.Role + "roles").</summary>
    public string? Role =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("roles")?.Value;

    public bool IsAdmin =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}