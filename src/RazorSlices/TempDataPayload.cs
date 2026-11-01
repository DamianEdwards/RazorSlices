using System.Buffers.Binary;
using System.Text;

namespace RazorSlices;

internal static class TempDataPayload
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static byte[] Serialize(Dictionary<string, TempData.Entry> entries, int limit)
    {
        var count = 0;
        var size = 5;
        foreach (var (key, entry) in entries)
        {
            if (entry.Consumed) { continue; }
            size = checked(size + 13 + Utf8.GetByteCount(key) + Utf8.GetByteCount(entry.Type) + Math.Max(ValueLength(entry), 0));
            count++;
        }
        if (count == 0) { return []; }
        if (size > limit) { throw new InvalidOperationException("TempData exceeds MaxPayloadBytes."); }
        var payload = new byte[size];
        payload[0] = 2;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(1), count);
        var offset = 5;
        foreach (var (key, entry) in entries)
        {
            if (entry.Consumed) { continue; }
            WriteString(payload, ref offset, key);
            WriteString(payload, ref offset, entry.Type);
            payload[offset++] = (byte)entry.Kind;
            var length = ValueLength(entry);
            WriteLength(payload, ref offset, length);
            if (!entry.IsNull)
            {
                var destination = payload.AsSpan(offset, length);
                switch (entry.Kind)
                {
                    case TempDataValueKind.Utf8: entry.Bytes.CopyTo(destination); break;
                    case TempDataValueKind.String: Utf8.GetBytes(entry.Text!, destination); break;
                    default: TempDataNativeValue.Write(entry, destination); break;
                }
                offset += length;
            }
        }
        return payload;
    }

    internal static Dictionary<string, TempData.Entry> Deserialize(byte[] payload, int limit)
    {
        var entries = new Dictionary<string, TempData.Entry>(StringComparer.OrdinalIgnoreCase);
        if (payload.Length == 0) { return entries; }
        if (payload.Length > limit || payload.Length < 5 || payload[0] != 2)
        {
            throw new FormatException("Invalid TempData payload size or version.");
        }
        var offset = 1;
        var count = ReadLength(payload, ref offset);
        if (count < 0 || count > (payload.Length - 5) / 13) { throw new FormatException("Invalid TempData entry count."); }
        for (var i = 0; i < count; i++)
        {
            var key = ReadString(payload, ref offset);
            var type = ReadString(payload, ref offset);
            if (offset == payload.Length) { throw new FormatException("Truncated TempData value kind."); }
            var kind = (TempDataValueKind)payload[offset++];
            var length = ReadLength(payload, ref offset);
            if (length < -1 || length > payload.Length - offset) { throw new FormatException("Invalid TempData value length."); }
            if (length == -1 && kind != TempDataValueKind.String) { throw new FormatException("Invalid null TempData value."); }
            var source = payload.AsSpan(offset, Math.Max(length, 0));
            var entry = kind switch
            {
                TempDataValueKind.String when type == "string" => TempData.Entry.FromString(
                    length == -1 ? null : Utf8.GetString(source)),
                TempDataValueKind.String => throw new FormatException("Invalid TempData string type tag."),
                TempDataValueKind.Utf8 when type != "string" => new TempData.Entry(type, source),
                _ => TempDataNativeValue.Read(type, kind, source)
            };
            offset += Math.Max(length, 0);
            if (type.Length == 0 || (entry.IsNull && type != "string") || !entries.TryAdd(key, entry))
            {
                throw new FormatException("Invalid or duplicate TempData entry.");
            }
        }
        if (offset != payload.Length) { throw new FormatException("Unexpected trailing TempData data."); }
        return entries;
    }

    private static int ValueLength(TempData.Entry entry) => entry.Kind switch
    {
        TempDataValueKind.String => entry.Text is null ? -1 : Utf8.GetByteCount(entry.Text),
        TempDataValueKind.Utf8 => entry.Length,
        TempDataValueKind.BigInteger => entry.BigInteger.GetByteCount(),
        _ => TempDataNativeValue.Size(entry.Kind)
    };

    private static void WriteString(byte[] payload, ref int offset, string value)
    {
        WriteLength(payload, ref offset, Utf8.GetByteCount(value));
        offset += Utf8.GetBytes(value, payload.AsSpan(offset));
    }

    private static string ReadString(byte[] payload, ref int offset)
    {
        var length = ReadLength(payload, ref offset);
        if (length < 0 || length > payload.Length - offset) { throw new FormatException("Invalid TempData text length."); }
        var value = Utf8.GetString(payload.AsSpan(offset, length));
        offset += length;
        return value;
    }

    private static void WriteLength(byte[] payload, ref int offset, int length)
    {
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset), length);
        offset += 4;
    }

    private static int ReadLength(byte[] payload, ref int offset)
    {
        if (payload.Length - offset < 4) { throw new FormatException("Truncated TempData payload."); }
        var length = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset));
        offset += 4;
        return length;
    }
}
