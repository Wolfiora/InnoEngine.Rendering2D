using System;
using System.Linq;
using Inno.Core.Mathematics;
using Xunit;

namespace Inno.Rendering2D.Tests;

public sealed class TilemapEditingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 0)]
    [InlineData(0, -12)]
    [InlineData(9, 12)]
    [InlineData(-9, 12)]
    [InlineData(9, -12)]
    [InlineData(-9, -12)]
    [InlineData(12, 9)]
    [InlineData(-12, -9)]
    public void TraceHasNoGapsOrDuplicateCells(
        int x,
        int y
    ) {
        TilemapPosition2D start = new(-3, 4);
        TilemapPosition2D end = new(x, y);
        TilemapPosition2D[] cells = TilemapEditing2D.Trace(start, end).ToArray();
        Assert.Equal(start, cells[0]);
        Assert.Equal(end, cells[^1]);
        Assert.Equal(Math.Max(Math.Abs(x + 3), Math.Abs(y - 4)) + 1, cells.Length);
        Assert.Equal(cells.Length, cells.Distinct().Count());
        for (int index = 1; index < cells.Length; index++)
        {
            Assert.InRange(Math.Abs(cells[index].x - cells[index - 1].x), 0, 1);
            Assert.InRange(Math.Abs(cells[index].y - cells[index - 1].y), 0, 1);
        }
    }

    [Fact]
    public void TraceRejectsExtremeCoordinatesBeforeOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TilemapEditing2D.Trace(new(int.MinValue, 0), new(int.MaxValue, 0)).ToArray());
    }

    [Fact]
    public void PaintMoveEraseAndHistoryPreserveExactSparseValues()
    {
        var map = new Tilemap2DAsset();
        map.SetLayers([new() {
            id = 0,
            name = "Ground",
            visible = true
        }]);
        TilemapCell2D value = new()
        {
            tileId = 7,
            color = Color.WHITE,
            flipX = true,
            quarterTurns = 3
        };
        TilemapStroke2D paint = TilemapEditing2D.PaintStroke(map, 0, [new(-1, 0), new(0, 0), new(1, 0), new(0, 0)], value);
        Assert.Equal(3, paint.changes.Count);
        paint.Undo(map);
        Assert.Empty(map.chunks);
        paint.Redo(map);
        TilemapStroke2D move = TilemapEditing2D.Move(map, 0, new(-1, 0, 1, 0), 1, 0);
        Assert.False(map.TryGetCell(-1, 0, 0, out _));
        Assert.True(map.TryGetCell(2, 0, 0, out TilemapCell2D cell));
        Assert.Equal(3, cell.quarterTurns);
        move.Undo(map);
        Assert.True(map.TryGetCell(-1, 0, 0, out cell));
        Assert.True(cell.flipX);
        TilemapStroke2D erase = TilemapEditing2D.Erase(map, 0, [new(-1, 0), new(0, 0), new(1, 0)]);
        Assert.Empty(map.chunks);
        erase.Undo(map);
        Assert.Equal(3, map.chunks.Sum(chunk => chunk.cells.Length));
    }

    [Fact]
    public void FillIsBoundedAndRepeatedValuesAreNoOps()
    {
        var map = new Tilemap2DAsset();
        map.SetLayers([new() {
            id = 0,
            name = "Ground",
            visible = true
        }]);
        TilemapCell2D value = new()
        {
            tileId = 1,
            color = Color.WHITE
        };
        TilemapStroke2D fill = TilemapEditing2D.Fill(map, 0, new(0, 0), new(-2, -1, 2, 1), value);
        Assert.Equal(15, fill.changes.Count);
        Assert.False(map.TryGetCell(3, 0, 0, out _));
        Assert.False(TilemapEditing2D.Fill(map, 0, new(0, 0), new(-2, -1, 2, 1), value).hasChanges);
        fill.Undo(map);
        Assert.Empty(map.chunks);
    }

    [Fact]
    public void ExtremeBoxesTerminateAndOverflowingMovesRemainAtomic()
    {
        var map = new Tilemap2DAsset();
        map.SetLayers([new() {
            id = 0,
            name = "Ground",
            visible = true
        }]);
        TilemapCell2D value = new() { tileId = 7, color = Color.WHITE };
        TilemapStroke2D stroke = TilemapEditing2D.Box(map, 0,
            new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), value);
        Assert.Single(stroke.changes);
        Assert.Throws<OverflowException>(() => TilemapEditing2D.Move(map, 0,
            new(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), 1, 0));
        Assert.True(map.TryGetCell(int.MaxValue, int.MaxValue, 0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => TilemapEditing2D.Box(map, 0,
            new(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue), value));
        Assert.Single(map.chunks);
    }
}
