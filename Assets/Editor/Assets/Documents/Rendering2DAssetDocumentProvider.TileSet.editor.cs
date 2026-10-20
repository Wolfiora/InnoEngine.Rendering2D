using System;
using System.Linq;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Mathematics;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed class TileSetDraft(TileSet2DAsset asset) : Draft
    {
        private TileDefinition2D[] m_tiles = CloneTiles(asset.tiles);
        private int m_selected;
        private int m_rule;
        private int m_frame;
        private string m_newName = "Grass";

        internal override byte[] Capture() => EditorAssets.EncodeNative(new TileSet2DAsset { tiles = CloneTiles(m_tiles) });

        internal override void Restore(byte[] bytes) => m_tiles = CloneTiles(EditorAssets.DecodeNative<TileSet2DAsset>(bytes).tiles);

        internal override void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            base.DrawInspector(context, previews, edits, readOnly);
            if (m_tiles.Length == 0)
                return;
            m_selected = Math.Clamp(m_selected, 0, m_tiles.Length - 1);
            Property("sprite", "Sprite", () => Selected().sprite, value => Update(Selected() with { sprite = value }));
            Property("color", "Tint", () => Selected().color, value => Update(Selected() with { color = value }));
            TileDefinition2D tile = Selected();
            if (tile.rules is { Length: > 0 })
            {
                m_rule = Math.Clamp(m_rule, 0, tile.rules.Length - 1);
                Property("rule.sprite", "Rule Result", () => Selected().rules[m_rule].sprite, value =>
                {
                    TileDefinition2D current = Selected();
                    current.rules[m_rule].sprite = value;
                    Update(current);
                });
            }

            if (tile.animation is { Length: > 0 })
            {
                m_frame = Math.Clamp(m_frame, 0, tile.animation.Length - 1);
                Property("animation.sprite", "Frame Sprite", () => Selected().animation[m_frame].sprite, value =>
                {
                    TileDefinition2D current = Selected();
                    current.animation[m_frame].sprite = value;
                    Update(current);
                });
            }

            void Property<T>(
                string path,
                string label,
                Func<T> getter,
                Action<T> setter
            ) => context.properties.DrawValue(
                context.editorContext,
                this,
                "tileset." + path,
                label,
                typeof(T),
                () => getter(),
                value => setter(value is T typed ? typed : default!),
                edits,
                readOnly
            );
        }

        internal override bool Draw(IEditorPreviewService previews)
        {
            bool changed = false;
            ImGuiWidget.SectionHeader("Tile Definitions", "Choose tiles by name. Map cells keep their numeric identities when a tile is renamed.");
            _ = Rendering2DEditorFields.Text("New Tile", ref m_newName);
            if (ImGui.Button("Add Tile"))
            {
                int id = 0;
                while (m_tiles.Any(value => value.id == id))
                    id = checked(id + 1);
                m_tiles = [..m_tiles, new TileDefinition2D
                {
                    id = id,
                    name = m_newName.Trim(),
                    color = InnoEngine.Mathematics.Color.WHITE,
                    animation = [],
                    rules = [],
                    metadata = ""
                }
                ];
                m_selected = m_tiles.Length - 1;
                changed = true;
            }

            if (m_tiles.Length == 0)
            {
                ImGuiWidget.Hint("Add a tile and assign a sprite. Animated and rule variants are optional.");
                return changed;
            }

            m_selected = Math.Clamp(m_selected, 0, m_tiles.Length - 1);
            if (ImGuiWidget.BeginBoundedCombo("##tile", TileName(m_tiles[m_selected])))
            {
                try
                {
                    for (int index = 0; index < m_tiles.Length; index++)
                    {
                        if (ImGui.Selectable(TileName(m_tiles[index]) + "##" + m_tiles[index].id, index == m_selected))
                            m_selected = index;
                    }
                }
                finally
                {
                    ImGuiWidget.EndBoundedCombo();
                }
            }

            TileDefinition2D tile = Selected();
            string name = tile.name ?? "";
            if (Rendering2DEditorFields.Text("Name", ref name))
            {
                tile.name = name;
                changed = true;
            }

            if (ImGui.SmallButton("Remove Tile"))
            {
                m_tiles = m_tiles.Where((
                    _,
                    index
                ) => index != m_selected).ToArray();
                m_selected = Math.Max(0, m_selected - 1);
                return true;
            }

            ImGuiWidget.Hint("Removing a definition preserves its map cells as unresolved tiles.");
            if (ImGuiWidget.SectionHeader("Animation", "Variable-duration frames override the base sprite while the tile is visible."))
            {
                tile.animation ??= [];
                if (ImGui.SmallButton("Add Animation Frame"))
                {
                    tile.animation = [..tile.animation, new TileAnimationFrame2D
                    {
                        sprite = tile.sprite,
                        duration = 0.125f
                    }
                    ];
                    m_frame = tile.animation.Length - 1;
                    changed = true;
                }

                for (int index = 0; index < tile.animation.Length; index++)
                {
                    if (ImGui.Selectable($"Frame {index + 1} · {tile.animation[index].duration:0.###} s", index == m_frame))
                        m_frame = index;
                }

                if (tile.animation.Length > 0)
                {
                    m_frame = Math.Clamp(m_frame, 0, tile.animation.Length - 1);
                    float duration = tile.animation[m_frame].duration;
                    if (Rendering2DEditorFields.Float("Duration (s)", ref duration, 1f / 240f, 3600f))
                    {
                        tile.animation[m_frame].duration = duration;
                        changed = true;
                    }

                    if (ImGui.SmallButton("Remove Animation Frame"))
                    {
                        tile.animation = tile.animation.Where((
                            _,
                            index
                        ) => index != m_frame).ToArray();
                        m_frame = Math.Max(0, m_frame - 1);
                        changed = true;
                    }
                }
            }

            if (ImGuiWidget.SectionHeader("Neighbor Rules", "Click neighbor cells to cycle Any / Same / Different / Empty / Occupied. Higher priority wins."))
            {
                tile.rules ??= [];
                if (ImGui.SmallButton("Add Rule"))
                {
                    tile.rules = [..tile.rules, new TileVisualRule2D
                    {
                        sprite = tile.sprite,
                        conditions = []
                    }
                    ];
                    m_rule = tile.rules.Length - 1;
                    changed = true;
                }

                for (int index = 0; index < tile.rules.Length; index++)
                {
                    if (ImGui.Selectable($"Rule {index + 1} · priority {tile.rules[index].priority}", index == m_rule))
                        m_rule = index;
                }

                if (tile.rules.Length > 0)
                {
                    m_rule = Math.Clamp(m_rule, 0, tile.rules.Length - 1);
                    TileVisualRule2D rule = tile.rules[m_rule];
                    int priority = rule.priority;
                    if (Rendering2DEditorFields.Integer("Priority", ref priority))
                    {
                        rule.priority = priority;
                        changed = true;
                    }

                    var conditions = (rule.conditions ?? []).ToList();
                    float edge = MathF.Min(70f * ImGuiWidget.style.zoom, MathF.Max(1f, (ImGui.GetContentRegionAvailable().X - 8f) / 3f));
                    for (int y = 1; y >= -1; y--)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            if (x > -1)
                                ImGui.SameLine(0, 2);
                            if (x == 0 && y == 0)
                            {
                                ImGui.Button("Tile##center", new System.Numerics.Vector2(edge, edge));
                                continue;
                            }

                            int found = conditions.FindIndex(item => item.x == x && item.y == y);
                            TileNeighborRule2D match = found < 0 ? TileNeighborRule2D.Any : conditions[found].rule;
                            if (ImGui.Button(match + $"##{x}:{y}", new System.Numerics.Vector2(edge, edge)))
                            {
                                var condition = new TileNeighborCondition2D
                                {
                                    x = x,
                                    y = y,
                                    rule = (TileNeighborRule2D)(((int)match + 1) % 5)
                                };
                                if (found < 0)
                                    conditions.Add(condition);
                                else
                                    conditions[found] = condition;
                                changed = true;
                            }
                        }
                    }

                    rule.conditions = conditions.Where(item => item.rule != TileNeighborRule2D.Any).ToArray();
                    tile.rules[m_rule] = rule;
                    if (ImGui.SmallButton("Remove Rule"))
                    {
                        tile.rules = tile.rules.Where((
                            _,
                            index
                        ) => index != m_rule).ToArray();
                        m_rule = Math.Max(0, m_rule - 1);
                        changed = true;
                    }
                }
            }

            if (ImGuiWidget.SectionHeader("Gameplay Metadata", "Optional domain data. Rendering does not interpret it."))
            {
                string metadata = tile.metadata ?? "";
                if (Rendering2DEditorFields.Text("Metadata", ref metadata))
                {
                    tile.metadata = metadata;
                    changed = true;
                }
            }

            Update(tile);
            return changed;
        }

        internal override bool Save(AssetPath path)
        {
            var result = new TileSet2DAsset();
            result.SetTiles(CloneTiles(m_tiles));
            return Rendering2DAssets.SaveTileSet(path, result);
        }

        private TileDefinition2D Selected() => m_tiles[m_selected];
        private void Update(TileDefinition2D tile) => m_tiles[m_selected] = tile;

        internal static string TileName(TileDefinition2D tile) => string.IsNullOrWhiteSpace(tile.name) ? $"Tile {tile.id}" : tile.name;
        private static TileDefinition2D[] CloneTiles(TileDefinition2D[] source) => source.Select(tile => tile with { animation = tile.animation?.ToArray() ?? [], rules = tile.rules?.Select(rule => rule with { conditions = rule.conditions?.ToArray() ?? [] }).ToArray() ?? [] }).ToArray();
    }
}
