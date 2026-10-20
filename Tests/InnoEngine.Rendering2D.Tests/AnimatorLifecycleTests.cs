using System;
using System.Collections.Generic;
using Inno.Adapter.Modules.DotNet;
using Inno.Adapter.Serialization.DotNet;
using Inno.Assets;
using Inno.Core.Identity;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Inno.Scene;
using Xunit;

namespace Inno.Rendering2D.Tests;

public sealed class AnimatorLifecycleTests
{
    [Fact]
    public void NamedFramesUseOrderedDisposableSubscriptionsAndReleaseAtDestroy()
    {
        using var modules = new ModuleHost(new ModuleHostOptions
        {
            catalogSource = new DotNetAssemblyCatalogSource(typeof(AnimatorLifecycleTests).Assembly)
        });
        modules.Register("Rendering2D.Tests", [typeof(SpriteAnimator2D).Assembly]);
        using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
        types.Rebuild();
        using var world = new SceneWorld(new IdentityAllocator(), types);
        using IDisposable scope = world.EnterScope();
        using IDisposable assets = AssetExecutionContext.EnterScope(new SpriteAssets());
        GameScene scene = SceneManager.LoadNewScene("Animation");
        GameObject owner = scene.CreateObject("Sprite");
        SpriteRenderer2D renderer = owner.AddComponent<SpriteRenderer2D>();
        SpriteAnimator2D animator = owner.AddComponent<SpriteAnimator2D>();
        var texture = new TextureAsset(16, 16, TextureColorSpace.Srgb, "png");
        animator.animation = new SpriteAnimation2DAsset();
        animator.animation.SetClips([new()
        {
            id = "walk", name = "Walk", frames = [new()
            {
                sprite = new() { texture = texture }, duration = 0.1f, eventId = "footstep"
            }]
        }]);
        var calls = new List<string>();
        using IDisposable low = animator.Listen(value => calls.Add("low:" + value.eventId));
        IDisposable high = animator.Listen(value => calls.Add("high:" + value.clipId), 10);
        animator.Play("walk");
        Assert.Equal(["high:walk", "low:footstep"], calls);
        Assert.Same(texture, renderer.sprite.texture);
        high.Dispose();
        calls.Clear();
        animator.Stop();
        Assert.Equal(["low:footstep"], calls);
        SpriteAnimationClip2D walk = animator.animation.clips[0];
        animator.animation.SetClips([walk, new()
        {
            id = "idle", name = "Idle", frames = [new() { sprite = new() { texture = texture }, duration = 1f }]
        }]);
        using IDisposable redirect = animator.Listen(_ => animator.Play("idle"), 20);
        animator.CrossFade("walk", 0.5f);
        Assert.Equal("idle", animator.clipId);
        Assert.Equal(0f, renderer.crossFadeWeight);
        Assert.True(scene.DestroyObject(owner));
        Assert.Throws<ObjectDisposedException>(() => animator.Listen(_ => { }));
        low.Dispose();
    }

    private sealed class SpriteAssets : IAssetLookup
    {
        private readonly MaterialAsset m_material = new();

        public TAsset Load<TAsset>(AssetPath path) where TAsset : AssetObject
            => typeof(TAsset) == typeof(MaterialAsset) && path.localPath == Rendering2DIds.defaultSpriteMaterialPath
                ? (TAsset)(AssetObject)m_material
                : throw new InvalidOperationException("The test did not provide this asset: " + path);

        public TAsset Load<TAsset>(Guid persistentId) where TAsset : AssetObject
            => throw new InvalidOperationException("The test did not provide this identity: " + persistentId);

        public bool TryLoad<TAsset>(
            AssetPath path,
            out TAsset? asset
        ) where TAsset : AssetObject
        {
            asset = typeof(TAsset) == typeof(MaterialAsset) ? m_material as TAsset : null;
            return asset is not null;
        }

        public bool TryLoad<TAsset>(
            Guid persistentId,
            out TAsset? asset
        ) where TAsset : AssetObject
        {
            asset = null;
            return false;
        }
    }
}
