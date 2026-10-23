using System.Buffers.Binary;
using System.Text;

namespace Tempest.Desktop.Documents;

/// <summary>
/// `v0.26.0` (§3.6): a glyph-level TrueType subsetter. The PDF backend
/// embeds every typeface whole (a 98 KB Space Mono for a dozen digits),
/// which is what put an issue sheet past 600 KB a page. This keeps the
/// font's tables and glyph ids exactly as they are and empties the outline
/// data of every glyph the document does not draw, so <c>cmap</c>,
/// <c>hmtx</c> and the rest stay valid and the text stays extractable.
/// Composite glyphs keep their components. CFF (<c>OTTO</c>) fonts are
/// returned unchanged. Anything malformed throws
/// <see cref="InvalidDataException"/>; the caller falls back to the whole
/// font.
/// </summary>
public static class TrueTypeSubsetter
{
    private const uint HeadTag = 0x68656164; // head
    private const uint MaxpTag = 0x6D617870; // maxp
    private const uint LocaTag = 0x6C6F6361; // loca
    private const uint GlyfTag = 0x676C7966; // glyf
    private const uint CmapTag = 0x636D6170; // cmap
    private const int MaxFontBytes = 16 * 1024 * 1024;

    private readonly record struct TableRecord(uint Tag, uint Checksum, uint Offset, uint Length);

