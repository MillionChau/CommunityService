namespace Community.Infrastructure.Settings;

/// <summary>
/// Cấu hình JWT khớp contract với IdentityService (đọc từ section "JwtSettings").
/// Token do IdentityService ký bằng HS256 với Secret chung — CommunityService chỉ xác thực.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
