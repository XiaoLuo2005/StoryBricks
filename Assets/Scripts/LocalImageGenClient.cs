using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LocalImageGenClient : MonoBehaviour
{
    private const string DefaultSize = "1920*1080";
    private const int MaxReferenceImages = 4;

    /// <summary>进行中的网络请求；退出播放或禁用物体时必须 Abort/Dispose，否则易出现 “invalid GC handle / previous domain”。</summary>
    private UnityWebRequest activeWebRequest;

    [Serializable]
    public class MarkerPromptMapping
    {
        public int markerId;
        [TextArea(2, 5)]
        public string prompt;
    }

    [Header("UI")]
    public RawImage targetImage;

    [Header("生图云端")]
    public string serverUrl = "http://39.97.174.49:8800/generate";

    [Header("360 全景")]
    [Tooltip("留空则从 serverUrl 推导为 …/generate-panorama")]
    public string panoramaServerUrl = "";

    [Tooltip("图生全景时上传源图的最长边（过大易 413）")]
    public int maxPanoramaSourceUploadEdge = 768;

    public string panoramaSize = "1536*768";

    [Header("Generation")]
    public string model = "wan2.6-image";
    public string size = DefaultSize;

    [Tooltip("上传 reference_images 前缩小最长边，避免请求体过大（nginx 413）")]
    public int maxReferenceUploadEdge = 512;

    [HideInInspector]
    [SerializeField]
    private string prompt = "A cute cartoon rabbit reading a storybook in a cozy room.";

    [Header("调试：img2img 参考图（最多 4 张）")]
    [Tooltip("Inspector 调试或右键 Generate Image With References 时使用")]
    public Texture2D[] debugReferenceImages;

    [Header("ArUco ID → 提示词")]
    [Tooltip("识别到对应 markerId 时使用该行提示词请求生图；多条相同 ID 时取列表中最靠前的一条。留空则沿用旧逻辑：仅 ID 0 使用组件内保存的默认提示词（已隐藏，可通过右键 Generate Image 调试）。")]
    public MarkerPromptMapping[] markerPromptMappings;

    void Start()
    {
        SetImageUIVisibility(false);
    }

    /// <summary>
    /// 根据 ArUco 标记 ID 查找配置的提示词并生图；未配置则跳过。
    /// </summary>
    public void GenerateImageForMarker(List<int> markerIds)
    {
        GenerateImageForMarker(markerIds, null);
    }

    /// <summary>
    /// 根据 ArUco ID 拼 Prompt，并可附带参考图走 img2img。
    /// </summary>
    public void GenerateImageForMarker(List<int> markerIds, Texture2D[] referenceTextures)
    {
        if (markerIds == null || markerIds.Count == 0) return;

        var validPrompts = new List<string>();
        foreach (int id in markerIds)
        {
            string resolved = ResolvePromptForMarker(id);
            if (!string.IsNullOrEmpty(resolved))
                validPrompts.Add(resolved);
        }

        if (validPrompts.Count == 0)
        {
            Debug.LogWarning("[Client] 传入的所有 ID 均未配置有效的提示词，已跳过生图。");
            return;
        }

        string combinedPrompt = string.Join(", ", validPrompts);
        Debug.Log($"[Client] 组合提示词生图: \"{combinedPrompt}\"");
        GenerateImage(combinedPrompt, referenceTextures);
    }

    /// <summary>文生图。</summary>
    public void GenerateImage(string promptToSend)
    {
        GenerateImage(promptToSend, (Texture2D[])null);
    }

    /// <summary>文生图或 img2img；referenceTextures 非空时走图像编辑（最多 4 张）。</summary>
    public void GenerateImage(string promptToSend, Texture2D[] referenceTextures)
    {
        StopAllCoroutines();
        CancelActiveWebRequest();
        StartCoroutine(GenerateImageCoroutine(promptToSend, referenceTextures));
    }

    /// <summary>从 Sprite 角色参考图生图。</summary>
    public void GenerateImageFromSprites(string promptToSend, Sprite[] referenceSprites)
    {
        Texture2D[] textures = SpritesToTextures(referenceSprites);
        GenerateImage(promptToSend, textures);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        CancelActiveWebRequest();
    }

    private void CancelActiveWebRequest()
    {
        if (activeWebRequest == null)
            return;
        activeWebRequest.Abort();
        activeWebRequest.Dispose();
        activeWebRequest = null;
    }

    private static void ReleaseWebRequest(ref UnityWebRequest req)
    {
        if (req != null)
        {
            req.Dispose();
            req = null;
        }
    }

    private string ResolvePromptForMarker(int markerId)
    {
        if (markerPromptMappings == null || markerPromptMappings.Length == 0)
        {
            if (markerId == 0 && !string.IsNullOrWhiteSpace(prompt))
                return prompt.Trim();
            return null;
        }

        foreach (var entry in markerPromptMappings)
        {
            if (entry != null && entry.markerId == markerId && !string.IsNullOrWhiteSpace(entry.prompt))
                return entry.prompt.Trim();
        }

        return null;
    }

    [ContextMenu("Generate Image")]
    public void GenerateImageDebug()
    {
        GenerateImage(prompt);
    }

    [ContextMenu("Generate Image With References")]
    public void GenerateImageWithReferencesDebug()
    {
        GenerateImage(prompt, debugReferenceImages);
    }

    public class GenerateOutcome
    {
        public bool success;
        public Texture2D texture;
        public string imageUrl;
        public string errorMessage;
    }

    /// <summary>协程内等待生图完成，结果写入 outcome。</summary>
    public IEnumerator GenerateImageAndWait(string promptToSend, Texture2D[] referenceTextures, GenerateOutcome outcome)
    {
        if (outcome == null)
            yield break;

        outcome.success = false;
        outcome.texture = null;
        outcome.imageUrl = null;
        outcome.errorMessage = null;

        if (targetImage == null)
        {
            outcome.errorMessage = "targetImage is not assigned.";
            yield break;
        }

        string[] referenceImages = null;
        var tempTextures = new List<Texture2D>();
        try
        {
            referenceImages = BuildReferenceImagePayload(referenceTextures, tempTextures);
        }
        catch (Exception ex)
        {
            outcome.errorMessage = ex.Message;
            yield break;
        }

        string json = BuildRequestJson(promptToSend, referenceImages);
        if (referenceImages != null && referenceImages.Length > 0)
            Debug.Log($"[Client] img2img 请求，参考图 {referenceImages.Length} 张，请求体约 {json.Length / 1024} KB");

        var req = new UnityWebRequest(serverUrl, UnityWebRequest.kHttpVerbPOST);
        activeWebRequest = req;
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        bool genOk = req.result == UnityWebRequest.Result.Success;
        string genBody = req.downloadHandler != null ? req.downloadHandler.text : "";
        string genErr = req.error;

        if (activeWebRequest == req)
        {
            activeWebRequest = null;
            ReleaseWebRequest(ref req);
        }

        CleanupTempTextures(tempTextures);

        if (!genOk)
        {
            outcome.errorMessage = (genErr ?? "request failed") + "\n" + genBody;
            yield break;
        }

        var response = JsonUtility.FromJson<GenerateResponse>(genBody);
        if (response == null || string.IsNullOrEmpty(response.image_url))
        {
            outcome.errorMessage = "Invalid generate response: " + genBody;
            yield break;
        }

        if (!isActiveAndEnabled)
        {
            outcome.errorMessage = "LocalImageGenClient disabled during generation.";
            yield break;
        }

        yield return DownloadTextureToOutcome(response.image_url, outcome);
    }

    /// <summary>把已生成的绘本页扩展为 360° equirectangular 全景（图生图）。</summary>
    public IEnumerator GeneratePanoramaAndWait(string promptToSend, Texture2D sourcePageTexture, GenerateOutcome outcome)
    {
        if (outcome == null)
            yield break;

        outcome.success = false;
        outcome.texture = null;
        outcome.imageUrl = null;
        outcome.errorMessage = null;

        if (sourcePageTexture == null)
        {
            outcome.errorMessage = "sourcePageTexture is required for panorama img2img.";
            yield break;
        }

        var tempTextures = new List<Texture2D>();
        string sourceDataUrl;
        try
        {
            // 先把平面页铺进 2:1 种子画布，再让模型做边缘扩展，避免整幅重画成另一场景
            int seedH = 512;
            int seedW = seedH * 2;
            if (maxPanoramaSourceUploadEdge > 0)
            {
                seedW = Mathf.Min(seedW, maxPanoramaSourceUploadEdge * 2);
                seedH = Mathf.Max(256, seedW / 2);
                seedW = seedH * 2;
            }

            Texture2D seed = StoryImageUtil.BuildEquirectangularSeed(
                sourcePageTexture, seedW, seedH, tempTextures);
            if (seed == null)
                throw new InvalidOperationException("BuildEquirectangularSeed failed");

            byte[] png = seed.EncodeToPNG();
            if (png == null || png.Length == 0)
                throw new InvalidOperationException("EncodeToPNG failed for panorama source");
            sourceDataUrl = "data:image/png;base64," + Convert.ToBase64String(png);
        }
        catch (Exception ex)
        {
            CleanupTempTextures(tempTextures);
            outcome.errorMessage = ex.Message;
            yield break;
        }

        string json = BuildPanoramaRequestJson(promptToSend, sourceDataUrl);
        Debug.Log($"[Client] 全景图生图请求体约 {json.Length / 1024} KB（2:1 种子图）");

        var req = new UnityWebRequest(ResolvePanoramaServerUrl(), UnityWebRequest.kHttpVerbPOST);
        activeWebRequest = req;
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        bool genOk = req.result == UnityWebRequest.Result.Success;
        string genBody = req.downloadHandler != null ? req.downloadHandler.text : "";
        string genErr = req.error;

        if (activeWebRequest == req)
        {
            activeWebRequest = null;
            ReleaseWebRequest(ref req);
        }

        CleanupTempTextures(tempTextures);

        if (!genOk)
        {
            outcome.errorMessage = (genErr ?? "request failed") + "\n" + genBody;
            yield break;
        }

        var response = JsonUtility.FromJson<GenerateResponse>(genBody);
        if (response == null || string.IsNullOrEmpty(response.image_url))
        {
            outcome.errorMessage = "Invalid panorama response: " + genBody;
            yield break;
        }

        if (!isActiveAndEnabled)
        {
            outcome.errorMessage = "LocalImageGenClient disabled during panorama generation.";
            yield break;
        }

        yield return DownloadTextureToOutcome(response.image_url, outcome);
        if (outcome.success)
            Debug.Log($"[Client] 全景图生成功 mode={response.mode} url={outcome.imageUrl}");
    }

    public string ResolvePanoramaServerUrl()
    {
        if (!string.IsNullOrWhiteSpace(panoramaServerUrl))
            return panoramaServerUrl.Trim();

        string baseUrl = (serverUrl ?? "").Trim();
        if (string.IsNullOrEmpty(baseUrl))
            return "http://127.0.0.1:8800/generate-panorama";

        const string generateSuffix = "/generate";
        if (baseUrl.EndsWith(generateSuffix, StringComparison.OrdinalIgnoreCase))
            return baseUrl.Substring(0, baseUrl.Length - generateSuffix.Length) + "/generate-panorama";

        return baseUrl.TrimEnd('/') + "/generate-panorama";
    }

    private string BuildPanoramaRequestJson(string promptToSend, string sourceImageDataUrl)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"prompt\":").Append(JsonQuote(promptToSend ?? "")).Append(',');
        sb.Append("\"source_image\":").Append(JsonQuote(sourceImageDataUrl)).Append(',');
        sb.Append("\"model\":").Append(JsonQuote(model)).Append(',');
        sb.Append("\"size\":").Append(JsonQuote(string.IsNullOrWhiteSpace(panoramaSize) ? "1536*768" : panoramaSize.Trim())).Append(',');
        sb.Append("\"n\":1");
        sb.Append('}');
        return sb.ToString();
    }

    private IEnumerator GenerateImageCoroutine(string promptToSend, Texture2D[] referenceTextures)
    {
        var outcome = new GenerateOutcome();
        yield return GenerateImageAndWait(promptToSend, referenceTextures, outcome);
        if (!outcome.success)
        {
            if (!string.IsNullOrEmpty(outcome.errorMessage))
                Debug.LogError(outcome.errorMessage);
            yield break;
        }

        if (targetImage == null || outcome.texture == null)
            yield break;

        SetImageUIVisibility(true);
        var previous = targetImage.texture as Texture2D;
        targetImage.texture = outcome.texture;
        if (previous != null && previous != outcome.texture)
            Destroy(previous);

        Debug.Log("Image generated and applied to RawImage.");
    }

    private string BuildRequestJson(string promptToSend, string[] referenceImages)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"prompt\":").Append(JsonQuote(promptToSend)).Append(',');
        sb.Append("\"model\":").Append(JsonQuote(model)).Append(',');
        sb.Append("\"size\":").Append(JsonQuote(string.IsNullOrWhiteSpace(size) ? DefaultSize : size.Trim())).Append(',');
        sb.Append("\"n\":1");

        if (referenceImages != null && referenceImages.Length > 0)
        {
            sb.Append(",\"reference_images\":[");
            for (int i = 0; i < referenceImages.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonQuote(referenceImages[i]));
            }
            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private string[] BuildReferenceImagePayload(Texture2D[] referenceTextures, List<Texture2D> tempTextures)
    {
        if (referenceTextures == null || referenceTextures.Length == 0)
            return null;

        var urls = new List<string>();
        int count = Mathf.Min(referenceTextures.Length, MaxReferenceImages);
        for (int i = 0; i < count; i++)
        {
            var tex = referenceTextures[i];
            if (tex == null) continue;
            urls.Add(TextureToDataUrl(tex, tempTextures));
        }

        return urls.Count > 0 ? urls.ToArray() : null;
    }

    private static Texture2D[] SpritesToTextures(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        var list = new List<Texture2D>();
        foreach (var sprite in sprites)
        {
            if (sprite == null) continue;
            list.Add(sprite.texture);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    private string TextureToDataUrl(Texture2D source, List<Texture2D> tempTextures)
    {
        Texture2D readable = EnsureReadable(source, tempTextures);
        Texture2D upload = StoryImageUtil.DownscaleIfNeeded(readable, maxReferenceUploadEdge, tempTextures);
        byte[] png = upload.EncodeToPNG();
        if (png == null || png.Length == 0)
            throw new InvalidOperationException("EncodeToPNG failed");
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    private static Texture2D EnsureReadable(Texture2D source, List<Texture2D> tempTextures)
    {
        if (source.isReadable)
            return source;

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            tempTextures.Add(copy);
            return copy;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static void CleanupTempTextures(List<Texture2D> tempTextures)
    {
        if (tempTextures == null) return;
        foreach (var tex in tempTextures)
        {
            if (tex != null)
                Destroy(tex);
        }
        tempTextures.Clear();
    }

    private static string JsonQuote(string value)
    {
        if (value == null) return "null";
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private IEnumerator DownloadTextureToOutcome(string imageUrl, GenerateOutcome outcome)
    {
        var req = UnityWebRequestTexture.GetTexture(imageUrl);
        activeWebRequest = req;

        yield return req.SendWebRequest();

        bool ok = req.result == UnityWebRequest.Result.Success;
        string dlErr = req.error;
        Texture2D tex = null;
        if (ok && req.downloadHandler != null)
            tex = DownloadHandlerTexture.GetContent(req);

        if (activeWebRequest == req)
        {
            activeWebRequest = null;
            ReleaseWebRequest(ref req);
        }

        if (!ok || tex == null)
        {
            outcome.errorMessage = "Download image failed: " + dlErr;
            yield break;
        }

        outcome.success = true;
        outcome.texture = tex;
        outcome.imageUrl = imageUrl;
        Debug.Log($"[Client] 生图成功 url={imageUrl}");
    }

    [Serializable]
    private class GenerateResponse
    {
        public string task_id;
        public string image_url;
        public string model;
        public string mode;
        public string detail;
    }

    private void SetImageUIVisibility(bool visible)
    {
        if (targetImage != null)
            targetImage.gameObject.SetActive(visible);
    }
}
