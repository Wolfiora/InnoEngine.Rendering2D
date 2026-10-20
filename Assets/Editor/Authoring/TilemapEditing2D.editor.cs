using System;
using System.Collections.Generic;
using System.Linq;

using InnoEngine.Mathematics;

namespace Inno.Rendering2D;

/// <summary>
/// Identifies one integer tilemap coordinate.
/// </summary>
/// <param name="x">
/// Horizontal map coordinate.
/// </param>
/// <param name="y">
/// Vertical map coordinate.
/// </param>
public readonly record struct TilemapPosition2D(
    int x,
    int y
);

/// <summary>
/// Defines an inclusive finite rectangle used by tilemap authoring operations.
/// </summary>
public readonly record struct TilemapSelection2D
{
    /// <summary>
    /// Creates a normalized inclusive selection rectangle.
    /// </summary>
    /// <param name="firstX">
    /// Horizontal coordinate of the first corner.
    /// </param>
    /// <param name="firstY">
    /// Vertical coordinate of the first corner.
    /// </param>
    /// <param name="secondX">
    /// Horizontal coordinate of the opposite corner.
    /// </param>
    /// <param name="secondY">
    /// Vertical coordinate of the opposite corner.
    /// </param>
    public TilemapSelection2D(
        int firstX,
        int firstY,
        int secondX,
        int secondY
    ) {
        minimumX = Math.Min(firstX, secondX);
        minimumY = Math.Min(firstY, secondY);
        maximumX = Math.Max(firstX, secondX);
        maximumY = Math.Max(firstY, secondY);
    }

    /// <summary>
    /// Gets the inclusive minimum horizontal coordinate.
    /// </summary>
    public int minimumX { get; }

    /// <summary>
    /// Gets the inclusive minimum vertical coordinate.
    /// </summary>
    public int minimumY { get; }

    /// <summary>
    /// Gets the inclusive maximum horizontal coordinate.
    /// </summary>
    public int maximumX { get; }

    /// <summary>
    /// Gets the inclusive maximum vertical coordinate.
    /// </summary>
    public int maximumY { get; }

    /// <summary>
    /// Gets whether the selection contains a coordinate.
    /// </summary>
    /// <param name="x">
    /// Horizontal map coordinate.
    /// </param>
    /// <param name="y">
    /// Vertical map coordinate.
    /// </param>
    /// <returns>
    /// Whether the coordinate is inside this inclusive selection.
    /// </returns>
    public bool Contains(
        int x,
        int y
    ) => x >= minimumX && x <= maximumX && y >= minimumY && y <= maximumY;
}

/// <summary>
/// Stores one relative cell used by the reusable stamp tool.
/// </summary>
/// <param name="x">
/// Horizontal offset from the stamp origin.
/// </param>
/// <param name="y">
/// Vertical offset from the stamp origin.
/// </param>
/// <param name="value">
/// Artwork, tint and transform flags to stamp.
/// </param>
public readonly record struct TilemapStampCell2D(
    int x,
    int y,
    TilemapCell2D value
);

/// <summary>
/// Stores optional occupancy without reserving a tile identifier as an empty sentinel.
/// </summary>
/// <param name="hasValue">
/// Whether this snapshot contains an occupied cell.
/// </param>
/// <param name="value">
/// Cell data; ignored when the snapshot is empty.
/// </param>
public readonly record struct TilemapCellSnapshot2D(
    bool hasValue,
    TilemapCell2D value
);

/// <summary>
/// Stores the before and after value for exactly one affected tilemap cell.
/// </summary>
/// <param name="layerId">
/// Stable affected layer identity.
/// </param>
/// <param name="x">
/// Horizontal map coordinate.
/// </param>
/// <param name="y">
/// Vertical map coordinate.
/// </param>
/// <param name="before">
/// Occupancy and value before the gesture.
/// </param>
/// <param name="after">
/// Occupancy and value after the gesture.
/// </param>
public readonly record struct TilemapCellChange2D(
    int layerId,
    int x,
    int y,
    TilemapCellSnapshot2D before,
    TilemapCellSnapshot2D after
);

/// <summary>
/// Owns the compact, deterministic result of one complete tilemap gesture.
/// </summary>
public sealed class TilemapStroke2D
{
    internal TilemapStroke2D(TilemapCellChange2D[] changes) => this.changes = changes;

    /// <summary>
    /// Gets affected cells in stable layer, Y, X order.
    /// </summary>
    public IReadOnlyList<TilemapCellChange2D> changes { get; }

    /// <summary>
    /// Gets whether the gesture changed at least one cell.
    /// </summary>
    public bool hasChanges => changes.Count != 0;

    /// <summary>
    /// Restores every affected cell to its value before the gesture.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    public void Undo(Tilemap2DAsset tilemap) => Apply(tilemap, useAfter: false);

