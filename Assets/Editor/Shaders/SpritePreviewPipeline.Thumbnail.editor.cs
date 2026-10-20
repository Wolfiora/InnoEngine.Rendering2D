using System;
using InnoEngine.Assets;
using InnoEngine.Mathematics;
using InnoEngine.Rendering;

namespace Inno.Rendering2D;

public sealed partial class SpritePreviewPipeline
{
    private ThumbnailGeometry? m_thumbnailGeometry;
    private byte[] m_thumbnailInstances = [];
    private long m_thumbnailRevision;

    internal static RenderDataChannelId thumbnailChannel => new(pipelineId + "/thumbnail");

    private bool TryPrepareThumbnail(
        RenderPipelineContext context,
        SpriteThumbnailRequest request,
        out RenderMaterialPass? material,
        out PersistentTextureHandle texture
    ) {
        material = null;
        texture = default;
        SpriteReference2D sprite = request.sprite;
        SpriteRegion2D region;
        if (sprite.atlas is SpriteAtlas2DAsset atlas)
        {
            if (!atlas.TryGetRegion(sprite.regionId, out region) || region.pageIndex < 0 || region.pageIndex >= atlas.pages.Length || !context.resourceService.TryResolveTextureArtifact(atlas.GetPageTexture(region.pageIndex), out texture))
                return false;
        }
        else if (sprite.texture is TextureAsset source && context.resourceService.TryResolveTexture(source, out texture))
        {
            region = new SpriteRegion2D
            {
                uvRect = new Rect(0, 0, 1, 1),
                sourceSizePixels = new Vector2(source.width, source.height),
                trimmedSizePixels = new Vector2(source.width, source.height)
            };
        }
        else
            return false;
        if (!context.resourceService.TryResolveGraphicsMaterial(
            Assets.Load<MaterialAsset>(Assets.LocalPath(Rendering2DIds.defaultSpriteMaterialPath)),
            Rendering2DIds.spriteContract,
            Rendering2DIds.alphaRole,
            m_vertices,
            null,
            out material
        ) || material is null)
            return false;
        float largest = MathF.Max(region.sourceSizePixels.x, region.sourceSizePixels.y);
        if (!(largest > 0f) || !float.IsFinite(largest))
            return false;
        float scale = 1.8f / largest;
        float left = -region.sourceSizePixels.x * scale * 0.5f + region.trimOffsetPixels.x * scale;
        float bottom = -region.sourceSizePixels.y * scale * 0.5f + region.trimOffsetPixels.y * scale;
        var geometry = new ThumbnailGeometry(
            left,
            bottom,
            left + region.trimmedSizePixels.x * scale,
            bottom + region.trimmedSizePixels.y * scale,
            MapUv(region, 0f, 0f),
            MapUv(region, 1f, 0f),
            MapUv(region, 0f, 1f)
        );
        if (m_thumbnailGeometry != geometry)
        {
            m_thumbnailGeometry = geometry;
            m_thumbnailRevision++;
            m_thumbnailInstances = Floats([geometry.left, geometry.bottom, geometry.right, geometry.bottom, geometry.left, geometry.top, geometry.bottomLeft.x, geometry.bottomLeft.y, geometry.bottomRight.x, geometry.bottomRight.y, geometry.topLeft.x, geometry.topLeft.y, 0, 0, 0, 0, 1, 1, 1, 1]);
        }

        return true;
    }

    private static Vector2 MapUv(
        SpriteRegion2D region,
        float x,
        float y
    ) => region.rotatedClockwise ? new Vector2(region.uvRect.x + y * region.uvRect.width, region.uvRect.y + x * region.uvRect.height) : new Vector2(region.uvRect.x + x * region.uvRect.width, region.uvRect.y + (1f - y) * region.uvRect.height);
    private readonly record struct ThumbnailGeometry(
        float left,
        float bottom,
        float right,
        float top,
        Vector2 bottomLeft,
        Vector2 bottomRight,
        Vector2 topLeft
    );
}