    /// <summary>Subsets <paramref name="font"/> to the glyphs that render <paramref name="codePoints"/> (plus glyph 0 and every composite component).</summary>
    public static byte[] Subset(ReadOnlySpan<byte> font, IEnumerable<int> codePoints)
    {
        ArgumentNullException.ThrowIfNull(codePoints);

        if (font.Length < 12 || font.Length > MaxFontBytes)
            throw new InvalidDataException("Not a TrueType font: bad length.");

        var version = BinaryPrimitives.ReadUInt32BigEndian(font);

        if (version == 0x4F54544F) // 'OTTO': CFF outlines, nothing to subset here.
            return font.ToArray();

        if (version != 0x00010000 && version != 0x74727565) // 1.0 or 'true'
            throw new InvalidDataException("Not a TrueType font: bad version.");

        var tables = ReadDirectory(font);
        var head = Find(tables, HeadTag);
        var maxp = Find(tables, MaxpTag);
        var loca = Find(tables, LocaTag);
        var glyf = Find(tables, GlyfTag);
        var cmap = Find(tables, CmapTag);

        var indexToLocFormat = BinaryPrimitives.ReadInt16BigEndian(font.Slice((int)head.Offset + 50));
        var numGlyphs = BinaryPrimitives.ReadUInt16BigEndian(font.Slice((int)maxp.Offset + 4));
        var offsets = ReadLoca(font.Slice((int)loca.Offset, (int)loca.Length), numGlyphs, indexToLocFormat);
        var glyfData = font.Slice((int)glyf.Offset, (int)glyf.Length);

        var keep = new bool[numGlyphs];
        keep[0] = true;
        var map = ReadCmap(font.Slice((int)cmap.Offset, (int)cmap.Length));
        var queue = new Queue<int>();

        foreach (var cp in codePoints)
        {
            if (map.TryGetValue(cp, out var gid) && gid < numGlyphs && !keep[gid])
            {
                keep[gid] = true;
                queue.Enqueue(gid);
            }
        }

        queue.Enqueue(0);

        while (queue.Count > 0)
        {
            var gid = queue.Dequeue();

            foreach (var component in Components(glyfData, offsets, gid))
            {
                if (component < numGlyphs && !keep[component])
                {
                    keep[component] = true;
                    queue.Enqueue(component);
                }
            }
        }

        // New glyf: kept glyphs copied, others empty; new loca: long format.
        var newGlyf = new MemoryStream();
        var newLoca = new byte[(numGlyphs + 1) * 4];

        for (var gid = 0; gid < numGlyphs; gid++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(newLoca.AsSpan(gid * 4), (uint)newGlyf.Length);

            if (!keep[gid])
                continue;

            var start = (int)offsets[gid];
            var end = (int)offsets[gid + 1];

            if (end > start && end <= glyfData.Length)
            {
                newGlyf.Write(glyfData.Slice(start, end - start));

                while (newGlyf.Length % 4 != 0)
                    newGlyf.WriteByte(0);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(newLoca.AsSpan(numGlyphs * 4), (uint)newGlyf.Length);

        var newHead = font.Slice((int)head.Offset, (int)head.Length).ToArray();
        BinaryPrimitives.WriteInt16BigEndian(newHead.AsSpan(50), 1);
        BinaryPrimitives.WriteUInt32BigEndian(newHead.AsSpan(8), 0);

        return Assemble(font, tables, new Dictionary<uint, byte[]>
        {
            [HeadTag] = newHead,
            [LocaTag] = newLoca,
            [GlyfTag] = newGlyf.ToArray(),
        });
    }

    private static List<TableRecord> ReadDirectory(ReadOnlySpan<byte> font)
    {
        var numTables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(4));

        if (numTables == 0 || numTables > 64 || 12 + numTables * 16 > font.Length)
            throw new InvalidDataException("Bad table directory.");

        var tables = new List<TableRecord>(numTables);

        for (var i = 0; i < numTables; i++)
        {
            var at = 12 + i * 16;
            var record = new TableRecord(
                BinaryPrimitives.ReadUInt32BigEndian(font.Slice(at)),
                BinaryPrimitives.ReadUInt32BigEndian(font.Slice(at + 4)),
                BinaryPrimitives.ReadUInt32BigEndian(font.Slice(at + 8)),
                BinaryPrimitives.ReadUInt32BigEndian(font.Slice(at + 12)));

            if ((long)record.Offset + record.Length > font.Length)
                throw new InvalidDataException("A table runs past the end of the font.");

            tables.Add(record);
        }

        return tables;
    }

    private static TableRecord Find(List<TableRecord> tables, uint tag) =>
        tables.FirstOrDefault(t => t.Tag == tag) is { Length: > 0 } t ? t : throw new InvalidDataException($"Missing table '{Encoding.ASCII.GetString(BitConverter.GetBytes(tag).Reverse().ToArray())}'.");

    private static uint[] ReadLoca(ReadOnlySpan<byte> loca, int numGlyphs, short format)
    {
        var offsets = new uint[numGlyphs + 1];

        for (var i = 0; i <= numGlyphs; i++)
        {
            if (format == 0)
            {
                if (i * 2 + 2 > loca.Length)
                    throw new InvalidDataException("Short loca is truncated.");

                offsets[i] = (uint)BinaryPrimitives.ReadUInt16BigEndian(loca.Slice(i * 2)) * 2;
            }
            else
            {
                if (i * 4 + 4 > loca.Length)
                    throw new InvalidDataException("Long loca is truncated.");

                offsets[i] = BinaryPrimitives.ReadUInt32BigEndian(loca.Slice(i * 4));
            }
        }

        return offsets;
    }

    private static IEnumerable<int> Components(ReadOnlySpan<byte> glyf, uint[] offsets, int gid)
    {
        var result = new List<int>();
        var start = (int)offsets[gid];
        var end = (int)offsets[gid + 1];

        if (end - start < 10 || end > glyf.Length)
            return result;

        var contours = BinaryPrimitives.ReadInt16BigEndian(glyf.Slice(start));

        if (contours >= 0)
            return result;

        var at = start + 10;

        while (at + 4 <= end)
        {
            var flags = BinaryPrimitives.ReadUInt16BigEndian(glyf.Slice(at));
            var component = BinaryPrimitives.ReadUInt16BigEndian(glyf.Slice(at + 2));
            result.Add(component);
            at += 4;
            at += (flags & 0x0001) != 0 ? 4 : 2;

            if ((flags & 0x0008) != 0)
                at += 2;
            else if ((flags & 0x0040) != 0)
                at += 4;
            else if ((flags & 0x0080) != 0)
                at += 8;

            if ((flags & 0x0020) == 0)
                break;
        }

        return result;
    }

    private static Dictionary<int, int> ReadCmap(ReadOnlySpan<byte> cmap)
    {
        var map = new Dictionary<int, int>();
        var count = BinaryPrimitives.ReadUInt16BigEndian(cmap.Slice(2));

        for (var i = 0; i < Math.Min(count, (ushort)32); i++)
        {
            var at = 4 + i * 8;

            if (at + 8 > cmap.Length)
                break;

            var platform = BinaryPrimitives.ReadUInt16BigEndian(cmap.Slice(at));
            var encoding = BinaryPrimitives.ReadUInt16BigEndian(cmap.Slice(at + 2));
            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(cmap.Slice(at + 4));

            if (offset + 4 > cmap.Length)
                continue;

            var unicode = platform == 0 || (platform == 3 && encoding is 1 or 10);

            if (!unicode)
                continue;

            var format = BinaryPrimitives.ReadUInt16BigEndian(cmap.Slice(offset));

            if (format == 4)
                ReadFormat4(cmap.Slice(offset), map);
            else if (format == 12)
                ReadFormat12(cmap.Slice(offset), map);
        }

        if (map.Count == 0)
            throw new InvalidDataException("No Unicode cmap subtable.");

        return map;
    }

    private static void ReadFormat4(ReadOnlySpan<byte> sub, Dictionary<int, int> map)
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(sub.Slice(2));
        var segCount = BinaryPrimitives.ReadUInt16BigEndian(sub.Slice(6)) / 2;
        var endAt = 14;
        var startAt = endAt + segCount * 2 + 2;
        var deltaAt = startAt + segCount * 2;
        var rangeAt = deltaAt + segCount * 2;

        if (rangeAt + segCount * 2 > sub.Length)
            throw new InvalidDataException("cmap format 4 is truncated.");

        for (var s = 0; s < segCount; s++)
        {
            int end = BinaryPrimitives.ReadUInt16BigEndian(sub.Slice(endAt + s * 2));
            int start = BinaryPrimitives.ReadUInt16BigEndian(sub.Slice(startAt + s * 2));
            int delta = BinaryPrimitives.ReadInt16BigEndian(sub.Slice(deltaAt + s * 2));
            int rangeOffset = BinaryPrimitives.ReadUInt16BigEndian(sub.Slice(rangeAt + s * 2));

            if (start > end || end == 0xFFFF && start == 0xFFFF)
                continue;

            for (var c = start; c <= end && c != 0xFFFF; c++)
            {
                int gid;

                if (rangeOffset == 0)
                {
                    gid = (c + delta) & 0xFFFF;
                }
                else
                {
                    var glyphAt = rangeAt + s * 2 + rangeOffset + (c - start) * 2;

                    if (glyphAt + 2 > Math.Min(sub.Length, length == 0 ? sub.Length : length))
                        continue;

                    gid = BinaryPrimitives.ReadUInt16BigEndian(sub.Slice(glyphAt));

                    if (gid != 0)
                        gid = (gid + delta) & 0xFFFF;
                }

                if (gid != 0)
                    map.TryAdd(c, gid);
            }
        }
    }

