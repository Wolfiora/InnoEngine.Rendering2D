using System;
using Inno.Core.Mathematics;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Xunit;

namespace Inno.Rendering2D.Tests;

public sealed class AssetValidationTests
{
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-1f)]
    [InlineData(0f)]
    public void AnimationAndTileFramesRejectInvalidTimes(float duration)
    {
        SpriteReference2D sprite = new() { texture = Texture() };
        var animation = new SpriteAnimation2DAsset();
        Assert.Throws<ArgumentException>(() => animation.SetClips([new()
        {
            id = "flight", name = "Flight", frames = [new() { sprite = sprite, duration = duration }]
        }]));
        var tiles = new TileSet2DAsset();
        Assert.Throws<ArgumentException>(() => tiles.SetTiles([new()
        {
            id = 0, name = "Water", animation = [new() { sprite = sprite, duration = duration }]
        }]));
        Assert.Empty(animation.clips);
        Assert.Empty(tiles.tiles);
    }

    [Fact]
    public void AtlasRejectsNonFiniteGeometryWithoutReplacingValidSlices()
    {
        var atlas = new SpriteAtlas2DAsset { sources = [new() { name = "Sheet", texture = Texture() }] };
        SpriteAtlasSlice2D valid = new()
        {
            id = SpriteRegionId.Create(), name = "Sprite", sourceIndex = 0,
            pixelRect = new Rect(0, 0, 16, 16), pivot = new Vector2(0.5f, 0.5f), outline = []
        };
        atlas.SetSlices([valid]);
        Assert.Throws<ArgumentException>(() => atlas.SetSlices([valid with { pixelRect = new Rect(float.NaN, 0, 16, 16) }]));
        Assert.Throws<ArgumentException>(() => atlas.SetSlices([valid with { pivot = new Vector2(float.NaN, 0) }]));
        Assert.Equal(valid.id, Assert.Single(atlas.slices).id);
    }

    [Fact]
    public void ParticleValidationRejectsNonFiniteParametersAndAmbiguousKeys()
    {
        var effect = new ParticleEffect2DAsset { sprite = new() { texture = Texture() } };
        effect.Validate();
        effect.emissionRate = float.NaN;
        Assert.Throws<InvalidOperationException>(effect.Validate);
        effect.emissionRate = 10f;
        effect.sizeOverLifetime = new() { keys = [new() { time = 0f, value = 1 }, new() { time = 0f, value = 2 }] };
        Assert.Throws<InvalidOperationException>(effect.Validate);
        effect.sizeOverLifetime = new() { keys = [new() { time = 0f, value = 1 }, new() { time = 1f, value = 0 }] };
        effect.Validate();
        Assert.Equal(0.5f, effect.sizeOverLifetime.Evaluate(0.5f));
        effect.colorOverLifetime = new() { keys = [new() { time = 1.1f, color = Color.WHITE }] };
        Assert.Throws<InvalidOperationException>(effect.Validate);
    }

    private static TextureAsset Texture() => new(16, 16, TextureColorSpace.Srgb, "png");
}
