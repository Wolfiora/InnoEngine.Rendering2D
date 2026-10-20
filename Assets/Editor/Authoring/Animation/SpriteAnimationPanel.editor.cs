using System;
using System.Numerics;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Interactions;

namespace Inno.Rendering2D;

/// <summary>
/// Offers a dockable animation workspace over the same document and History used by the Inspector.
/// </summary>
/// <param name="interactions">
/// Session-owned selection, document and module coordinator.
/// </param>
[EditorPanel("rendering2d.animation", "Sprite Animation", order: 310, defaultOpen: false, menuPath: "Authoring")]
public sealed class SpriteAnimationPanel(EditorInteractions interactions) : EditorPanel
{
    /// <inheritdoc />
    public override Vector2 initialSize => new(880, 560);

    /// <inheritdoc />
    public override bool allowScrolling => false;

    /// <inheritdoc />
    protected override void OnDraw(EditorContext context)
    {
        SpriteAnimation2DAsset? animation = interactions.selection.selectedTarget switch
        {
            SpriteAnimation2DAsset asset => asset,
            SpriteAnimator2D animator => animator.animation,
            _ => null
        };
        string? path = animation?.assetPath.ToString();
        if (path is null && interactions.selection.selectedTarget is AssetFileEntry entry && string.Equals(entry.extension, ".ispriteanimation2d", StringComparison.OrdinalIgnoreCase))
            path = entry.assetPath.ToString();
        if (path is null || animation is { isMissing: true })
        {
            ImGuiWidget.Hint("Select a Sprite Animation asset or a Sprite Animator. Create one from the File Browser → Create → Rendering 2D → Sprites.");
            return;
        }

        if (!interactions.TryGetModule<Rendering2DAssetDocumentModule>(out var module) || module is null)
            return;
        bool visible = ImGui.BeginChild("animation-workspace");
        try
        {
            if (visible)
                ImGuiWidget.SectionLayout(() => module.DrawAnimationWorkspace(path, animation?.identity.persistentId ?? Guid.Empty));
        }
        finally
        {
            ImGui.EndChild();
        }
    }
}
