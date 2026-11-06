namespace Community.Application.Common.Helpers;

public static class ReputationTierHelper
{
    public static (int Level, string Title, string Badge) GetTierInfo(int prp)
    {
        if (prp >= 2500) return (5, "Legend", "🏆 Huyền thoại");
        if (prp >= 1000) return (4, "Master", "⭐ Chuyên gia");
        if (prp >= 400) return (3, "Senior", "🚀 Senior");
        if (prp >= 100) return (2, "Junior", "💻 Lập trình viên");
        return (1, "Newbie", "🌱 Tân thủ");
    }

    public static string GetTierBadge(int prp) => GetTierInfo(prp).Badge;
    public static string GetTierTitle(int prp) => GetTierInfo(prp).Title;
}
