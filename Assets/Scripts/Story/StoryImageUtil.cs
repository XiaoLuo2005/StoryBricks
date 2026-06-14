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
}
