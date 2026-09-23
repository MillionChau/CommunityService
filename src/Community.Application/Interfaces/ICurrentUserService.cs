namespace Community.Application.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        string? UserName { get; }
        bool IsAuthenticated { get; }

        /// <summary>Role hiện tại từ JWT (ClaimTypes.Role do IdentityService ký) — null nếu chưa đăng nhập.</summary>
        string? Role { get; }

        /// <summary>Tiện ích: người dùng hiện tại có phải Admin không.</summary>
        bool IsAdmin { get; }
    }
}