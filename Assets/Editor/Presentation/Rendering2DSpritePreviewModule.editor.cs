using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Rendering;
using InnoEngine.Rendering;

namespace Inno.Rendering2D;

/// <summary>
/// Owns bounded, generation-scoped sprite previews for visible 2D authoring controls.
/// </summary>
/// <param name="previews">
/// Session-owned offscreen rendering and texture presentation service.
/// </param>
[EditorModule("rendering2d.sprite-previews", order: 390)]
public sealed class Rendering2DSpritePreviewModule(IEditorPreviewService previews) : EditorModule
{
    private readonly Dictionary<string, PreviewState> m_states = new(StringComparer.Ordinal);
    private long m_frame;

    internal bool Draw(
        SpriteReference2D sprite,
        string owner,
        float size,
        bool showStatus = true
    ) {
        if (!sprite.isAssigned || sprite.texture is { isMissing: true } || sprite.atlas is { isMissing: true })
            return false;
        if (!m_states.TryGetValue(owner, out PreviewState? state))
        {
            if (m_states.Count >= 128)
                return false;
            m_states.Add(owner, state = new("rendering2d/sprite/" + owner));
        }

        state.frame = m_frame;
        size = MathF.Max(1f, MathF.Min(size * ImGuiWidget.style.zoom, ImGui.GetContentRegionAvailable().X));
        if (state.submittedFrame != m_frame)
        {
            var data = new RenderFrameData();
            data.Set(SpritePreviewPipeline.thumbnailChannel, new SpriteThumbnailRequest(sprite));
            int pixels = Math.Max(1, (int)MathF.Ceiling(size));
            var composition = new EditorViewportComposition(state.id, pixels, pixels, RenderTextureFormat.RGBA8, [new EditorViewportLayer(state.id, state.pipeline, data, 0)]);
            state.ready = previews.TryRender(composition, out state.handle);
            state.submittedFrame = m_frame;
        }

        if (state.ready)
            previews.Draw(state.handle, new Vector2(size));
        else if (showStatus)
            ImGuiWidget.Hint("Sprite preview preparing...");
        return state.ready;
    }

    /// <inheritdoc />
    protected override void OnUpdate(EditorContext context)
    {
        m_frame++;
        foreach (string owner in m_states.Where(pair => pair.Value.frame < m_frame - 2).Select(pair => pair.Key).ToArray())
            Release(owner);
    }

    /// <inheritdoc />
    protected override void OnStop(EditorContext context) => ReleaseAll();
    /// <inheritdoc />
    protected override void OnDispose() => ReleaseAll();
    private void ReleaseAll()
    {
        foreach (string owner in m_states.Keys.ToArray())
            Release(owner);
    }

    private void Release(string owner)
    {
        PreviewState state = m_states[owner];
        previews.ReleaseRendered(state.id);
        m_states.Remove(owner);
    }

    private sealed class PreviewState(string id)
    {
        internal readonly string id = id;
        internal readonly RenderPipelineAsset pipeline = new()
        {
            pipelineTypeId = SpritePreviewPipeline.pipelineId
        };
        internal long frame;
        internal long submittedFrame = -1;

        internal bool ready;

        internal EditorPreviewHandle handle;
    }
}

internal sealed record SpriteThumbnailRequest(SpriteReference2D sprite);
