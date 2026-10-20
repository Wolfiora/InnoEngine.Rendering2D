using System;
using InnoEditor.Interactions;

namespace Inno.Rendering2D;

/// <summary>
/// Interprets neutral, reload-safe History records for every native Rendering2D asset draft.
/// </summary>
/// <param name="documents">
/// Current document generation that resolves neutral draft records.
/// </param>
[EditorHistoryHandler(Rendering2DIds.assetDraftHistory)]
public sealed class Rendering2DAssetDraftHistoryHandler(Rendering2DAssetDocumentModule documents) : EditorHistoryHandler
{
    /// <inheritdoc />
    protected override EditorHistoryAvailability Query(
        EditorHistoryContext context,
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        ArgumentNullException.ThrowIfNull(context);
        return documents.QueryHistory(change, direction);
    }

    /// <inheritdoc />
    protected override EditorHistoryResult Apply(
        EditorHistoryContext context,
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        ArgumentNullException.ThrowIfNull(context);
        return documents.ApplyHistory(change, direction);
    }

    /// <inheritdoc />
    protected override bool TryMerge(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) => documents.TryMergeHistory(older, newer, out merged);
}
