using InnoEngine.Events;

namespace Inno.Rendering2D;

/// <summary>
/// Identifies a named frame entered by an animator without retaining a live scene object.
/// </summary>
/// <param name="clipId">
/// Stable identity of the playing clip.
/// </param>
/// <param name="frameIndex">
/// Zero-based frame entered by playback.
/// </param>
/// <param name="eventId">
/// Nonempty gameplay event identity authored on the frame.
/// </param>
public sealed class SpriteAnimationFrameEvent2D(
    string clipId,
    int frameIndex,
    string eventId
) : Event
{
    /// <summary>
    /// Gets the stable clip identity captured at dispatch.
    /// </summary>
    public string clipId { get; } = clipId;
    /// <summary>
    /// Gets the zero-based frame captured at dispatch.
    /// </summary>
    public int frameIndex { get; } = frameIndex;
    /// <summary>
    /// Gets the authored gameplay event identity.
    /// </summary>
    public string eventId { get; } = eventId;
}
