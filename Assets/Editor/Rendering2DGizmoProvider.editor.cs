using System;
#if INNO_ENGINE_VALIDATION
using Inno.Editor.Rendering;
using Inno.Core.Mathematics;
using Inno.Scene;
#else
using InnoEditor.Rendering;
using InnoEngine.Mathematics;
using InnoEngine.Scene;
#endif

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
        const int segments = 48;
        Vector3 previous = transform.TransformPoint(new Vector3(light.range, 0f, 0f));
        for (int index = 1; index <= segments; index++)
        {
            float angle = index * (2f * MathF.PI / segments);
            Vector3 next = transform.TransformPoint(new Vector3(
                light.range * MathF.Cos(angle), light.range * MathF.Sin(angle), 0f));
            sink.Line(previous, next);
            previous = next;
        }
        if (light.kind == LightKind2D.Spot)
        {
            float halfAngle = light.spotAngle * MathF.PI / 360f;
            Vector3 center = transform.worldPosition;
            foreach (float angle in new[] { -halfAngle, halfAngle })
            {
                sink.Line(center, transform.TransformPoint(new Vector3(
                    light.range * MathF.Cos(angle), light.range * MathF.Sin(angle), 0f)));
            }
        }
    }
}
