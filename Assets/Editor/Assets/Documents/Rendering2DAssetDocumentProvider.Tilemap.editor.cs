using System;
using System.Collections.Generic;
using System.Linq;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using CellSize = InnoEngine.Mathematics.Vector2;

using InnoEngine.Mathematics;
using TilemapCanvasColor = System.Numerics.Vector4;
using TilemapCanvasPoint = System.Numerics.Vector2;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed class TilemapDraft(Tilemap2DAsset asset) : Draft
    {
        private readonly Tilemap2DAsset m_asset = CopyContent(asset);
        private int m_layerId = asset.layers.Length > 0 ? asset.layers[0].id : 0;
        private int m_tileId;
        private int m_originX;
        private int m_originY;
        private float m_zoom = 1f;
        private PaintTool m_tool;
        private bool m_flipX;
        private bool m_flipY;
        private int m_turns;
        private InnoEngine.Mathematics.Color m_tint = InnoEngine.Mathematics.Color.WHITE;
        private byte[]? m_gestureBefore;
        private TilemapPosition2D m_start;
        private TilemapPosition2D m_end;
        private TilemapPosition2D? m_lastCell;
        private TilemapSelection2D? m_selection;
        private TilemapStampCell2D[] m_stamp = [];
        private string m_newLayer = "Layer";
        private string m_tileSearch = "";
        private TilemapSelection2D m_visibleBounds;

        internal override bool isEditing => m_gestureBefore is not null;

        internal override byte[]? TakeGestureBefore()
        {
            byte[]? before = m_gestureBefore;
            m_gestureBefore = null;
            m_lastCell = null;
            return before;
        }

        internal override byte[]? CompleteGesture()
        {
            byte[]? before = m_gestureBefore;
            if (before is null)
                return null;
            try
            {
                Finish(m_visibleBounds);
                m_gestureBefore = null;
                m_lastCell = null;
                return before;
            }
            catch
            {
                Restore(before);
                throw;
            }
        }

        internal override byte[] Capture() => EditorAssets.EncodeNative(m_asset);

        internal override void Restore(byte[] bytes)
        {
            Tilemap2DAsset restored = EditorAssets.DecodeNative<Tilemap2DAsset>(bytes);
            m_asset.tileSet = restored.tileSet;
            m_asset.cellSize = restored.cellSize;
            m_asset.chunkSize = restored.chunkSize;
            m_asset.layers = restored.layers;
            m_asset.chunks = restored.chunks;
            m_gestureBefore = null;
            m_lastCell = null;
        }

        internal override void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            if (m_gestureBefore is not null && !ImGui.IsMouseButtonDown() && CompleteGesture() is byte[] completedGesture)
                ((InspectorDraftEdits)edits).RecordApplied(completedGesture, "Paint Tilemap");
            base.DrawInspector(context, previews, edits, readOnly);
            context.properties.DrawValue(
                context.editorContext,
                this,
                "tilemap.tileset",
                "Tile Set",
                typeof(TileSet2DAsset),
                () => m_asset.tileSet,
                value => m_asset.tileSet = value as TileSet2DAsset,
                edits,
                readOnly
            );
            if (m_asset.tileSet is not TileSet2DAsset tileSet || tileSet.isMissing || tileSet.tiles.Length == 0)
            {
                ImGuiWidget.Hint("Assign a Tile Set with at least one tile to begin painting. Missing cells are preserved.");
                return;
            }

            context.interactions.TryGetModule<Rendering2DSpritePreviewModule>(out var thumbnails);
            if (!tileSet.TryGetTile(m_tileId, out _))
                m_tileId = tileSet.tiles[0].id;
            DrawPalette(tileSet, thumbnails);
            ImGuiWidget.PropertyRow("brush-tint", "Brush Tint", () =>
            {
                TilemapCanvasColor color = new(m_tint.r, m_tint.g, m_tint.b, m_tint.a);
                if (ImGui.ColorEditLinear4("##color", ref color))
                    m_tint = new InnoEngine.Mathematics.Color(color.X, color.Y, color.Z, color.W);
            });
            ImGui.BeginDisabled(readOnly);
            try
            {
                DrawCanvas(tileSet, thumbnails, (InspectorDraftEdits)edits, readOnly);
            }
            catch
            {
                if (m_gestureBefore is byte[] before)
                    Restore(before);
                throw;
            }
            finally
            {
                ImGui.EndDisabled();
            }
        }

        internal override bool Draw(IEditorPreviewService previews)
        {
            bool changed = false;
            if (ImGuiWidget.SectionHeader("Map", "Sparse cells retain their identities; only visible cells are drawn by the editor."))
            {
                float x = m_asset.cellSize.x;
                float y = m_asset.cellSize.y;
                int chunk = m_asset.chunkSize;
                bool geometry = Rendering2DEditorFields.Float("Cell Width", ref x, 0.0001f, 100000f);
                geometry |= Rendering2DEditorFields.Float("Cell Height", ref y, 0.0001f, 100000f);
                geometry |= Rendering2DEditorFields.Integer("Chunk Size", ref chunk, 1, 256);
                if (geometry)
                {
                    m_asset.cellSize = new CellSize(x, y);
                    m_asset.chunkSize = chunk;
                    changed = true;
                }
            }

            if (ImGuiWidget.SectionHeader("Layers", "Choose the painting layer. Deleting a layer with cells is disabled to preserve its content."))
            {
                TilemapLayer2D[] layers = m_asset.layers;
                string label = layers.FirstOrDefault(layer => layer.id == m_layerId).name ?? "Choose a layer";
                if (ImGuiWidget.BeginBoundedCombo("##layer", label))
                {
                    try
                    {
                        foreach (TilemapLayer2D layer in layers)
                        {
                            if (ImGui.Selectable(layer.name + "##" + layer.id, layer.id == m_layerId))
                                m_layerId = layer.id;
                        }
                    }
                    finally
                    {
                        ImGuiWidget.EndBoundedCombo();
                    }
                }

                int selected = Array.FindIndex(layers, layer => layer.id == m_layerId);
                if (selected >= 0)
                {
                    string name = layers[selected].name ?? "";
                    bool visible = layers[selected].visible;
                    int order = layers[selected].order;
                    bool edited = Rendering2DEditorFields.Text("Name", ref name);
                    edited |= Rendering2DEditorFields.Toggle("Visible", ref visible);
                    edited |= Rendering2DEditorFields.Integer("Order", ref order);
                    if (edited)
                    {
                        layers[selected].name = name;
                        layers[selected].visible = visible;
                        layers[selected].order = order;
                        m_asset.layers = layers;
                        changed = true;
                    }

                    bool occupied = m_asset.chunks.Any(chunk => chunk.layerId == m_layerId && chunk.cells.Length > 0);
                    ImGui.BeginDisabled(occupied);
                    try
                    {
                        if (ImGui.SmallButton("Delete Empty Layer"))
                        {
                            m_asset.layers = layers.Where(layer => layer.id != m_layerId).ToArray();
                            m_layerId = m_asset.layers.Length == 0 ? 0 : m_asset.layers[0].id;
                            changed = true;
                        }
                    }
                    finally
                    {
                        ImGui.EndDisabled();
                    }
                }

                _ = Rendering2DEditorFields.Text("New Layer", ref m_newLayer);
                if (ImGui.SmallButton("Add Layer"))
                {
                    int id = 0;
                    while (layers.Any(layer => layer.id == id))
                        id = checked(id + 1);
                    m_asset.layers = [..layers, new TilemapLayer2D
                    {
                        id = id,
                        name = m_newLayer.Trim(),
                        visible = true,
                        order = layers.Length
                    }
                    ];
                    m_layerId = id;
                    changed = true;
                }
            }

            return changed;
        }

        internal override bool Save(AssetPath path)
        {
            m_asset.SetLayers(m_asset.layers);
            return Rendering2DAssets.SaveTilemap(path, CopyContent(m_asset));
        }

        private static Tilemap2DAsset CopyContent(Tilemap2DAsset source) => new()
        {
            tileSet = source.tileSet,
            cellSize = source.cellSize,
            chunkSize = source.chunkSize,
            layers = source.layers.ToArray(),
            chunks = source.chunks.Select(chunk => chunk with { cells = chunk.cells?.ToArray() ?? [] }).ToArray()
        };

        private void DrawPalette(
            TileSet2DAsset tileSet,
            Rendering2DSpritePreviewModule? thumbnails
        ) {
            if (!ImGuiWidget.SectionHeader("Tile Palette", "Choose artwork by name, then paint on the map below."))
                return;
            _ = Rendering2DEditorFields.Text("Search", ref m_tileSearch);
            float edge = 62f * ImGuiWidget.style.zoom;
            int columns = Math.Max(1, (int)(ImGui.GetContentRegionAvailable().X / (edge + 4)));
            int index = 0;
            foreach (TileDefinition2D tile in tileSet.tiles.Where(tile => TileSetDraft.TileName(tile).Contains(m_tileSearch, StringComparison.OrdinalIgnoreCase)).Take(128))
            {
                if (index % columns != 0)
                    ImGui.SameLine(0, 4);
                ImGui.PushId(tile.id);
                TilemapCanvasPoint cursor = ImGui.GetCursorPosition();
                if (ImGui.Button("##tile", new TilemapCanvasPoint(edge, edge + 20)))
                    m_tileId = tile.id;
                TilemapCanvasPoint minimum = ImGui.GetItemMinimum();
                TilemapCanvasPoint maximum = ImGui.GetItemMaximum();
                TilemapCanvasPoint next = ImGui.GetCursorPosition();
                ImGui.SetCursorPosition(cursor + new TilemapCanvasPoint(3));
                thumbnails?.Draw(tile.sprite, $"tile/{tileSet.identity.persistentId}/{tile.id}", MathF.Max(1, (edge - 6) / ImGuiWidget.style.zoom), false);
                ImGui.SetCursorPosition(next);
                string name = TileSetDraft.TileName(tile);
                while (name.Length > 1 && ImGui.MeasureText(name).X > edge - 6)
                    name = name[..^1];
                ImGui.DrawText(new TilemapCanvasPoint(minimum.X + 3, maximum.Y - 18), ImGui.ToPackedColor(EditorPalette.text), name);
                if (tile.id == m_tileId)
                    ImGui.DrawRectangle(minimum, maximum, ImGui.ToPackedColor(EditorPalette.warning), 3, 2);
                ImGui.PopId();
                index++;
            }

            if (tileSet.TryGetTile(m_tileId, out TileDefinition2D selected))
                thumbnails?.Draw(selected.sprite, "tile-palette/selected", 100f);
        }

        private void DrawCanvas(
            TileSet2DAsset tileSet,
            Rendering2DSpritePreviewModule? thumbnails,
            InspectorDraftEdits edits,
            bool readOnly
        ) {
            if (!ImGuiWidget.SectionHeader("Paint", "Drag Brush/Erase across cells. Box and Select use the start/end cells. Fill is bounded to the visible map."))
                return;
            if (!m_asset.layers.Any(layer => layer.id == m_layerId))
            {
                ImGuiWidget.Hint("Add a layer before painting.");
                return;
            }

            _ = Rendering2DEditorFields.Choice("Tool", ref m_tool);
            _ = Rendering2DEditorFields.Float("Zoom", ref m_zoom, 0.5f, 3f);
            _ = Rendering2DEditorFields.Integer("Origin X", ref m_originX, -1000000, 1000000);
            _ = Rendering2DEditorFields.Integer("Origin Y", ref m_originY, -1000000, 1000000);
            _ = Rendering2DEditorFields.Toggle("Flip X", ref m_flipX);
            _ = Rendering2DEditorFields.Toggle("Flip Y", ref m_flipY);
            _ = Rendering2DEditorFields.Integer("Quarter Turns", ref m_turns, 0, 3);
            if (ImGui.SmallButton("Copy Selection") && m_selection is TilemapSelection2D selection)
            {
                m_stamp = TilemapEditing2D.Select(m_asset, m_layerId, selection).Select(cell => new TilemapStampCell2D(cell.x - selection.minimumX, cell.y - selection.minimumY, cell)).ToArray();
                m_tool = PaintTool.Stamp;
            }

            float edge = 28f * m_zoom * ImGuiWidget.style.zoom;
            int columns = Math.Clamp((int)(ImGui.GetContentRegionAvailable().X / (edge + 2f)), 1, 32);
            int rows = Math.Clamp((int)(220f * ImGuiWidget.style.zoom / (edge + 2f)), 3, 24);
            var bounds = new TilemapSelection2D(m_originX, m_originY, m_originX + columns - 1, m_originY + rows - 1);
            m_visibleBounds = bounds;
            bool held = !readOnly && ImGui.IsMouseButtonDown();
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (column > 0)
                        ImGui.SameLine(0, 2f);
                    int x = m_originX + column;
                    int y = m_originY + row;
                    var position = new TilemapPosition2D(x, y);
                    bool occupied = m_asset.TryGetCell(x, y, m_layerId, out TilemapCell2D cell);
                    string title = occupied && tileSet.TryGetTile(cell.tileId, out TileDefinition2D tile) ? TileSetDraft.TileName(tile)[..1] : occupied ? "?" : "·";
                    ImGui.Button(title + $"##cell:{x}:{y}", new TilemapCanvasPoint(edge, edge));
                    TilemapCanvasPoint minimum = ImGui.GetItemMinimum();
                    TilemapCanvasPoint maximum = ImGui.GetItemMaximum();
                    TilemapCanvasPoint next = ImGui.GetCursorPosition();
                    bool hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
                    if (!readOnly && ImGui.IsItemClicked())
                    {
                        m_gestureBefore = edits.current;
                        m_start = position;
                        m_end = position;
                        m_lastCell = null;
                    }

                    if (!readOnly && m_gestureBefore is not null && hovered && (held || m_lastCell is null))
                    {
                        m_end = position;
                        if (m_lastCell != position)
                            Paint(position);
                        m_lastCell = position;
                    }

                    if (occupied && tileSet.TryGetTile(cell.tileId, out TileDefinition2D visual) && thumbnails is not null)
                    {
                        TilemapCanvasPoint cursor = ImGui.GetCursorPosition();
                        ImGui.SetCursorPosition(cursor + minimum - ImGui.GetCursorScreenPosition() + new TilemapCanvasPoint(2));
                        thumbnails.Draw(visual.sprite, $"tile/{tileSet.identity.persistentId}/{visual.id}", MathF.Max(1, (edge - 4) / ImGuiWidget.style.zoom), false);
                        ImGui.SetCursorPosition(next);
                    }

                    if (m_selection is TilemapSelection2D selected && selected.Contains(x, y))
                        ImGui.DrawRectangle(minimum, maximum, ImGui.ToPackedColor(EditorPalette.warning), 0f, 2f);
                }
            }

            if (m_gestureBefore is not null && !held)
            {
                Finish(bounds);
                byte[] before = m_gestureBefore;
                m_gestureBefore = null;
                m_lastCell = null;
                edits.RecordApplied(before, "Paint Tilemap");
            }

            ImGuiWidget.Hint($"Visible: ({bounds.minimumX}, {bounds.minimumY}) – ({bounds.maximumX}, {bounds.maximumY}) · {m_asset.chunks.Length} sparse chunks");
            if (m_tool == PaintTool.Stamp && m_stamp.Length == 0)
                ImGuiWidget.Hint("Select occupied cells and Copy Selection before stamping.");
        }

        private void Paint(TilemapPosition2D position)
        {
            if (m_tool == PaintTool.Brush)
                _ = TilemapEditing2D.PaintStroke(m_asset, m_layerId, TilemapEditing2D.Trace(m_lastCell ?? position, position), PaintValue());
            else if (m_tool == PaintTool.Erase)
                _ = TilemapEditing2D.Erase(m_asset, m_layerId, TilemapEditing2D.Trace(m_lastCell ?? position, position));
            else if (m_tool == PaintTool.Pick && m_asset.TryGetCell(position.x, position.y, m_layerId, out TilemapCell2D cell))
            {
                m_tileId = cell.tileId;
                m_tint = cell.color;
                m_flipX = cell.flipX;
                m_flipY = cell.flipY;
                m_turns = cell.quarterTurns;
            }
            else if (m_tool == PaintTool.Select)
                m_selection = new TilemapSelection2D(m_start.x, m_start.y, position.x, position.y);
        }

        private void Finish(TilemapSelection2D bounds)
        {
            if (m_tool == PaintTool.Box)
                _ = TilemapEditing2D.Box(m_asset, m_layerId, new TilemapSelection2D(m_start.x, m_start.y, m_end.x, m_end.y), PaintValue());
            else if (m_tool == PaintTool.Fill)
                _ = TilemapEditing2D.Fill(m_asset, m_layerId, m_start, bounds, PaintValue());
            else if (m_tool == PaintTool.Move && m_selection is TilemapSelection2D selected)
            {
                int x = m_end.x - m_start.x;
                int y = m_end.y - m_start.y;
                _ = TilemapEditing2D.Move(m_asset, m_layerId, selected, x, y);
                m_selection = new TilemapSelection2D(selected.minimumX + x, selected.minimumY + y, selected.maximumX + x, selected.maximumY + y);
            }
            else if (m_tool == PaintTool.Stamp && m_stamp.Length > 0)
                _ = TilemapEditing2D.Stamp(m_asset, m_layerId, m_end, m_stamp);
        }

        private TilemapCell2D PaintValue() => new()
        {
            tileId = m_tileId,
            color = m_tint,
            flipX = m_flipX,
            flipY = m_flipY,
            quarterTurns = m_turns
        };
        private enum PaintTool
        {
            Brush,
            Erase,
            Pick,
            Box,
            Fill,
            Select,
            Move,
            Stamp
        }
    }
}
