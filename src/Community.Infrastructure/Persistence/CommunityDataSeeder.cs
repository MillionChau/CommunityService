using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Community.Infrastructure.Persistence;

public static class CommunityDataSeeder
{
    public static async Task SeedAsync(CommunityDbContext context, ILogger logger)
    {
        try
        {
            // Author GUIDs khớp với IdentityDataSeeder
            var adminAuthorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var devAuthorId = Guid.Parse("00000000-0000-0000-0000-000000000002");

            // 1. TechZones
            var tzDotnetId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
            var tzPythonId = Guid.Parse("a2222222-2222-2222-2222-222222222222");
            var tzReactId = Guid.Parse("a3333333-3333-3333-3333-333333333333");
            var tzGolangId = Guid.Parse("a4444444-4444-4444-4444-444444444444");
            var tzDevopsId = Guid.Parse("a5555555-5555-5555-5555-555555555555");

            if (!await context.TechZones.AnyAsync())
            {
                logger.LogInformation("Seeding default TechZones...");
                var techZones = new List<TechZone>
                {
                    new()
                    {
                        Id = tzDotnetId,
                        Name = ".NET Core & C#",
                        Slug = "dotnet",
                        Description = "Hệ sinh thái .NET 10, ASP.NET Core, C# và kiến trúc Enterprise hiện đại",
                        IconUrl = "code",
                        ColorCode = "#512BD4",
                        Tags = "dotnet,csharp,aspnet,efcore",
                        DisplayOrder = 1,
                        Status = 1,
                        PostsCount = 1,
                        FollowersCount = 450,
                        CreatedDate = DateTime.UtcNow
                    },
                    new()
                    {
                        Id = tzPythonId,
                        Name = "Python & AI / Data",
                        Slug = "python-ai",
                        Description = "Trí tuệ nhân tạo, LLM, Machine Learning, FastAPI và PyTorch",
                        IconUrl = "cpu",
                        ColorCode = "#3776AB",
                        Tags = "python,fastapi,ai,llm,pytorch",
                        DisplayOrder = 2,
                        Status = 1,
                        PostsCount = 2,
                        FollowersCount = 520,
                        CreatedDate = DateTime.UtcNow
                    },
                    new()
                    {
                        Id = tzReactId,
                        Name = "TypeScript & React",
                        Slug = "typescript-react",
                        Description = "Frontend hiện đại, React 19, TypeScript, Next.js và Tailwind CSS",
                        IconUrl = "layout",
                        ColorCode = "#3178C6",
                        Tags = "react,typescript,nextjs,tailwind",
                        DisplayOrder = 3,
                        Status = 1,
                        PostsCount = 1,
                        FollowersCount = 380,
                        CreatedDate = DateTime.UtcNow
                    },
                    new()
                    {
                        Id = tzGolangId,
                        Name = "Go & Cloud Native",
                        Slug = "golang",
                        Description = "Golang, High Concurrency, Kubernetes, Microservices và gRPC",
                        IconUrl = "cloud",
                        ColorCode = "#00ADD8",
                        Tags = "golang,kubernetes,cloudnative,grpc",
                        DisplayOrder = 4,
                        Status = 1,
                        PostsCount = 0,
                        FollowersCount = 290,
                        CreatedDate = DateTime.UtcNow
                    },
                    new()
                    {
                        Id = tzDevopsId,
                        Name = "DevOps & Container",
                        Slug = "devops",
                        Description = "CI/CD, Docker, GitHub Actions, Helm, Monitoring và Security",
                        IconUrl = "terminal",
                        ColorCode = "#2496ED",
                        Tags = "docker,devops,cicd,linux",
                        DisplayOrder = 5,
                        Status = 1,
                        PostsCount = 1,
                        FollowersCount = 310,
                        CreatedDate = DateTime.UtcNow
                    }
                };

                await context.TechZones.AddRangeAsync(techZones);
                await context.SaveChangesAsync();
            }

            // 2. Posts
            var post1Id = Guid.Parse("b1111111-1111-1111-1111-111111111111");
            var post2Id = Guid.Parse("b2222222-2222-2222-2222-222222222222");
            var post3Id = Guid.Parse("b3333333-3333-3333-3333-333333333333");
            var post4Id = Guid.Parse("b4444444-4444-4444-4444-444444444444");
            var post5Id = Guid.Parse("b5555555-5555-5555-5555-555555555555");

            if (!await context.Posts.AnyAsync())
            {
                logger.LogInformation("Seeding default Posts...");
                var posts = new List<Post>
                {
                    new()
                    {
                        Id = post1Id,
                        Content = "Kiến trúc Microservices với .NET 10, YARP Reverse Proxy & PostgreSQL.\n\nChia sẻ kinh nghiệm thiết kế kiến trúc phân tán thực tế cho dự án DevRadar: Xử lý JWT rotation, Rate limiting và tích hợp kiểm duyệt AI tự động qua Python QualityService. Hệ thống tách biệt Gateway làm điểm điều hướng tập trung, kết hợp PostgreSQL riêng biệt cho mỗi bounded context.",
                        AuthorId = devAuthorId,
                        TechZoneId = tzDotnetId,
                        Tags = "dotnet10,microservices,yarp,postgres",
                        LikesCount = 128,
                        CommentsCount = 2,
                        SharesCount = 15,
                        BookmarksCount = 45,
                        ViewsCount = 890,
                        Status = 1, // Published
                        IsApproved = 1,
                        IsPinned = 1,
                        IsDeleted = 0,
                        CreatedBy = devAuthorId,
                        CreatedDate = DateTime.UtcNow.AddHours(-6)
                    },
                    new()
                    {
                        Id = post2Id,
                        Content = "Xây dựng GitHub Crawler Engine & Phân tích Radar Chart 5 trục với Python.\n\nKhám phá giải pháp thu thập dữ liệu GitHub qua GraphQL API, tính toán điểm uy tín Repo Health (A+ 94/100) và trực quan hóa năng lực lập trình viên trên đồ thị ngũ giác. Tích hợp AI Summary tự động phân tích commit logs và highlight công nghệ nổi bật.",
                        AuthorId = devAuthorId,
                        TechZoneId = tzPythonId,
                        Tags = "python,graphql,crawler,radarchart",
                        LikesCount = 95,
                        CommentsCount = 2,
                        SharesCount = 10,
                        BookmarksCount = 32,
                        ViewsCount = 640,
                        Status = 1,
                        IsApproved = 1,
                        IsPinned = 0,
                        IsDeleted = 0,
                        CreatedBy = devAuthorId,
                        CreatedDate = DateTime.UtcNow.AddHours(-18)
                    },
                    new()
                    {
                        Id = post3Id,
                        Content = "Pipeline kiểm duyệt nội dung độc hại (Toxicity Moderation) thời gian thực sử dụng QualityService.\n\nCách hệ thống DevRadar tự động gắn cờ bài viết vi phạm Non-IT hoặc Toxic bằng mô hình AI FastAPI, đồng thời tích hợp Heuristic Fallback Engine bảo đảm uptime 100% khi AI Provider chạm rate limit 429.",
                        AuthorId = adminAuthorId,
                        TechZoneId = tzPythonId,
                        Tags = "ai,moderation,nlp,quality",
                        LikesCount = 78,
                        CommentsCount = 0,
                        SharesCount = 8,
                        BookmarksCount = 19,
                        ViewsCount = 420,
                        Status = 1,
                        IsApproved = 1,
                        IsPinned = 0,
                        IsDeleted = 0,
                        CreatedBy = adminAuthorId,
                        CreatedDate = DateTime.UtcNow.AddDays(-1)
                    },
                    new()
                    {
                        Id = post4Id,
                        Content = "Tối ưu hóa hiệu năng React 19 Single Page App kết hợp Tailwind CSS và SignalR Realtime Notification.\n\nQuản lý trạng thái thông báo tức thời qua WebSocket Hub xuyên qua YARP Gateway, kiến trúc Axios Client tự động Refresh Token, và kỹ thuật lazy-loading components nâng cao tốc độ tải trang.",
                        AuthorId = devAuthorId,
                        TechZoneId = tzReactId,
                        Tags = "react,vite,signalr,tailwind",
                        LikesCount = 64,
                        CommentsCount = 0,
                        SharesCount = 5,
                        BookmarksCount = 28,
                        ViewsCount = 380,
                        Status = 1,
                        IsApproved = 1,
                        IsPinned = 0,
                        IsDeleted = 0,
                        CreatedBy = devAuthorId,
                        CreatedDate = DateTime.UtcNow.AddDays(-2)
                    },
                    new()
                    {
                        Id = post5Id,
                        Content = "Triển khai hệ thống DevRadar với Docker Compose: Multi-stage build cho 5 Microservices (.NET, Python, Node).\n\nTối ưu kích thước image dưới 200MB, tự động cấu hình network bridge, phân bổ volume persistence cho PostgreSQL, và bảo đảm tính nhất quán giữa môi trường Local Dev và Production Container.",
                        AuthorId = adminAuthorId,
                        TechZoneId = tzDevopsId,
                        Tags = "docker,microservices,devops,compose",
                        LikesCount = 112,
                        CommentsCount = 0,
                        SharesCount = 20,
                        BookmarksCount = 51,
                        ViewsCount = 920,
                        Status = 1,
                        IsApproved = 1,
                        IsPinned = 0,
                        IsDeleted = 0,
                        CreatedBy = adminAuthorId,
                        CreatedDate = DateTime.UtcNow.AddDays(-3)
                    }
                };

                await context.Posts.AddRangeAsync(posts);
                await context.SaveChangesAsync();

                // 3. Comments (kèm replies lồng nhau)
                var c1Id = Guid.Parse("c1111111-1111-1111-1111-111111111111");
                var c1ReplyId = Guid.Parse("c1111111-1111-1111-1111-111111111112");
                var c2Id = Guid.Parse("c2222222-2222-2222-2222-222222222221");
                var c2ReplyId = Guid.Parse("c2222222-2222-2222-2222-222222222222");

                var comments = new List<Comment>
                {
                    new()
                    {
                        Id = c1Id,
                        CommentId = c1Id,
                        PostId = post1Id,
                        AuthorId = adminAuthorId,
                        Content = "Bài viết rất chi tiết và đúng chuẩn Clean Architecture! Việc cấu hình YARP Gateway làm Reverse Proxy giúp bảo mật toàn bộ backend microservices rất tốt.",
                        LikesCount = 12,
                        RepliesCount = 1,
                        Status = 1,
                        IsApproved = 1,
                        IsDeleted = 0,
                        CreatedBy = adminAuthorId,
                        CreatedDate = DateTime.UtcNow.AddHours(-4)
                    },
                    new()
                    {
                        Id = c1ReplyId,
                        CommentId = c1ReplyId,
                        PostId = post1Id,
                        ParentCommentId = c1Id,
                        AuthorId = devAuthorId,
                        Content = "Cảm ơn anh! Sắp tới nhóm sẽ bổ sung thêm OpenTelemetry tracing để trace trọn vẹn request từ Frontend qua YARP vào các downstream services.",
                        LikesCount = 5,
                        RepliesCount = 0,
                        Status = 1,
                        IsApproved = 1,
                        IsDeleted = 0,
                        CreatedBy = devAuthorId,
                        CreatedDate = DateTime.UtcNow.AddHours(-3)
                    },
                    new()
                    {
                        Id = c2Id,
                        CommentId = c2Id,
                        PostId = post2Id,
                        AuthorId = adminAuthorId,
                        Content = "Radar Chart ngũ giác tính điểm các tiêu chí nào vậy bạn? Có tính theo commit frequency và PR reviews không?",
                        LikesCount = 8,
                        RepliesCount = 1,
                        Status = 1,
                        IsApproved = 1,
                        IsDeleted = 0,
                        CreatedBy = adminAuthorId,
                        CreatedDate = DateTime.UtcNow.AddHours(-15)
                    },
                    new()
                    {
                        Id = c2ReplyId,
                        CommentId = c2ReplyId,
                        PostId = post2Id,
                        ParentCommentId = c2Id,
                        AuthorId = devAuthorId,
                        Content = "Đúng rồi anh, 5 trục gồm: Velocity (tốc độ commit), Quality (CI/CD test coverage), Collaboration (PR & Issue reviews), Popularity (Stars & Forks), và Consistency (tính đều đặn theo tuần).",
                        LikesCount = 7,
                        RepliesCount = 0,
                        Status = 1,
                        IsApproved = 1,
                        IsDeleted = 0,
                        CreatedBy = devAuthorId,
                        CreatedDate = DateTime.UtcNow.AddHours(-14)
                    }
                };

                await context.Comments.AddRangeAsync(comments);
                await context.SaveChangesAsync();

                logger.LogInformation("Successfully seeded default Community data (TechZones, Posts, Comments).");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while seeding default Community data.");
        }
    }
}
