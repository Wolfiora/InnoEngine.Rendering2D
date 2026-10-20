using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEngine.Assets;

namespace Inno.Rendering2D;

/// <summary>
/// Draws native Rendering2D authoring drafts through the shared Inspector.
/// </summary>
[InspectionDrawer(typeof(SpriteAtlas2DAsset))]
[InspectionDrawer(typeof(SpriteAnimation2DAsset))]
[InspectionDrawer(typeof(TileSet2DAsset))]
[InspectionDrawer(typeof(Tilemap2DAsset))]
[InspectionDrawer(typeof(PostProcessProfile2DAsset))]
[InspectionDrawer(typeof(ParticleEffect2DAsset))]
public sealed class Rendering2DAssetInspectionDrawer : InspectionDrawer<AssetObject>
{
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;

    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        AssetObject target
    ) => (target.name, null);
    /// <inheritdoc />
    protected override void DrawHeader(
        InspectionDrawContext context,
        AssetObject target
    ) {
        if (context.interactions.TryGetModule<Rendering2DAssetDocumentModule>(out var module) && module is not null)
        {
            module.DrawInspectorHeader(context, target);
        }
    }

    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        AssetObject target
    ) {
        if (context.interactions.TryGetModule<Rendering2DAssetDocumentModule>(out var module) && module is not null)
        {
            module.DrawInspector(context, target);
        }
    }
}
