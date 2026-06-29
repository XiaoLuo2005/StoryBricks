using System;
using System.IO;
using UnityEngine;

/// <summary>读写 16-bit PCM mono WAV，供用户录音播放。</summary>
public static class WavFileUtil
{
    public static void WriteMono16(string path, float[] samples, int sampleRate)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path required");
        if (samples == null)
            samples = Array.Empty<float>();

        byte[] wav = PcmFloatWavEncoder.EncodeMono16(samples, sampleRate);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, wav);
    }

    public static AudioClip LoadClip(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 44)
            return null;

        if (bytes[0] != (byte)'R' || bytes[1] != (byte)'I' || bytes[2] != (byte)'F' || bytes[3] != (byte)'F')
            return null;

        int channels = BitConverter.ToInt16(bytes, 22);
        int sampleRate = BitConverter.ToInt32(bytes, 24);
        int bits = BitConverter.ToInt16(bytes, 34);
        if (channels <= 0 || sampleRate <= 0 || bits != 16)
            return null;

        int dataOffset = 12;
        while (dataOffset + 8 <= bytes.Length)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(bytes, dataOffset, 4);
            int chunkSize = BitConverter.ToInt32(bytes, dataOffset + 4);
            if (chunkId == "data")
            {
                dataOffset += 8;
                int sampleCount = chunkSize / 2 / channels;
                var interleaved = new float[sampleCount * channels];
                for (int i = 0; i < interleaved.Length; i++)
                {
                    short s = BitConverter.ToInt16(bytes, dataOffset + i * 2);
                    interleaved[i] = s / 32768f;
                }

                float[] mono;
                if (channels == 1)
                {
                    mono = interleaved;
                }
                else
                {
                    mono = new float[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float sum = 0f;
                        for (int c = 0; c < channels; c++)
                            sum += interleaved[i * channels + c];
                        mono[i] = sum / channels;
                    }
                }

                var clip = AudioClip.Create("wav", mono.Length, 1, sampleRate, false);
                clip.SetData(mono, 0);
                return clip;
            }

            dataOffset += 8 + chunkSize;
            if (chunkSize % 2 != 0)
                dataOffset++;
        }

        return null;
    }
}
