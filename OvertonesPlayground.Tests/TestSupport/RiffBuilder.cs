using System.Text;

namespace OvertonesPlayground.Tests.TestSupport;

///<summary>
///Builds RIFF/WAVE bytes chunk by chunk, including the malformed shapes seen in real sample libraries (data chunk
///larger than the file, chunks before <c>fmt </c>, missing pad bytes), so the tolerant reader can be tested precisely.
///</summary>
public sealed class RiffBuilder
{
    #region Fields
    private readonly List<byte[]> _chunks = [];
    #endregion

    #region Private methods
    private static byte[] Chunk(string id, byte[] body, uint? declaredSize = null, bool pad = true)
    {
        using MemoryStream stream = new();
        stream.Write(Encoding.ASCII.GetBytes(id));
        stream.Write(BitConverter.GetBytes(declaredSize ?? (uint)body.Length));
        stream.Write(body);
        if (pad && body.Length % 2 == 1)
        {
            stream.WriteByte(0);
        }

        return stream.ToArray();
    }
    #endregion

    #region Public methods
    ///<summary>24-bit little-endian PCM bytes for integer sample values.</summary>
    public static byte[] Pcm24(params int[] samples) => [.. samples.SelectMany(sample => new[] { (byte)sample, (byte)(sample >> 8), (byte)(sample >> 16) })];

    ///<summary>16-bit little-endian PCM bytes.</summary>
    public static byte[] Pcm16(params short[] samples) => [.. samples.SelectMany(BitConverter.GetBytes)];

    ///<summary>32-bit float bytes.</summary>
    public static byte[] Float32(params float[] samples) => [.. samples.SelectMany(BitConverter.GetBytes)];

    ///<summary>Appends any chunk.</summary>
    public RiffBuilder Add(string id, byte[] body, uint? declaredSize = null, bool pad = true)
    {
        _chunks.Add(Chunk(id, body, declaredSize, pad));
        return this;
    }

    ///<summary>Appends a <c>data</c> chunk; pass <paramref name="declaredSize"/> to make it lie about its length.</summary>
    public RiffBuilder Data(byte[] pcm, uint? declaredSize = null) => Add("data", pcm, declaredSize);

    ///<summary>Appends a plain 16-byte <c>fmt </c> chunk.</summary>
    public RiffBuilder Format(ushort tag, ushort channels, uint sampleRate, ushort bits)
    {
        ushort blockAlign = (ushort)(channels * bits / 8);
        using MemoryStream body = new();
        body.Write(BitConverter.GetBytes(tag));
        body.Write(BitConverter.GetBytes(channels));
        body.Write(BitConverter.GetBytes(sampleRate));
        body.Write(BitConverter.GetBytes(sampleRate * blockAlign));
        body.Write(BitConverter.GetBytes(blockAlign));
        body.Write(BitConverter.GetBytes(bits));
        return Add("fmt ", body.ToArray());
    }

    ///<summary>Appends a 40-byte <c>WAVE_FORMAT_EXTENSIBLE</c> <c>fmt </c> chunk.</summary>
    public RiffBuilder FormatExtensible(ushort subFormat, ushort channels, uint sampleRate, ushort bits)
    {
        ushort blockAlign = (ushort)(channels * bits / 8);
        using MemoryStream body = new();
        body.Write(BitConverter.GetBytes((ushort)0xFFFE));
        body.Write(BitConverter.GetBytes(channels));
        body.Write(BitConverter.GetBytes(sampleRate));
        body.Write(BitConverter.GetBytes(sampleRate * blockAlign));
        body.Write(BitConverter.GetBytes(blockAlign));
        body.Write(BitConverter.GetBytes(bits));
        body.Write(BitConverter.GetBytes((ushort)22));
        body.Write(BitConverter.GetBytes(bits));
        body.Write(BitConverter.GetBytes(3u));
        body.Write(BitConverter.GetBytes(subFormat));
        body.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71 });
        return Add("fmt ", body.ToArray());
    }

    ///<summary>Assembles the file: <c>RIFF</c> header plus every chunk added so far.</summary>
    public byte[] Build()
    {
        using MemoryStream stream = new();
        stream.Write(Encoding.ASCII.GetBytes("RIFF"));
        stream.Write(BitConverter.GetBytes((uint)(4 + _chunks.Sum(chunk => chunk.Length))));
        stream.Write(Encoding.ASCII.GetBytes("WAVE"));
        foreach (byte[] chunk in _chunks)
        {
            stream.Write(chunk);
        }

        return stream.ToArray();
    }

    ///<summary>A complete 16-bit PCM file.</summary>
    public static byte[] Wav16(ushort channels, uint sampleRate, params short[] samples) =>
        new RiffBuilder().Format(1, channels, sampleRate, 16).Data(Pcm16(samples)).Build();

    ///<summary>A complete 24-bit PCM file.</summary>
    public static byte[] Wav24(ushort channels, uint sampleRate, params int[] samples) =>
        new RiffBuilder().Format(1, channels, sampleRate, 24).Data(Pcm24(samples)).Build();
    #endregion
}
