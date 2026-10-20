using System;
using System.Linq;
using Inno.Core.Mathematics;
using Xunit;

namespace Inno.Rendering2D.Tests;

public sealed class AtlasSlicingTests
{
    [Fact]
    public void GridPreservesEditedIdentityAndOtherSources()
    {
        SpriteAtlasSlice2D retained = new()
        {
            id = SpriteRegionId.Create(),
            name = "Hero",
            sourceIndex = 0,
            pixelRect = new Rect(0, 0, 16, 16),
            pivot = new Vector2(0.2f, 0.7f),
            outline = []
        };
        SpriteAtlasSlice2D other = retained with
        {
            id = SpriteRegionId.Create(),
            sourceIndex = 1
        };
        SpriteAtlasSlice2D[] slices = SpriteAtlasSlicing2D.Grid([retained, other], 0, "Sheet", 35, 32, 16, 16, Vector2.ZERO);
        Assert.Equal(5, slices.Length);
        Assert.Contains(other, slices);
        Assert.Equal(retained, slices.Single(slice => slice.id == retained.id));
        Assert.All(slices.Where(slice => slice.sourceIndex == 0), slice => Assert.True(slice.pixelRect.right <= 32));
        SpriteAtlasSlice2D[] repeated = SpriteAtlasSlicing2D.Grid(slices, 0, "Renamed", 35, 32, 16, 16, Vector2.ONE);
        Assert.Equal(slices.Select(slice => slice.id), repeated.Select(slice => slice.id));
    }

    [Theory]
    [InlineData(0, 16, 1, 1)]
    [InlineData(16, 16, 0, 1)]
    [InlineData(1000, 1000, 1, 1)]
    public void InvalidOrUnboundedGridsFailBeforeAllocating(
        int width,
        int height,
        int cellWidth,
        int cellHeight
    ) => Assert.Throws<ArgumentOutOfRangeException>(() => SpriteAtlasSlicing2D.Grid([], 0, "Sheet", width, height, cellWidth, cellHeight, Vector2.ZERO));
    [Fact]
    public void OversizedCellsProduceAnEmptySourceWithoutAnUnboundedLoop()
    {
        Assert.Empty(SpriteAtlasSlicing2D.Grid([], 0, "Sheet", 1, int.MaxValue, 2, 1, Vector2.ZERO));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteAtlasSlicing2D.Grid([], 0, "Sheet", 16, 16, 16, 16, new Vector2(float.NaN, 0)));
    }
}
