using System;
using System.Linq;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Rendering;
using InnoEngine.Assets;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed class PostProcessDraft(PostProcessProfile2DAsset asset) : Draft
    {
        private float m_exposure = asset.exposure;
        private float m_contrast = asset.contrast;
        private float m_saturation = asset.saturation;
        private bool m_toneMapping = asset.toneMapping;
        private float m_bloomIntensity = asset.bloomIntensity;
        private float m_bloomThreshold = asset.bloomThreshold;
        private int m_bloomLevels = asset.bloomLevels;
        private float m_bloomScatter = asset.bloomScatter;
        private float m_vignette = asset.vignette;
        private int m_pixelation = asset.pixelation;

        internal override byte[] Capture() => EditorAssets.EncodeNative(CreateAsset(normalize: false));

        internal override void Restore(byte[] bytes)
        {
            PostProcessProfile2DAsset restored = EditorAssets.DecodeNative<PostProcessProfile2DAsset>(bytes);
            m_exposure = restored.exposure;
            m_contrast = restored.contrast;
            m_saturation = restored.saturation;
            m_toneMapping = restored.toneMapping;
            m_bloomIntensity = restored.bloomIntensity;
            m_bloomThreshold = restored.bloomThreshold;
            m_bloomLevels = restored.bloomLevels;
            m_bloomScatter = restored.bloomScatter;
            m_vignette = restored.vignette;
            m_pixelation = restored.pixelation;
        }

        internal override bool Draw(IEditorPreviewService previews)
        {
            ImGui.SeparatorText("Color and Tone");
            bool changed = ImGui.InputFloat("Exposure", ref m_exposure);
            changed |= ImGui.InputFloat("Contrast", ref m_contrast);
            changed |= ImGui.InputFloat("Saturation", ref m_saturation);
            changed |= ImGui.Checkbox("Tone Mapping", ref m_toneMapping);
            ImGui.SeparatorText("Effects");
            changed |= ImGui.InputFloat("Bloom Intensity", ref m_bloomIntensity);
            changed |= ImGui.InputFloat("Bloom Threshold", ref m_bloomThreshold);
            changed |= ImGui.InputInt("Bloom Levels", ref m_bloomLevels);
            changed |= ImGui.SliderFloat("Bloom Scatter", ref m_bloomScatter, 0f, 1f);
            changed |= ImGui.SliderFloat("Vignette", ref m_vignette, 0f, 1f);
            changed |= ImGui.InputInt("Pixelation", ref m_pixelation);
            return changed;
        }

        internal override void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            if (ImGuiWidget.SectionHeader("Color and Tone", "Exposure, contrast, saturation, and tone mapping shape the complete camera result."))
            {
                Property("exposure", "Exposure", () => m_exposure, value => m_exposure = value);
                Property("contrast", "Contrast", () => m_contrast, value => m_contrast = value, minimum: 0.0001);
                Property("saturation", "Saturation", () => m_saturation, value => m_saturation = value, minimum: 0);
                Property("toneMapping", "Tone Mapping", () => m_toneMapping, value => m_toneMapping = value);
            }

            if (ImGuiWidget.SectionHeader("Bloom", "Bloom extracts bright pixels and spreads them through a bounded downsample pyramid."))
            {
                Property("bloomIntensity", "Intensity", () => m_bloomIntensity, value => m_bloomIntensity = value, minimum: 0);
                Property("bloomThreshold", "Threshold", () => m_bloomThreshold, value => m_bloomThreshold = value, minimum: 0);
                Property("bloomLevels", "Levels", () => m_bloomLevels, value => m_bloomLevels = value, minimum: 1, maximum: 8);
                Property("bloomScatter", "Scatter", () => m_bloomScatter, value => m_bloomScatter = value, minimum: 0, maximum: 1);
            }

            if (ImGuiWidget.SectionHeader("Screen Effects", "These operations are evaluated after color and bloom composition."))
            {
                Property("vignette", "Vignette", () => m_vignette, value => m_vignette = value, minimum: 0, maximum: 1);
                Property("pixelation", "Pixelation", () => m_pixelation, value => m_pixelation = value, minimum: 1);
            }

            void Property<T>(
                string path,
                string label,
                Func<T> getter,
                Action<T> setter,
                double? minimum = null,
                double? maximum = null
            ) {
                context.properties.DrawValue(
                    context.editorContext,
                    this,
                    "post-process." + path,
                    label,
                    typeof(T),
                    () => getter(),
                    value => setter(value is T typed ? typed : throw new InvalidOperationException($"'{path}' received an incompatible value.")),
                    edits,
                    readOnly,
                    minimum: minimum,
                    maximum: maximum
                );
            }
        }

        internal override bool Save(AssetPath path)
        {
            if (!float.IsFinite(m_exposure) || !float.IsFinite(m_contrast) || !float.IsFinite(m_saturation)
                || !float.IsFinite(m_bloomIntensity) || !float.IsFinite(m_bloomThreshold)
                || !float.IsFinite(m_bloomScatter) || !float.IsFinite(m_vignette))
                throw new ArgumentException("Post-processing values must be finite.", nameof(path));
            return Rendering2DAssets.SavePostProcess(path, CreateAsset(normalize: true));
        }

        private PostProcessProfile2DAsset CreateAsset(bool normalize) => new()
        {
            exposure = m_exposure,
            contrast = normalize ? MathF.Max(0.0001f, m_contrast) : m_contrast,
            saturation = normalize ? MathF.Max(0f, m_saturation) : m_saturation,
            toneMapping = m_toneMapping,
            bloomIntensity = normalize ? MathF.Max(0f, m_bloomIntensity) : m_bloomIntensity,
            bloomThreshold = normalize ? MathF.Max(0f, m_bloomThreshold) : m_bloomThreshold,
            bloomLevels = normalize ? Math.Clamp(m_bloomLevels, 1, 8) : m_bloomLevels,
            bloomScatter = normalize ? Math.Clamp(m_bloomScatter, 0f, 1f) : m_bloomScatter,
            vignette = normalize ? Math.Clamp(m_vignette, 0f, 1f) : m_vignette,
            pixelation = normalize ? Math.Max(1, m_pixelation) : m_pixelation
        };
    }
}
