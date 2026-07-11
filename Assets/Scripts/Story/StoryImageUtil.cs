using System.Collections.Generic;
using UnityEngine;

/// <summary>Sprite / Texture 转可读贴图，供生图 reference_images 上传。</summary>
public static class StoryImageUtil
{
    /// <summary>缩小过长边，降低 base64 请求体体积（避免 nginx 413）。</summary>
    public static Texture2D DownscaleIfNeeded(Texture2D source, int maxEdge, List<Texture2D> ownedCopies)
    {
        if (source == null || maxEdge <= 0)
            return source;

        int w = source.width;
        int h = source.height;
        int maxDim = Mathf.Max(w, h);
        if (maxDim <= maxEdge)
            return source;

        float scale = maxEdge / (float)maxDim;
        int nw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
        int nh = Mathf.Max(1, Mathf.RoundToInt(h * scale));

        var dstRt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(source, dstRt);
            RenderTexture.active = dstRt;
            var copy = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            copy.Apply();
            if (ownedCopies != null)
                ownedCopies.Add(copy);
            return copy;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(dstRt);
        }
    }

    /// <summary>
    /// 提取 Sprite 像素为独立 Texture2D。调用方负责 Destroy（锚图等持久纹理勿销毁）。
    /// </summary>
    public static Texture2D SpriteToTexture(Sprite sprite)
    {
        if (sprite == null)
            return null;

        var source = sprite.texture;
        if (source == null)
            return null;

        var rect = sprite.textureRect;
        int w = (int)rect.width;
        int h = (int)rect.height;
        if (w <= 0 || h <= 0)
            return null;

        if (source.isReadable &&
            rect.x == 0 && rect.y == 0 &&
            w == source.width && h == source.height)
        {
            return source;
        }

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
            copy.Apply();
            return copy;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    public static Texture2D DuplicateTexture(Texture2D source)
    {
        if (source == null)
            return null;

        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        if (source.isReadable)
        {
            copy.SetPixels(source.GetPixels());
            copy.Apply();
            return copy;
        }

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            return copy;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>
    /// 把平面绘本页铺满 2:1 画布（cover 裁切），用于环视展示，避免「中间多贴一张图」。
    /// </summary>
    public static Texture2D BuildEquirectangularCover(Texture2D sourcePage, int outWidth, int outHeight, List<Texture2D> ownedCopies)
    {
        if (sourcePage == null || outWidth < 2 || outHeight < 1)
            return null;

        Texture2D readable = sourcePage.isReadable ? sourcePage : DuplicateTexture(sourcePage);
        if (readable == null)
            return null;
        if (!ReferenceEquals(readable, sourcePage) && ownedCopies != null)
            ownedCopies.Add(readable);

        float scale = Mathf.Max(outWidth / (float)readable.width, outHeight / (float)readable.height);
        int srcW = Mathf.Max(1, Mathf.RoundToInt(readable.width * scale));
        int srcH = Mathf.Max(1, Mathf.RoundToInt(readable.height * scale));
        float u0 = ((srcW - outWidth) * 0.5f) / srcW;
        float v0 = ((srcH - outHeight) * 0.5f) / srcH;
        float u1 = u0 + outWidth / (float)srcW;
        float v1 = v0 + outHeight / (float)srcH;

        var dst = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
        var pixels = new Color32[outWidth * outHeight];
        for (int y = 0; y < outHeight; y++)
        {
            float v = Mathf.Lerp(v0, v1, (y + 0.5f) / outHeight);
            for (int x = 0; x < outWidth; x++)
            {
                float u = Mathf.Lerp(u0, u1, (x + 0.5f) / outWidth);
                pixels[y * outWidth + x] = readable.GetPixelBilinear(u, v);
            }
        }

        dst.SetPixels32(pixels);
        dst.Apply(false, false);
        if (ownedCopies != null)
            ownedCopies.Add(dst);
        return dst;
    }

    /// <summary>
    /// AI 全景种子：原图居中，四周留扩展带（供模型向外延伸，不是最终展示图）。
    /// </summary>
    public static Texture2D BuildEquirectangularSeed(Texture2D sourcePage, int outWidth, int outHeight, List<Texture2D> ownedCopies)
    {
        if (sourcePage == null || outWidth < 2 || outHeight < 1)
            return null;

        Texture2D readable = sourcePage.isReadable ? sourcePage : DuplicateTexture(sourcePage);
        if (readable == null)
            return null;
        if (!ReferenceEquals(readable, sourcePage) && ownedCopies != null)
            ownedCopies.Add(readable);

        float bandH = outHeight * 0.55f;
        float scale = Mathf.Min(outWidth * 0.72f / readable.width, bandH / readable.height);
        int drawW = Mathf.Max(1, Mathf.RoundToInt(readable.width * scale));
        int drawH = Mathf.Max(1, Mathf.RoundToInt(readable.height * scale));
        int x0 = (outWidth - drawW) / 2;
        int y0 = (outHeight - drawH) / 2;

        var dst = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
        var pixels = new Color32[outWidth * outHeight];

        Color32 sky = SampleAvg(readable, 0.5f, 0.85f);
        Color32 ground = SampleAvg(readable, 0.5f, 0.08f);

        for (int y = 0; y < outHeight; y++)
        {
            float vn = y / (float)(outHeight - 1);
            Color32 fill = vn > 0.55f ? sky : ground;
            for (int x = 0; x < outWidth; x++)
                pixels[y * outWidth + x] = fill;
        }

        for (int y = 0; y < drawH; y++)
        {
            float v = (y + 0.5f) / drawH;
            for (int x = 0; x < drawW; x++)
            {
                float u = (x + 0.5f) / drawW;
                int dx = x0 + x;
                int dy = y0 + y;
                if ((uint)dx >= (uint)outWidth || (uint)dy >= (uint)outHeight)
                    continue;
                pixels[dy * outWidth + dx] = readable.GetPixelBilinear(u, v);
            }
        }

        for (int y = y0; y < y0 + drawH; y++)
        {
            float v = (y - y0 + 0.5f) / drawH;
            Color32 leftSample = readable.GetPixelBilinear(0.02f, v);
            Color32 rightSample = readable.GetPixelBilinear(0.98f, v);
            for (int x = 0; x < x0; x++)
                pixels[y * outWidth + x] = leftSample;
            for (int x = x0 + drawW; x < outWidth; x++)
                pixels[y * outWidth + x] = rightSample;
        }

        dst.SetPixels32(pixels);
        dst.Apply(false, false);
        if (ownedCopies != null)
            ownedCopies.Add(dst);
        return dst;
    }

    static Color32 SampleAvg(Texture2D tex, float u, float v)
    {
        Color c = tex.GetPixelBilinear(u, v);
        return (Color32)c;
    }
}
