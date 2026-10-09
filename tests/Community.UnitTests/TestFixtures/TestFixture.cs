using Community.Application.Common.Mappings;
using Community.Application.Interfaces;
using Community.Application.Common.Models;
using Community.Domain.Contracts;
using Community.Domain.Entities;
using Community.Domain.Interfaces;
using Community.Infrastructure.Persistence;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Community.UnitTests.TestFixtures;

/// <summary>
/// Fixture dùng chung cho unit test handlers: mock toàn bộ dependency ngoài
/// (repositories, unit of work, current user, moderation, realtime, notification client).
/// Mapper là AutoMapper THẬT chạy MappingProfile của Application để test đúng mapping.
/// </summary>
public class TestFixture
{
    public Mock<IPostRepository> PostRepository { get; } = new();
    public Mock<ICommentRepository> CommentRepository { get; } = new();
    public Mock<IPostReportRepository> PostReportRepository { get; } = new();
    public Mock<IPostLikeRepository> PostLikeRepository { get; } = new();
    public Mock<ICommentLikeRepository> CommentLikeRepository { get; } = new();
    public Mock<IPostBookmarkRepository> PostBookmarkRepository { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<ICurrentUserService> CurrentUser { get; } = new();
    public Mock<IContentModerationService> Moderation { get; } = new();
    public Mock<IRealTimeNotifier> Notifier { get; } = new();
    public Mock<INotificationClient> NotificationClient { get; } = new();
    public IMapper Mapper { get; }

    public TestFixture()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        Mapper = config.CreateMapper();

        // Mặc định: moderation luôn pass (stub behavior)
        Moderation.Setup(m => m.CheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModerationResult { IsValid = true, Toxicity = "safe", QualityScore = 90 });
    }

    /// <summary>Đặt user hiện tại đăng nhập (đi kèm UserName).</summary>
    public void LoginAs(Guid userId, string? userName = "tester", bool isAdmin = false)
    {
        CurrentUser.SetupGet(u => u.UserId).Returns(userId);
        CurrentUser.SetupGet(u => u.UserName).Returns(userName);
        CurrentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        CurrentUser.SetupGet(u => u.Role).Returns(isAdmin ? "Admin" : "User");
        CurrentUser.SetupGet(u => u.IsAdmin).Returns(isAdmin);
    }

    /// <summary>Đặt user hiện tại chưa đăng nhập (Guest).</summary>
    public void Logout()
    {
        CurrentUser.SetupGet(u => u.UserId).Returns((Guid?)null);
        CurrentUser.SetupGet(u => u.UserName).Returns((string?)null);
        CurrentUser.SetupGet(u => u.IsAuthenticated).Returns(false);
        CurrentUser.SetupGet(u => u.Role).Returns((string?)null);
        CurrentUser.SetupGet(u => u.IsAdmin).Returns(false);
    }

    /// <summary>
    /// Bọc danh sách thành IQueryable hỗ trợ EF async (ToListAsync/CountAsync...)
    /// thông qua EF InMemory provider — dùng cho mock GetByExpression của repository.
    /// </summary>
    public IQueryable<T> ToAsyncQueryable<T>(List<T> items) where T : class
    {
        var options = new DbContextOptionsBuilder<CommunityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CommunityDbContext(options);
        context.Set<T>().AddRange(items);
        context.SaveChanges();
        return context.Set<T>();
    }

    /// <summary>Bọc danh sách rỗng thành async queryable.</summary>
    public IQueryable<T> ToEmptyAsyncQueryable<T>() where T : class => ToAsyncQueryable<T>([]);

    /// <summary>Tạo bài viết mẫu đã publish.</summary>
    public static Post CreatePost(Guid? authorId = null, int status = 1, string? content = "Sample post content")
        => new()
        {
            Id = Guid.NewGuid(),
            Content = content,
            AuthorId = authorId ?? Guid.NewGuid(),
            Status = status,
            LikesCount = 0,
            CommentsCount = 0,
            SharesCount = 0,
            BookmarksCount = 0,
            ViewsCount = 0,
            CreatedDate = DateTime.UtcNow
        };

    /// <summary>Tạo bình luận mẫu.</summary>
    public static Comment CreateComment(Guid? postId = null, Guid? authorId = null, int? status = 1)
        => new()
        {
            Id = Guid.NewGuid(),
            PostId = postId ?? Guid.NewGuid(),
            AuthorId = authorId ?? Guid.NewGuid(),
            Content = "Sample comment",
            Status = status,
            LikesCount = 0,
            CreatedDate = DateTime.UtcNow
        };

    /// <summary>Tạo báo cáo vi phạm mẫu.</summary>
    public static PostReport CreateReport(Guid? postId = null, Guid? reporterId = null, int status = 0)
        => new()
        {
            Id = Guid.NewGuid(),
            PostId = postId ?? Guid.NewGuid(),
            ReporterId = reporterId ?? Guid.NewGuid(),
            Reason = "Spam content",
            Status = status,
            CreatedDate = DateTime.UtcNow
        };
}
