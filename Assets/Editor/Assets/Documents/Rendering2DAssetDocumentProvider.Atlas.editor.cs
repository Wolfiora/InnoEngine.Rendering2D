using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Rendering;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed partial class AtlasDraft(SpriteAtlas2DAsset asset) : Draft
    {
        private readonly Guid m_assetId = asset.identity.persistentId;
        private SpriteAtlasPackingSettings2D m_packing = asset.packing;
        private SpriteAtlasSource2D[] m_sources = asset.sources.ToArray();
        private SpriteAtlasPage2D[] m_pages = asset.pages.ToArray();
        private SpriteRegion2D[] m_regions = asset.regions.ToArray();
        private readonly List<SpriteAtlasSlice2D> m_slices = asset.slices.Select(CloneSlice).ToList();
        private int m_sourceIndex;
        private int m_gridWidth = 32;
        private int m_gridHeight = 32;
        private int m_rectX;
        private int m_rectY;
        private int m_rectWidth = 32;
        private int m_rectHeight = 32;
        private float m_pivotX = 0.5f;
        private float m_pivotY = 0.5f;
        private string m_packingStatus = string.Empty;

        internal override byte[] Capture() => EditorAssets.EncodeNative(CreateAsset(normalize: false));

        internal override void Restore(byte[] bytes)
        {
            SpriteAtlas2DAsset restored = EditorAssets.DecodeNative<SpriteAtlas2DAsset>(bytes);
            m_packing = restored.packing;
            m_sources = restored.sources.ToArray();
            m_pages = restored.pages.ToArray();
            m_regions = restored.regions.ToArray();
            m_slices.Clear();
            m_slices.AddRange(restored.slices.Select(CloneSlice));
        }

        internal override bool Draw(IEditorPreviewService previews)
        {
            ImGui.SeparatorText("Packing");
            int maximumWidth = m_packing.maximumWidth;
            int maximumHeight = m_packing.maximumHeight;
            int padding = m_packing.padding;
            int extrude = m_packing.extrude;
            bool allowRotation = m_packing.allowRotation;
            bool trimTransparent = m_packing.trimTransparent;
            bool changed = Rendering2DEditorFields.Integer("Maximum Width", ref maximumWidth);
            changed |= Rendering2DEditorFields.Integer("Maximum Height", ref maximumHeight);
            changed |= Rendering2DEditorFields.Integer("Padding", ref padding);
            changed |= Rendering2DEditorFields.Integer("Extrude", ref extrude);
            changed |= Rendering2DEditorFields.Toggle("Allow Rotation", ref allowRotation);
            changed |= Rendering2DEditorFields.Toggle("Trim Transparent", ref trimTransparent);
            if (changed)
            {
                m_packing.maximumWidth = maximumWidth;
                m_packing.maximumHeight = maximumHeight;
                m_packing.padding = padding;
                m_packing.extrude = extrude;
                m_packing.allowRotation = allowRotation;
                m_packing.trimTransparent = trimTransparent;
            }

            ImGui.SeparatorText("Sources");
            if (ImGui.SmallButton("Add Texture Source"))
            {
                m_sources = [..m_sources, new SpriteAtlasSource2D
                {
                    name = "Source " + (m_sources.Length + 1)
                }
                ];
                m_sourceIndex = m_sources.Length - 1;
                changed = true;
            }

            ImGui.Text($"Sources: {m_sources.Length}   Slices: {m_slices.Count}   Pages: {m_pages.Length}   Regions: {m_regions.Length}");
            for (int sourceIndex = 0; sourceIndex < m_sources.Length; sourceIndex++)
            {
                SpriteAtlasSource2D source = m_sources[sourceIndex];
                string dimensions = source.texture is null ? "missing" : $"{source.texture.width} x {source.texture.height}";
                if (ImGui.Selectable($"Source {sourceIndex}: {source.name} ({dimensions})", m_sourceIndex == sourceIndex))
                    m_sourceIndex = sourceIndex;
            }
            if (m_sources.Length > 0)
            {
                m_sourceIndex = Math.Clamp(m_sourceIndex, 0, m_sources.Length - 1);
                int sliceCount = m_slices.Count(slice => slice.sourceIndex == m_sourceIndex);
                if (ImGui.SmallButton($"Remove Source ({sliceCount} sprites)"))
                {
                    int removed = m_sourceIndex;
                    m_sources = m_sources.Where((
                        _,
                        index
                    ) => index != removed).ToArray();
                    m_slices.RemoveAll(slice => slice.sourceIndex == removed);
                    for (int index = 0; index < m_slices.Count; index++)
                    {
                        SpriteAtlasSlice2D slice = m_slices[index];
                        if (slice.sourceIndex > removed)
                            slice.sourceIndex--;
                        m_slices[index] = slice;
                    }
                    m_sourceIndex = Math.Max(0, removed - 1);
                    changed = true;
                }
            }

            ImGui.SeparatorText("Slicing");
            _ = Rendering2DEditorFields.Integer("Grid Width", ref m_gridWidth);
            _ = Rendering2DEditorFields.Integer("Grid Height", ref m_gridHeight);
            if (ImGui.Button("Slice Grid"))
                changed |= SliceGrid();
            ImGui.SameLine();
            if (ImGui.Button("Whole Source"))
                changed |= AddWholeSource();
            _ = Rendering2DEditorFields.Integer("Rect X", ref m_rectX);
            _ = Rendering2DEditorFields.Integer("Rect Y", ref m_rectY);
            _ = Rendering2DEditorFields.Integer("Rect Width", ref m_rectWidth);
            _ = Rendering2DEditorFields.Integer("Rect Height", ref m_rectHeight);
            _ = ImGui.SliderFloat("Pivot X", ref m_pivotX, 0f, 1f);
            _ = ImGui.SliderFloat("Pivot Y", ref m_pivotY, 0f, 1f);
            if (ImGui.Button("Add Manual Slice"))
                changed |= AddManualSlice();
            ImGui.SameLine();
            if (ImGui.Button("Packing Preview"))
                BuildPackingPreview();
            if (!string.IsNullOrWhiteSpace(m_packingStatus))
                ImGui.Text(m_packingStatus);
            changed |= DrawSourceCanvas(previews, m_editBaseline ?? throw new InvalidOperationException("Atlas edits require the current document snapshot."));
            ImGui.SeparatorText("Regions");
            m_selectedSlice = Math.Clamp(m_selectedSlice, 0, Math.Max(0, m_slices.Count - 1));
            if (m_slices.Count > 0 && ImGuiWidget.BeginBoundedCombo("##region", m_slices[m_selectedSlice].name))
            {
                try
                {
                    for (int index = 0; index < m_slices.Count; index++)
                    {
                        if (ImGui.Selectable(m_slices[index].name + "##" + m_slices[index].id.value, index == m_selectedSlice))
                            m_selectedSlice = index;
                    }
                }
                finally
                {
                    ImGuiWidget.EndBoundedCombo();
                }
            }

            for (int sliceIndex = 0; sliceIndex < m_slices.Count; sliceIndex++)
            {
                if (sliceIndex != m_selectedSlice)
                    continue;
                SpriteAtlasSlice2D slice = m_slices[sliceIndex];
                ImGui.PushId(sliceIndex + 20_000);
                string name = slice.name ?? string.Empty;
                int x = (int)slice.pixelRect.x;
                int y = (int)slice.pixelRect.y;
                int width = (int)slice.pixelRect.width;
                int height = (int)slice.pixelRect.height;
                float pivotX = slice.pivot.x;
                float pivotY = slice.pivot.y;
                float borderLeft = slice.borderPixels.X;
                float borderBottom = slice.borderPixels.Y;
                float borderRight = slice.borderPixels.Z;
                float borderTop = slice.borderPixels.W;
                bool sliceChanged = Rendering2DEditorFields.Text("Name", ref name);
                sliceChanged |= Rendering2DEditorFields.Integer("X", ref x);
                sliceChanged |= Rendering2DEditorFields.Integer("Y", ref y);
                sliceChanged |= Rendering2DEditorFields.Integer("Width", ref width);
                sliceChanged |= Rendering2DEditorFields.Integer("Height", ref height);
                sliceChanged |= ImGui.SliderFloat("Pivot X", ref pivotX, 0f, 1f);
                sliceChanged |= ImGui.SliderFloat("Pivot Y", ref pivotY, 0f, 1f);
                sliceChanged |= Rendering2DEditorFields.Float("Border Left", ref borderLeft);
                sliceChanged |= Rendering2DEditorFields.Float("Border Bottom", ref borderBottom);
                sliceChanged |= Rendering2DEditorFields.Float("Border Right", ref borderRight);
                sliceChanged |= Rendering2DEditorFields.Float("Border Top", ref borderTop);
                if (sliceChanged)
                {
                    slice.name = name;
                    slice.pixelRect = new InnoEngine.Mathematics.Rect(x, y, width, height);
                    slice.pivot = new InnoEngine.Mathematics.Vector2(pivotX, pivotY);
                    slice.borderPixels = new System.Numerics.Vector4(MathF.Max(0f, borderLeft), MathF.Max(0f, borderBottom), MathF.Max(0f, borderRight), MathF.Max(0f, borderTop));
                    m_slices[sliceIndex] = slice;
                    changed = true;
                }

                ImGui.Text($"Outline: {slice.outline?.Length ?? 0} points");
                if (ImGui.Button("Rectangle Outline"))
                {
                    slice.outline = [InnoEngine.Mathematics.Vector2.ZERO, new InnoEngine.Mathematics.Vector2(1f, 0f), InnoEngine.Mathematics.Vector2.ONE, new InnoEngine.Mathematics.Vector2(0f, 1f)];
                    m_slices[sliceIndex] = slice;
                    changed = true;
                }

                ImGui.SameLine();
                if (ImGui.Button("Automatic Outline"))
                {
                    slice.outline = [];
                    m_slices[sliceIndex] = slice;
                    changed = true;
                }

                if (ImGui.Button("Remove Slice"))
                {
                    m_slices.RemoveAt(sliceIndex);
                    m_selectedSlice = Math.Max(0, m_selectedSlice - 1);
                    changed = true;
                }

                ImGui.PopId();
            }

            if (m_assetId != Guid.Empty && Assets.TryLoad(m_assetId, out SpriteAtlas2DAsset? published) && published is { isMissing: false } && published.pages.Length > 0)
            {
                SpriteAtlasPage2D page = published.pages[0];
                ImGui.Text($"Published page · {page.width} × {page.height}");
                if (previews.TryGetTextureArtifact(published.GetPageTexture(0), page.width, page.height, out EditorPreviewHandle handle))
                {
                    float scale = MathF.Min(MathF.Max(1, ImGui.GetContentRegionAvailable().X) / page.width, 320f * ImGuiWidget.style.zoom / page.height);
                    previews.Draw(handle, new System.Numerics.Vector2(page.width * scale, page.height * scale));
                }
            }

            return changed;
        }

        internal override bool Save(AssetPath path)
        {
            SpriteAtlas2DAsset value = CreateAsset(normalize: true);
            value.SetSlices(value.slices);
            return Rendering2DAssets.SaveAtlas(path, value);
        }

        private SpriteAtlas2DAsset CreateAsset(bool normalize)
        {
            SpriteAtlasPackingSettings2D packing = m_packing;
            if (normalize)
            {
                packing.maximumWidth = Math.Max(1, packing.maximumWidth);
                packing.maximumHeight = Math.Max(1, packing.maximumHeight);
                packing.padding = Math.Max(0, packing.padding);
                packing.extrude = Math.Max(0, packing.extrude);
            }

            return new SpriteAtlas2DAsset
            {
                packing = packing,
                sources = m_sources,
                slices = m_slices.Select(CloneSlice).ToArray(),
                pages = m_pages,
                regions = m_regions
            };
        }

        private bool SliceGrid()
        {
            if (!TryGetSelectedSource(out TextureAsset? texture) || texture is null)
                return false;
            SpriteAtlasSlice2D[] slices = SpriteAtlasSlicing2D.Grid(
                m_slices,
                m_sourceIndex,
                GetSourceName(),
                texture.width,
                texture.height,
                Math.Max(1, m_gridWidth),
                Math.Max(1, m_gridHeight),
                new InnoEngine.Mathematics.Vector2(m_pivotX, m_pivotY)
            );
            m_slices.Clear();
            m_slices.AddRange(slices);
            return true;
        }

        private bool AddWholeSource()
        {
            if (!TryGetSelectedSource(out TextureAsset? texture) || texture is null)
                return false;
            return AddSlice(CreateSlice(GetSourceName(), new InnoEngine.Mathematics.Rect(0f, 0f, texture.width, texture.height)));
        }

        private bool AddManualSlice()
        {
            if (!TryGetSelectedSource(out TextureAsset? texture) || texture is null)
                return false;
            int width = Math.Max(1, m_rectWidth);
            int height = Math.Max(1, m_rectHeight);
            int x = Math.Max(0, m_rectX);
            int y = Math.Max(0, m_rectY);
            if ((long)x + width > texture.width || (long)y + height > texture.height)
            {
                m_packingStatus = "Manual slice is outside the selected source.";
                return false;
            }

            return AddSlice(CreateSlice($"{GetSourceName()}_{x}_{y}", new InnoEngine.Mathematics.Rect(x, y, width, height)));
        }

        private bool AddSlice(SpriteAtlasSlice2D slice)
        {
            int existing = m_slices.FindIndex(candidate => candidate.sourceIndex == slice.sourceIndex && candidate.pixelRect == slice.pixelRect);
            if (existing >= 0)
            {
                m_selectedSlice = existing;
                m_packingStatus = "This rectangle already has a sprite. Its identity was preserved.";
                return false;
            }

            m_slices.Add(slice);
            m_selectedSlice = m_slices.Count - 1;
            return true;
        }

        private SpriteAtlasSlice2D CreateSlice(
            string name,
            InnoEngine.Mathematics.Rect rect
        ) => new()
        {
            id = SpriteRegionId.Create(),
            name = name,
            sourceIndex = m_sourceIndex,
            pixelRect = rect,
            pivot = new InnoEngine.Mathematics.Vector2(m_pivotX, m_pivotY),
            outline = []
        };
        private bool TryGetSelectedSource(out TextureAsset? texture)
        {
            if (m_sources.Length == 0)
            {
                texture = null;
                m_packingStatus = "Assign at least one source texture in the Inspector.";
                return false;
            }

            m_sourceIndex = Math.Clamp(m_sourceIndex, 0, m_sources.Length - 1);
            texture = m_sources[m_sourceIndex].texture;
            if (texture is null)
                m_packingStatus = "The selected source texture is missing.";
            return texture is not null;
        }

        private string GetSourceName()
        {
            string value = m_sources[m_sourceIndex].name ?? string.Empty;
            return string.IsNullOrWhiteSpace(value) ? $"Sprite {m_sourceIndex}" : value.Trim();
        }

        private void BuildPackingPreview()
        {
            try
            {
                SpriteAtlasPackResult2D result = SpriteAtlasMaxRectsPacker2D.Pack(
                    m_slices.Select(slice => new SpriteAtlasPackInput2D(slice.id.value, Math.Max(1, (int)slice.pixelRect.width), Math.Max(1, (int)slice.pixelRect.height))),
                    m_packing
                );
                m_packingStatus = $"{result.placements.Count} regions fit in {result.pages.Count} deterministic page(s).";
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                m_packingStatus = exception.Message;
            }
        }

        private static SpriteAtlasSlice2D CloneSlice(SpriteAtlasSlice2D source)
        {
            source.outline = source.outline?.ToArray() ?? [];
            return source;
        }
    }
}
