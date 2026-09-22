using System.Buffers.Binary;

namespace FrameWebforCS.Printing;

internal static class TrueTypeCollectionExtractor
{
    private const uint CollectionSignature = 0x74746366;
    private const uint HeadTableTag = 0x68656164;
    private const uint ChecksumMagic = 0xB1B0AFBA;
    private const int MaximumFaces = 64;
    private const int MaximumTables = 256;
    private const int MaximumFontBytes = 64 * 1024 * 1024;

    public static byte[] ExtractFace(ReadOnlySpan<byte> collection, int faceIndex)
    {
        if (collection.Length < 16 || ReadUInt32(collection, 0) != CollectionSignature)
        {
            throw new InvalidDataException("The installed font is not a TrueType collection.");
        }

        uint faceCount = ReadUInt32(collection, 8);
        if (faceCount is 0 or > MaximumFaces || faceIndex < 0 || (uint)faceIndex >= faceCount)
        {
            throw new InvalidDataException("The TrueType collection has an invalid face count or face index.");
        }

        int faceOffsetsEnd = checked(12 + ((int)faceCount * 4));
        EnsureRange(collection, 0, faceOffsetsEnd);
        int faceOffset = ToInt32(ReadUInt32(collection, 12 + (faceIndex * 4)));
        EnsureRange(collection, faceOffset, 12);
        int tableCount = ReadUInt16(collection, faceOffset + 4);
        if (tableCount is <= 0 or > MaximumTables)
        {
            throw new InvalidDataException("The TrueType collection face has an invalid table count.");
        }

        int directoryLength = checked(12 + (tableCount * 16));
        EnsureRange(collection, faceOffset, directoryLength);
        List<TableRecord> records = new(tableCount);
        int outputLength = Align4(directoryLength);
        for (int index = 0; index < tableCount; index++)
        {
            int recordOffset = checked(faceOffset + 12 + (index * 16));
            uint tag = ReadUInt32(collection, recordOffset);
            uint checksum = ReadUInt32(collection, recordOffset + 4);
            int sourceOffset = ToInt32(ReadUInt32(collection, recordOffset + 8));
            int length = ToInt32(ReadUInt32(collection, recordOffset + 12));
            EnsureRange(collection, sourceOffset, length);
            int outputOffset = outputLength;
            outputLength = checked(outputLength + Align4(length));
            if (outputLength > MaximumFontBytes)
            {
                throw new InvalidDataException("The extracted installed font exceeds the font byte limit.");
            }

            records.Add(new TableRecord(tag, checksum, sourceOffset, outputOffset, length));
        }

        byte[] font = new byte[outputLength];
        collection.Slice(faceOffset, 12).CopyTo(font);
        int? headOffset = null;
        for (int index = 0; index < records.Count; index++)
        {
            TableRecord record = records[index];
            int outputRecord = 12 + (index * 16);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(outputRecord, 4), record.Tag);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(outputRecord + 4, 4), record.Checksum);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(outputRecord + 8, 4), (uint)record.OutputOffset);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(outputRecord + 12, 4), (uint)record.Length);
            collection.Slice(record.SourceOffset, record.Length).CopyTo(font.AsSpan(record.OutputOffset));
            if (record.Tag == HeadTableTag)
            {
                headOffset = record.OutputOffset;
            }
        }

        if (headOffset is null)
        {
            throw new InvalidDataException("The installed TrueType face has no head table.");
        }

        EnsureRange(font, headOffset.Value, 12);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headOffset.Value + 8, 4), 0);
        uint checksumValue = ComputeChecksum(font);
        uint adjustment = unchecked(ChecksumMagic - checksumValue);
        BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headOffset.Value + 8, 4), adjustment);
        return font;
    }

    private static uint ComputeChecksum(ReadOnlySpan<byte> bytes)
    {
        uint checksum = 0;
        int fullWordLength = bytes.Length & ~3;
        for (int offset = 0; offset < fullWordLength; offset += 4)
        {
            checksum = unchecked(checksum + BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4)));
        }

        if (fullWordLength != bytes.Length)
        {
            Span<byte> last = stackalloc byte[4];
            bytes[fullWordLength..].CopyTo(last);
            checksum = unchecked(checksum + BinaryPrimitives.ReadUInt32BigEndian(last));
        }

        return checksum;
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        EnsureRange(bytes, offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        EnsureRange(bytes, offset, 4);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
    }

    private static int ToInt32(uint value)
    {
        if (value > int.MaxValue)
        {
            throw new InvalidDataException("The installed font contains an oversized table offset.");
        }

        return (int)value;
    }

    private static void EnsureRange(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The installed font contains an invalid table range.");
        }
    }

    private readonly record struct TableRecord(
        uint Tag,
        uint Checksum,
        int SourceOffset,
        int OutputOffset,
        int Length);
}