    private static void ReadFormat12(ReadOnlySpan<byte> sub, Dictionary<int, int> map)
    {
        var groups = BinaryPrimitives.ReadUInt32BigEndian(sub.Slice(12));

        if (groups > 100_000 || 16 + groups * 12 > sub.Length)
            throw new InvalidDataException("cmap format 12 is truncated.");

        for (var g = 0; g < groups; g++)
        {
            var at = 16 + g * 12;
            var start = BinaryPrimitives.ReadUInt32BigEndian(sub.Slice(at));
            var end = BinaryPrimitives.ReadUInt32BigEndian(sub.Slice(at + 4));
            var gid = BinaryPrimitives.ReadUInt32BigEndian(sub.Slice(at + 8));

            if (end < start || end - start > 0x10FFFF)
                continue;

            for (var c = start; c <= end && c <= 0x10FFFF; c++)
                map.TryAdd((int)c, (int)(gid + (c - start)));
        }
    }

    private static byte[] Assemble(ReadOnlySpan<byte> font, List<TableRecord> tables, Dictionary<uint, byte[]> replaced)
    {
        var count = tables.Count;
        var directoryLength = 12 + count * 16;
        var output = new MemoryStream();
        var header = font.Slice(0, 12).ToArray();
        output.Write(header);
        output.Write(new byte[count * 16]);

        var records = new List<(TableRecord Record, int Offset, byte[] Data)>(count);

        foreach (var table in tables.OrderBy(t => t.Offset))
        {
            var data = replaced.TryGetValue(table.Tag, out var r) ? r : font.Slice((int)table.Offset, (int)table.Length).ToArray();

            while (output.Length % 4 != 0)
                output.WriteByte(0);

            var offset = (int)output.Length;
            output.Write(data);
            records.Add((table, offset, data));
        }

        while (output.Length % 4 != 0)
            output.WriteByte(0);

        var bytes = output.ToArray();
        var ordered = records.OrderBy(r => r.Record.Tag).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var (record, offset, data) = ordered[i];
            var at = 12 + i * 16;
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at), record.Tag);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at + 4), Checksum(data));
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at + 8), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at + 12), (uint)data.Length);
        }

        var headEntry = ordered.First(r => r.Record.Tag == HeadTag);
        var adjustment = unchecked(0xB1B0AFBA - Checksum(bytes));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(headEntry.Offset + 8), adjustment);
        return bytes;
    }

    private static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;

        for (var i = 0; i < data.Length; i += 4)
        {
            uint word = 0;

            for (var b = 0; b < 4; b++)
                word = (word << 8) | (i + b < data.Length ? data[i + b] : (byte)0);

            sum = unchecked(sum + word);
        }

        return sum;
    }
}
