using System;
using System.Collections.Generic;
using System.Linq;
using InnoEngine.Mathematics;

namespace Inno.Rendering2D;

/// <summary>
/// Creates deterministic source slices while preserving identities for unchanged rectangles.
/// </summary>
public static class SpriteAtlasSlicing2D
{
    /// <summary>
    /// Replaces one source's slices with a complete-cell grid and preserves every other source.
    /// </summary>
    /// <param name="existing">
    /// Current slices, including manual authoring data.
    /// </param>
    /// <param name="sourceIndex">
    /// The source being sliced.
    /// </param>
    /// <param name="sourceName">
    /// The display-name prefix for newly created regions.
    /// </param>
    /// <param name="width">
    /// Source width in pixels.
    /// </param>
    /// <param name="height">
    /// Source height in pixels.
    /// </param>
    /// <param name="cellWidth">
    /// Positive cell width.
    /// </param>
    /// <param name="cellHeight">
    /// Positive cell height.
    /// </param>
    /// <param name="pivot">
    /// Normalized pivot for newly created slices.
    /// </param>
    /// <returns>
    /// New grid slices plus unchanged slices from other sources; partial edge cells are excluded.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Dimensions, source index, pivot or grid count exceed their bounds.
    /// </exception>
    public static SpriteAtlasSlice2D[] Grid(
        IReadOnlyList<SpriteAtlasSlice2D> existing,
        int sourceIndex,
        string sourceName,
        int width,
        int height,
        int cellWidth,
        int cellHeight,
        Vector2 pivot
    ) {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellHeight);
        if (!float.IsFinite(pivot.x) || !float.IsFinite(pivot.y) || pivot.x < 0 || pivot.x > 1 || pivot.y < 0 || pivot.y > 1)
            throw new ArgumentOutOfRangeException(nameof(pivot));
        long count = (long)(width / cellWidth) * (height / cellHeight);
        if (count > 65536)
            throw new ArgumentOutOfRangeException(nameof(cellWidth), "A grid cannot contain more than 65,536 sprites.");
        if (count == 0)
            return existing.Where(slice => slice.sourceIndex != sourceIndex).ToArray();
        var previous = existing.Where(slice => slice.sourceIndex == sourceIndex).GroupBy(slice => slice.pixelRect).ToDictionary(group => group.Key, group => group.First());
        var result = existing.Where(slice => slice.sourceIndex != sourceIndex).ToList();
        for (int y = 0; y <= height - cellHeight; y += cellHeight)
        {
            for (int x = 0; x <= width - cellWidth; x += cellWidth)
            {
                var rect = new Rect(x, y, cellWidth, cellHeight);
                if (previous.TryGetValue(rect, out SpriteAtlasSlice2D retained))
                    result.Add(retained);
                else
                    result.Add(new SpriteAtlasSlice2D {
                        id = SpriteRegionId.Create(),
                        name = $"{sourceName}_{x}_{y}",
                        sourceIndex = sourceIndex,
                        pixelRect = rect,
                        pivot = pivot,
                        outline = []
                    });
            }
        }

        return result.ToArray();
    }
}
