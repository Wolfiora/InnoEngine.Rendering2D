using System;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;

namespace Inno.Rendering2D;

/// <summary>
/// Draws selected Rendering2D source files through the same native asset drafts.
/// </summary>
/// <param name="icons">
/// Shared source-icon provider from the Inspector composition.
/// </param>
[InspectionDrawer(typeof(AssetFileEntry), priority: 100, conditional: true)]
public sealed class Rendering2DAssetSourceInspectionDrawer(IInspectionIconProvider<AssetFileEntry> icons) : InspectionDrawer<AssetFileEntry>
{
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;

    /// <inheritdoc />
    protected override bool CanInspect(AssetFileEntry target) => !target.isDirectory && Rendering2DAssetDocumentProvider.Supports(target.extension);
    /// <inheritdoc />
    protected override string GetIcon(
        InspectionDrawContext context,
        AssetFileEntry target
    ) => icons.GetIcon(target);
    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        AssetFileEntry target
    ) => (target.nameWithoutExtension, null);
    /// <inheritdoc />
    protected override void DrawHeader(
        InspectionDrawContext context,
        AssetFileEntry target
    ) {
        if (context.interactions.TryGetModule<Rendering2DAssetDocumentModule>(out var module) && module is not null)
        {
            module.DrawInspectorHeader(context, target);
        }
    }

    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        AssetFileEntry target
    ) {
        if (context.interactions.TryGetModule<Rendering2DAssetDocumentModule>(out var module) && module is not null)
        {
            module.DrawInspector(context, target);
        }
    }
}
