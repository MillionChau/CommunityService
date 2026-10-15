using System.Linq.Expressions;
using Community.Application.Features.TechZones.Queries.GetActiveTechZones;
using Community.Domain.Entities;
using Community.UnitTests.TestFixtures;
using Moq;
using Xunit;

namespace Community.UnitTests.Features.TechZones;

public class GetActiveTechZonesQueryHandlerTests
{
    private readonly TestFixture _f = new();

    [Fact]
    public async Task Handle_ShouldReturnOnlyActiveTechZones_OrderedByDisplayOrder()
    {
        // Arrange (UC-14)
        var zone1 = new TechZone { Id = Guid.NewGuid(), Name = ".NET", Slug = "dotnet", Status = 1, DisplayOrder = 2 };
        var zone2 = new TechZone { Id = Guid.NewGuid(), Name = "AI / Data", Slug = "ai-data", Status = 1, DisplayOrder = 1 };
        var zoneInactive = new TechZone { Id = Guid.NewGuid(), Name = "Old Tech", Slug = "old-tech", Status = 0, DisplayOrder = 3 };

        var all = _f.ToAsyncQueryable(new List<TechZone> { zone1, zone2, zoneInactive });
        _f.TechZoneRepository
            .Setup(r => r.GetByExpression(It.IsAny<Expression<Func<TechZone, bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<TechZone, bool>>? expr, CancellationToken _) =>
                expr == null ? all : all.Where(expr));

        var handler = new GetActiveTechZonesQueryHandler(_f.TechZoneRepository.Object, _f.Mapper);

        // Act
        var result = await handler.Handle(new GetActiveTechZonesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var items = result.Data!.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("ai-data", items[0].Slug); // DisplayOrder 1 trước
        Assert.Equal("dotnet", items[1].Slug);  // DisplayOrder 2 sau
    }
}
