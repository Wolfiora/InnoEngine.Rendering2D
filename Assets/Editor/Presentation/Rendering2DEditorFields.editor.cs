using System;
using InnoEditor.ImGui;

namespace Inno.Rendering2D;

internal static class Rendering2DEditorFields
{
    internal static void ContinueActions(string nextLabel)
    {
        float right = ImGui.GetCursorScreenPosition().X + ImGui.GetContentRegionAvailable().X;
        float required = ImGui.MeasureText(nextLabel).X + 28f * ImGuiWidget.style.zoom;
        if (ImGui.GetItemMaximum().X + required < right)
            ImGui.SameLine();
    }

    internal static bool Float(
        string label,
        ref float value,
        float minimum = float.MinValue,
        float maximum = float.MaxValue
    ) {
        float edited = value;
        bool changed = false;
        ImGuiWidget.PropertyRow(label, label, () => changed = ImGui.InputFloat("##value", ref edited));
        if (!changed || !float.IsFinite(edited))
            return false;
        value = Math.Clamp(edited, minimum, maximum);
        return true;
    }

    internal static bool Integer(
        string label,
        ref int value,
        int minimum = int.MinValue,
        int maximum = int.MaxValue
    ) {
        int edited = value;
        bool changed = false;
        ImGuiWidget.PropertyRow(label, label, () => changed = ImGui.InputInt("##value", ref edited));
        if (changed)
            value = Math.Clamp(edited, minimum, maximum);
        return changed;
    }

    internal static bool Text(
        string label,
        ref string value
    ) {
        string edited = value;
        bool changed = false;
        ImGuiWidget.PropertyRow(label, label, () => changed = ImGui.InputText("##value", ref edited, 512));
        value = edited;
        return changed;
    }

    internal static bool Toggle(
        string label,
        ref bool value
    ) {
        bool edited = value;
        bool changed = false;
        ImGuiWidget.PropertyRow(label, label, () => changed = ImGui.Checkbox("##value", ref edited));
        value = edited;
        return changed;
    }

    internal static bool Choice<T>(
        string label,
        ref T value
    )
        where T : struct, Enum
    {
        T edited = value;
        bool changed = false;
        ImGuiWidget.PropertyRow(label, label, () =>
        {
            if (!ImGuiWidget.BeginBoundedCombo("##value", ImGuiWidget.NicifyName(edited.ToString())))
                return;
            try
            {
                foreach (T item in Enum.GetValues<T>())
                {
                    if (!ImGui.Selectable(ImGuiWidget.NicifyName(item.ToString()), item.Equals(edited)))
                        continue;
                    edited = item;
                    changed = true;
                }
            }
            finally
            {
                ImGuiWidget.EndBoundedCombo();
            }
        });
        value = edited;
        return changed;
    }
}
