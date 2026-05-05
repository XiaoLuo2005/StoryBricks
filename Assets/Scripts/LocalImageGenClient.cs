using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LocalImageGenClient : MonoBehaviour
{
    private const string FixedSize = "1024*1024";

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

    [Header("Generation")]
    public string model = "wanx-v1";

    [HideInInspector]
    [SerializeField]
    private string prompt = "A cute cartoon rabbit reading a storybook in a cozy room.";

    [Header("ArUco ID → 提示词")]
    [Tooltip("识别到对应 markerId 时使用该行提示词请求生图；多条相同 ID 时取列表中最靠前的一条。留空则沿用旧逻辑：仅 ID 0 使用组件内保存的默认提示词（已隐藏，可通过右键 Generate Image 调试）。")]
    public MarkerPromptMapping[] markerPromptMappings;

    /// <summary>
    /// 根据 ArUco 标记 ID 查找配置的提示词并生图；未配置则跳过。
    /// </summary>
    public void GenerateImageForMarker(int markerId)
    {
        string resolved = ResolvePromptForMarker(markerId);
        if (string.IsNullOrEmpty(resolved))
        {
            Debug.LogWarning($"LocalImageGenClient: 未在 markerPromptMappings 中为 ID {markerId} 配置提示词，已跳过生图。");
            return;
        }

        StopAllCoroutines();
        CancelActiveWebRequest();
        StartCoroutine(GenerateImageCoroutine(resolved));
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
    public void GenerateImage()
    {
        StopAllCoroutines();
        CancelActiveWebRequest();
        StartCoroutine(GenerateImageCoroutine(prompt));
    }

    private IEnumerator GenerateImageCoroutine(string promptToSend)
    {
        if (targetImage == null)
        {
            Debug.LogError("targetImage is not assigned.");
            yield break;
        }

        var requestBody = new GenerateRequest
        {
            prompt = promptToSend,
            model = model,
            size = FixedSize,
            n = 1
        };

        string json = JsonUtility.ToJson(requestBody);

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

        if (!genOk)
        {
            Debug.LogError("Generate request failed: " + genErr + "\n" + genBody);
            yield break;
        }

        var response = JsonUtility.FromJson<GenerateResponse>(genBody);
        if (response == null || string.IsNullOrEmpty(response.image_url))
        {
            Debug.LogError("Invalid generate response: " + genBody);
            yield break;
        }

        if (!isActiveAndEnabled || targetImage == null)
            yield break;

        yield return DownloadTexture(response.image_url);
    }

    private IEnumerator DownloadTexture(string imageUrl)
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
            Debug.LogError("Download image failed: " + dlErr);
            yield break;
        }

        if (targetImage == null)
            yield break;

        var previous = targetImage.texture as Texture2D;
        targetImage.texture = tex;
        if (previous != null && previous != tex)
            Destroy(previous);

        Debug.Log("Image generated and applied to RawImage.");
    }

    [Serializable]
    private class GenerateRequest
    {
        public string prompt;
        public string model;
        public string size;
        public int n;
    }

    [Serializable]
    private class GenerateResponse
    {
        public string task_id;
        public string image_url;
        public string model;
    }
}
