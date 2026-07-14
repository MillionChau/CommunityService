using Community.Application.Interfaces;
using Community.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Community.Infrastructure.Persistence;

public class CommunityDbContext : DbContext, IApplicationDbContext
{
    public CommunityDbContext(DbContextOptions<CommunityDbContext> options) : base(options) {}
    
    public DbSet<Post> Posts => Set<Post>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommunityDbContext).Assembly);
    }
}
