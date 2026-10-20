using System;
using System.Collections.Generic;
using System.Linq;

namespace Inno.Rendering2D;

/// <summary>
/// Applies deterministic copy-on-edit operations to sprite clips without touching live scene playback.
/// </summary>
public static class SpriteAnimationEditing2D
{
    /// <summary>
    /// Finds the frame containing a finite time, using half-open frame intervals.
    /// </summary>
    /// <param name="frames">
    /// Ordered frames with finite positive durations.
    /// </param>
    /// <param name="time">
    /// Finite clip-local time; negative values select the first frame and the end selects the final frame.
    /// </param>
    /// <returns>
    /// The frame index, or minus one for an empty clip.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A frame has an invalid duration.
    /// </exception>
    public static int FrameAt(
        IReadOnlyList<SpriteAnimationFrame2D> frames,
        float time
    ) {
        ArgumentNullException.ThrowIfNull(frames);
        if (!float.IsFinite(time))
            throw new ArgumentOutOfRangeException(nameof(time));
        _ = Duration(frames);
        float cursor = 0f;
        for (int index = 0; index < frames.Count; index++)
        {
            cursor += frames[index].duration;
            if (MathF.Max(0f, time) < cursor)
                return index;
        }

        return frames.Count - 1;
    }

    /// <summary>
    /// Computes clip length after rejecting invalid frame times.
    /// </summary>
    /// <param name="frames">
    /// Ordered frames.
    /// </param>
    /// <returns>
    /// The finite nonnegative total length in seconds.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// A frame time or total length is not finite and positive.
    /// </exception>
    public static float Duration(IReadOnlyList<SpriteAnimationFrame2D> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        float duration = 0f;
        foreach (SpriteAnimationFrame2D frame in frames)
        {
            ValidateDuration(frame.duration);
            duration += frame.duration;
        }

        if (!float.IsFinite(duration))
            throw new ArgumentException("The animation is too long.", nameof(frames));
        return duration;
    }

    /// <summary>
    /// Copies a frame to the following slot without aliasing the source array.
    /// </summary>
    /// <param name="frames">
    /// Ordered source frames.
    /// </param>
    /// <param name="index">
    /// Frame to duplicate.
    /// </param>
    /// <returns>
    /// A new array containing both copies.
    /// </returns>
    public static SpriteAnimationFrame2D[] Duplicate(
        IReadOnlyList<SpriteAnimationFrame2D> frames,
        int index
    ) {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= frames.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var result = frames.ToList();
        result.Insert(index + 1, frames[index]);
        return result.ToArray();
    }

    /// <summary>
    /// Moves a frame while retaining its duration, sprite and event identity.
    /// </summary>
    /// <param name="frames">
    /// Ordered source frames.
    /// </param>
    /// <param name="from">
    /// Existing frame index.
    /// </param>
    /// <param name="to">
    /// Final index in the returned array.
    /// </param>
    /// <returns>
    /// A reordered copy; the source remains unchanged.
    /// </returns>
    public static SpriteAnimationFrame2D[] Move(
        IReadOnlyList<SpriteAnimationFrame2D> frames,
        int from,
        int to
    ) {
        ArgumentNullException.ThrowIfNull(frames);
        if (from < 0 || from >= frames.Count || to < 0 || to >= frames.Count)
            throw new ArgumentOutOfRangeException(nameof(from));
        var result = frames.ToList();
        SpriteAnimationFrame2D value = result[from];
        result.RemoveAt(from);
        result.Insert(to, value);
        return result.ToArray();
    }

    private static void ValidateDuration(float duration)
    {
        if (!float.IsFinite(duration) || duration <= 0f)
            throw new ArgumentException("Frame durations must be finite and greater than zero.");
    }
}
