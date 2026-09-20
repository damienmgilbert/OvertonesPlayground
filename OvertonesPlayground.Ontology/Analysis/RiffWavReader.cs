using System.Buffers.Binary;
using System.Text;

namespace OvertonesPlayground.Ontology.Analysis;

///<summary>
///A tolerant RIFF/WAVE decoder for real-world sample libraries. It reads 8/16/24/32-bit integer and 32/64-bit float
///PCM, <c>WAVE_FORMAT_EXTENSIBLE</c>, skips unknown chunks (including <c>JUNK</c> / <c>bext</c> before <c>fmt </c>),
///copes with missing pad bytes, and clamps a <c>data</c> chunk that declares more bytes than the file contains.
///</summary>
public static class RiffWavReader
{
    #region Constants
    private const ushort FormatPcm = 1;
    private const ushort FormatFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;
    #endregion

    #region Private methods
    private static bool LooksLikeFourCc(ReadOnlySpan<byte> data, int position)
    {
        bool isOutOfRange = position < 0 || position + 4 > data.Length;
        if (isOutOfRange)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            byte b = data[position + i];
            bool isPrintable = b is >= 0x20 and < 0x7F;
            if (!isPrintable)
            {
                return false;
            }
        }

        return true;
    }

    ///<summary>Position of the next chunk header, honouring the RIFF pad byte unless the file evidently omits it.</summary>
    private static int NextChunk(ReadOnlySpan<byte> data, long bodyStart, long size)
    {
        long end = bodyStart + size;
        bool isOdd = (size & 1) == 1;
        if (isOdd)
        {
            bool padLooksRight = LooksLikeFourCc(data, (int)Math.Min(end + 1, int.MaxValue));
            bool unpaddedLooksRight = LooksLikeFourCc(data, (int)Math.Min(end, int.MaxValue));
            bool omitsPad = unpaddedLooksRight && !padLooksRight;
            end += omitsPad ? 0 : 1;
        }

        return (int)Math.Min(end, int.MaxValue);
    }

    private static void Decode(ReadOnlySpan<byte> data, WavFormat format, int bytesPerSample, float[][] channels)
    {
        int channelCount = channels.Length;
        int frames = channels[0].Length;
        int stride = channelCount * bytesPerSample;

        for (int frame = 0; frame < frames; frame++)
        {
            ReadOnlySpan<byte> row = data.Slice(frame * stride, stride);
            for (int channel = 0; channel < channelCount; channel++)
            {
                ReadOnlySpan<byte> s = row.Slice(channel * bytesPerSample, bytesPerSample);
                channels[channel][frame] = format.IsFloat
                    ? DecodeFloat(s)
                    : bytesPerSample switch
                    {
                        1 => (s[0] - 128) / 128f,
                        2 => BinaryPrimitives.ReadInt16LittleEndian(s) / 32768f,
                        3 => ((s[0] | (s[1] << 8) | ((sbyte)s[2] << 16))) / 8388608f,
                        _ => BinaryPrimitives.ReadInt32LittleEndian(s) / 2147483648f,
                    };
            }
        }
    }

    private static float DecodeFloat(ReadOnlySpan<byte> sample)
    {
        double value = sample.Length == 8
            ? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(sample))
            : BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample));
        bool isBad = double.IsNaN(value) || double.IsInfinity(value);
        return isBad ? 0f : (float)value;
    }

    private static void ParseInfoList(ReadOnlySpan<byte> body, Dictionary<string, string> tags)
    {
        bool isInfo = body.Length >= 4 && Encoding.ASCII.GetString(body[..4]) == "INFO";
        if (!isInfo)
        {
            return;
        }

        int position = 4;
        while (position + 8 <= body.Length)
        {
            string id = Encoding.ASCII.GetString(body.Slice(position, 4));
            int size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(position + 4, 4)), (uint)int.MaxValue);
            int start = position + 8;
            bool isTruncated = start + size > body.Length;
            if (isTruncated)
            {
                break;
            }

            string value = Encoding.UTF8.GetString(body.Slice(start, size)).TrimEnd('\0', ' ');
            bool hasValue = value.Length > 0 && value.All(c => !char.IsControl(c));
            if (hasValue)
            {
                tags[id] = value;
            }

            position = start + size + (size & 1);
        }
    }
    #endregion

    #region Public methods
    ///<summary>True when <paramref name="data"/> starts with a RIFF/WAVE header.</summary>
    public static bool IsRiffWave(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WAVE"u8);

    ///<summary>Decodes a WAV file held in memory.</summary>
    ///<exception cref="NotSupportedException">The data is not RIFF/WAVE or uses an unsupported encoding.</exception>
    ///<exception cref="InvalidDataException">The header is corrupt.</exception>
    public static WavData Read(ReadOnlySpan<byte> data)
    {
        if (!IsRiffWave(data))
        {
            throw new NotSupportedException("The file is not a RIFF/WAVE file.");
        }

        List<string> chunkIds = [];
        Dictionary<string, string> infoTags = [];
        int loopCount = 0;
        int? unityNote = null;
        double? acidTempo = null;
        int? acidRoot = null;
        bool? acidOneShot = null;

        bool hasFormat = false;
        ushort tag = 0;
        int channelCount = 0;
        int sampleRate = 0;
        int bits = 0;
        int bytesPerSample = 0;
        bool isExtensible = false;

        int dataStart = -1;
        long dataLength = 0;
        bool truncated = false;

        int position = 12;
        while (position + 8 <= data.Length)
        {
            string id = Encoding.ASCII.GetString(data.Slice(position, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(position + 4, 4));
            int bodyStart = position + 8;
            long remaining = data.Length - bodyStart;
            chunkIds.Add(id);

            if (id == "data")
            {
                bool declaresToEnd = size == 0xFFFFFFFF;
                bool overruns = size > remaining;
                dataStart = bodyStart;
                dataLength = declaresToEnd || overruns ? remaining : size;
                truncated = overruns && !declaresToEnd;
                position = NextChunk(data, bodyStart, dataLength);
                continue;
            }

            bool bodyFits = size <= remaining;
            ReadOnlySpan<byte> body = bodyFits ? data.Slice(bodyStart, (int)size) : ReadOnlySpan<byte>.Empty;
            if (bodyFits)
            {
                switch (id)
                {
                    case "fmt " when size >= 16:
                        tag = BinaryPrimitives.ReadUInt16LittleEndian(body);
                        channelCount = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                        sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                        int blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(body[12..]);
                        bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
                        isExtensible = tag == FormatExtensible;
                        if (isExtensible && size >= 26)
                        {
                            tag = BinaryPrimitives.ReadUInt16LittleEndian(body[24..]);
                        }

                        bool blockAlignIsUsable = channelCount > 0 && blockAlign > 0 && blockAlign % channelCount == 0;
                        bytesPerSample = blockAlignIsUsable ? blockAlign / channelCount : (bits + 7) / 8;
                        hasFormat = true;
                        break;
                    case "smpl" when size >= 36:
                        unityNote = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[12..]);
                        loopCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[28..]);
                        break;
                    case "acid" when size >= 24:
                        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(body);
                        acidOneShot = (flags & 1) != 0;
                        acidRoot = BinaryPrimitives.ReadUInt16LittleEndian(body[4..]);
                        float tempo = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(body[20..]));
                        acidTempo = tempo is >= 20f and <= 400f ? Math.Round(tempo, 2) : null;
                        break;
                    case "LIST":
                        ParseInfoList(body, infoTags);
                        break;
                }
            }

            position = bodyFits ? NextChunk(data, bodyStart, size) : data.Length;
        }

        if (!hasFormat)
        {
            throw new InvalidDataException("The WAV file has no fmt chunk.");
        }

        if (dataStart < 0)
        {
            throw new InvalidDataException("The WAV file has no data chunk.");
        }

        bool isFloat = tag == FormatFloat;
        bool isPcm = tag == FormatPcm;
        if (!isPcm && !isFloat)
        {
            throw new NotSupportedException($"WAV format tag {tag} is not supported (only PCM and IEEE float).");
        }

        bool badChannels = channelCount is < 1 or > 16;
        bool badRate = sampleRate is < 1000 or > 768000;
        bool badWidth = isFloat ? bytesPerSample is not (4 or 8) : bytesPerSample is < 1 or > 4;
        if (badChannels || badRate || badWidth)
        {
            throw new InvalidDataException($"Unsupported WAV layout: {channelCount} ch, {sampleRate} Hz, {bytesPerSample * 8}-bit.");
        }

        int frameSize = channelCount * bytesPerSample;
        int frames = (int)(dataLength / frameSize);
        float[][] channels = new float[channelCount][];
        for (int c = 0; c < channelCount; c++)
        {
            channels[c] = new float[frames];
        }

        WavFormat format = new(channelCount, sampleRate, bytesPerSample * 8, isFloat, isExtensible, truncated);
        if (frames > 0)
        {
            Decode(data.Slice(dataStart, frames * frameSize), format, bytesPerSample, channels);
        }

        RiffMetadata metadata = new(chunkIds, loopCount, unityNote, acidTempo, acidRoot, acidOneShot, infoTags);
        return new WavData(new AudioBuffer(sampleRate, channels), metadata, format);
    }

    ///<summary>Decodes a WAV file from <paramref name="stream"/> (which need not be seekable).</summary>
    public static WavData Read(Stream stream)
    {
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return Read(copy.GetBuffer().AsSpan(0, (int)copy.Length));
    }

    ///<summary>Decodes the WAV file at <paramref name="path"/>.</summary>
    public static WavData ReadFile(string path) => Read(File.ReadAllBytes(path));
    #endregion
}
