using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using InnoEditor.Assets;
using InnoEditor.Core;
using InnoEditor.ImGui;
using InnoEditor.Inspection;
using InnoEditor.Interactions;
using InnoEditor.Rendering;
using InnoEngine.Assets;
using InnoEngine.Core;
using InnoEngine.Rendering;

namespace Inno.Rendering2D;

internal sealed partial class Rendering2DAssetDocumentProvider(
    IEditorDocumentService documents,
    IEditorPreviewService previews,
    IEditorHistory history
) : EditorDocumentProvider
{
    private static readonly string[] S_EXTENSIONS = [".ispriteatlas2d", ".ispriteanimation2d", ".itileset2d", ".itilemap2d", ".ipostprocess2d", ".iparticle2d"];
    private readonly Dictionary<Guid, Draft> m_drafts = [];
    private readonly Dictionary<Guid, byte[]> m_baselines = [];
    private readonly Dictionary<Guid, byte[]> m_snapshots = [];
    private readonly Dictionary<Guid, string> m_errors = [];

    public override string id => Rendering2DIds.assetDocumentProvider;

    public override bool CanOpen(string assetPath) => Supports(Path.GetExtension(assetPath));
    internal void CompleteGestures(bool force = false)
    {
        if (!m_drafts.Values.Any(draft => draft.isEditing))
            return;
        foreach (EditorDocumentContext document in documents.documents)
        {
            if (m_drafts.TryGetValue(document.documentId, out Draft? draft)
                && (force || Time.frameCount > draft.lastDrawnFrame + 1)
                && draft.CompleteGesture() is byte[] before)
                RecordApplied(document, draft, before, "Edit 2D Asset Gesture");
        }
    }

    internal void RetireGestures()
    {
        // The candidate may already own the type cache here. Retire with frozen bytes only.
        foreach (EditorDocumentContext context in documents.documents)
        {
            if (!m_drafts.TryGetValue(context.documentId, out Draft? draft)
                || draft.TakeGestureBefore() is not byte[] before)
                continue;
            byte[] after = m_snapshots[context.documentId];
            if (before.AsSpan().SequenceEqual(after))
                continue;
            var data = new ChangeData(context.documentId, context.assetPath, before, after);
            var change = new EditorHistoryChange(Rendering2DIds.assetDraftHistory, EditorHistoryPayload.FromBytes(EncodeChange(data)));
            try
            {
                history.RecordApplied("Edit 2D Asset Gesture", change);
            }
            catch
            {
                change.Dispose();
                throw;
            }
        }
    }

    internal void DrawAnimationWorkspace(
        EditorDocumentContext context,
        Rendering2DSpritePreviewModule? thumbnails
    ) {
        if (!m_drafts.ContainsKey(context.documentId))
            EnsureDraft(context);
        if (m_drafts[context.documentId] is not AnimationDraft animation)
            return;
        bool readOnly = AssetPath.Parse(context.assetPath).source != AssetSourceId.project;
        animation.lastDrawnFrame = Time.frameCount;
        animation.DrawWorkspace(previews, new InspectorDraftEdits(this, context, animation, readOnly), readOnly);
        animation.DrawPreview(thumbnails);
        if (animation.isEditing)
            SynchronizeState(context, animation);
        ImGuiWidget.Hint("The selected frame is shared with the Inspector. Assign its Sprite there. Save publishes the source; preview never dispatches gameplay events.");
        if (m_errors.TryGetValue(context.documentId, out string? error))
            ImGuiWidget.Hint("Cannot save: " + error);
    }

    internal static bool Supports(string extension) => Array.Exists(S_EXTENSIONS, value => string.Equals(value, extension, StringComparison.OrdinalIgnoreCase));

    public override void Open(EditorDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.title = Path.GetFileNameWithoutExtension(context.assetPath);
    }

    private void EnsureDraft(EditorDocumentContext context)
    {
        if (m_drafts.ContainsKey(context.documentId))
            return;
        Draft draft = CreateDraft(context);
        byte[] baseline = draft.Capture();
        byte[] current = baseline;
        if (context.isDirty && context.TryGetViewParameter("draft", out string encodedDraft) && context.TryGetViewParameter("baseline", out string encodedBaseline))
        {
            baseline = Convert.FromBase64String(encodedBaseline);
            current = Convert.FromBase64String(encodedDraft);
            draft.Restore(current);
        }

        m_drafts[context.documentId] = draft;
        m_baselines[context.documentId] = baseline;
        SynchronizeSnapshot(context, current);
    }

    internal void DrawInspector(
        EditorDocumentContext context,
        InspectionDrawContext inspection
    ) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inspection);
        if (!m_drafts.TryGetValue(context.documentId, out Draft? draft))
        {
            EnsureDraft(context);
            draft = m_drafts[context.documentId];
        }

        bool readOnly = AssetPath.Parse(context.assetPath).source != AssetSourceId.project;
        if (ImGuiWidget.SectionHeader("Asset", "The source remains authoritative. Save publishes draft changes through the normal asset import path."))
        {
            ImGuiWidget.Hint(context.isDirty ? "Unsaved changes · runtime content unchanged" : "Saved source");
            if (readOnly)
                ImGuiWidget.Hint("Installed Plugin asset · copy to the project to edit");
            if (m_errors.TryGetValue(context.documentId, out string? failure))
                ImGuiWidget.Hint("Cannot save: " + failure);
        }

        draft.lastDrawnFrame = Time.frameCount;
        var edits = new InspectorDraftEdits(this, context, draft, readOnly);
        draft.DrawInspector(inspection, previews, edits, readOnly);
        if (draft.isEditing)
            SynchronizeState(context, draft);
    }

    internal void DrawInspectorHeader(EditorDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!m_drafts.ContainsKey(context.documentId))
            EnsureDraft(context);
        bool readOnly = AssetPath.Parse(context.assetPath).source != AssetSourceId.project;
        ImGui.BeginDisabled(readOnly);
        try
        {
            if (ImGui.Button("Save"))
                _ = documents.Save(context.documentId);
            ImGui.SameLine();
            if (ImGui.Button("Revert"))
                _ = documents.Revert(context.documentId);
        }
        finally
        {
            ImGui.EndDisabled();
        }
    }

    public override bool Save(EditorDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        CompleteGestures(force: true);
        EnsureDraft(context);
        if (!m_drafts.TryGetValue(context.documentId, out Draft? draft))
            return false;
        try
        {
            if (!draft.Save(AssetPath.Parse(context.assetPath)))
            {
                m_errors[context.documentId] = "The source could not be committed. The draft remains editable.";
                return false;
            }
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or IOException)
        {
            m_errors[context.documentId] = failure.Message;
            return false;
        }

        m_errors.Remove(context.documentId);
        m_baselines[context.documentId] = m_snapshots[context.documentId];
        SynchronizeSnapshot(context, m_snapshots[context.documentId]);
        return true;
    }

    public override bool Revert(EditorDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureDraft(context);
        byte[] before = m_snapshots[context.documentId];
        Draft replacement = CreateDraft(context);
        m_drafts[context.documentId] = replacement;
        m_baselines[context.documentId] = replacement.Capture();
        m_errors.Remove(context.documentId);
        RecordApplied(context, replacement, before, "Revert 2D Asset");
        return true;
    }

    public override void Close(EditorDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = m_drafts.Remove(context.documentId);
        _ = m_baselines.Remove(context.documentId);
        _ = m_snapshots.Remove(context.documentId);
        _ = m_errors.Remove(context.documentId);
    }

    internal EditorHistoryAvailability QueryHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        try
        {
            ChangeData data = DecodeChange(change);
            if (!m_drafts.TryGetValue(data.documentId, out Draft? draft))
                return EditorHistoryAvailability.Unavailable($"Reopen '{data.assetPath}' to use its History.");
            byte[] expected = direction == EditorHistoryDirection.Undo ? data.after : data.before;
            if (draft.isEditing)
                return EditorHistoryAvailability.Unavailable("Finish the current 2D gesture before using History.");
            return m_snapshots[data.documentId].AsSpan().SequenceEqual(expected) ? EditorHistoryAvailability.Available() : EditorHistoryAvailability.Unavailable("The 2D asset draft changed outside this History entry.");
        }
        catch (Exception error) when (error is IOException or FormatException or InvalidOperationException or ArgumentException)
        {
            return EditorHistoryAvailability.Unavailable(error.Message);
        }
    }

    internal EditorHistoryResult ApplyHistory(
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        try
        {
            ChangeData data = DecodeChange(change);
            if (!m_drafts.TryGetValue(data.documentId, out Draft? draft))
                return EditorHistoryResult.Failure($"Reopen '{data.assetPath}' to use its History.");
            byte[] expected = direction == EditorHistoryDirection.Undo ? data.after : data.before;
            if (!m_snapshots[data.documentId].AsSpan().SequenceEqual(expected))
                return EditorHistoryResult.Failure("The 2D asset draft changed outside this History entry.");
            EditorDocumentContext? document = documents.documents.FirstOrDefault(value => value.documentId == data.documentId);
            if (document is null)
                return EditorHistoryResult.Failure($"Reopen '{data.assetPath}' to use its History.");
            byte[] before = m_snapshots[data.documentId];
            try
            {
                byte[] restored = direction == EditorHistoryDirection.Undo ? data.before : data.after;
                draft.Restore(restored);
                SynchronizeSnapshot(document, restored);
            }
            catch
            {
                draft.Restore(before);
                SynchronizeSnapshot(document, before);
                throw;
            }

            return EditorHistoryResult.Success();
        }
        catch (Exception error) when (error is IOException or FormatException or InvalidOperationException or ArgumentException)
        {
            return EditorHistoryResult.Failure(error.Message);
        }
    }

    internal static bool TryMergeHistory(
        EditorHistoryChange older,
        EditorHistoryChange newer,
        out EditorHistoryChange? merged
    ) {
        merged = null;
        try
        {
            ChangeData first = DecodeChange(older);
            ChangeData second = DecodeChange(newer);
            if (older.mergeKey is null || !string.Equals(older.mergeKey, newer.mergeKey, StringComparison.Ordinal) || first.documentId != second.documentId || !first.after.AsSpan().SequenceEqual(second.before))
                return false;
            var combined = new ChangeData(first.documentId, second.assetPath, first.before, second.after);
            merged = new EditorHistoryChange(Rendering2DIds.assetDraftHistory, EditorHistoryPayload.FromBytes(EncodeChange(combined)), older.mergeKey);
            return true;
        }
        catch (Exception error) when (error is IOException or FormatException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private void RecordApplied(
        EditorDocumentContext context,
        Draft draft,
        byte[] before,
        string name,
        string? mergeKey = null
    ) {
        byte[] after = draft.Capture();
        if (before.AsSpan().SequenceEqual(after))
            return;
        var data = new ChangeData(context.documentId, context.assetPath, before, after);
        var change = new EditorHistoryChange(Rendering2DIds.assetDraftHistory, EditorHistoryPayload.FromBytes(EncodeChange(data)), mergeKey);
        try
        {
            SynchronizeSnapshot(context, after);
            history.RecordApplied(name, change);
        }
        catch
        {
            change.Dispose();
            draft.Restore(before);
            SynchronizeSnapshot(context, before);
            throw;
        }
    }

    private void SynchronizeState(
        EditorDocumentContext context,
        Draft draft
    ) => SynchronizeSnapshot(context, draft.Capture());

    private void SynchronizeSnapshot(
        EditorDocumentContext context,
        byte[] current
    ) {
        m_snapshots[context.documentId] = current;
        byte[] baseline = m_baselines[context.documentId];
        context.SetViewParameter("draft", Convert.ToBase64String(current));
        context.SetViewParameter("baseline", Convert.ToBase64String(baseline));
        documents.SetDirty(context.documentId, !current.AsSpan().SequenceEqual(baseline));
    }

    private static byte[] EncodeChange(ChangeData data)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(data.documentId.ToByteArray());
        writer.Write(data.assetPath);
        writer.Write(data.before.Length);
        writer.Write(data.before);
        writer.Write(data.after.Length);
        writer.Write(data.after);
        return stream.ToArray();
    }

    private static ChangeData DecodeChange(EditorHistoryChange change)
    {
        if (!string.Equals(change.kind, Rendering2DIds.assetDraftHistory, StringComparison.Ordinal))
            throw new InvalidOperationException("The History entry does not belong to 2D asset drafts.");
        using var stream = new MemoryStream(change.payload.ReadBytes(), writable: false);
        using var reader = new BinaryReader(stream);
        var documentId = new Guid(reader.ReadBytes(16));
        string assetPath = reader.ReadString();
        byte[] before = ReadBytes(reader);
        byte[] after = ReadBytes(reader);
        if (stream.Position != stream.Length)
            throw new FormatException("The 2D asset History payload has trailing data.");
        return new ChangeData(documentId, assetPath, before, after);
        static byte[] ReadBytes(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 268_435_456)
                throw new FormatException("The 2D asset History payload length is invalid.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("The 2D asset History payload is incomplete.");
            return bytes;
        }
    }

    private sealed record ChangeData(
        Guid documentId,
        string assetPath,
        byte[] before,
        byte[] after
    );
    private Draft CreateDraft(EditorDocumentContext context)
    {
        string extension = Path.GetExtension(context.assetPath);
        if (string.Equals(extension, ".ispriteatlas2d", StringComparison.OrdinalIgnoreCase))
            return new AtlasDraft(Load<SpriteAtlas2DAsset>(context));
        if (string.Equals(extension, ".ispriteanimation2d", StringComparison.OrdinalIgnoreCase))
            return new AnimationDraft(Load<SpriteAnimation2DAsset>(context));
        if (string.Equals(extension, ".itileset2d", StringComparison.OrdinalIgnoreCase))
            return new TileSetDraft(Load<TileSet2DAsset>(context));
        if (string.Equals(extension, ".itilemap2d", StringComparison.OrdinalIgnoreCase))
            return new TilemapDraft(Load<Tilemap2DAsset>(context));
        if (string.Equals(extension, ".ipostprocess2d", StringComparison.OrdinalIgnoreCase))
            return new PostProcessDraft(Load<PostProcessProfile2DAsset>(context));
        if (string.Equals(extension, ".iparticle2d", StringComparison.OrdinalIgnoreCase))
            return new ParticleDraft(Load<ParticleEffect2DAsset>(context));
        throw new InvalidOperationException($"Unsupported 2D document source '{context.assetPath}'.");
    }

    private static TAsset Load<TAsset>(EditorDocumentContext context)
        where TAsset : AssetObject
    {
        if (context.assetId != Guid.Empty && Assets.TryLoad(context.assetId, out TAsset? identified) && identified is not null)
        {
            return identified;
        }

        if (Assets.TryLoad(AssetPath.Parse(context.assetPath), out TAsset? located) && located is not null)
        {
            return located;
        }

        throw new InvalidOperationException($"2D asset '{context.assetPath}' is not available.");
    }

    private abstract class Draft
    {
        internal long lastDrawnFrame;
        internal virtual bool isEditing => false;
        internal virtual byte[]? CompleteGesture() => null;
        internal virtual byte[]? TakeGestureBefore() => null;
        internal abstract byte[] Capture();

        internal abstract void Restore(byte[] bytes);

        internal abstract bool Draw(IEditorPreviewService previews);

        internal abstract bool Save(AssetPath path);

        internal virtual void DrawInspector(
            InspectionDrawContext context,
            IEditorPreviewService previews,
            IInspectionPropertyEditService edits,
            bool readOnly
        ) {
            byte[] before = ((InspectorDraftEdits)edits).current;
            if (!readOnly)
            {
                try
                {
                    if (Draw(previews))
                        ((InspectorDraftEdits)edits).RecordApplied(before, "Edit 2D Asset");
                }
                catch
                {
                    Restore(before);
                    throw;
                }
            }
            else if (readOnly)
            {
                ImGui.BeginDisabled();
                try
                {
                    _ = Draw(previews);
                }
                finally
                {
                    ImGui.EndDisabled();
                }
            }
        }
    }

    private sealed class InspectorDraftEdits(
        Rendering2DAssetDocumentProvider provider,
        EditorDocumentContext context,
        Draft draft,
        bool readOnly
    ) : IInspectionPropertyEditService
    {
        internal byte[] current => provider.m_snapshots[context.documentId];

        public bool ChangeProperty(
            object owner,
            string propertyName,
            Action mutation,
            string historyName
        ) {
            if (readOnly)
                return false;
            byte[] before = current;
            try
            {
                mutation();
            }
            catch
            {
                draft.Restore(before);
                throw;
            }

            provider.RecordApplied(context, draft, before, historyName);
            return true;
        }

        internal void RecordApplied(
            byte[] before,
            string historyName
        ) => provider.RecordApplied(context, draft, before, historyName);
    }
}
