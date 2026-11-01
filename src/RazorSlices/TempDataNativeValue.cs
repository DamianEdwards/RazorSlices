using System.Buffers.Binary;
using System.Numerics;

namespace RazorSlices;

internal enum TempDataValueKind : byte
{
    Utf8, String, Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64,
    Int128, UInt128, NInt, NUInt, Half, Single, Double, Decimal, BigInteger, Complex,
    DateTime, DateTimeOffset, DateOnly, TimeOnly, TimeSpan, Guid
}

internal static class NativeType<T>
{
    internal static readonly TempDataValueKind Kind = GetKind();

    private static TempDataValueKind GetKind()
    {
        var type = typeof(T);
        if (type == typeof(bool)) { return TempDataValueKind.Boolean; }
        if (type == typeof(byte)) { return TempDataValueKind.Byte; }
        if (type == typeof(sbyte)) { return TempDataValueKind.SByte; }
        if (type == typeof(short)) { return TempDataValueKind.Int16; }
        if (type == typeof(ushort)) { return TempDataValueKind.UInt16; }
        if (type == typeof(int)) { return TempDataValueKind.Int32; }
        if (type == typeof(uint)) { return TempDataValueKind.UInt32; }
        if (type == typeof(long)) { return TempDataValueKind.Int64; }
        if (type == typeof(ulong)) { return TempDataValueKind.UInt64; }
        if (type == typeof(Int128)) { return TempDataValueKind.Int128; }
        if (type == typeof(UInt128)) { return TempDataValueKind.UInt128; }
        if (type == typeof(nint)) { return TempDataValueKind.NInt; }
        if (type == typeof(nuint)) { return TempDataValueKind.NUInt; }
        if (type == typeof(Half)) { return TempDataValueKind.Half; }
        if (type == typeof(float)) { return TempDataValueKind.Single; }
        if (type == typeof(double)) { return TempDataValueKind.Double; }
        if (type == typeof(decimal)) { return TempDataValueKind.Decimal; }
        if (type == typeof(BigInteger)) { return TempDataValueKind.BigInteger; }
        if (type == typeof(Complex)) { return TempDataValueKind.Complex; }
        if (type == typeof(DateTime)) { return TempDataValueKind.DateTime; }
        if (type == typeof(DateTimeOffset)) { return TempDataValueKind.DateTimeOffset; }
        if (type == typeof(DateOnly)) { return TempDataValueKind.DateOnly; }
        if (type == typeof(TimeOnly)) { return TempDataValueKind.TimeOnly; }
        if (type == typeof(TimeSpan)) { return TempDataValueKind.TimeSpan; }
        if (type == typeof(Guid)) { return TempDataValueKind.Guid; }
        return TempDataValueKind.Utf8;
    }
}

internal static class TempDataNativeValue
{
    internal static int Size(TempDataValueKind kind) => kind switch
    {
        TempDataValueKind.Boolean or TempDataValueKind.Byte or TempDataValueKind.SByte => 1,
        TempDataValueKind.Int16 or TempDataValueKind.UInt16 or TempDataValueKind.Half => 2,
        TempDataValueKind.Int32 or TempDataValueKind.UInt32 or TempDataValueKind.Single or TempDataValueKind.DateOnly => 4,
        TempDataValueKind.Int64 or TempDataValueKind.UInt64 or TempDataValueKind.NInt or TempDataValueKind.NUInt or
            TempDataValueKind.Double or TempDataValueKind.DateTime or TempDataValueKind.TimeOnly or TempDataValueKind.TimeSpan => 8,
        TempDataValueKind.DateTimeOffset => 10,
        TempDataValueKind.Int128 or TempDataValueKind.UInt128 or TempDataValueKind.Decimal or
            TempDataValueKind.Complex or TempDataValueKind.Guid => 16,
        _ => throw new FormatException("Invalid fixed-width TempData value kind.")
    };