    /// <summary>
    /// Reapplies every affected cell's value after the gesture.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    public void Redo(Tilemap2DAsset tilemap) => Apply(tilemap, useAfter: true);

    private void Apply(
        Tilemap2DAsset tilemap,
        bool useAfter
    ) {
        ArgumentNullException.ThrowIfNull(tilemap);
        var writes = new TilemapCellWrite2D[changes.Count];
        for (int index = 0; index < changes.Count; index++)
        {
            TilemapCellChange2D change = changes[index];
            TilemapCellSnapshot2D snapshot = useAfter ? change.after : change.before;
            writes[index] = snapshot.hasValue
                ? TilemapCellWrite2D.Set(change.layerId, WithPosition(snapshot.value, change.x, change.y))
                : TilemapCellWrite2D.Remove(change.layerId, change.x, change.y);
        }
        _ = tilemap.ApplyCells(writes);
    }

    internal static TilemapCell2D WithPosition(
        TilemapCell2D value,
        int x,
        int y
    ) {
        value.x = x;
        value.y = y;
        return value;
    }
}

/// <summary>
/// Provides deterministic Tile Palette operations that emit one compact change set per gesture.
/// </summary>
public static class TilemapEditing2D
{
    private const int C_MAX_CELLS = 1_048_576;

    /// <summary>
    /// Enumerates an inclusive cell line so fast pointer movement does not leave brush gaps.
    /// </summary>
    /// <param name="start">
    /// First cell in the stroke segment.
    /// </param>
    /// <param name="end">
    /// Last cell in the stroke segment.
    /// </param>
    /// <returns>
    /// Each crossed integer step, including both endpoints exactly once.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The segment exceeds the bounded interactive stroke budget.
    /// </exception>
    public static IEnumerable<TilemapPosition2D> Trace(
        TilemapPosition2D start,
        TilemapPosition2D end
    ) {
        long dx = Math.Abs((long)end.x - start.x);
        long dy = Math.Abs((long)end.y - start.y);
        if (Math.Max(dx, dy) > 65536)
            throw new ArgumentOutOfRangeException(nameof(end), "A stroke segment exceeds 65,536 cell steps.");
        int x = start.x;
        int y = start.y;
        int sx = x < end.x ? 1 : -1;
        int sy = y < end.y ? 1 : -1;
        long error = dx - dy;
        while (true)
        {
            yield return new TilemapPosition2D(x, y);
            if (x == end.x && y == end.y)
                yield break;
            long twice = 2 * error;
            if (twice > -dy)
            {
                error -= dy;
                x += sx;
            }

            if (twice < dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    /// <summary>
    /// Paints one cell.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="position">
    /// Cell to paint.
    /// </param>
    /// <param name="value">
    /// Cell artwork and flags; its stored coordinates are replaced by the target position.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    public static TilemapStroke2D Brush(
        Tilemap2DAsset tilemap,
        int layerId,
        TilemapPosition2D position,
        TilemapCell2D value
    )
        => PaintStroke(tilemap, layerId, [position], value);

    /// <summary>
    /// Paints a freehand sequence, coalescing repeated coordinates into one change.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="positions">
    /// Finite cell sequence; duplicate coordinates are coalesced.
    /// </param>
    /// <param name="value">
    /// Cell artwork and flags; its stored coordinates are replaced by the target position.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    public static TilemapStroke2D PaintStroke(
        Tilemap2DAsset tilemap,
        int layerId,
        IEnumerable<TilemapPosition2D> positions,
        TilemapCell2D value
    ) {
        ArgumentNullException.ThrowIfNull(positions);
        var builder = new StrokeBuilder(tilemap);
        foreach (TilemapPosition2D position in positions)
            builder.Set(layerId, position.x, position.y, new TilemapCellSnapshot2D(true, value));
        return builder.Commit();
    }

    /// <summary>
    /// Erases one freehand sequence, coalescing repeated coordinates.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="positions">
    /// Finite cell sequence; duplicate coordinates are coalesced.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    public static TilemapStroke2D Erase(
        Tilemap2DAsset tilemap,
        int layerId,
        IEnumerable<TilemapPosition2D> positions
    ) {
        ArgumentNullException.ThrowIfNull(positions);
        var builder = new StrokeBuilder(tilemap);
        foreach (TilemapPosition2D position in positions)
            builder.Set(layerId, position.x, position.y, default);
        return builder.Commit();
    }

