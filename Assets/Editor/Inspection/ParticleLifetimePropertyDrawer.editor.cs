using System;
using System.Linq;
using InnoEditor.ImGui;
using InnoEditor.Inspection;

using InnoEngine.Mathematics;
using ParticleEditorColor = System.Numerics.Vector4;
using ParticleEditorPoint = System.Numerics.Vector2;

namespace Inno.Rendering2D;

/// <summary>
/// Presents particle lifetime curves and gradients with sampled previews and ordered, named key controls.
/// </summary>
[PropertyDrawer(typeof(ParticleCurve2D), priority: 100)]
[PropertyDrawer(typeof(ParticleGradient2D), priority: 100)]
public sealed class ParticleLifetimePropertyDrawer : IPropertyDrawer
{
    /// <inheritdoc />
    public void Draw(PropertyDrawContext context)
    {
        if (context.propertyType == typeof(ParticleCurve2D))
            DrawCurve(context, context.GetValue() is ParticleCurve2D curve ? curve : default);
        else
            DrawGradient(context, context.GetValue() is ParticleGradient2D gradient ? gradient : default);
    }

    private static void DrawCurve(
        PropertyDrawContext context,
        ParticleCurve2D curve
    ) {
        ParticleCurveKey2D[] keys = curve.keys ?? [];
        float maximum = MathF.Max(1f, keys.Length == 0 ? 1f : keys.Max(key => float.IsFinite(key.value) ? key.value : 0f));
        ParticleEditorPoint origin = ImGui.GetCursorScreenPosition();
        ParticleEditorPoint size = new(MathF.Max(1f, ImGui.GetContentRegionAvailable().X), 72f * ImGuiWidget.style.zoom);
        ImGui.Dummy(size);
        ImGui.DrawFilledRectangle(origin, origin + size, ImGui.ToPackedColor(EditorPalette.frame));
        ParticleEditorPoint previous = origin + new ParticleEditorPoint(0, size.Y * (1f - Sample(curve, 0, maximum)));
        for (int index = 1; index <= 32; index++)
        {
            float time = index / 32f;
            ParticleEditorPoint next = origin + new ParticleEditorPoint(time * size.X, size.Y * (1f - Sample(curve, time, maximum)));
            ImGui.DrawLine(previous, next, ImGui.ToPackedColor(EditorPalette.warning), 2f);
            previous = next;
        }
        ImGuiWidget.Hint("Birth → End of lifetime");
        int selected = SelectKey(context, keys.Select(key => key.time).ToArray());
        if (selected >= 0)
        {
            float time = keys[selected].time;
            float value = keys[selected].value;
            bool changed = Rendering2DEditorFields.Float("Time", ref time, 0, 1);
            changed |= Rendering2DEditorFields.Float("Size", ref value, 0, 10000);
            if (changed)
            {
                ParticleCurveKey2D[] replacement = keys.ToArray();
                replacement[selected] = new() { time = time, value = value };
                if (replacement.Select(key => key.time).Distinct().Count() == replacement.Length)
                    context.SetValue(new ParticleCurve2D { keys = replacement.OrderBy(key => key.time).ToArray() });
            }
            if (ImGui.SmallButton("Remove Key"))
                context.SetValue(new ParticleCurve2D { keys = keys.Where((
                    _,
                    index
                ) => index != selected).ToArray() });
        }
        if (keys.Length < 64 && ImGui.SmallButton("Add Key"))
        {
            float time = NextTime(keys.Select(key => key.time).ToArray());
            context.SetValue(new ParticleCurve2D
            {
                keys = keys.Append(new() { time = time, value = curve.Evaluate(time) }).OrderBy(key => key.time).ToArray()
            });
        }
    }