    // Use explicit wire encodings, not native CLR layouts or machine byte order.
    internal static void Write(TempData.Entry entry, Span<byte> destination)
    {
        switch (entry.Kind)
        {
            case TempDataValueKind.Boolean: destination[0] = entry.ReadNative<bool>() ? (byte)1 : (byte)0; break;
            case TempDataValueKind.Byte: destination[0] = entry.ReadNative<byte>(); break;
            case TempDataValueKind.SByte: destination[0] = unchecked((byte)entry.ReadNative<sbyte>()); break;
            case TempDataValueKind.Int16: BinaryPrimitives.WriteInt16LittleEndian(destination, entry.ReadNative<short>()); break;
            case TempDataValueKind.UInt16: BinaryPrimitives.WriteUInt16LittleEndian(destination, entry.ReadNative<ushort>()); break;
            case TempDataValueKind.Int32: BinaryPrimitives.WriteInt32LittleEndian(destination, entry.ReadNative<int>()); break;
            case TempDataValueKind.UInt32: BinaryPrimitives.WriteUInt32LittleEndian(destination, entry.ReadNative<uint>()); break;
            case TempDataValueKind.Int64: BinaryPrimitives.WriteInt64LittleEndian(destination, entry.ReadNative<long>()); break;
            case TempDataValueKind.UInt64: BinaryPrimitives.WriteUInt64LittleEndian(destination, entry.ReadNative<ulong>()); break;
            case TempDataValueKind.Int128: BinaryPrimitives.WriteInt128LittleEndian(destination, entry.ReadNative<Int128>()); break;
            case TempDataValueKind.UInt128: BinaryPrimitives.WriteUInt128LittleEndian(destination, entry.ReadNative<UInt128>()); break;
            case TempDataValueKind.NInt: BinaryPrimitives.WriteInt64LittleEndian(destination, entry.ReadNative<nint>()); break;
            case TempDataValueKind.NUInt: BinaryPrimitives.WriteUInt64LittleEndian(destination, entry.ReadNative<nuint>()); break;
            case TempDataValueKind.Half: BinaryPrimitives.WriteInt16LittleEndian(destination, BitConverter.HalfToInt16Bits(entry.ReadNative<Half>())); break;
            case TempDataValueKind.Single: BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(entry.ReadNative<float>())); break;
            case TempDataValueKind.Double: BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(entry.ReadNative<double>())); break;
            case TempDataValueKind.Decimal:
                Span<int> bits = stackalloc int[4];
                decimal.GetBits(entry.ReadNative<decimal>(), bits);
                for (var i = 0; i < bits.Length; i++) { BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], bits[i]); }
                break;
            case TempDataValueKind.BigInteger:
                if (!entry.BigInteger.TryWriteBytes(destination, out var written) || written != destination.Length)
                {
                    throw new InvalidOperationException("Failed to encode the TempData BigInteger.");
                }
                break;
            case TempDataValueKind.Complex:
                var number = entry.ReadNative<Complex>();
                BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(number.Real));
                BinaryPrimitives.WriteInt64LittleEndian(destination[8..], BitConverter.DoubleToInt64Bits(number.Imaginary));
                break;
            case TempDataValueKind.DateTime:
                BinaryPrimitives.WriteInt64LittleEndian(destination, entry.ReadNative<DateTime>().ToBinary());
                break;
            case TempDataValueKind.DateTimeOffset:
                var date = entry.ReadNative<DateTimeOffset>();
                BinaryPrimitives.WriteInt64LittleEndian(destination, date.Ticks);
                BinaryPrimitives.WriteInt16LittleEndian(destination[8..], checked((short)date.Offset.TotalMinutes));
                break;
            case TempDataValueKind.DateOnly: BinaryPrimitives.WriteInt32LittleEndian(destination, entry.ReadNative<DateOnly>().DayNumber); break;
            case TempDataValueKind.TimeOnly: BinaryPrimitives.WriteInt64LittleEndian(destination, entry.ReadNative<TimeOnly>().Ticks); break;
            case TempDataValueKind.TimeSpan: BinaryPrimitives.WriteInt64LittleEndian(destination, entry.ReadNative<TimeSpan>().Ticks); break;
            case TempDataValueKind.Guid:
                if (!entry.ReadNative<Guid>().TryWriteBytes(destination)) { throw new InvalidOperationException("Failed to encode the TempData Guid."); }
                break;
            default: throw new FormatException("Invalid native TempData value kind.");
        }
    }

    internal static TempData.Entry Read(string type, TempDataValueKind kind, ReadOnlySpan<byte> source)
    {
        if (kind == TempDataValueKind.BigInteger)
        {
            if (source.IsEmpty) { throw new FormatException("Invalid TempData BigInteger length."); }
            return Create(type, kind, new BigInteger(source));
        }
        if (source.Length != Size(kind)) { throw new FormatException("Invalid native TempData value length."); }
        return kind switch
        {
            TempDataValueKind.Boolean => source[0] <= 1 ? Create(type, kind, source[0] != 0)
                : throw new FormatException("Invalid TempData Boolean."),
            TempDataValueKind.Byte => Create(type, kind, source[0]),
            TempDataValueKind.SByte => Create(type, kind, unchecked((sbyte)source[0])),
            TempDataValueKind.Int16 => Create(type, kind, BinaryPrimitives.ReadInt16LittleEndian(source)),
            TempDataValueKind.UInt16 => Create(type, kind, BinaryPrimitives.ReadUInt16LittleEndian(source)),
            TempDataValueKind.Int32 => Create(type, kind, BinaryPrimitives.ReadInt32LittleEndian(source)),
            TempDataValueKind.UInt32 => Create(type, kind, BinaryPrimitives.ReadUInt32LittleEndian(source)),
            TempDataValueKind.Int64 => Create(type, kind, BinaryPrimitives.ReadInt64LittleEndian(source)),
            TempDataValueKind.UInt64 => Create(type, kind, BinaryPrimitives.ReadUInt64LittleEndian(source)),
            TempDataValueKind.Int128 => Create(type, kind, BinaryPrimitives.ReadInt128LittleEndian(source)),
            TempDataValueKind.UInt128 => Create(type, kind, BinaryPrimitives.ReadUInt128LittleEndian(source)),
            TempDataValueKind.NInt => Create(type, kind, checked((nint)BinaryPrimitives.ReadInt64LittleEndian(source))),
            TempDataValueKind.NUInt => Create(type, kind, checked((nuint)BinaryPrimitives.ReadUInt64LittleEndian(source))),
            TempDataValueKind.Half => Create(type, kind, BitConverter.Int16BitsToHalf(BinaryPrimitives.ReadInt16LittleEndian(source))),
            TempDataValueKind.Single => Create(type, kind, BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(source))),
            TempDataValueKind.Double => Create(type, kind, BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source))),
            TempDataValueKind.Decimal => Create(type, kind, ReadDecimal(source)),
            TempDataValueKind.Complex => Create(type, kind, new Complex(
                BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source)),
                BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source[8..])))),
            TempDataValueKind.DateTime => Create(type, kind, DateTime.FromBinary(BinaryPrimitives.ReadInt64LittleEndian(source))),
            TempDataValueKind.DateTimeOffset => Create(type, kind, new DateTimeOffset(
                BinaryPrimitives.ReadInt64LittleEndian(source), TimeSpan.FromMinutes(BinaryPrimitives.ReadInt16LittleEndian(source[8..])))),
            TempDataValueKind.DateOnly => Create(type, kind, DateOnly.FromDayNumber(BinaryPrimitives.ReadInt32LittleEndian(source))),
            TempDataValueKind.TimeOnly => Create(type, kind, new TimeOnly(BinaryPrimitives.ReadInt64LittleEndian(source))),
            TempDataValueKind.TimeSpan => Create(type, kind, new TimeSpan(BinaryPrimitives.ReadInt64LittleEndian(source))),
            TempDataValueKind.Guid => Create(type, kind, new Guid(source)),
            _ => throw new FormatException("Invalid native TempData value kind.")
        };
    }

    private static TempData.Entry Create<T>(string type, TempDataValueKind kind, T value)
    {
        if (type != typeof(T).FullName) { throw new FormatException("Native TempData type tag mismatch."); }
        return TempData.Entry.FromNative(type, kind, value);
    }

    private static decimal ReadDecimal(ReadOnlySpan<byte> source)
    {
        var flags = BinaryPrimitives.ReadInt32LittleEndian(source[12..]);
        var scale = (byte)(flags >> 16);
        if ((flags & 0x7F00FFFF) != 0 || scale > 28) { throw new FormatException("Invalid TempData decimal flags."); }
        return new decimal(BinaryPrimitives.ReadInt32LittleEndian(source),
            BinaryPrimitives.ReadInt32LittleEndian(source[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(source[8..]), flags < 0, scale);
    }
}
