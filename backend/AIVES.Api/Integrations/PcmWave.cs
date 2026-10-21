using System.Text;
using AIVES.Api.Features.Workflow;
using static AIVES.Api.Features.Workflow.WorkflowRules;

namespace AIVES.Api.Integrations;

public static class PcmWave
{
    public static void ValidateInput(byte[] wav)
    {
        Require(wav.Length is >= 46 and <= 480044 && wav.Length % 2 == 0 && Encoding.ASCII.GetString(wav, 0, 4) == "RIFF"
            && Encoding.ASCII.GetString(wav, 8, 8) == "WAVEfmt " && BitConverter.ToInt32(wav, 16) == 16
            && BitConverter.ToInt16(wav, 20) == 1 && BitConverter.ToInt16(wav, 22) == 1 && BitConverter.ToInt32(wav, 24) == 16000
            && BitConverter.ToInt32(wav, 28) == 32000 && BitConverter.ToInt16(wav, 32) == 2 && BitConverter.ToInt16(wav, 34) == 16
            && Encoding.ASCII.GetString(wav, 36, 4) == "data" && BitConverter.ToInt32(wav, 40) == wav.Length - 44
            && BitConverter.ToInt32(wav, 4) == wav.Length - 8, "Audio phải là PCM WAV mono 16kHz 16bit, tối đa 15 giây.", 415);
    }

    public static byte[] Output(byte[] audio, string mime)
    {
        Require(audio.Length is > 0 and <= 16000000, "ProviderInvalidResponse: audio rỗng hoặc quá lớn.", 502);
        if (mime.Split(';')[0] is "audio/wav" or "audio/x-wav")
        {
            Require(audio.Length >= 44 && Encoding.ASCII.GetString(audio, 0, 4) == "RIFF" && Encoding.ASCII.GetString(audio, 8, 4) == "WAVE", "ProviderInvalidResponse: WAV thiếu header.", 502);
            return audio; // New Gemini TTS returns WAV; do not wrap it twice.
        }
        Require(mime.StartsWith("audio/L16", StringComparison.OrdinalIgnoreCase) || mime.StartsWith("audio/pcm", StringComparison.OrdinalIgnoreCase), "ProviderInvalidResponse: MIME TTS chưa hỗ trợ.", 502);
        var rate = 24000;
        var match = System.Text.RegularExpressions.Regex.Match(mime, "rate=(\\d+)");
        if (match.Success) Require(int.TryParse(match.Groups[1].Value, out rate) && rate is >= 8000 and <= 48000, "ProviderInvalidResponse: sample rate sai.", 502);
        Require(audio.Length % 2 == 0, "ProviderInvalidResponse: PCM16 không đủ sample.", 502);
        return Wrap(audio, rate);
    }

    public static byte[] Wrap(byte[] pcm, int rate)
    {
        using var buffer = new MemoryStream(); using var w = new BinaryWriter(buffer, Encoding.ASCII, true);
        w.Write("RIFF"u8); w.Write(36 + pcm.Length); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(pcm.Length); w.Write(pcm);
        return buffer.ToArray();
    }

    // Provider probe / test utility: decode PCM16 mono WAV chunks and resample without external tools.
    public static byte[] ToInput(byte[] wav)
    {
        Require(wav.Length >= 44 && Encoding.ASCII.GetString(wav, 0, 4) == "RIFF" && Encoding.ASCII.GetString(wav, 8, 4) == "WAVE", "Cần WAV PCM16 mono.", 415);
        var rate = 0; byte[]? pcm = null;
        for (var offset = 12; offset + 8 <= wav.Length;)
        {
            var length = BitConverter.ToInt32(wav, offset + 4);
            Require(length >= 0 && length <= wav.Length - offset - 8, "WAV chunk sai.", 415);
            var name = Encoding.ASCII.GetString(wav, offset, 4);
            if (name == "fmt ")
            {
                Require(length >= 16 && BitConverter.ToInt16(wav, offset + 8) == 1 && BitConverter.ToInt16(wav, offset + 10) == 1 && BitConverter.ToInt16(wav, offset + 22) == 16, "Chỉ hỗ trợ WAV PCM16 mono.", 415);
                rate = BitConverter.ToInt32(wav, offset + 12);
            }
            if (name == "data") pcm = wav.AsSpan(offset + 8, length).ToArray();
            offset += 8 + length + (length & 1);
        }
        Require(rate is >= 8000 and <= 96000 && pcm is { Length: > 0 } && pcm.Length % 2 == 0 && pcm.Length / (double)(rate * 2) <= 15, "WAV cần PCM16 mono tối đa 15 giây.", 415);
        var count = (int)((long)(pcm.Length / 2) * 16000 / rate); var result = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var source = i * rate / 16000.0; var index = (int)source;
            var a = BitConverter.ToInt16(pcm, index * 2); var b = BitConverter.ToInt16(pcm, Math.Min(index + 1, pcm.Length / 2 - 1) * 2);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(i * 2, 2), (short)(a + (b - a) * (source - index)));
        }
        return Wrap(result, 16000);
    }
}
