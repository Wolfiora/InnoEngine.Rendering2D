using System;
using System.IO;
using InnoEditor.Interactions;
using InnoEngine.Assets;
using InnoEngine.Mathematics;

namespace Inno.Rendering2D;

/// <summary>
/// Encodes and records reload-safe tilemap gestures in the shared Editor history.
/// </summary>
public static class TilemapHistory2D
{
    private const uint C_PAYLOAD_MAGIC = 0x32534D54;
    /// <summary>
    /// Gets the stable history protocol handled by the 2D Plugin.
    /// </summary>
    public const string kind = "inno.rendering2d.tilemap-stroke";
    /// <summary>
    /// Records an already applied gesture as exactly one Editor history transaction.
    /// </summary>
    /// <param name="history">
    /// Shared Editor history owner.
    /// </param>
    /// <param name="name">
    /// User-facing operation description.
    /// </param>
    /// <param name="path">
    /// Current source path of the tilemap.
    /// </param>
    /// <param name="tilemap">
    /// Asset whose persistent identity is recorded.
    /// </param>
    /// <param name="stroke">
    /// Already applied compact change set; an empty set is ignored.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// A required history, tilemap or stroke is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The operation name is empty.
    /// </exception>
    public static void RecordApplied(
        IEditorHistory history,
        string name,
        AssetPath path,
        Tilemap2DAsset tilemap,
        TilemapStroke2D stroke
    ) {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(tilemap);
        ArgumentNullException.ThrowIfNull(stroke);
        if (!stroke.hasChanges)
            return;
        using EditorHistoryTransaction transaction = history.BeginTransaction(name);
        history.RecordApplied(name, new EditorHistoryChange(kind, EditorHistoryPayload.FromBytes(Encode(path, tilemap.identity.persistentId, stroke))));
        transaction.Commit();
    }

    internal static byte[] Encode(
        AssetPath path,
        Guid assetId,
        TilemapStroke2D stroke
    ) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(C_PAYLOAD_MAGIC);
        writer.Write(assetId.ToByteArray());
        writer.Write(path.ToString());
        writer.Write(stroke.changes.Count);
        for (int index = 0; index < stroke.changes.Count; index++)
        {
            TilemapCellChange2D change = stroke.changes[index];
            writer.Write(change.layerId);
            writer.Write(change.x);
            writer.Write(change.y);
            WriteSnapshot(writer, change.before);
            WriteSnapshot(writer, change.after);
        }

        writer.Flush();
        return stream.ToArray();
    }

    internal static (Guid assetId, string path, TilemapStroke2D stroke) Decode(ReadOnlySpan<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != C_PAYLOAD_MAGIC)
            throw new InvalidDataException("The tilemap history payload header is invalid.");
        Guid assetId = new(reader.ReadBytes(16));
        string path = reader.ReadString();
        int count = reader.ReadInt32();
        if (count < 0 || count > 16_777_216)
            throw new InvalidDataException("Tilemap history cell count is invalid.");
        var changes = new TilemapCellChange2D[count];
        for (int index = 0; index < count; index++)
        {
            int layerId = reader.ReadInt32();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            changes[index] = new TilemapCellChange2D(layerId, x, y, ReadSnapshot(reader, x, y), ReadSnapshot(reader, x, y));
        }

        if (stream.Position != stream.Length)
            throw new InvalidDataException("Tilemap history payload contains trailing data.");
        return (assetId, path, new TilemapStroke2D(changes));
    }

    private static void WriteSnapshot(
        BinaryWriter writer,
        TilemapCellSnapshot2D snapshot
    ) {
        writer.Write(snapshot.hasValue);
        if (!snapshot.hasValue)
            return;
        TilemapCell2D cell = snapshot.value;
        writer.Write(cell.tileId);
        writer.Write(cell.color.r);
        writer.Write(cell.color.g);
        writer.Write(cell.color.b);
        writer.Write(cell.color.a);
        writer.Write(cell.flipX);
        writer.Write(cell.flipY);
        writer.Write(cell.quarterTurns);
    }

    private static TilemapCellSnapshot2D ReadSnapshot(
        BinaryReader reader,
        int x,
        int y
    ) {
        if (!reader.ReadBoolean())
            return default;
        return new TilemapCellSnapshot2D(
            true,
            new TilemapCell2D
            {
                x = x,
                y = y,
                tileId = reader.ReadInt32(),
                color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                flipX = reader.ReadBoolean(),
                flipY = reader.ReadBoolean(),
                quarterTurns = reader.ReadInt32()
            }
        );
    }
}

/// <summary>
/// Applies the current generation of the compact tilemap stroke protocol.
/// </summary>
[EditorHistoryHandler(TilemapHistory2D.kind)]
public sealed class TilemapHistoryHandler2D : EditorHistoryHandler
{
    /// <inheritdoc />
    protected override EditorHistoryAvailability Query(
        EditorHistoryContext context,
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            (Guid assetId, string path, _) = TilemapHistory2D.Decode(change.payload.ReadBytes());
            return TryLoad(assetId, path, out _) ? EditorHistoryAvailability.Available() : EditorHistoryAvailability.Unavailable($"Tilemap '{path}' is not available.");
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            return EditorHistoryAvailability.Unavailable(exception.Message);
        }
    }

    /// <inheritdoc />
    protected override EditorHistoryResult Apply(
        EditorHistoryContext context,
        EditorHistoryChange change,
        EditorHistoryDirection direction
    ) {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            (Guid assetId, string path, TilemapStroke2D stroke) = TilemapHistory2D.Decode(change.payload.ReadBytes());
            if (!TryLoad(assetId, path, out Tilemap2DAsset? tilemap) || tilemap is null)
                return EditorHistoryResult.Failure($"Tilemap '{path}' is not available.");
            if (direction == EditorHistoryDirection.Undo)
                stroke.Undo(tilemap);
            else
                stroke.Redo(tilemap);
            foreach (EditorDocumentContext document in context.interactions.documents.documents)
            {
                if (document.assetId == assetId || string.Equals(document.assetPath, path, StringComparison.Ordinal))
                {
                    context.interactions.documents.SetDirty(document.documentId);
                }
            }

            return EditorHistoryResult.Success();
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or IOException)
        {
            return EditorHistoryResult.Failure(exception.Message);
        }
    }

    private static bool TryLoad(
        Guid assetId,
        string path,
        out Tilemap2DAsset? tilemap
    ) {
        if (assetId != Guid.Empty && Assets.TryLoad(assetId, out Tilemap2DAsset? identified) && identified is not null)
        {
            tilemap = identified;
            return true;
        }

        if (Assets.TryLoad(AssetPath.Parse(path), out Tilemap2DAsset? located) && located is not null)
        {
            tilemap = located;
            return true;
        }

        tilemap = null;
        return false;
    }
}
