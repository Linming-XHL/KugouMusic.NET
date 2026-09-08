using System.Buffers.Binary;
using System.Text;

namespace AvaloniaSilkEffects.Sonnet;

// The lexicon remains in one prefix-compressed byte array. Only a block index is
// materialized; neither all words nor their prefixes become managed strings.
internal sealed class SonnetChineseLexicon
{
    private readonly byte[] _data = SonnetLanguageData.Read("Chinese.bin", "SNL1");
    private readonly int _count;
    private readonly int _blocks;
    private readonly int _maxBytes;
    public int MaxLength { get; }
    public double UnknownScore { get; }
    public static SonnetChineseLexicon Instance => Holder.Value;
    private static class Holder { internal static readonly SonnetChineseLexicon Value = new(); }

    private SonnetChineseLexicon()
    {
        _count = SonnetLanguageData.Int(_data, 4);
        MaxLength = SonnetLanguageData.Int(_data, 8);
        _maxBytes = SonnetLanguageData.Int(_data, 12);
        _blocks = SonnetLanguageData.Int(_data, 16);
        UnknownScore = -SonnetLanguageData.Double(_data, 20);
    }

    public bool TryGet(ReadOnlySpan<char> text, out double score, out SonnetLexicalKind kind)
    {
        score = UnknownScore;
        kind = SonnetLexicalKind.Other;
        if (text.IsEmpty || text.Length > MaxLength) return false;
        Span<byte> key = stackalloc byte[_maxBytes];
        var byteCount = Encoding.UTF8.GetByteCount(text);
        if (byteCount > key.Length) return false;
        key = key[..Encoding.UTF8.GetBytes(text, key)];
        var low = 0;
        var high = _blocks - 1;
        var block = -1;
        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            var cursor = SonnetLanguageData.Int(_data, 28 + mid * 4);
            SonnetLanguageData.VarInt(_data, ref cursor); // first entry has no prefix
            var length = SonnetLanguageData.VarInt(_data, ref cursor);
            var compare = _data.AsSpan(cursor, length).SequenceCompareTo(key);
            if (compare <= 0) { block = mid; low = mid + 1; }
            else high = mid - 1;
        }
        if (block < 0) return false;
        Span<byte> word = stackalloc byte[_maxBytes];
        var position = SonnetLanguageData.Int(_data, 28 + block * 4);
        for (var index = 0; index < Math.Min(32, _count - block * 32); index++)
        {
            var prefix = SonnetLanguageData.VarInt(_data, ref position);
            var suffix = SonnetLanguageData.VarInt(_data, ref position);
            _data.AsSpan(position, suffix).CopyTo(word[prefix..]);
            position += suffix;
            var compare = word[..(prefix + suffix)].SequenceCompareTo(key);
            if (compare == 0)
            {
                score = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(position, 4));
                kind = (SonnetLexicalKind)_data[position + 4];
                return true;
            }
            if (compare > 0) return false;
            position += 5;
        }
        return false;
    }
}

internal static class SonnetLanguageData
{
    public static byte[] Read(string name, string signature)
    {
        using var stream = typeof(SonnetLanguageData).Assembly.GetManifestResourceStream("Sonnet.LanguageData." + name)
            ?? throw new InvalidOperationException($"Missing Sonnet language resource: {name}");
        var data = new byte[checked((int)stream.Length)];
        stream.ReadExactly(data);
        if (!data.AsSpan(0, 4).SequenceEqual(Encoding.ASCII.GetBytes(signature)))
            throw new InvalidDataException($"Invalid Sonnet language resource: {name}");
        return data;
    }

    public static int Int(byte[] data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
    public static double Double(byte[] data, int offset) => BinaryPrimitives.ReadDoubleLittleEndian(data.AsSpan(offset, 8));
    public static int VarInt(byte[] data, ref int cursor)
    {
        var value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var next = data[cursor++];
            value |= (next & 127) << shift;
            if (next < 128) return value;
        }
        throw new InvalidDataException("Invalid Sonnet language data integer.");
    }
}
