// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Brain.Data.Collection;

namespace Brain.IO;

/// <summary>
///     <para>
///         Contains all logic for parsing Brain's native file format. Losslessly reads or writes neural attributes,
///         positions, synapses, and the simulatory time to/from network instances.
///     </para>
///     <para>
///         Layout (every number little-endian):
///         <code>
///         "BRNN"           magic, so a wrong file fails immediately instead of loading garbage
///         u16 version      only bumps on changes old readers can't skip past
///         sections, each:  u32 tag, i64 byte length, payload
///           HEAD           i32 neuron count, f64 time. must come first
///           POTL, REST...  one f32 per neuron, one section per field (see FloatFields)
///           LSPK           one f64 per neuron (last spike)
///           SYNS           outbound CSR: i32 rowPtr[count + 1], i32 columns[nnz], f32 values[nnz]
///         </code>
///     </para>
///     <para>
///         Sections are what keep this forward compatible: a reader skips any tag it doesn't know, and a neuron field
///         missing from an older file just keeps its default. Adding a field (or synaptic delays, plasticity...) means
///         adding a section, not breaking every existing file.
///     </para>
/// </summary>
internal static class BrainFormat
{
    private const ushort Version = 1;

    private static readonly byte[] Magic = [.. "BRNN"u8];

    private static readonly uint HeadTag      = Tag("HEAD"u8);
    private static readonly uint LastSpikeTag = Tag("LSPK"u8);
    private static readonly uint SynapsesTag  = Tag("SYNS"u8);

    // every per-neuron f32 field and the tag it's saved under
    private static readonly (uint Tag, FloatField Field)[] FloatFields =
    [
        (Tag("POTL"u8), pool => pool.Potential),
        (Tag("REST"u8), pool => pool.RestPotential),
        (Tag("RSET"u8), pool => pool.ResetPotential),
        (Tag("LEAK"u8), pool => pool.LeakRate),
        (Tag("THRS"u8), pool => pool.Threshold),
        (Tag("INPT"u8), pool => pool.InputCurrent),
        (Tag("XPOS"u8), pool => pool.X),
        (Tag("YPOS"u8), pool => pool.Y),
        (Tag("ZPOS"u8), pool => pool.Z)
    ];

    /// <summary>Write <paramref name="network" /> to <paramref name="stream" />, committing pending synapses first.</summary>
    public static void Write(Network network, Stream stream)
    {
        EnsureLittleEndian();

        network.Connectome.CommitChanges();

        var pool  = network.Neurons;
        var count = network.Count;

        stream.Write(Magic, 0, Magic.Length);
        WriteUInt16(stream, Version);

        WriteSectionHeader(stream, HeadTag, sizeof(int) + sizeof(double));
        WriteInt32(stream, count);
        WriteInt64(stream, BitConverter.DoubleToInt64Bits(network.Time));

        foreach (var (tag, field) in FloatFields)
        {
            WriteSectionHeader(stream, tag, (long)count * sizeof(float));
            WriteSpan<float>(stream, field(pool));
        }

        WriteSectionHeader(stream, LastSpikeTag, (long)count * sizeof(double));
        WriteSpan<double>(stream, pool.LastSpike);

        // rebuild row pointers from row lengths, since only rows are public
        var rowPtr = new int[count + 1];

        for (var i = 0; i < count; i++)
            rowPtr[i + 1] = rowPtr[i] + network.Connectome.From(i).Count;

        var nnz = rowPtr[count];

        WriteSectionHeader(
            stream,
            SynapsesTag,
            (long)(count + 1) * sizeof(int) + (long)nnz * (sizeof(int) + sizeof(float))
        );
        WriteSpan<int>(stream, rowPtr);

        // every row's columns, then every row's values, so each array stays contiguous in the file
        for (var i = 0; i < count; i++)
            WriteSpan(stream, network.Connectome.From(i).Columns);

        for (var i = 0; i < count; i++)
            WriteSpan(stream, network.Connectome.From(i).Values);
    }

