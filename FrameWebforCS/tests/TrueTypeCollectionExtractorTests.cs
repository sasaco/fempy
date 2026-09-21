using System.Buffers.Binary;
using PDF_Manager.Printing;

namespace PDF_Manager.Tests;

public sealed class TrueTypeCollectionExtractorTests
{
    [Fact]
    public void ExtractFace_RewritesAbsoluteOffsetsAndProducesValidWholeFontChecksum()
    {
        byte[] collection = CreateCollection(0x11223344);
        byte[] original = collection.ToArray();

        byte[] font = TrueTypeCollectionExtractor.ExtractFace(collection, faceIndex: 0);

        Assert.Equal(original, collection);
        Assert.Equal(40, font.Length);
        Assert.Equal(0x00010000u, ReadUInt32(font, 0));
        Assert.Equal(1, ReadUInt16(font, 4));
        Assert.Equal(0x68656164u, ReadUInt32(font, 12));
        Assert.Equal(28u, ReadUInt32(font, 20));
        Assert.Equal(12u, ReadUInt32(font, 24));
        Assert.Equal(0x11223344u, ReadUInt32(font, 28));
        Assert.Equal(0xB1B0AFBAu, ComputeChecksum(font));
    }

    [Fact]
    public void ExtractFace_SelectsRequestedFaceWithoutReadingSiblingFace()
    {
        byte[] collection = CreateTwoFaceCollection(0x11111111, 0x22222222);

        byte[] first = TrueTypeCollectionExtractor.ExtractFace(collection, faceIndex: 0);
        byte[] second = TrueTypeCollectionExtractor.ExtractFace(collection, faceIndex: 1);

        Assert.Equal(0x11111111u, ReadUInt32(first, 28));
        Assert.Equal(0x22222222u, ReadUInt32(second, 28));
        Assert.Equal(0xB1B0AFBAu, ComputeChecksum(first));
        Assert.Equal(0xB1B0AFBAu, ComputeChecksum(second));
    }

    [Theory]
    [MemberData(nameof(InvalidCollections))]
    public void ExtractFace_RejectsMalformedCollectionBeforeUnsafeRead(
        string description,
        byte[] collection,
        int faceIndex,
        string messageFragment)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            TrueTypeCollectionExtractor.ExtractFace(collection, faceIndex));

        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(description));
    }

    public static TheoryData<string, byte[], int, string> InvalidCollections()
    {
        byte[] badSignature = CreateCollection(1);
        WriteUInt32(badSignature, 0, 0);
        byte[] zeroFaces = CreateCollection(1);
        WriteUInt32(zeroFaces, 8, 0);
        byte[] tooManyFaces = CreateCollection(1);
        WriteUInt32(tooManyFaces, 8, 65);
        byte[] truncatedFaceOffsets = CreateCollection(1)[..16];
        WriteUInt32(truncatedFaceOffsets, 8, 2);
        byte[] oversizedFaceOffset = CreateCollection(1);
        WriteUInt32(oversizedFaceOffset, 12, 0x80000000);
        byte[] faceOutsideCollection = CreateCollection(1);
        WriteUInt32(faceOutsideCollection, 12, (uint)(faceOutsideCollection.Length - 4));
        byte[] zeroTables = CreateCollection(1);
        WriteUInt16(zeroTables, 36, 0);
        byte[] tooManyTables = CreateCollection(1);
        WriteUInt16(tooManyTables, 36, 257);
        byte[] truncatedDirectory = CreateCollection(1)[..55];
        byte[] oversizedTableOffset = CreateCollection(1);
        WriteUInt32(oversizedTableOffset, 52, 0x80000000);
        byte[] oversizedTableLength = CreateCollection(1);
        WriteUInt32(oversizedTableLength, 56, 0x80000000);
        byte[] tableOutsideCollection = CreateCollection(1);
        WriteUInt32(tableOutsideCollection, 52, (uint)(tableOutsideCollection.Length - 4));
        WriteUInt32(tableOutsideCollection, 56, 12);
        byte[] missingHead = CreateCollection(1);
        WriteUInt32(missingHead, 44, 0x6E616D65);
        byte[] shortHead = CreateCollection(1);
        WriteUInt32(shortHead, 56, 8);

        return new TheoryData<string, byte[], int, string>
        {
            { "truncated header", new byte[15], 0, "collection" },
            { "bad signature", badSignature, 0, "collection" },
            { "zero faces", zeroFaces, 0, "face count" },
            { "too many faces", tooManyFaces, 0, "face count" },
            { "negative face index", CreateCollection(1), -1, "face count" },
            { "face index at count", CreateCollection(1), 1, "face count" },
            { "truncated face offset array", truncatedFaceOffsets, 0, "range" },
            { "oversized face offset", oversizedFaceOffset, 0, "offset" },
            { "face outside collection", faceOutsideCollection, 0, "range" },
            { "zero tables", zeroTables, 0, "table count" },
            { "too many tables", tooManyTables, 0, "table count" },
            { "truncated directory", truncatedDirectory, 0, "range" },
            { "oversized table offset", oversizedTableOffset, 0, "offset" },
            { "oversized table length", oversizedTableLength, 0, "offset" },
            { "table outside collection", tableOutsideCollection, 0, "range" },
            { "missing head", missingHead, 0, "no head" },
            { "short head", shortHead, 0, "range" },
        };
    }

    private static byte[] CreateCollection(uint marker)
    {
        byte[] bytes = new byte[76];
        WriteUInt32(bytes, 0, 0x74746366);
        WriteUInt32(bytes, 4, 0x00010000);
        WriteUInt32(bytes, 8, 1);
        WriteUInt32(bytes, 12, 32);
        WriteFace(bytes, 32, 64, marker);
        return bytes;
    }

    private static byte[] CreateTwoFaceCollection(uint firstMarker, uint secondMarker)
    {
        byte[] bytes = new byte[156];
        WriteUInt32(bytes, 0, 0x74746366);
        WriteUInt32(bytes, 4, 0x00010000);
        WriteUInt32(bytes, 8, 2);
        WriteUInt32(bytes, 12, 32);
        WriteUInt32(bytes, 16, 60);
        WriteFace(bytes, 32, 128, firstMarker);
        WriteFace(bytes, 60, 144, secondMarker);
        return bytes;
    }

    private static void WriteFace(byte[] bytes, int faceOffset, int tableOffset, uint marker)
    {
        WriteUInt32(bytes, faceOffset, 0x00010000);
        WriteUInt16(bytes, faceOffset + 4, 1);
        WriteUInt32(bytes, faceOffset + 12, 0x68656164);
        WriteUInt32(bytes, faceOffset + 16, 0);
        WriteUInt32(bytes, faceOffset + 20, (uint)tableOffset);
        WriteUInt32(bytes, faceOffset + 24, 12);
        WriteUInt32(bytes, tableOffset, marker);
    }

    private static uint ComputeChecksum(ReadOnlySpan<byte> bytes)
    {
        uint value = 0;
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            value = unchecked(value + BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4)));
        }

        return value;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
}
