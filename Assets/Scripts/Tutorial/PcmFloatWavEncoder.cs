using System;
using System.IO;
using UnityEngine;

/// <summary>将麦克风录到的 float PCM 单声道编码为 WAV（16-bit LE），供 Whisper 上传。</summary>
public static class PcmFloatWavEncoder
{
    public static byte[] EncodeMono16(float[] samples, int sampleRate)
    {
        if (samples == null || samples.Length == 0)
            return Array.Empty<byte>();

        var pcm = new byte[samples.Length * 2];
        int o = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var f = Mathf.Clamp(samples[i], -1f, 1f);
            short s = (short)Mathf.RoundToInt(f * 32767f);
            pcm[o++] = (byte)(s & 0xff);
            pcm[o++] = (byte)((s >> 8) & 0xff);
        }

        return WrapWav(pcm, sampleRate, channels: 1, bitsPerSample: 16);
    }

    static byte[] WrapWav(byte[] pcm, int sampleRate, short channels, short bitsPerSample)
    {
        using var ms = new MemoryStream(44 + pcm.Length);
        using var bw = new BinaryWriter(ms);

        int blockAlign = (short)(channels * bitsPerSample / 8);
        int byteRate = sampleRate * blockAlign;
        int dataChunkSize = pcm.Length;

        bw.Write(new[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + dataChunkSize);
        bw.Write(new[] { 'W', 'A', 'V', 'E' });
        bw.Write(new[] { 'f', 'm', 't', ' ' });
        bw.Write(16);
        bw.Write((short)1);
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write(bitsPerSample);
        bw.Write(new[] { 'd', 'a', 't', 'a' });
        bw.Write(dataChunkSize);
        bw.Write(pcm);

        return ms.ToArray();
    }
}