    /// <summary>
    ///     Build a new network from the file in <paramref name="stream" />. The pool and connectome are filled first and
    ///     the network is only constructed at the very end, so an invalid file throws before any network exists.
    /// </summary>
    /// <exception cref="InvalidDataException">The stream isn't a valid Brain network file.</exception>
    public static Network Read(Stream stream)
    {
        EnsureLittleEndian();

        Span<byte> magic = stackalloc byte[4];
        ReadExactly(stream, magic);

        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Not a Brain network file (missing \"BRNN\" magic)");

        var version = ReadUInt16(stream);

        if (version > Version)
            throw new InvalidDataException($"File is version {version}, but this Brain only reads up to {Version}");

        NeuronPool? pool = null;
        var         time = 0d;

        // outbound CSR arrays from SYNS, turned into a connectome once the pool is final
        (int[] RowPtr, int[] Columns, float[] Values)? synapses = null;

        while (TryReadUInt32(stream, out var tag))
        {
            var length = ReadInt64(stream);

            // a negative length would make Skip seek backwards and reread the same section forever
            if (length < 0)
                throw new InvalidDataException($"{TagName(tag)} section has negative length {length}");

            if (tag == HeadTag)
            {
                if (pool is not null)
                    throw new InvalidDataException("File has more than one HEAD section");

                const long headLength = sizeof(int) + sizeof(double);

                if (length < headLength)
                    throw new InvalidDataException($"HEAD section is {length} bytes, expected at least {headLength}");

                var count = ReadInt32(stream);
                time = BitConverter.Int64BitsToDouble(ReadInt64(stream));

                if (count < 0)
                    throw new InvalidDataException($"Negative neuron count {count}");

                // defaults first, so any field this file doesn't have still holds something sane
                pool = new NeuronPool(count);
                pool.Add(count);

                // a newer Brain may append fields to HEAD; skip whatever this version doesn't know about
                Skip(stream, length - headLength);
                continue;
            }

            // everything past HEAD is sized by the neuron count
            if (pool is null)
                throw new InvalidDataException("HEAD section must come first");

            var n = pool.Count;

            if (tag == LastSpikeTag)
            {
                ExpectLength(tag, length, (long)n * sizeof(double));
                ReadSpan(stream, pool.LastSpike);
            }
            else if (tag == SynapsesTag)
                synapses = ReadSynapses(stream, n, length);
            else if (FindFloatField(tag) is { } field)
            {
                ExpectLength(tag, length, (long)n * sizeof(float));
                ReadSpan(stream, field(pool));
            }
            else
                Skip(stream, length); // written by a newer Brain; not ours to understand
        }

        if (pool is null)
            throw new InvalidDataException("File has no HEAD section");

        var connectome = synapses is { } s
            ? new Connectome<float>(pool, s.RowPtr, s.Columns, s.Values)
            : new Connectome<float>(pool);

        return new Network(connectome, time);
    }

    private static (int[] RowPtr, int[] Columns, float[] Values) ReadSynapses(Stream stream, int count, long length)
    {
        var rowPtr = new int[count + 1];
        ReadSpan<int>(stream, rowPtr);

        var nnz = rowPtr[count];

        if (nnz < 0)
            throw new InvalidDataException($"Negative synapse count {nnz}");

        ExpectLength(SynapsesTag, length, (long)(count + 1) * sizeof(int) + (long)nnz * (sizeof(int) + sizeof(float)));

        var columns = new int[nnz];
        var values  = new float[nnz];
        ReadSpan<int>(stream, columns);
        ReadSpan<float>(stream, values);

        // the CSR trusts its arrays completely, so a corrupt file has to be caught here rather than as an
        // IndexOutOfRangeException mid-step (or worse, silently wrong rows)
        if (rowPtr[0] != 0)
            throw new InvalidDataException("Synapse rows must start at 0");

        for (var pre = 0; pre < count; pre++)
        {
            if (rowPtr[pre + 1] < rowPtr[pre])
                throw new InvalidDataException($"Synapse row {pre} ends before it starts");

            for (var k = rowPtr[pre]; k < rowPtr[pre + 1]; k++)
            {
                if ((uint)columns[k] >= (uint)count)
                    throw new InvalidDataException($"Synapse {pre} → {columns[k]} targets a nonexistent neuron");

                if (k > rowPtr[pre] && columns[k] <= columns[k - 1])
                    throw new InvalidDataException($"Synapse row {pre} isn't strictly ascending");

                if (values[k] == 0f)
                {
                    throw new InvalidDataException(
                        $"Synapse {pre} → {columns[k]} has weight 0; absent synapses should not be written"
                    );
                }
            }
        }

        return (rowPtr, columns, values);
    }

