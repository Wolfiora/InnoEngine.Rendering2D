using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEngine.Assets;
using InnoEngine.Rendering;

namespace Inno.Rendering2D;

/// <summary>
/// Presents sprite sources by asset and region name while keeping storage identities private.
/// </summary>
[PropertyDrawer(typeof(SpriteReference2D), priority: 100)]
public sealed class SpriteReferencePropertyDrawer : IPropertyDrawer
{
    /// <inheritdoc />
    public void Draw(PropertyDrawContext context)
    {
        SpriteReference2D value = context.GetValue() is SpriteReference2D sprite ? sprite : default;
        bool atlasMode = value.atlas is not null || value.regionId.isValid;
        if (context.TryGetTextState("source-mode", out string? mode) && !value.isAssigned)
            atlasMode = mode == "atlas";
        if (ImGuiWidget.BeginBoundedCombo("##sprite-kind", atlasMode ? "Atlas region" : "Texture"))
        {
            try
            {
                if (ImGui.Selectable("Texture", !atlasMode))
                {
                    atlasMode = false;
                    context.SetTextState("source-mode", "texture");
                    if (value.atlas is not null || value.regionId.isValid)
                        context.SetValue(new SpriteReference2D());
                }

                if (ImGui.Selectable("Atlas region", atlasMode))
                {
                    atlasMode = true;
                    context.SetTextState("source-mode", "atlas");
                    if (value.texture is not null)
                        context.SetValue(new SpriteReference2D());
                }
            }
            finally
            {
                ImGuiWidget.EndBoundedCombo();
            }
        }

        if (atlasMode)
        {
            SpriteReference2D? pending = null;
            ImGui.SetNextItemWidth(MathF.Max(1, ImGui.GetContentRegionAvailable().X));
            context.DrawInlineChild("Atlas", typeof(SpriteAtlas2DAsset), () => Read().atlas, selected =>
            {
                SpriteAtlas2DAsset? atlas = selected as SpriteAtlas2DAsset;
                pending = new SpriteReference2D
                {
                    atlas = atlas
                };
            });
            if (pending is SpriteReference2D replacement)
                context.SetValue(replacement);
            value = Read();
            SpriteAtlas2DAsset? atlas = value.atlas;
            if (atlas is { isMissing: false })
            {
                string title = atlas.TryGetRegion(value.regionId, out SpriteRegion2D selected) ? RegionName(selected) : value.regionId.isValid ? "Missing region" : "Choose a sprite";
                if (ImGuiWidget.BeginBoundedCombo("##sprite-region", title))
                {
                    try
                    {
                        string search = context.TryGetTextState("region-search", out string? saved) ? saved ?? "" : "";
                        if (ImGui.InputTextWithHint("##search", "Search sprites...", ref search, 256))
                            context.SetTextState("region-search", search);
                        if (ImGui.Selectable("None", !value.regionId.isValid))
                            context.SetValue(new SpriteReference2D { atlas = atlas });
                        foreach (SpriteRegion2D region in atlas.regions)
                        {
                            if (!RegionName(region).Contains(search, StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (ImGui.Selectable(RegionName(region) + "##" + region.id.value, region.id == value.regionId))
                                context.SetValue(new SpriteReference2D { atlas = atlas, regionId = region.id });
                        }

                        if (atlas.regions.Length == 0)
                            ImGuiWidget.Hint("Open the Atlas, add slices, then Save to publish its sprites.");
                    }
                    finally
                    {
                        ImGuiWidget.EndBoundedCombo();
                    }
                }
            }
        }
        else
        {
            SpriteReference2D? pending = null;
            ImGui.SetNextItemWidth(MathF.Max(1, ImGui.GetContentRegionAvailable().X));
            context.DrawInlineChild("Texture", typeof(TextureAsset), () => Read().texture, selected => pending = new SpriteReference2D { texture = selected as TextureAsset });
            if (pending is SpriteReference2D replacement)
                context.SetValue(replacement);
        }

        value = Read();
        if ((value.isAssigned || value.atlas is not null || value.regionId.isValid) && ImGui.SmallButton("Clear Sprite"))
            context.SetValue(new SpriteReference2D());
        if (value.texture is { isMissing: false } source)
            ImGuiWidget.Hint(source.name);
        if (value.atlas is { isMissing: true } || value.texture is { isMissing: true })
            ImGuiWidget.Hint("Missing sprite source · its reference is preserved until you replace or clear it.");
        else if (value.atlas is not null && value.regionId.isValid && !value.atlas.TryGetRegion(value.regionId, out _))
            ImGuiWidget.Hint("Missing region · restore it in the Atlas, or choose a replacement.");
        else if (!value.isAssigned)
            ImGuiWidget.Hint("Choose a source. Sprite renderers can also use a procedural shape.");
        if (context.interactions.TryGetModule<Rendering2DSpritePreviewModule>(out var previews) && previews is not null)
            previews.Draw(value, context.path, 88f);
        SpriteReference2D Read() => context.GetValue() is SpriteReference2D current ? current : default;
    }

    private static string RegionName(SpriteRegion2D region) => string.IsNullOrWhiteSpace(region.name) ? "Unnamed sprite" : region.name;
}
