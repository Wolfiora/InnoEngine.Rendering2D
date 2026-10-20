using System;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using InnoEngine.Assets;

namespace Inno.Rendering2D;

/// <summary>
/// Registers stable document ownership; typed drafts hydrate only after candidate publication.
/// </summary>
/// <param name="interactions">
/// Editor-owned documents, History and selection coordinator.
/// </param>
/// <param name="previews">
/// Editor-owned generation-scoped preview service.
/// </param>
[EditorModule("rendering2d.asset-documents", order: 400)]
public sealed class Rendering2DAssetDocumentModule(
    EditorInteractions interactions,
    IEditorPreviewService previews
) : EditorModule
{
    private IDisposable? m_registration;
    private Rendering2DAssetDocumentProvider? m_provider;

    internal void DrawAnimationWorkspace(
        string path,
        Guid assetId
    ) {
        if (m_provider is null)
            return;
        EditorDocumentContext document = interactions.documents.Open(path, assetId);
        m_provider.DrawInspectorHeader(document);
        interactions.TryGetModule<Rendering2DSpritePreviewModule>(out var thumbnails);
        m_provider.DrawAnimationWorkspace(document, thumbnails);
    }

    internal void DrawInspectorHeader(
        InspectionDrawContext context,
        AssetObject asset
    ) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(asset);
        Rendering2DAssetDocumentProvider provider = m_provider ?? throw new InvalidOperationException("The 2D asset document provider is unavailable.");
        EditorDocumentContext document = interactions.documents.Open(asset.assetPath.ToString(), asset.identity.persistentId);
        provider.DrawInspectorHeader(document);
    }

    internal void DrawInspectorHeader(
        InspectionDrawContext context,
        AssetFileEntry source
    ) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        Rendering2DAssetDocumentProvider provider = m_provider ?? throw new InvalidOperationException("The 2D asset document provider is unavailable.");
        EditorDocumentContext document = interactions.documents.Open(source.assetPath.ToString(), Guid.Empty);
        provider.DrawInspectorHeader(document);
    }

    internal void DrawInspector(
        InspectionDrawContext context,
        AssetObject asset
    ) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(asset);
        Rendering2DAssetDocumentProvider provider = m_provider ?? throw new InvalidOperationException("The 2D asset document provider is unavailable.");
        EditorDocumentContext document = interactions.documents.Open(asset.assetPath.ToString(), asset.identity.persistentId);
        provider.DrawInspector(document, context);
    }

    internal void DrawInspector(
        InspectionDrawContext context,
        AssetFileEntry source
    ) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        Rendering2DAssetDocumentProvider provider = m_provider ?? throw new InvalidOperationException("The 2D asset document provider is unavailable.");
        EditorDocumentContext document = interactions.documents.Open(source.assetPath.ToString(), Guid.Empty);
        provider.DrawInspector(document, context);
    }

    internal EditorHistoryAvailability QueryHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) => m_provider?.QueryHistory(change, direction) ?? EditorHistoryAvailability.Unavailable("The 2D asset draft provider is reloading.");
    internal EditorHistoryResult ApplyHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) => m_provider?.ApplyHistory(change, direction) ?? EditorHistoryResult.Failure("The 2D asset draft provider is reloading.");
    internal bool TryMergeHistory(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) => Rendering2DAssetDocumentProvider.TryMergeHistory(older, newer, out merged);
    /// <inheritdoc />
    protected override void OnStart(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        m_provider = new Rendering2DAssetDocumentProvider(interactions.documents, previews, interactions.history);
        m_registration = interactions.documents.RegisterProvider(m_provider);
    }

    /// <inheritdoc />
    protected override void OnUpdate(EditorContext context) => m_provider?.CompleteGestures();

    /// <inheritdoc />
    protected override void OnStop(EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        m_provider?.RetireGestures();
        m_registration?.Dispose();
        m_registration = null;
        m_provider = null;
    }

    /// <inheritdoc />
    protected override void OnDispose()
    {
        m_registration?.Dispose();
        m_registration = null;
        m_provider = null;
    }
}