    private static FloatField? FindFloatField(uint tag)
    {
        foreach (var (fieldTag, field) in FloatFields)
        {
            if (fieldTag == tag)
                return field;
        }

        return null;
    }

    // packs 4 ASCII chars so that, written little-endian, they read as the same text in a hex editor
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    private static uint Tag(ReadOnlySpan<byte> name)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(name);
    }

    private static string TagName(uint tag)
    {
        return new string(
            new[] { (char)(tag & 0xFF), (char)((tag >> 8) & 0xFF), (char)((tag >> 16) & 0xFF), (char)(tag >> 24) }
        );
    }

    private static void ExpectLength(uint tag, long actual, long expected)
    {
        if (actual != expected)
            throw new InvalidDataException($"{TagName(tag)} section is {actual} bytes, expected {expected}");
    }

    // arrays go to and from the stream as raw memory, which only matches the file's little-endian layout on
    // little-endian hardware. that's every platform .NET and Unity ship to (x86, x64, ARM), so rather than byte-swapping
    // for hardware nobody runs, refuse loudly. supporting big-endian later only touches the helpers below
    private static void EnsureLittleEndian()
    {
        if (!BitConverter.IsLittleEndian)
            throw new PlatformNotSupportedException("Brain network files currently need a little-endian CPU");
    }

    private static void WriteSectionHeader(Stream stream, uint tag, long length)
    {
        WriteUInt32(stream, tag);
        WriteInt64(stream, length);
    }

    private static void WriteSpan<T>(Stream stream, ReadOnlySpan<T> span) where T : struct
    {
        stream.Write(MemoryMarshal.AsBytes(span));
    }

    private static void ReadSpan<T>(Stream stream, Span<T> span) where T : struct
    {
        ReadExactly(stream, MemoryMarshal.AsBytes(span));
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static ushort ReadUInt16(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        ReadExactly(stream, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    private static int ReadInt32(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        ReadExactly(stream, bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static long ReadInt64(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        ReadExactly(stream, bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    // false only at a clean end of file, i.e., between sections
    private static bool TryReadUInt32(Stream stream, out uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        var        read  = stream.Read(bytes);

        if (read == 0)
        {
            value = 0;
            return false;
        }

        ReadExactly(stream, bytes[read..]);
        value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        return true;
    }

    // Stream.Read may return fewer bytes than asked (e.g., network or compressed streams), so loop until full
    // TODO: Stream.ReadExactly on unity 6.8 CoreCLR
    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        while (buffer.Length > 0)
        {
            var read = stream.Read(buffer);

            if (read == 0)
                throw new InvalidDataException("File ends partway through a section");

            buffer = buffer[read..];
        }
    }

    private static void Skip(Stream stream, long length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Attempt to skip negative length");

        if (stream.CanSeek)
        {
            // seeking past the end succeeds silently, and the next read would then look like a clean end of file
            if (length > stream.Length - stream.Position)
                throw new InvalidDataException("File ends partway through a section");

            stream.Seek(length, SeekOrigin.Current);
            return;
        }

        Span<byte> scratch = stackalloc byte[4096];

        while (length > 0)
        {
            var chunk = (int)Math.Min(length, scratch.Length);
            ReadExactly(stream, scratch[..chunk]);
            length -= chunk;
        }
    }

    // ---- primitives ----

    // returns a span over one of the pool's SoA arrays. custom delegate since Span can't be a Func type argument
    private delegate Span<float> FloatField(NeuronPool pool);
}