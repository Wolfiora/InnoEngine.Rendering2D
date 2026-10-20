using System;
using System.Linq;
using InnoEditor.Assets;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Rendering;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider
{
    private sealed class ParticleDraft(ParticleEffect2DAsset asset) : Draft
    {
        private SpriteReference2D m_sprite = asset.sprite;
        private SpriteReference2D[] m_flipbookFrames = asset.flipbookFrames.ToArray();
        private float m_flipbookFramesPerSecond = asset.flipbookFramesPerSecond;
        private MaterialAsset? m_material = asset.material;
        private SpriteBlendMode2D m_blendMode = asset.blendMode;
        private SpriteSamplingMode2D m_sampling = asset.sampling;
        private ParticleEmitterShape2D m_shape = asset.shape;
        private ParticleSimulationSpace2D m_simulationSpace = asset.simulationSpace;
        private int m_maximumParticles = asset.maximumParticles;
        private float m_emissionRate = asset.emissionRate;
        private float m_minimumLifetime = asset.minimumLifetime;
        private float m_maximumLifetime = asset.maximumLifetime;
        private float m_minimumSpeed = asset.minimumSpeed;
        private float m_maximumSpeed = asset.maximumSpeed;
        private InnoEngine.Mathematics.Vector2 m_shapeSize = asset.shapeSize;
        private float m_coneAngle = asset.coneAngle;
        private InnoEngine.Mathematics.Vector2 m_gravity = asset.gravity;
        private float m_noiseStrength = asset.noiseStrength;
        private ParticleCurve2D m_sizeOverLifetime = Clone(asset.sizeOverLifetime);
        private ParticleGradient2D m_colorOverLifetime = Clone(asset.colorOverLifetime);

        internal override byte[] Capture() => EditorAssets.EncodeNative(CreateAsset(normalize: false));

        internal override void Restore(byte[] bytes)
        {
            ParticleEffect2DAsset restored = EditorAssets.DecodeNative<ParticleEffect2DAsset>(bytes);
            m_sprite = restored.sprite;
            m_flipbookFrames = restored.flipbookFrames.ToArray();
            m_flipbookFramesPerSecond = restored.flipbookFramesPerSecond;
            m_material = restored.material;
            m_blendMode = restored.blendMode;
            m_sampling = restored.sampling;
            m_shape = restored.shape;
            m_simulationSpace = restored.simulationSpace;
            m_maximumParticles = restored.maximumParticles;
            m_emissionRate = restored.emissionRate;
            m_minimumLifetime = restored.minimumLifetime;
            m_maximumLifetime = restored.maximumLifetime;
            m_minimumSpeed = restored.minimumSpeed;
            m_maximumSpeed = restored.maximumSpeed;
            m_shapeSize = restored.shapeSize;
            m_coneAngle = restored.coneAngle;
            m_gravity = restored.gravity;
            m_noiseStrength = restored.noiseStrength;
            m_sizeOverLifetime = Clone(restored.sizeOverLifetime);
            m_colorOverLifetime = Clone(restored.colorOverLifetime);
        }

        internal override bool Draw(IEditorPreviewService previews)
        {
            ImGui.SeparatorText("Emission");
            bool changed = ImGui.InputInt("Maximum Particles", ref m_maximumParticles);
            changed |= ImGui.InputFloat("Emission Rate", ref m_emissionRate);
            ImGui.SeparatorText("Lifetime and Motion");
            changed |= ImGui.InputFloat("Minimum Lifetime", ref m_minimumLifetime);
            changed |= ImGui.InputFloat("Maximum Lifetime", ref m_maximumLifetime);
            changed |= ImGui.InputFloat("Minimum Speed", ref m_minimumSpeed);
            changed |= ImGui.InputFloat("Maximum Speed", ref m_maximumSpeed);
            changed |= ImGui.InputFloat("Noise Strength", ref m_noiseStrength);
            changed |= ImGui.InputFloat("Flipbook FPS", ref m_flipbookFramesPerSecond);
            ImGui.Text($"Emitter: {m_shape}   Space: {m_simulationSpace}   Flipbook frames: {m_flipbookFrames.Length}");
            return changed;
        }

        internal override void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            if (ImGuiWidget.SectionHeader("Rendering", "Sprite, flipbook, Material, blending, and sampling define particle appearance."))
            {
                Property("sprite", "Sprite", () => m_sprite, value => m_sprite = value);
                Property("flipbookFrames", "Flipbook Frames", () => m_flipbookFrames, value => m_flipbookFrames = value ?? []);
                Property("flipbookFramesPerSecond", "Flipbook FPS", () => m_flipbookFramesPerSecond, value => m_flipbookFramesPerSecond = value, minimum: 0);
                Property<MaterialAsset?>("material", "Material", () => m_material, value => m_material = value);
                Property("blendMode", "Blend Mode", () => m_blendMode, value => m_blendMode = value);
                Property("sampling", "Sampling", () => m_sampling, value => m_sampling = value);
            }

            if (ImGuiWidget.SectionHeader("Emitter", "Emitter shape, capacity, rate, and simulation space define where particles begin."))
            {
                Property("shape", "Shape", () => m_shape, value => m_shape = value);
                Property("simulationSpace", "Simulation Space", () => m_simulationSpace, value => m_simulationSpace = value);
                Property("maximumParticles", "Maximum Particles", () => m_maximumParticles, value => m_maximumParticles = value, minimum: 1);
                Property("emissionRate", "Emission Rate", () => m_emissionRate, value => m_emissionRate = value, minimum: 0);
                Property("shapeSize", "Shape Size", () => m_shapeSize, value => m_shapeSize = value);
                Property("coneAngle", "Cone Angle", () => m_coneAngle, value => m_coneAngle = value, minimum: 0, maximum: 360);
            }

            if (ImGuiWidget.SectionHeader("Lifetime and Motion", "Lifetime, speed, gravity, and noise define deterministic particle movement."))
            {
                Property("minimumLifetime", "Minimum Lifetime", () => m_minimumLifetime, value => m_minimumLifetime = value, minimum: 0.0001);
                Property("maximumLifetime", "Maximum Lifetime", () => m_maximumLifetime, value => m_maximumLifetime = value, minimum: 0.0001);
                Property("minimumSpeed", "Minimum Speed", () => m_minimumSpeed, value => m_minimumSpeed = value);
                Property("maximumSpeed", "Maximum Speed", () => m_maximumSpeed, value => m_maximumSpeed = value);
                Property("gravity", "Gravity", () => m_gravity, value => m_gravity = value);
                Property("noiseStrength", "Noise Strength", () => m_noiseStrength, value => m_noiseStrength = value);
            }

            if (ImGuiWidget.SectionHeader("Over Lifetime", "Curves and gradients evolve particle size and color over normalized lifetime."))
            {
                Property("sizeOverLifetime", "Size", () => m_sizeOverLifetime, value => m_sizeOverLifetime = value);
                Property("colorOverLifetime", "Color", () => m_colorOverLifetime, value => m_colorOverLifetime = value);
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
                    "particle." + path,
                    label,
                    typeof(T),
                    () => getter(),
                    value => setter(value is T typed ? typed : value is null && default(T) is null ? default! : throw new InvalidOperationException($"'{path}' received an incompatible value.")),
                    edits,
                    readOnly,
                    minimum: minimum,
                    maximum: maximum
                );
            }
        }

        internal override bool Save(AssetPath path)
        {
            ParticleEffect2DAsset value = CreateAsset(normalize: true);
            try
            {
                value.Validate();
            }
            catch (InvalidOperationException error)
            {
                throw new ArgumentException(error.Message, nameof(path), error);
            }
            return Rendering2DAssets.SaveParticleEffect(path, value);
        }

        private ParticleEffect2DAsset CreateAsset(bool normalize)
        {
            float minimumLifetime = normalize ? MathF.Max(0.0001f, m_minimumLifetime) : m_minimumLifetime;
            float minimumSpeed = m_minimumSpeed;
            return new ParticleEffect2DAsset
            {
                sprite = m_sprite,
                flipbookFrames = m_flipbookFrames.ToArray(),
                flipbookFramesPerSecond = normalize ? MathF.Max(0f, m_flipbookFramesPerSecond) : m_flipbookFramesPerSecond,
                material = m_material,
                blendMode = m_blendMode,
                sampling = m_sampling,
                shape = m_shape,
                simulationSpace = m_simulationSpace,
                maximumParticles = normalize ? Math.Max(1, m_maximumParticles) : m_maximumParticles,
                emissionRate = normalize ? MathF.Max(0f, m_emissionRate) : m_emissionRate,
                minimumLifetime = minimumLifetime,
                maximumLifetime = normalize ? MathF.Max(minimumLifetime, m_maximumLifetime) : m_maximumLifetime,
                minimumSpeed = minimumSpeed,
                maximumSpeed = normalize ? MathF.Max(minimumSpeed, m_maximumSpeed) : m_maximumSpeed,
                shapeSize = m_shapeSize,
                coneAngle = normalize ? Math.Clamp(m_coneAngle, 0f, 360f) : m_coneAngle,
                gravity = m_gravity,
                noiseStrength = m_noiseStrength,
                sizeOverLifetime = Clone(m_sizeOverLifetime),
                colorOverLifetime = Clone(m_colorOverLifetime)
            };
        }

        private static ParticleCurve2D Clone(ParticleCurve2D value)
        {
            value.keys = value.keys?.ToArray() ?? [];
            return value;
        }

        private static ParticleGradient2D Clone(ParticleGradient2D value)
        {
            value.keys = value.keys?.ToArray() ?? [];
            return value;
        }
    }
}
