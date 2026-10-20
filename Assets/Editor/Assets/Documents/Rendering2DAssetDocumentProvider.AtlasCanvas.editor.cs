using System;
using System.Linq;
using System.Numerics;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Rendering;
using InnoEngine.Rendering;
using Rect = InnoEngine.Mathematics.Rect;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed partial class AtlasDraft
    {
        private int m_selectedSlice;
        private byte[]? m_dragBefore;
        private byte[]? m_editBaseline;
        private Rect m_dragRect;

        internal override bool isEditing => m_dragBefore is not null;

        internal override byte[]? TakeGestureBefore() => CompleteGesture();

        internal override byte[]? CompleteGesture()
        {
            byte[]? before = m_dragBefore;
            m_dragBefore = null;
            return before;
        }

        internal override void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            var draftEdits = (InspectorDraftEdits)edits;
            byte[] before = draftEdits.current;
            m_editBaseline = before;
            ImGui.BeginDisabled(readOnly);
            bool changed;
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
                ImGui.EndDisabled();
                m_editBaseline = null;
            }

            if (!readOnly && m_dragBefore is not null && !ImGui.IsMouseButtonDown())
            {
                byte[] gesture = m_dragBefore;
                m_dragBefore = null;
                draftEdits.RecordApplied(gesture, "Edit Atlas Slice");
            }
            else if (!readOnly && changed && m_dragBefore is null)
                draftEdits.RecordApplied(before, "Edit Sprite Atlas");
            if (m_sources.Length == 0)
                return;
            m_sourceIndex = Math.Clamp(m_sourceIndex, 0, m_sources.Length - 1);
            if (!ImGuiWidget.SectionHeader("Selected Source", "Choose a texture using the standard asset selector or drag it from the File Browser."))
                return;
            Property("name", "Name", () => m_sources[m_sourceIndex].name, value => m_sources[m_sourceIndex].name = value);
            Property<TextureAsset?>("texture", "Color Texture", () => m_sources[m_sourceIndex].texture, value => m_sources[m_sourceIndex].texture = value);
            Property<TextureAsset?>("normal", "Normal Map", () => m_sources[m_sourceIndex].normalMap, value => m_sources[m_sourceIndex].normalMap = value);
            Property<TextureAsset?>("emission", "Emission Map", () => m_sources[m_sourceIndex].emissionMap, value => m_sources[m_sourceIndex].emissionMap = value);
            void Property<T>(
                string path,
                string label,
                Func<T> getter,
                Action<T> setter
            ) => context.properties.DrawValue(
                context.editorContext,
                this,
                "atlas.source." + path,
                label,
                typeof(T),
                () => getter(),
                value => setter(value is T typed ? typed : default!),
                edits,
                readOnly
            );
        }

        private bool DrawSourceCanvas(
            IEditorPreviewService previews,
            byte[] before
        ) {
            if (!TryGetSelectedSource(out TextureAsset? texture) || texture is null || texture.isMissing)
                return false;
            ImGui.SeparatorText("Source Canvas");
            ImGuiWidget.Hint("Click a sprite to select it. Drag its body to move, or its lower-right corner to resize.");
            if (!previews.TryGetTexture(texture, out EditorPreviewHandle handle))
            {
                ImGuiWidget.Hint("Source preview preparing...");
                return false;
            }

            float scale = MathF.Min(MathF.Max(1f, ImGui.GetContentRegionAvailable().X) / Math.Max(1, texture.width), 320f * ImGuiWidget.style.zoom / Math.Max(1, texture.height));
            Vector2 origin = ImGui.GetCursorPosition();
            Vector2 screen = ImGui.GetCursorScreenPosition();
            previews.Draw(handle, new Vector2(Math.Max(1, texture.width) * scale, Math.Max(1, texture.height) * scale));
            Vector2 after = ImGui.GetCursorPosition();
            bool changed = false;
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.12f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.2f));
            try
            {
                for (int index = 0; index < m_slices.Count && index < 4096; index++)
                {
                    var slice = m_slices[index];
                    if (slice.sourceIndex != m_sourceIndex)
                        continue;
                    float x = Math.Clamp(slice.pixelRect.x, 0f, texture.width);
                    float y = Math.Clamp(slice.pixelRect.y, 0f, texture.height);
                    float right = Math.Clamp(slice.pixelRect.x + slice.pixelRect.width, x, texture.width);
                    float bottom = Math.Clamp(slice.pixelRect.y + slice.pixelRect.height, y, texture.height);
                    if (right <= x || bottom <= y)
                        continue;
                    Vector2 minimum = screen + new Vector2(x, y) * scale;
                    Vector2 maximum = screen + new Vector2(right, bottom) * scale;
                    ImGui.SetCursorPosition(origin + new Vector2(x, y) * scale);
                    Vector2 extent = maximum - minimum;
                    float grip = MathF.Min(10f * ImGuiWidget.style.zoom, MathF.Min(extent.X, extent.Y) / 3f);
                    _ = ImGui.Button("##slice:" + slice.id.value, new Vector2(extent.X, MathF.Max(1f, extent.Y - grip)));
                    if (ImGui.IsItemClicked())
                        m_selectedSlice = index;
                    if (ImGui.IsItemActive() && ImGui.IsMouseButtonDown() && (ImGui.GetMouseDragDelta().LengthSquared() > 0f))
                    {
                        if (m_dragBefore is null)
                        {
                            m_dragBefore = before;
                            m_dragRect = slice.pixelRect;
                        }
                        Vector2 delta = ImGui.GetMouseDragDelta();
                        float movedX = Math.Clamp(MathF.Round(m_dragRect.x + delta.X / scale), 0, MathF.Max(0, texture.width - slice.pixelRect.width));
                        float movedY = Math.Clamp(MathF.Round(m_dragRect.y + delta.Y / scale), 0, MathF.Max(0, texture.height - slice.pixelRect.height));
                        slice.pixelRect = new Rect(movedX, movedY, slice.pixelRect.width, slice.pixelRect.height);
                        m_slices[index] = slice;
                        changed = true;
                    }
                    ImGui.SetCursorPosition(origin + new Vector2(right, bottom) * scale - new Vector2(grip));
                    _ = ImGui.Button("##resize:" + slice.id.value, new Vector2(grip));
                    if (ImGui.IsItemClicked())
                        m_selectedSlice = index;
                    if (ImGui.IsItemActive() && ImGui.IsMouseButtonDown() && ImGui.GetMouseDragDelta().LengthSquared() > 0f)
                    {
                        if (m_dragBefore is null)
                        {
                            m_dragBefore = before;
                            m_dragRect = slice.pixelRect;
                        }
                        Vector2 delta = ImGui.GetMouseDragDelta();
                        float resizedWidth = Math.Clamp(MathF.Round(m_dragRect.width + delta.X / scale), 1f, texture.width - m_dragRect.x);
                        float resizedHeight = Math.Clamp(MathF.Round(m_dragRect.height + delta.Y / scale), 1f, texture.height - m_dragRect.y);
                        slice.pixelRect = new Rect(m_dragRect.x, m_dragRect.y, resizedWidth, resizedHeight);
                        m_slices[index] = slice;
                        changed = true;
                    }

                    ImGui.DrawRectangle(
                        minimum,
                        maximum,
                        ImGui.ToPackedColor(index == m_selectedSlice ? EditorPalette.warning : EditorPalette.textDisabled),
                        0,
                        index == m_selectedSlice ? 2f : 1f
                    );
                }
            }
            finally
            {
                ImGui.PopStyleColor(3);
                ImGui.SetCursorPosition(after);
            }

            return changed;
        }
    }
}
