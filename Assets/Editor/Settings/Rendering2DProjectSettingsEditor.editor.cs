using System;
using InnoEngine.Assets;
using InnoEngine.Rendering;
using InnoEngine.Settings;
using InnoEditor.ImGui;
using InnoEditor.Settings;

namespace Inno.Rendering2D;

/// <summary>
/// Provides the renderer-limit section of the unified 2D Project Settings page.
/// </summary>
[ProjectSettingPath("Project/Rendering/2D/Renderer")]
public sealed class Rendering2DProjectSettingsEditor : ProjectSettingEditor<Rendering2DProjectSettings>
{
    private string? m_pipelinePath;
    private string m_pipelineError = string.Empty;
    /// <inheritdoc />
    public override string section => "Renderer";
    /// <inheritdoc />
    public override string description => "Configure project-wide 2D rendering pipeline and batching limits.";

    /// <inheritdoc />
    protected override void OnDraw(Rendering2DProjectSettings setting)
    {
        m_pipelinePath ??= setting.pipeline?.assetPath.ToString() ?? string.Empty;
        ImGui.Text("Pipeline: " + (setting.pipeline?.name ?? "Not configured"));
        ImGuiWidget.Hint("Use the bundled default, or enter a 2D Pipeline source path.");
        _ = Rendering2DEditorFields.Text("Pipeline Source", ref m_pipelinePath);
        if (ImGui.Button("Assign Pipeline"))
        {
            try
            {
                RenderPipelineAsset candidate = Assets.Load<RenderPipelineAsset>(AssetPath.Parse(m_pipelinePath));
                if (candidate.pipelineTypeId != Rendering2DIds.pipeline)
                    throw new InvalidOperationException("Choose a 2D Pipeline.");
                setting.pipeline = candidate;
                m_pipelineError = string.Empty;
            }
            catch (Exception error)
            {
                m_pipelineError = error.Message;
            }
        }

        if (ImGui.Button("Use Plugin Default Pipeline"))
        {
            setting.pipeline = Assets.Load<RenderPipelineAsset>(Assets.LocalPath("Pipelines/Default2D.irenderpipeline"));
            m_pipelinePath = setting.pipeline.assetPath.ToString();
            m_pipelineError = string.Empty;
        }

        if (m_pipelineError.Length != 0)
            ImGui.Text(m_pipelineError);
        ImGui.Separator();
        float pixelsPerUnit = setting.defaultPixelsPerUnit;
        if (Rendering2DEditorFields.Float("Default Pixels Per Unit", ref pixelsPerUnit, 0.001f))
            setting.defaultPixelsPerUnit = pixelsPerUnit;
        int frameLimit = setting.maximumQuadsPerFrame;
        if (Rendering2DEditorFields.Integer("Maximum Quads Per Frame", ref frameLimit, 1))
            setting.maximumQuadsPerFrame = frameLimit;
        int batchLimit = setting.maximumQuadsPerBatch;
        if (Rendering2DEditorFields.Integer("Maximum Quads Per Batch", ref batchLimit, 1))
            setting.maximumQuadsPerBatch = batchLimit;
        int tiledLimit = setting.maximumTiledSpriteQuads;
        if (Rendering2DEditorFields.Integer("Maximum Tiled Sprite Quads", ref tiledLimit, 1))
            setting.maximumTiledSpriteQuads = tiledLimit;
    }
}

/// <summary>
/// Provides the sorting-layer section of the unified 2D Project Settings page.
/// </summary>
[ProjectSettingPath("Project/Rendering/2D/Sorting Layers", order: 100)]
public sealed class Rendering2DSortingLayersProjectSettingsEditor : ProjectSettingEditor<Rendering2DProjectSettings>
{
    private string m_newName = "Layer";
    private int m_newOrder;
    /// <inheritdoc />
    public override string section => "Sorting Layers";
    /// <inheritdoc />
    public override string description => "Configure sorting-layer names and order. IDs are generated automatically as projectId.name.";

    /// <inheritdoc />
    protected override void OnDraw(Rendering2DProjectSettings setting)
    {
        var layers = new System.Collections.Generic.List<SortingLayer2DDefinition>(setting.sortingLayers);
        bool changed = false;
        for (int index = 0; index < layers.Count; index++)
        {
            SortingLayer2DDefinition layer = layers[index];
            ImGui.PushId(index);
            string name = layer.name ?? string.Empty;
            int order = layer.order;
            changed |= Rendering2DEditorFields.Text("Name", ref name);
            changed |= Rendering2DEditorFields.Integer("Order", ref order);
            if (index > 0 && ImGui.Button("Remove"))
            {
                layers.RemoveAt(index--);
                changed = true;
            }
            else
            {
                layers[index] = layer.With(name, order);
            }

            ImGui.PopId();
            ImGui.Separator();
        }

        if (changed)
            TryApplyLayers(setting, layers);
        _ = Rendering2DEditorFields.Text("New Name", ref m_newName);
        _ = Rendering2DEditorFields.Integer("New Order", ref m_newOrder);
        if (ImGui.Button("Add Sorting Layer") && !string.IsNullOrWhiteSpace(m_newName))
        {
            layers.Add(new SortingLayer2DDefinition(m_newName, m_newOrder));
            TryApplyLayers(setting, layers);
        }
    }

    private static void TryApplyLayers(
        Rendering2DProjectSettings setting,
        System.Collections.Generic.IEnumerable<SortingLayer2DDefinition> layers
    ) {
        try
        {
            setting.SetSortingLayers(layers);
        }
        catch (ArgumentException exception)
        {
            ImGui.Text(exception.Message);
        }
    }
}