    /// <summary>
    /// Paints every cell inside an inclusive box.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="bounds">
    /// Inclusive finite area bounding the operation.
    /// </param>
    /// <param name="value">
    /// Cell artwork and flags; its stored coordinates are replaced by the target position.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    public static TilemapStroke2D Box(
        Tilemap2DAsset tilemap,
        int layerId,
        TilemapSelection2D bounds,
        TilemapCell2D value
    ) {
        ValidateArea(bounds);
        var builder = new StrokeBuilder(tilemap);
        for (long y = bounds.minimumY; y <= bounds.maximumY; y++)
        {
            for (long x = bounds.minimumX; x <= bounds.maximumX; x++)
                builder.Set(layerId, (int)x, (int)y, new TilemapCellSnapshot2D(true, value));
        }

        return builder.Commit();
    }

    /// <summary>
    /// Flood-fills an exact connected value within finite inclusive bounds.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="origin">
    /// Starting cell in map coordinates.
    /// </param>
    /// <param name="bounds">
    /// Inclusive finite area bounding the operation.
    /// </param>
    /// <param name="value">
    /// Cell artwork and flags; its stored coordinates are replaced by the target position.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    public static TilemapStroke2D Fill(
        Tilemap2DAsset tilemap,
        int layerId,
        TilemapPosition2D origin,
        TilemapSelection2D bounds,
        TilemapCell2D value
    ) {
        ArgumentNullException.ThrowIfNull(tilemap);
        ValidateArea(bounds);
        if (!bounds.Contains(origin.x, origin.y))
            throw new ArgumentOutOfRangeException(nameof(origin), "Fill origin must be inside its finite bounds.");
        var builder = new StrokeBuilder(tilemap);
        TilemapCellSnapshot2D target = builder.Read(layerId, origin.x, origin.y);
        TilemapCellSnapshot2D replacement = new(true, value);
        if (Equivalent(target, replacement))
            return builder.Commit();
        var pending = new Queue<TilemapPosition2D>();
        var visited = new HashSet<TilemapPosition2D>();
        pending.Enqueue(origin);
        while (pending.Count != 0)
        {
            TilemapPosition2D position = pending.Dequeue();
            if (!bounds.Contains(position.x, position.y) || !visited.Add(position) || !Equivalent(builder.Read(layerId, position.x, position.y), target))
            {
                continue;
            }

            builder.Set(layerId, position.x, position.y, replacement);
            if (position.x > bounds.minimumX)
                pending.Enqueue(new TilemapPosition2D(position.x - 1, position.y));
            if (position.x < bounds.maximumX)
                pending.Enqueue(new TilemapPosition2D(position.x + 1, position.y));
            if (position.y > bounds.minimumY)
                pending.Enqueue(new TilemapPosition2D(position.x, position.y - 1));
            if (position.y < bounds.maximumY)
                pending.Enqueue(new TilemapPosition2D(position.x, position.y + 1));
        }

        return builder.Commit();
    }

    /// <summary>
    /// Returns occupied cells inside an inclusive selection in stable Y then X order.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="bounds">
    /// Inclusive finite area bounding the operation.
    /// </param>
    /// <returns>
    /// Occupied cells sorted by Y then X; empty when none match.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    public static IReadOnlyList<TilemapCell2D> Select(
        Tilemap2DAsset tilemap,
        int layerId,
        TilemapSelection2D bounds
    ) {
        ArgumentNullException.ThrowIfNull(tilemap);
        return tilemap.chunks
            .Where(chunk => chunk.layerId == layerId)
            .SelectMany(static chunk => chunk.cells ?? [])
            .Where(cell => bounds.Contains(cell.x, cell.y))
            .OrderBy(static cell => cell.y)
            .ThenBy(static cell => cell.x)
            .ToArray();
    }

    /// <summary>
    /// Moves occupied cells in a selection by an integer offset as one overlap-safe gesture.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="selection">
    /// Inclusive source area.
    /// </param>
    /// <param name="offsetX">
    /// Horizontal translation in cells.
    /// </param>
    /// <param name="offsetY">
    /// Vertical translation in cells.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A translated coordinate exceeds the integer range; no cells are changed.
    /// </exception>
    public static TilemapStroke2D Move(
        Tilemap2DAsset tilemap,
        int layerId,
        TilemapSelection2D selection,
        int offsetX,
        int offsetY
    ) {
        IReadOnlyList<TilemapCell2D> selected = Select(tilemap, layerId, selection);
        var builder = new StrokeBuilder(tilemap);
        for (int index = 0; index < selected.Count; index++)
            builder.Set(layerId, selected[index].x, selected[index].y, default);
        for (int index = 0; index < selected.Count; index++)
        {
            TilemapCell2D cell = selected[index];
            builder.Set(layerId, checked(cell.x + offsetX), checked(cell.y + offsetY), new TilemapCellSnapshot2D(true, cell));
        }

        return builder.Commit();
    }

    /// <summary>
    /// Places a reusable sparse stamp at an integer origin.
    /// </summary>
    /// <param name="tilemap">
    /// Caller-owned tilemap changed atomically by this operation.
    /// </param>
    /// <param name="layerId">
    /// Stable identity of the affected layer.
    /// </param>
    /// <param name="origin">
    /// Starting cell in map coordinates.
    /// </param>
    /// <param name="cells">
    /// Sparse cells positioned relative to the stamp origin.
    /// </param>
    /// <returns>
    /// Applied change set, possibly empty; Undo and Redo preserve unaffected cells.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// A required tilemap or sequence is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The operation exceeds the cell budget or uses an invalid bounded origin.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A translated coordinate exceeds the integer range; no cells are changed.
    /// </exception>
    public static TilemapStroke2D Stamp(
        Tilemap2DAsset tilemap,
        int layerId,
        TilemapPosition2D origin,
        IEnumerable<TilemapStampCell2D> cells
    ) {
        ArgumentNullException.ThrowIfNull(cells);
        var builder = new StrokeBuilder(tilemap);
        foreach (TilemapStampCell2D cell in cells)
        {
            builder.Set(layerId, checked(origin.x + cell.x), checked(origin.y + cell.y), new TilemapCellSnapshot2D(true, cell.value));
        }

        return builder.Commit();
    }

    private static bool Equivalent(
        TilemapCellSnapshot2D left,
        TilemapCellSnapshot2D right
    )
        => left.hasValue == right.hasValue
           && (!left.hasValue || EqualityComparer<TilemapCell2D>.Default.Equals(left.value, right.value));

    private static void ValidateArea(TilemapSelection2D bounds)
    {
        long width = (long)bounds.maximumX - bounds.minimumX + 1;
        long height = (long)bounds.maximumY - bounds.minimumY + 1;
        if (width > C_MAX_CELLS || height > C_MAX_CELLS || width * height > C_MAX_CELLS)
            throw new ArgumentOutOfRangeException(nameof(bounds), "One tilemap gesture cannot visit more than 1,048,576 cells.");
    }

    private sealed class StrokeBuilder
    {
        private readonly Tilemap2DAsset m_tilemap;
        private readonly Dictionary<CellKey, MutableChange> m_changes = [];

        internal StrokeBuilder(Tilemap2DAsset tilemap) => m_tilemap = tilemap ?? throw new ArgumentNullException(nameof(tilemap));

        internal TilemapCellSnapshot2D Read(
            int layerId,
            int x,
            int y
        ) {
            var key = new CellKey(layerId, x, y);
            if (m_changes.TryGetValue(key, out MutableChange? change))
                return change.after;
            return m_tilemap.TryGetCell(x, y, layerId, out TilemapCell2D value)
                ? new TilemapCellSnapshot2D(true, value)
                : default;
        }

        internal void Set(
            int layerId,
            int x,
            int y,
            TilemapCellSnapshot2D after
        ) {
            var key = new CellKey(layerId, x, y);
            if (!m_changes.TryGetValue(key, out MutableChange? change))
            {
                TilemapCellSnapshot2D before = m_tilemap.TryGetCell(x, y, layerId, out TilemapCell2D value)
                    ? new TilemapCellSnapshot2D(true, value)
                    : default;
                if (m_changes.Count >= C_MAX_CELLS)
                    throw new ArgumentOutOfRangeException(nameof(after), "The tilemap gesture exceeds its cell budget.");
                change = new MutableChange(before, after);
                m_changes.Add(key, change);
            }
            else
            {
                change.after = after;
            }
            if (Equivalent(change.before, change.after))
                m_changes.Remove(key);
        }

        internal TilemapStroke2D Commit()
        {
            TilemapCellChange2D[] changes = m_changes
                .Select(static pair => new TilemapCellChange2D(
                    pair.Key.layerId,
                    pair.Key.x,
                    pair.Key.y,
                    pair.Value.before,
                    pair.Value.after))
                .OrderBy(static change => change.layerId)
                .ThenBy(static change => change.y)
                .ThenBy(static change => change.x)
                .ToArray();
            var writes = new TilemapCellWrite2D[changes.Length];
            for (int index = 0; index < changes.Length; index++)
            {
                TilemapCellChange2D change = changes[index];
                writes[index] = change.after.hasValue
                    ? TilemapCellWrite2D.Set(
                        change.layerId,
                        TilemapStroke2D.WithPosition(change.after.value, change.x, change.y))
                    : TilemapCellWrite2D.Remove(change.layerId, change.x, change.y);
            }
            _ = m_tilemap.ApplyCells(writes);
            return new TilemapStroke2D(changes);
        }
    }

    private sealed class MutableChange(
        TilemapCellSnapshot2D before,
        TilemapCellSnapshot2D after
    ) {
        internal TilemapCellSnapshot2D before { get; } = before;
        internal TilemapCellSnapshot2D after { get; set; } = after;
    }

    private readonly record struct CellKey(
        int layerId,
        int x,
        int y
    );
}
