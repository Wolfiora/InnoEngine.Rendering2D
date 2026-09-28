using System;
using InnoEditor.Rendering;
using InnoEngine.Mathematics;
using InnoEngine.Scene;

namespace Inno.Rendering2D;

/// <summary>Contributes selectable 2D camera and light icons with selected influence outlines.</summary>
[EditorGizmoProviderExtension("inno.rendering2d.scene-gizmos")]
public sealed class Rendering2DGizmoProvider : EditorGizmoProvider
{
    /// <inheritdoc />
    public override void Collect(EditorGizmoContext context, IEditorGizmoSink sink)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sink);
        foreach (GameScene scene in context.content.GetValues<GameScene>())
        {
            if (scene.isDestroyed)
                continue;
            foreach (GameObject owner in scene.GetObjects())
            {
                if (!owner.activeInHierarchy)
                    continue;
                bool selected = owner.identity.runtimeIdentity == context.selected;
                if (owner.TryGetComponent(out Camera2D? camera) && camera is { isActiveAndEnabled: true })
                {
                    sink.Icon(owner.identity, owner.transform.worldPosition, "camera");
                    if (selected)
                        DrawCamera(camera, context, sink);
                }
                if (owner.TryGetComponent(out Light2D? light) && light is { isActiveAndEnabled: true })
                {
                    sink.Icon(owner.identity, owner.transform.worldPosition, "light");
                    if (selected)
                        DrawLight(light, sink);
                }
            }
        }
    }

    private static void DrawCamera(Camera2D camera, EditorGizmoContext context, IEditorGizmoSink sink)
    {
        float halfHeight = camera.orthographicSize;
        float aspect = (float)context.pixelWidth / Math.Max(1, context.pixelHeight);
        float halfWidth = halfHeight * aspect;
        Vector3[] corners =
        [
            camera.gameObject.transform.TransformPoint(new Vector3(-halfWidth, -halfHeight, 0f)),
            camera.gameObject.transform.TransformPoint(new Vector3(halfWidth, -halfHeight, 0f)),
            camera.gameObject.transform.TransformPoint(new Vector3(halfWidth, halfHeight, 0f)),
            camera.gameObject.transform.TransformPoint(new Vector3(-halfWidth, halfHeight, 0f))
        ];
        for (int index = 0; index < corners.Length; index++)
            sink.Line(corners[index], corners[(index + 1) % corners.Length]);
    }

    private static void DrawLight(Light2D light, IEditorGizmoSink sink)
    {
        if (light.kind == LightKind2D.Global)
            return;
        Transform transform = light.gameObject.transform;
        if (light.kind == LightKind2D.Freeform && light.shape.Length > 1)
        {
            for (int index = 0; index < light.shape.Length; index++)
            {
                Vector2 start = light.shape[index];
                Vector2 end = light.shape[(index + 1) % light.shape.Length];
                sink.Line(transform.TransformPoint(new Vector3(start.x, start.y, 0f)),
                    transform.TransformPoint(new Vector3(end.x, end.y, 0f)));
            }
            return;
        }
        if (light.kind == LightKind2D.Spot)
        {
            float halfAngle = light.spotAngle * MathF.PI / 360f;
            Vector3 center = transform.worldPosition;
            Vector3 forward = transform.TransformPoint(new Vector3(0f, 1f, 0f)) - center;
            float length = MathF.Sqrt(forward.x * forward.x + forward.y * forward.y);
            if (length <= 0.000001f)
            {
                forward = Vector3.Transform(new Vector3(0f, 1f, 0f), transform.worldRotation);
                length = MathF.Sqrt(forward.x * forward.x + forward.y * forward.y);
                if (length <= 0.000001f)
                    return;
            }
            float directionX = forward.x / length;
            float directionY = forward.y / length;
            const int arcSegments = 32;
            Vector3 start = SpotArcPoint(center, directionX, directionY, light.range, -halfAngle);
            Vector3 previous = start;
            for (int index = 1; index <= arcSegments; index++)
            {
                float angle = -halfAngle + index * (2f * halfAngle / arcSegments);
                Vector3 next = SpotArcPoint(center, directionX, directionY, light.range, angle);
                sink.Line(previous, next);
                previous = next;
            }
            sink.Line(center, start);
            sink.Line(center, previous);
            return;
        }
        const int segments = 48;
        Vector3 origin = transform.worldPosition;
        Vector3 circlePrevious = origin + new Vector3(light.range, 0f, 0f);
        for (int index = 1; index <= segments; index++)
        {
            float angle = index * (2f * MathF.PI / segments);
            Vector3 next = origin + new Vector3(
                light.range * MathF.Cos(angle), light.range * MathF.Sin(angle), 0f);
            sink.Line(circlePrevious, next);
            circlePrevious = next;
        }
    }

    private static Vector3 SpotArcPoint(
        Vector3 center, float directionX, float directionY, float range, float angle)
        => center + new Vector3(
            range * (directionX * MathF.Cos(angle) - directionY * MathF.Sin(angle)),
            range * (directionX * MathF.Sin(angle) + directionY * MathF.Cos(angle)), 0f);
}
