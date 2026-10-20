using System;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Scene;
using InnoEngine.Scene;
using InnoEngine.Settings;

namespace Inno.Rendering2D;

/// <summary>
/// Organizes 2D components around authoring tasks and records edits through the scene owner.
/// </summary>
/// <param name="edits">
/// Scene-owned atomic property and History coordinator.
/// </param>
[InspectionDrawer(typeof(SpriteRenderer2D))]
[InspectionDrawer(typeof(SpriteAnimator2D))]
[InspectionDrawer(typeof(TilemapRenderer2D))]
[InspectionDrawer(typeof(SortingGroup2D))]
[InspectionDrawer(typeof(Camera2D))]
[InspectionDrawer(typeof(Light2D))]
[InspectionDrawer(typeof(SpriteMask2D))]
[InspectionDrawer(typeof(ShadowCaster2D))]
[InspectionDrawer(typeof(ParticleSystem2D))]
public sealed class Rendering2DComponentInspectionDrawer(SceneEdits edits) : InspectionDrawer<GameBehavior>
{
    /// <inheritdoc />
    public override string icon => ImGuiIcon.File;

    /// <inheritdoc />
    protected override (string, Action<string>?) BindName(
        InspectionDrawContext context,
        GameBehavior target
    ) => (ImGuiWidget.NicifyName(target.GetType().Name), null);
    /// <inheritdoc />
    protected override void Draw(
        InspectionDrawContext context,
        GameBehavior target
    ) {
        if (target is SpriteRenderer2D sprite)
        {
            DrawProperties(context, ["sprite", "primitive", "drawMode", "size", "pivot", "pixelsPerUnit", "flipX", "flipY", "sampling", "material", "color", "blendMode"]);
            if (sprite.material is null || sprite.material.isMissing)
                ImGuiWidget.Hint("Assign a Sprite Material to render this object.");
            DrawSorting(context, target, sprite.sortingLayer, value => sprite.sortingLayer = value);
            DrawProperties(context, ["orderInLayer", "maskInteraction", "receiveLighting", "normalMap", "emissionMap", "emissionColor", "lightBlendStyles"]);
            if (ImGuiWidget.SectionHeader("Advanced", "Culling padding and pointer occlusion do not change the sprite artwork."))
                DrawProperties(context, ["boundsPadding", "pointerPassThrough"]);
            return;
        }

        if (target is SpriteAnimator2D animator)
        {
            DrawProperties(context, ["animation"]);
            if (animator.animation is SpriteAnimation2DAsset animation && !animation.isMissing)
            {
                ImGuiWidget.PropertyRow("animation-clip", "Clip", () =>
                {
                    string label = animation.TryGetClip(animator.clipId, out SpriteAnimationClip2D clip) ? ClipName(clip) : "Choose a clip";
                    if (!ImGuiWidget.BeginBoundedCombo("##clip", label))
                        return;
                    try
                    {
                        foreach (SpriteAnimationClip2D candidate in animation.clips)
                        {
                            if (ImGui.Selectable(ClipName(candidate) + "##" + candidate.id, candidate.id == animator.clipId))
                                edits.ChangeProperty(animator, nameof(animator.clipId), () => animator.clipId = candidate.id, "Choose Animation Clip");
                        }

                        if (animation.clips.Length == 0)
                            ImGuiWidget.Hint("The animation library is empty. Open it to create a clip.");
                    }
                    finally
                    {
                        ImGuiWidget.EndBoundedCombo();
                    }
                });
                if (ImGui.Button("Edit Animation"))
                    context.interactions.SetSelection(animation);
            }

            DrawProperties(context, ["playOnStart", "speed"]);
            if (!animator.gameObject.TryGetComponent<SpriteRenderer2D>(out _))
                ImGuiWidget.Hint("Add a Sprite Renderer to this GameObject to display its animation.");
            return;
        }

        if (target is SortingGroup2D group)
        {
            DrawSorting(context, target, group.sortingLayer, value => group.sortingLayer = value);
            DrawProperties(context, ["orderInLayer"]);
            return;
        }

        if (target is ParticleSystem2D particles)
        {
            DrawProperties(context, ["effect", "playOnStart", "seed"]);
            if (particles.effect is { isMissing: false } && ImGui.Button("Edit Particle Effect"))
                context.interactions.SetSelection(particles.effect);
            DrawSorting(context, target, particles.sortingLayer, value => particles.sortingLayer = value);
            DrawProperties(context, ["orderInLayer"]);
            return;
        }

        if (target is SpriteMask2D mask)
        {
            DrawProperties(context, ["sprite", "primitive", "material", "size", "pivot", "pixelsPerUnit", "flipX", "flipY", "sampling", "alphaCutoff"]);
            DrawSorting(context, target, mask.sortingLayer, value => mask.sortingLayer = value);
            DrawProperties(context, ["frontOrder", "backOrder"]);
            if (ImGuiWidget.SectionHeader("Advanced", "Padding expands culling bounds without changing mask coverage."))
                DrawProperties(context, ["boundsPadding"]);
            return;
        }

        if (target is TilemapRenderer2D tilemap)
        {
            DrawProperties(context, ["tilemap", "material", "color", "blendMode", "sampling"]);
            if (tilemap.tilemap is not null && ImGui.Button("Edit Tilemap"))
                context.interactions.SetSelection(tilemap.tilemap);
            DrawSorting(context, target, tilemap.sortingLayer, value => tilemap.sortingLayer = value);
            DrawProperties(context, ["orderInLayer", "receiveLighting", "normalMap", "emissionMap", "emissionColor", "lightBlendStyles"]);
            return;
        }

        context.DrawProperties();
    }

    internal static string ClipName(SpriteAnimationClip2D clip) => string.IsNullOrWhiteSpace(clip.name) ? "Unnamed clip" : clip.name;

    private static void DrawProperties(
        InspectionDrawContext context,
        string[] properties
    ) {
        foreach (string property in properties)
        {
            if (context.HasProperty(property))
                context.DrawProperty(property);
        }
    }

    private void DrawSorting(
        InspectionDrawContext context,
        GameBehavior target,
        string current,
        Action<string> assign
    ) {
        ImGui.SeparatorText("Sorting");
        SortingLayer2DDefinition[] layers = Settings.Get<Rendering2DProjectSettings>(Rendering2DProjectSettings.id).sortingLayers;
        string label = "Missing layer · " + current;
        foreach (SortingLayer2DDefinition layer in layers)
        {
            if (layer.localId == current)
                label = layer.name;
        }

        ImGuiWidget.PropertyRow("sorting-layer", "Sorting Layer", () =>
        {
            if (!ImGuiWidget.BeginBoundedCombo("##layer", label))
                return;
            try
            {
                foreach (SortingLayer2DDefinition layer in layers)
                {
                    if (ImGui.Selectable(layer.name + "##" + layer.localId, layer.localId == current))
                        edits.ChangeProperty(target, "sortingLayer", () => assign(layer.localId), "Change Sorting Layer");
                }

                ImGuiWidget.Hint("Manage layers in Project Settings → Rendering → 2D.");
            }
            finally
            {
                ImGuiWidget.EndBoundedCombo();
            }
        });
    }
}
