using System;
using System.Linq;
using System.Numerics;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Core;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed class AnimationDraft(SpriteAnimation2DAsset asset) : Draft
    {
        private SpriteAnimationClip2D[] m_clips = CloneClips(asset.clips);
        private int m_selectedClip;
        private int m_selectedFrame;
        private int m_firstFrame;
        private float m_playhead;
        private float m_framesPerSecond = 12f;
        private float m_zoom = 1f;
        private bool m_playing;
        private bool m_adjacentFrames;
        private bool m_readOnly;
        private string m_newName = "Idle";
        private byte[]? m_resizeBefore;
        private byte[]? m_editBaseline;
        private long m_lastPreviewFrame = -1;
        private float m_resizeDuration;
        private float m_resizePixelsPerSecond;

        internal override bool isEditing => m_resizeBefore is not null;

        internal override byte[]? TakeGestureBefore() => CompleteGesture();

        internal override byte[]? CompleteGesture()
        {
            byte[]? before = m_resizeBefore;
            m_resizeBefore = null;
            return before;
        }

        internal override byte[] Capture() => EditorAssets.EncodeNative(new SpriteAnimation2DAsset { clips = CloneClips(m_clips) });

        internal override void Restore(byte[] bytes)
        {
            m_clips = CloneClips(EditorAssets.DecodeNative<SpriteAnimation2DAsset>(bytes).clips);
            m_playing = false;
            m_resizeBefore = null;
        }

        internal override void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            DrawWorkspace(previews, (InspectorDraftEdits)edits, readOnly);
            if (!TryFrames(out SpriteAnimationFrame2D[] frames))
                return;
            m_selectedFrame = Math.Clamp(m_selectedFrame, 0, frames.Length - 1);
            if (ImGuiWidget.SectionHeader("Frame Sprite", "Choose its artwork. Preview playback does not dispatch gameplay events."))
            {
                context.properties.DrawValue(
                    context.editorContext,
                    this,
                    "animation.frame.sprite",
                    "Sprite",
                    typeof(SpriteReference2D),
                    () => SelectedFrame().sprite,
                    value => ReplaceFrame(SelectedFrame() with { sprite = value is SpriteReference2D sprite ? sprite : default }),
                    edits,
                    readOnly
                );
            }

            context.interactions.TryGetModule<Rendering2DSpritePreviewModule>(out var thumbnails);
            DrawPreview(thumbnails);
        }

        internal void DrawWorkspace(
            IEditorPreviewService previews,
            InspectorDraftEdits edits,
            bool readOnly
        ) {
            byte[] before = ((InspectorDraftEdits)edits).current;
            bool changed;
            m_readOnly = readOnly;
            m_editBaseline = before;
            try
            {
                changed = Draw(previews);
            }
            catch
            {
                Restore(before);
                throw;
            }
            finally
            {
                m_readOnly = false;
                m_editBaseline = null;
            }

            var draftEdits = (InspectorDraftEdits)edits;
            if (!readOnly && m_resizeBefore is not null && !ImGui.IsMouseButtonDown())
            {
                byte[] gesture = m_resizeBefore;
                m_resizeBefore = null;
                draftEdits.RecordApplied(gesture, "Resize Animation Frame");
            }
            else if (!readOnly && changed && m_resizeBefore is null)
                draftEdits.RecordApplied(before, "Edit Animation");
        }

        internal void DrawPreview(Rendering2DSpritePreviewModule? thumbnails)
        {
            if (thumbnails is null || !TryFrames(out SpriteAnimationFrame2D[] frames))
                return;
            int active = SpriteAnimationEditing2D.FrameAt(frames, m_playhead);
            if (m_adjacentFrames && active > 0)
                thumbnails.Draw(frames[active - 1].sprite, "animation/previous", 72f);
            thumbnails.Draw(frames[active].sprite, "animation/current", 150f);
            if (m_adjacentFrames && active + 1 < frames.Length)
                thumbnails.Draw(frames[active + 1].sprite, "animation/next", 72f);
        }

        internal override bool Draw(IEditorPreviewService previews)
        {
            bool changed = false;
            if (ImGuiWidget.SectionHeader("Clips", "Names are editable; the playback identity stays stable."))
            {
                string title = m_clips.Length == 0 ? "No clips" : Rendering2DComponentInspectionDrawer.ClipName(m_clips[Math.Clamp(m_selectedClip, 0, m_clips.Length - 1)]);
                if (ImGuiWidget.BeginBoundedCombo("##clips", title))
                {
                    try
                    {
                        for (int index = 0; index < m_clips.Length; index++)
                        {
                            if (!ImGui.Selectable(Rendering2DComponentInspectionDrawer.ClipName(m_clips[index]) + "##" + index, index == m_selectedClip))
                                continue;
                            m_selectedClip = index;
                            m_selectedFrame = 0;
                            m_playhead = 0;
                            m_firstFrame = 0;
                        }
                    }
                    finally
                    {
                        ImGuiWidget.EndBoundedCombo();
                    }
                }

                _ = Rendering2DEditorFields.Text("New Clip", ref m_newName);
                if (EditButton("Add Clip"))
                {
                    m_clips = [..m_clips, new SpriteAnimationClip2D
                    {
                        id = Guid.NewGuid().ToString("N"),
                        name = string.IsNullOrWhiteSpace(m_newName) ? "New Clip" : m_newName.Trim(),
                        loop = true,
                        frames = []
                    }
                    ];
                    m_selectedClip = m_clips.Length - 1;
                    m_selectedFrame = 0;
                    m_playhead = 0;
                    changed = true;
                }
            }

            if (m_clips.Length == 0)
            {
                ImGuiWidget.Hint("Add a clip, add frames, choose their sprites, then Save.");
                return changed;
            }

            m_selectedClip = Math.Clamp(m_selectedClip, 0, m_clips.Length - 1);
            SpriteAnimationClip2D clip = m_clips[m_selectedClip];
            if (ImGuiWidget.SectionHeader("Selected Clip", "Changing the name preserves the clip identity used by Animator components."))
            {
                string name = clip.name ?? "";
                bool loop = clip.loop;
                ImGui.BeginDisabled(m_readOnly);
                try
                {
                    changed |= Rendering2DEditorFields.Text("Name", ref name);
                    changed |= Rendering2DEditorFields.Toggle("Loop", ref loop);
                }
                finally
                {
                    ImGui.EndDisabled();
                }
                clip.name = name;
                clip.loop = loop;
                m_clips[m_selectedClip] = clip;
                if (EditButton("Delete Clip"))
                {
                    m_clips = m_clips.Where((
                        _,
                        index
                    ) => index != m_selectedClip).ToArray();
                    m_selectedClip = Math.Max(0, m_selectedClip - 1);
                    m_playing = false;
                    return true;
                }
            }

            SpriteAnimationFrame2D[] frames = clip.frames ?? [];
            if (ImGuiWidget.SectionHeader("Timeline", "Click to select. Drag a frame horizontally to resize its duration."))
            {
                if (ImGui.Button(m_playing ? "Pause" : "Play"))
                    m_playing = !m_playing;
                Rendering2DEditorFields.ContinueActions("Stop");
                if (ImGui.Button("Stop"))
                {
                    m_playing = false;
                    m_playhead = 0;
                }

                Rendering2DEditorFields.ContinueActions("Previous");
                if (ImGui.SmallButton("Previous") && frames.Length > 0)
                    Step(frames, -1);
                Rendering2DEditorFields.ContinueActions("Next");
                if (ImGui.SmallButton("Next") && frames.Length > 0)
                    Step(frames, 1);
                _ = Rendering2DEditorFields.Float("New Frame FPS", ref m_framesPerSecond, 1f, 240f);
                _ = Rendering2DEditorFields.Float("Zoom", ref m_zoom, 0.5f, 8f);
                _ = Rendering2DEditorFields.Toggle("Adjacent Frames", ref m_adjacentFrames);
                float duration = SpriteAnimationEditing2D.Duration(frames);
                if (m_playing && duration > 0f && m_lastPreviewFrame != Time.frameCount)
                {
                    m_lastPreviewFrame = Time.frameCount;
                    m_playhead += MathF.Max(0f, Time.unscaledDeltaTime);
                    if (m_playhead >= duration)
                    {
                        m_playhead = clip.loop ? m_playhead % duration : duration;
                        m_playing &= clip.loop;
                    }
                }

                if (duration > 0f)
                    _ = ImGui.SliderFloat("##playhead", ref m_playhead, 0f, duration);
                ImGuiWidget.Hint($"{m_playhead:0.000} / {duration:0.000} s · {frames.Length} frames");
                changed |= DrawTimeline(frames);
            }

            if (EditButton("Add Frame"))
            {
                SpriteAnimationFrame2D frame = frames.Length == 0 ? new() : frames[Math.Clamp(m_selectedFrame, 0, frames.Length - 1)];
                frame.duration = 1f / m_framesPerSecond;
                frame.eventId = "";
                clip.frames = [.. frames, frame];
                m_clips[m_selectedClip] = clip;
                m_selectedFrame = clip.frames.Length - 1;
                return true;
            }

            if (frames.Length == 0)
            {
                ImGuiWidget.Hint("Add a frame to begin. Assign its sprite before saving.");
                return changed;
            }

            m_selectedFrame = Math.Clamp(m_selectedFrame, 0, frames.Length - 1);
            Rendering2DEditorFields.ContinueActions("Duplicate");
            if (EditButton("Duplicate"))
            {
                clip.frames = SpriteAnimationEditing2D.Duplicate(frames, m_selectedFrame);
                m_selectedFrame++;
                changed = true;
            }

            Rendering2DEditorFields.ContinueActions("Remove");
            if (EditButton("Remove"))
            {
                clip.frames = frames.Where((
                    _,
                    index
                ) => index != m_selectedFrame).ToArray();
                m_selectedFrame = Math.Max(0, m_selectedFrame - 1);
                changed = true;
            }

            if (EditButton("Move Left") && m_selectedFrame > 0)
            {
                clip.frames = SpriteAnimationEditing2D.Move(frames, m_selectedFrame, m_selectedFrame - 1);
                m_selectedFrame--;
                changed = true;
            }

            Rendering2DEditorFields.ContinueActions("Move Right");
            if (EditButton("Move Right") && m_selectedFrame + 1 < frames.Length)
            {
                clip.frames = SpriteAnimationEditing2D.Move(frames, m_selectedFrame, m_selectedFrame + 1);
                m_selectedFrame++;
                changed = true;
            }

            m_clips[m_selectedClip] = clip;
            if (clip.frames is { Length: > 0 })
            {
                m_selectedFrame = Math.Clamp(m_selectedFrame, 0, clip.frames.Length - 1);
                SpriteAnimationFrame2D frame = SelectedFrame();
                float duration = frame.duration;
                string eventId = frame.eventId ?? "";
                bool edited;
                ImGui.BeginDisabled(m_readOnly);
                try
                {
                    edited = Rendering2DEditorFields.Float("Duration (s)", ref duration, 1f / 240f, 3600f);
                    edited |= Rendering2DEditorFields.Text("Event", ref eventId);
                }
                finally
                {
                    ImGui.EndDisabled();
                }
                if (edited)
                {
                    frame.duration = duration;
                    frame.eventId = eventId;
                    ReplaceFrame(frame);
                    changed = true;
                }
            }

            return changed;
        }

        internal override bool Save(AssetPath path)
        {
            var value = new SpriteAnimation2DAsset();
            value.SetClips(CloneClips(m_clips));
            return Rendering2DAssets.SaveAnimation(path, value);
        }

        private bool DrawTimeline(SpriteAnimationFrame2D[] frames)
        {
            if (frames.Length == 0)
                return false;
            m_firstFrame = Math.Clamp(m_firstFrame, 0, frames.Length - 1);
            _ = Rendering2DEditorFields.Integer("First Frame", ref m_firstFrame, 0, frames.Length - 1);
            float width = MathF.Max(1f, ImGui.GetContentRegionAvailable().X);
            float pixelsPerSecond = Math.Clamp(width / MathF.Max(SpriteAnimationEditing2D.Duration(frames), 0.01f) * m_zoom, 24f, 1000f);
            float used = 0f;
            float start = frames.Take(m_firstFrame).Sum(frame => frame.duration);
            bool changed = false;
            for (int index = m_firstFrame; index < frames.Length && index < m_firstFrame + 128; index++)
            {
                float itemWidth = MathF.Max(28f, frames[index].duration * pixelsPerSecond);
                if (used + itemWidth > width && used > 0f)
                    break;
                if (used > 0f)
                    ImGui.SameLine(0, 2);
                ImGui.PushId(index);
                if (ImGui.Button($"{index + 1}" + (string.IsNullOrEmpty(frames[index].eventId) ? "" : " *"), new Vector2(MathF.Min(itemWidth, width), 42f * ImGuiWidget.style.zoom)))
                {
                    m_selectedFrame = index;
                    m_playhead = start;
                }

                Vector2 minimum = ImGui.GetItemMinimum();
                Vector2 maximum = ImGui.GetItemMaximum();
                if (m_selectedFrame == index)
                    ImGui.DrawRectangle(minimum, maximum, ImGui.ToPackedColor(EditorPalette.text), 3f, 2f);
                if (m_playhead >= start && m_playhead < start + frames[index].duration)
                {
                    float x = minimum.X + (m_playhead - start) / frames[index].duration * (maximum.X - minimum.X);
                    ImGui.DrawLine(new Vector2(x, minimum.Y), new Vector2(x, maximum.Y), ImGui.ToPackedColor(EditorPalette.warning), 2f);
                }

                if (!m_readOnly && ImGui.IsItemActive() && ImGui.IsMouseButtonDown() && MathF.Abs(ImGui.GetMouseDragDelta().X) > 0f)
                {
                    if (m_resizeBefore is null)
                    {
                        m_resizeBefore = m_editBaseline ?? throw new InvalidOperationException("Animation edits require the current document snapshot.");
                        m_resizeDuration = frames[index].duration;
                        m_resizePixelsPerSecond = pixelsPerSecond;
                    }
                    frames[index].duration = Math.Clamp(m_resizeDuration + ImGui.GetMouseDragDelta().X / m_resizePixelsPerSecond, 1f / 240f, 3600f);
                    changed = true;
                }

                ImGui.PopId();
                start += frames[index].duration;
                used += itemWidth + 2;
            }

            return changed;
        }

        private bool EditButton(string label)
        {
            ImGui.BeginDisabled(m_readOnly);
            try
            {
                return ImGui.SmallButton(label);
            }
            finally
            {
                ImGui.EndDisabled();
            }
        }

        private bool TryFrames(out SpriteAnimationFrame2D[] frames)
        {
            frames = m_clips.Length > 0 ? m_clips[Math.Clamp(m_selectedClip, 0, m_clips.Length - 1)].frames ?? [] : [];
            return frames.Length > 0;
        }

        private SpriteAnimationFrame2D SelectedFrame() => m_clips[m_selectedClip].frames[m_selectedFrame];
        private void ReplaceFrame(SpriteAnimationFrame2D frame) => m_clips[m_selectedClip].frames[m_selectedFrame] = frame;

        private void Step(
            SpriteAnimationFrame2D[] frames,
            int direction
        ) {
            m_playing = false;
            m_selectedFrame = Math.Clamp(SpriteAnimationEditing2D.FrameAt(frames, m_playhead) + direction, 0, frames.Length - 1);
            m_playhead = frames.Take(m_selectedFrame).Sum(frame => frame.duration);
        }

        private static SpriteAnimationClip2D[] CloneClips(SpriteAnimationClip2D[] source) => source.Select(clip => clip with { frames = clip.frames?.ToArray() ?? [] }).ToArray();
    }
}
