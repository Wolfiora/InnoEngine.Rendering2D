using System;
using System.IO;
using System.Numerics;
using Inno.Adapter.Modules.DotNet;
using Inno.Core.Identity;
using Inno.Core.Input;
using Inno.Core.Logging;
using Inno.Editor.Core;
using Inno.Editor.Interactions;
using Inno.Editor.Rendering;
using Inno.Extensibility.Modules;
using Inno.Extensibility.Types;
using Inno.Rendering;
using Inno.Rendering.Assets;
using Xunit;

namespace Inno.Rendering2D.Tests;

public sealed class DocumentLifecycleTests
{
    [Fact]
    public void ProviderRegistrationPreservesNeutralDraftsWithoutReadingCandidateAssets()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Rendering2D-Documents-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        try
        {
            using var modules = new ModuleHost(new ModuleHostOptions
            {
                catalogSource = new DotNetAssemblyCatalogSource(typeof(DocumentLifecycleTests).Assembly)
            });
            using var types = new TypeCatalog(modules, new ReflectionTypeCatalogSource());
            using var logs = new LogRouter();
            var identities = new IdentityAllocator();
            var context = new EditorContext(root, new EditorKeyboardPolicy(KeyModifier.Control, "Ctrl"));
            using var runtime = new EditorInteractionRuntime(context, types, logs, [types, identities]);
            runtime.Start();
            IEditorDocumentService documents = runtime.interactions.documents;
            IDisposable placeholder = documents.RegisterProvider(new PendingProvider());
            EditorDocumentContext retained = documents.Open("Assets/Flight.ispriteanimation2d", Guid.NewGuid());
            retained.SetViewParameter("draft", "frozen neutral draft");
            retained.SetViewParameter("baseline", "frozen neutral baseline");
            documents.SetDirty(retained.documentId);
            placeholder.Dispose();

            // There is deliberately no Asset Pipeline or native UI context during registry activation.
            using (var first = new Rendering2DAssetDocumentModule(runtime.interactions, new UnavailablePreviews()))
            {
                first.Start(context);
                Assert.True(retained.isProviderAvailable);
                Assert.True(retained.isDirty);
                Assert.Equal("frozen neutral draft", retained.viewParameters["draft"]);
                Assert.Equal("Flight", retained.title);
                _ = documents.Open("Assets/Map.itilemap2d", Guid.NewGuid());
                first.Stop(context);
                Assert.False(retained.isProviderAvailable);
            }

            using var replacement = new Rendering2DAssetDocumentModule(runtime.interactions, new UnavailablePreviews());
            replacement.Start(context);
            Assert.True(retained.isProviderAvailable);
            Assert.Equal(2, documents.documents.Count);
            Assert.Equal("frozen neutral baseline", retained.viewParameters["baseline"]);
            replacement.Stop(context);
        }
        finally
        {
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
            Assert.StartsWith(temporaryRoot, root, StringComparison.OrdinalIgnoreCase);
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class PendingProvider : EditorDocumentProvider
    {
        public override string id => Rendering2DIds.assetDocumentProvider;
        public override bool CanOpen(string assetPath) => assetPath.EndsWith(".ispriteanimation2d", StringComparison.Ordinal);
        public override bool Save(EditorDocumentContext context) => throw new InvalidOperationException("Candidate assets are unavailable.");
    }

    private sealed class UnavailablePreviews : IEditorPreviewService
    {
        public uint deviceGeneration => throw Unavailable();
        public bool TryRender(
            EditorViewportComposition composition,
            out EditorPreviewHandle handle
        ) => throw Unavailable();
        public void ReleaseRendered(string viewportId) => throw Unavailable();
        public bool TryGetTexture(
            TextureAsset texture,
            out EditorPreviewHandle handle
        ) => throw Unavailable();
        public bool TryGetTextureArtifact(
            RenderTextureArtifactReference texture,
            int pixelWidth,
            int pixelHeight,
            out EditorPreviewHandle handle
        ) => throw Unavailable();
        public void Draw(
            EditorPreviewHandle handle,
            Vector2 logicalSize
        ) => throw Unavailable();
        public bool Release(EditorPreviewHandle handle) => throw Unavailable();
        public void ReleaseAll() => throw Unavailable();
        private static InvalidOperationException Unavailable() => new("A candidate registration must not use presentation services.");
    }
}
