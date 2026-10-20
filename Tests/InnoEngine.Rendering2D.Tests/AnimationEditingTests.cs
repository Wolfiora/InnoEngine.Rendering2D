using System;
using Inno.Assets;
using Inno.Rendering.Assets;
using Inno.Rendering;
using Xunit;

namespace Inno.Rendering2D.Tests;

public sealed class AnimationEditingTests
{
    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(0f, 0)]
    [InlineData(0.249f, 0)]
    [InlineData(0.25f, 1)]
    [InlineData(0.749f, 1)]
    [InlineData(0.75f, 2)]
    [InlineData(1f, 2)]
    [InlineData(9f, 2)]
    public void PlayheadUsesHalfOpenFrameIntervals(
        float time,
        int expected
    ) {
        SpriteAnimationFrame2D[] frames = Frames();
        Assert.Equal(expected, SpriteAnimationEditing2D.FrameAt(frames, time));
        Assert.Equal(1f, SpriteAnimationEditing2D.Duration(frames));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidDurationAnywhereRejectsTheWholeClip(float duration)
    {
        SpriteAnimationFrame2D[] frames = Frames();
        frames[2].duration = duration;
        Assert.Throws<ArgumentException>(() => SpriteAnimationEditing2D.FrameAt(frames, 0f));
        Assert.Throws<ArgumentException>(() => SpriteAnimationEditing2D.Duration(frames));
    }

    [Fact]
    public void EmptyAndNonFinitePlayheadsAreExplicit()
    {
        Assert.Equal(-1, SpriteAnimationEditing2D.FrameAt([], 0));
        Assert.Equal(0, SpriteAnimationEditing2D.Duration([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteAnimationEditing2D.FrameAt(Frames(), float.NaN));
        Assert.Throws<ArgumentException>(() => SpriteAnimationEditing2D.Duration([new() { duration = float.MaxValue }, new() { duration = float.MaxValue }]));
    }

    [Fact]
    public void DuplicateAndMovePreserveValuesWithoutMutatingTheSource()
    {
        SpriteAnimationFrame2D[] frames = Frames();
        SpriteAnimationFrame2D[] duplicate = SpriteAnimationEditing2D.Duplicate(frames, 1);
        Assert.Equal(4, duplicate.Length);
        Assert.Equal(frames[1], duplicate[2]);
        SpriteAnimationFrame2D[] moved = SpriteAnimationEditing2D.Move(frames, 0, 2);
        Assert.Equal("a", moved[2].eventId);
        Assert.Equal("a", frames[0].eventId);
        duplicate[0].duration = 10f;
        Assert.Equal(0.25f, frames[0].duration);
        Assert.Throws<ArgumentOutOfRangeException>(() => SpriteAnimationEditing2D.Duplicate(frames, 3));
    }

    [Fact]
    public void ClipNamesAndReplacementArraysDoNotChangeStableIdentity()
    {
        var texture = new TextureAsset(16, 16, TextureColorSpace.Srgb, "png");
        SpriteAnimationFrame2D[] frames = [new()
        {
            sprite = new()
            {
                texture = texture
            },
            duration = 0.1f
        }
        ];
        var asset = new SpriteAnimation2DAsset();
        asset.SetClips([new() {
            id = "stable-id",
            name = "Walk",
            frames = frames
        }]);
        frames[0].duration = -1;
        Assert.True(asset.TryGetClip("stable-id", out SpriteAnimationClip2D clip));
        Assert.Equal(0.1f, clip.frames[0].duration);
        clip.name = "Run";
        asset.SetClips([clip]);
        Assert.True(asset.TryGetClip("stable-id", out clip));
        Assert.Equal("Run", clip.name);
    }

    [Fact]
    public void AnimatorSubscriptionsUseCoreEventsAndValidateTransitions()
    {
        var animator = new SpriteAnimator2D();
        using IDisposable listener = animator.Listen(value => value.HandleInHub());
        Assert.Throws<ArgumentOutOfRangeException>(() => animator.CrossFade("clip", float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => animator.CrossFade("clip", float.PositiveInfinity));
        Assert.Throws<ArgumentNullException>(() => animator.Listen(null!));
    }

    private static SpriteAnimationFrame2D[] Frames() => [new()
    {
        duration = 0.25f,
        eventId = "a"
    }, new()
    {
        duration = 0.5f,
        eventId = "b"
    }, new()
    {
        duration = 0.25f,
        eventId = "c"
    }
    ];
}
