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
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }
    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}