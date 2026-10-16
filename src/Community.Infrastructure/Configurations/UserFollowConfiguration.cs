using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Community.Infrastructure.Configurations;

public class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.ToTable("UserFollows");

        builder.HasKey(f => f.Id);

        // Đảm bảo không follow trùng 1 người
        builder.HasIndex(f => new { f.FollowerId, f.FolloweeId })
            .IsUnique()
            .HasDatabaseName("UX_UserFollows_Follower_Followee");

        builder.HasIndex(f => f.FollowerId);
        builder.HasIndex(f => f.FolloweeId);
    }
}
