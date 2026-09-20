using System.Text;

namespace OvertonesPlayground.Tests.Ontology;

public sealed class RiffWavReaderTests
{
    private const double Tolerance = 1e-6;

    #region Sample formats
    [Fact]
    public void Read_Pcm16Mono_DecodesSamplesToUnitRange()
    {
        byte[] file = RiffBuilder.Wav16(1, 44100, 0, 16384, -16384, short.MaxValue);

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(1, wav.Format.Channels);
        Assert.Equal(44100, wav.Audio.SampleRate);
        Assert.Equal(16, wav.Format.BitsPerSample);
        Assert.Equal(0.0, wav.Audio.Channels[0][0], Tolerance);
        Assert.Equal(0.5, wav.Audio.Channels[0][1], Tolerance);
        Assert.Equal(-0.5, wav.Audio.Channels[0][2], Tolerance);
        Assert.Equal(1.0, wav.Audio.Channels[0][3], 1e-4);
    }

    [Fact]
    public void Read_Pcm24Stereo_DeinterleavesChannelsWithoutLosingPrecision()
    {
        int quarter = 1 << 21;
        byte[] file = RiffBuilder.Wav24(2, 48000, quarter, -quarter, 1, -1);

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(2, wav.Audio.ChannelCount);
        Assert.Equal(2, wav.Audio.FrameCount);
        Assert.Equal(0.25, wav.Audio.Channels[0][0], Tolerance);
        Assert.Equal(-0.25, wav.Audio.Channels[1][0], Tolerance);
        Assert.Equal(1.0 / 8388608.0, wav.Audio.Channels[0][1], 1e-9);
        Assert.Equal(-1.0 / 8388608.0, wav.Audio.Channels[1][1], 1e-9);
    }

    [Fact]
    public void Read_Pcm24MostNegativeValue_SignExtendsToMinusOne()
    {
        byte[] file = RiffBuilder.Wav24(1, 44100, -8388608);

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(-1.0, wav.Audio.Channels[0][0], Tolerance);
    }

    [Fact]
    public void Read_Pcm8Unsigned_CentresSilenceOnZero()
    {
        byte[] file = new RiffBuilder().Format(1, 1, 8000, 8).Data([128, 255, 0]).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(0.0, wav.Audio.Channels[0][0], Tolerance);
        Assert.True(wav.Audio.Channels[0][1] > 0.99);
        Assert.Equal(-1.0, wav.Audio.Channels[0][2], Tolerance);
    }

    [Fact]
    public void Read_Float32_PassesValuesThroughAndReplacesNaN()
    {
        byte[] file = new RiffBuilder().Format(3, 1, 44100, 32).Data(RiffBuilder.Float32(0.25f, -0.75f, float.NaN)).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.True(wav.Format.IsFloat);
        Assert.Equal("Float", wav.Format.EncodingName);
        Assert.Equal(0.25, wav.Audio.Channels[0][0], Tolerance);
        Assert.Equal(-0.75, wav.Audio.Channels[0][1], Tolerance);
        Assert.Equal(0.0, wav.Audio.Channels[0][2], Tolerance);
    }

    [Fact]
    public void Read_ExtensiblePcm24_IsDecodedLikePlainPcm()
    {
        byte[] file = new RiffBuilder().FormatExtensible(1, 2, 96000, 24).Data(RiffBuilder.Pcm24(1 << 22, -(1 << 22))).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.True(wav.Format.IsExtensible);
        Assert.Equal("PCM-Extensible", wav.Format.EncodingName);
        Assert.Equal(96000, wav.Audio.SampleRate);
        Assert.Equal(0.5, wav.Audio.Channels[0][0], Tolerance);
        Assert.Equal(-0.5, wav.Audio.Channels[1][0], Tolerance);
    }

    [Fact]
    public void Read_ExtensibleFloat_IsRecognisedAsFloat()
    {
        byte[] file = new RiffBuilder().FormatExtensible(3, 1, 44100, 32).Data(RiffBuilder.Float32(0.5f)).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.True(wav.Format.IsFloat);
        Assert.Equal(0.5, wav.Audio.Channels[0][0], Tolerance);
    }
    #endregion

