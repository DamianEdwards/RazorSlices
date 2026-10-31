using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.Extensions.ObjectPool;

namespace RazorSlices;

/// <summary>
/// Reflection-free JSON serialization compatible with MVC's default TempData payloads.
/// Supports null, strings, integers, booleans, DateTime, Guid, enums, integer/string collections,
/// and string-to-string dictionaries. JSON strings are inferred as Guid or DateTime when possible;
/// empty arrays deserialize as null, matching MVC.
/// </summary>
public sealed class RazorSliceTempDataSerializer : TempDataSerializer
{
    private static readonly ObjectPool<SerializationState> States =
        new DefaultObjectPool<SerializationState>(new StatePolicy());

    /// <inheritdoc />
    public override bool CanSerializeType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string) || type == typeof(int) || type == typeof(bool)
            || type == typeof(DateTime) || type == typeof(Guid) || type.IsEnum
            || typeof(ICollection<int>).IsAssignableFrom(type)
            || typeof(ICollection<string>).IsAssignableFrom(type)
            || typeof(IDictionary<string, string>).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public override byte[] Serialize(IDictionary<string, object?> values)
    {
        if (values is null || values.Count == 0) { return []; }
        var state = States.Get();
        try
        {
            var writer = state.Writer;
            writer.WriteStartObject();
            foreach (var entry in values)
            {
                writer.WritePropertyName(entry.Key);
                WriteValue(writer, entry.Value);
            }
            writer.WriteEndObject();
            writer.Flush();
            return state.Buffer.WrittenSpan.ToArray();
        }
        finally
        {
            States.Return(state);
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string text: writer.WriteStringValue(text); break;
            case int number: writer.WriteNumberValue(number); break;
            case bool flag: writer.WriteBooleanValue(flag); break;
            case DateTime date: writer.WriteStringValue(date); break;
            case Guid id: writer.WriteStringValue(id); break;
            case Enum enumeration:
                writer.WriteNumberValue(Convert.ToInt32(enumeration, CultureInfo.InvariantCulture));
                break;
            case ICollection<int> numbers:
                writer.WriteStartArray();
                foreach (var number in numbers) { writer.WriteNumberValue(number); }
                writer.WriteEndArray();
                break;
            case ICollection<string> strings:
                writer.WriteStartArray();
                foreach (var text in strings) { writer.WriteStringValue(text); }
                writer.WriteEndArray();
                break;
            case IDictionary<string, string> dictionary:
                writer.WriteStartObject();
                foreach (var entry in dictionary) { writer.WriteString(entry.Key, entry.Value); }
                writer.WriteEndObject();
                break;
            default:
                throw new InvalidOperationException($"TempData cannot serialize '{value.GetType()}'.");
        }
    }

    /// <inheritdoc />
    public override IDictionary<string, object?> Deserialize(byte[] unprotectedData)
    {
        ArgumentNullException.ThrowIfNull(unprotectedData);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (unprotectedData.Length == 0) { return result; }
        var reader = new Utf8JsonReader(unprotectedData);
        ReadExpected(ref reader, JsonTokenType.StartObject);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) { throw new JsonException("Expected a TempData key."); }
            var key = reader.GetString()!;
            if (!reader.Read()) { throw new JsonException("Expected a TempData value."); }
            result[key] = ReadValue(ref reader);
        }
        if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
        {
            throw new JsonException("Expected a single complete TempData object.");
        }
        return result;
    }

    private static object? ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null: return null;
            case JsonTokenType.True: return true;
            case JsonTokenType.False: return false;
            case JsonTokenType.Number: return reader.GetInt32();
            case JsonTokenType.String:
                if (reader.TryGetGuid(out var id)) { return id; }
                if (reader.TryGetDateTime(out var date)) { return date; }
                return reader.GetString();
            case JsonTokenType.StartArray: return ReadArray(ref reader);
            case JsonTokenType.StartObject:
                var dictionary = new Dictionary<string, string?>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) { throw new JsonException("Expected a dictionary key."); }
                    var key = reader.GetString()!;
                    if (!reader.Read()) { throw new JsonException("Expected a dictionary value."); }
                    dictionary[key] = reader.GetString();
                }
                if (reader.TokenType != JsonTokenType.EndObject) { throw new JsonException("Incomplete dictionary."); }
                return dictionary;
            default:
                throw new InvalidOperationException($"TempData cannot deserialize JSON token '{reader.TokenType}'.");
        }
    }

    private static object? ReadArray(ref Utf8JsonReader reader)
    {
        // Count without building a DOM or an intermediate List, then allocate only the returned array.
        var counter = reader;
        var count = 0;
        while (counter.Read() && counter.TokenType != JsonTokenType.EndArray)
        {
            counter.Skip();
            count++;
        }
        if (counter.TokenType != JsonTokenType.EndArray) { throw new JsonException("Incomplete array."); }
        if (count == 0)
        {
            reader = counter;
            return null;
        }
        reader.Read();
        if (reader.TokenType == JsonTokenType.Number)
        {
            var numbers = new int[count];
            for (var i = 0; i < count; i++)
            {
                numbers[i] = reader.GetInt32();
                reader.Read();
            }
            return numbers;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var strings = new string?[count];
            for (var i = 0; i < count; i++)
            {
                strings[i] = reader.GetString();
                reader.Read();
            }
            return strings;
        }
        throw new InvalidOperationException("TempData arrays must contain integers or strings.");
    }

    private static void ReadExpected(ref Utf8JsonReader reader, JsonTokenType token)
    {
        if (!reader.Read() || reader.TokenType != token) { throw new JsonException($"Expected '{token}'."); }
    }

    private sealed class SerializationState
    {
        public ArrayBufferWriter<byte> Buffer { get; } = new(256);
        public Utf8JsonWriter Writer { get; }

        public SerializationState() => Writer = new(Buffer);
    }

    private sealed class StatePolicy : PooledObjectPolicy<SerializationState>
    {
        public override SerializationState Create() => new();

        public override bool Return(SerializationState obj)
        {
            // Clear even a failed write, so pooled storage never retains plaintext TempData.
            obj.Writer.Flush();
            obj.Buffer.Clear();
            if (obj.Buffer.Capacity > 64 * 1024)
            {
                obj.Writer.Dispose();
                return false;
            }
            obj.Writer.Reset(obj.Buffer);
            return true;
        }
    }
}
