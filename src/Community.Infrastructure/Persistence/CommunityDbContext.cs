using Community.Application.Interfaces;
using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Persistence;

public class CommunityDbContext : DbContext, IApplicationDbContext
{
    public CommunityDbContext(DbContextOptions<CommunityDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostReport> PostReports => Set<PostReport>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<CommentLike> CommentLikes => Set<CommentLike>();
    public DbSet<PostBookmark> PostBookmarks => Set<PostBookmark>();
    public DbSet<TechZone> TechZones => Set<TechZone>();
    public DbSet<BookmarkFolder> BookmarkFolders => Set<BookmarkFolder>();
    public DbSet<CommentReport> CommentReports => Set<CommentReport>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);
    }
}