    private static void DrawGradient(
        PropertyDrawContext context,
        ParticleGradient2D gradient
    ) {
        ParticleGradientKey2D[] keys = gradient.keys ?? [];
        ParticleEditorPoint origin = ImGui.GetCursorScreenPosition();
        ParticleEditorPoint size = new(MathF.Max(1f, ImGui.GetContentRegionAvailable().X), 24f * ImGuiWidget.style.zoom);
        ImGui.Dummy(size);
        for (int index = 0; index < 32; index++)
        {
            var color = gradient.Evaluate(index / 31f);
            ImGui.DrawFilledRectangle(
                origin + new ParticleEditorPoint(size.X * index / 32f, 0),
                origin + new ParticleEditorPoint(size.X * (index + 1) / 32f, size.Y),
                ImGui.ToPackedColor(new ParticleEditorColor(Display(color.r), Display(color.g), Display(color.b), float.IsFinite(color.a) ? color.a : 0f)));
        }
        int selected = SelectKey(context, keys.Select(key => key.time).ToArray());
        if (selected >= 0)
        {
            float time = keys[selected].time;
            var color = keys[selected].color;
            ParticleEditorColor edited = new(color.r, color.g, color.b, color.a);
            bool changed = Rendering2DEditorFields.Float("Time", ref time, 0, 1);
            changed |= ImGui.ColorEditLinear4("##color", ref edited);
            if (changed)
            {
                ParticleGradientKey2D[] replacement = keys.ToArray();
                replacement[selected] = new() { time = time, color = new(edited.X, edited.Y, edited.Z, edited.W) };
                if (replacement.Select(key => key.time).Distinct().Count() == replacement.Length)
                    context.SetValue(new ParticleGradient2D { keys = replacement.OrderBy(key => key.time).ToArray() });
            }
            if (ImGui.SmallButton("Remove Key"))
                context.SetValue(new ParticleGradient2D { keys = keys.Where((
                    _,
                    index
                ) => index != selected).ToArray() });
        }
        if (keys.Length < 64 && ImGui.SmallButton("Add Key"))
        {
            float time = NextTime(keys.Select(key => key.time).ToArray());
            context.SetValue(new ParticleGradient2D
            {
                keys = keys.Append(new() { time = time, color = gradient.Evaluate(time) }).OrderBy(key => key.time).ToArray()
            });
        }
    }

    private static int SelectKey(
        PropertyDrawContext context,
        float[] times
    ) {
        int selected = context.TryGetTextState("key", out string? saved) && int.TryParse(saved, out int index) ? index : 0;
        if (times.Length == 0)
        {
            ImGuiWidget.Hint("No keys · constant default");
            return -1;
        }
        selected = Math.Clamp(selected, 0, times.Length - 1);
        if (ImGuiWidget.BeginBoundedCombo("##key", $"Key {selected + 1} · {times[selected]:0.###}"))
        {
            try
            {
                for (int candidate = 0; candidate < times.Length; candidate++)
                {
                    if (!ImGui.Selectable($"Key {candidate + 1} · {times[candidate]:0.###}", candidate == selected))
                        continue;
                    selected = candidate;
                    context.SetTextState("key", selected.ToString());
                }
            }
            finally
            {
                ImGuiWidget.EndBoundedCombo();
            }
        }
        return selected;
    }

    private static float NextTime(float[] times)
    {
        if (!times.Contains(0f))
            return 0f;
        if (!times.Contains(1f))
            return 1f;
        float[] ordered = times.Order().ToArray();
        float widest = -1f;
        float result = 0.5f;
        for (int index = 1; index < ordered.Length; index++)
        {
            float width = ordered[index] - ordered[index - 1];
            if (width <= widest)
                continue;
            widest = width;
            result = ordered[index - 1] + width * 0.5f;
        }
        return result;
    }

    private static float Display(float linear)
    {
        float value = float.IsFinite(linear) ? Math.Clamp(linear, 0f, 1f) : 0f;
        return value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    }

    private static float Sample(
        ParticleCurve2D curve,
        float time,
        float maximum
    ) {
        float value = curve.Evaluate(time);
        return float.IsFinite(value) ? Math.Clamp(value / maximum, 0, 1) : 0f;
    }
}