    #region Malformed files seen in the real corpus
    [Fact]
    public void Read_DataChunkLargerThanFile_ClampsToTheAudioPresentAndFlagsIt()
    {
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 24).Data(RiffBuilder.Pcm24(100, 200, 300, 400), declaredSize: 1_000_000).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.True(wav.Format.WasTruncated);
        Assert.Equal(4, wav.Audio.FrameCount);
    }

    [Fact]
    public void Read_DataChunkEndingMidFrame_DropsThePartialFrame()
    {
        byte[] pcm = [.. RiffBuilder.Pcm24(100, 200), 0x01, 0x02];
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 24).Data(pcm, declaredSize: 50).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(2, wav.Audio.FrameCount);
    }

    [Fact]
    public void Read_JunkAndBextBeforeFmt_AreSkipped()
    {
        byte[] file = new RiffBuilder()
            .Add("JUNK", new byte[28])
            .Add("bext", new byte[602])
            .Format(1, 1, 44100, 16)
            .Data(RiffBuilder.Pcm16(1000, -1000))
            .Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(2, wav.Audio.FrameCount);
        Assert.Equal(["JUNK", "bext", "fmt ", "data"], wav.Metadata.ChunkIds);
    }

    [Fact]
    public void Read_OddSizedChunkWithoutPadByte_StillFindsTheNextChunk()
    {
        byte[] file = new RiffBuilder()
            .Add("junk", new byte[3], pad: false)
            .Format(1, 1, 44100, 16)
            .Data(RiffBuilder.Pcm16(1, 2, 3))
            .Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(3, wav.Audio.FrameCount);
    }

    [Fact]
    public void Read_ChunkAfterDataIsStillParsed()
    {
        byte[] loop = new byte[36];
        BitConverter.GetBytes(60u).CopyTo(loop, 12);
        BitConverter.GetBytes(1u).CopyTo(loop, 28);
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 16).Data(RiffBuilder.Pcm16(1, 2)).Add("smpl", loop).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(2, wav.Audio.FrameCount);
        Assert.Equal(60, wav.Metadata.SmplUnityNote);
        Assert.Equal(1, wav.Metadata.SampleLoopCount);
    }
    #endregion

    #region Metadata
    [Fact]
    public void Read_AcidChunkWithTempo_ExposesTempoRootAndOneShotFlag()
    {
        byte[] acid = new byte[24];
        BitConverter.GetBytes(0u).CopyTo(acid, 0);
        BitConverter.GetBytes((ushort)57).CopyTo(acid, 4);
        BitConverter.GetBytes(120.0f).CopyTo(acid, 20);
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 16).Add("acid", acid).Data(RiffBuilder.Pcm16(1)).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal(120.0, wav.Metadata.AcidTempo);
        Assert.Equal(57, wav.Metadata.AcidRootNote);
        Assert.False(wav.Metadata.AcidIsOneShot);
    }

    [Fact]
    public void Read_AcidChunkWithZeroTempo_IsTreatedAsMissing()
    {
        byte[] acid = new byte[24];
        BitConverter.GetBytes(1u).CopyTo(acid, 0);
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 16).Add("acid", acid).Data(RiffBuilder.Pcm16(1)).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Null(wav.Metadata.AcidTempo);
        Assert.True(wav.Metadata.AcidIsOneShot);
    }

    [Fact]
    public void Read_ListInfoChunk_ExposesTextTags()
    {
        using MemoryStream info = new();
        info.Write(Encoding.ASCII.GetBytes("INFO"));
        info.Write(Encoding.ASCII.GetBytes("IGNR"));
        info.Write(BitConverter.GetBytes(8u));
        info.Write(Encoding.ASCII.GetBytes("Hip Hop\0"));
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 16).Add("LIST", info.ToArray()).Data(RiffBuilder.Pcm16(1)).Build();

        WavData wav = RiffWavReader.Read(file);

        Assert.Equal("Hip Hop", wav.Metadata.InfoTags["IGNR"]);
    }
    #endregion

    #region Rejected input
    [Fact]
    public void Read_CompressedFormatTag_ThrowsNotSupported()
    {
        byte[] file = new RiffBuilder().Format(2, 1, 44100, 4).Data([1, 2, 3, 4]).Build();

        _ = Assert.Throws<NotSupportedException>(() => RiffWavReader.Read(file));
    }

    [Fact]
    public void Read_AiffContainer_ThrowsNotSupported()
    {
        byte[] file = [.. Encoding.ASCII.GetBytes("FORM"), 0, 0, 0, 4, .. Encoding.ASCII.GetBytes("AIFC")];

        _ = Assert.Throws<NotSupportedException>(() => RiffWavReader.Read(file));
        Assert.False(RiffWavReader.IsRiffWave(file));
    }

    [Fact]
    public void Read_FileWithoutDataChunk_ThrowsInvalidData()
    {
        byte[] file = new RiffBuilder().Format(1, 1, 44100, 16).Build();

        _ = Assert.Throws<InvalidDataException>(() => RiffWavReader.Read(file));
    }

    [Fact]
    public void Read_FileWithoutFmtChunk_ThrowsInvalidData()
    {
        byte[] file = new RiffBuilder().Data([1, 2]).Build();

        _ = Assert.Throws<InvalidDataException>(() => RiffWavReader.Read(file));
    }

    [Fact]
    public void Read_StreamOverload_MatchesTheByteOverload()
    {
        byte[] file = RiffBuilder.Wav24(1, 44100, 1000, -2000, 3000);
        using MemoryStream stream = new(file);

        WavData fromStream = RiffWavReader.Read(stream);
        WavData fromBytes = RiffWavReader.Read(file);

        Assert.Equal(fromBytes.Audio.Channels[0], fromStream.Audio.Channels[0]);
    }
    #endregion
}
