using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LocalImageGenClient : MonoBehaviour
{
    private const string FixedSize = "1024*1024";

    [Header("UI")]
    public RawImage targetImage;

    [Header("Local Server")]
    public string serverUrl = "http://127.0.0.1:8000/generate";

    [Header("Generation")]
    [TextArea(2, 5)]
    public string prompt = "A cute cartoon rabbit reading a storybook in a cozy room.";
    public string model = "wanx-v1";

    [ContextMenu("Generate Image")]
    public void GenerateImage()
    {
        StartCoroutine(GenerateImageCoroutine());
    }

    private IEnumerator GenerateImageCoroutine()
    {
        if (targetImage == null)
        {
            Debug.LogError("targetImage is not assigned.");
            yield break;
        }

        var requestBody = new GenerateRequest
        {
            prompt = prompt,
            model = model,
            size = FixedSize,
            n = 1
        };

        string json = JsonUtility.ToJson(requestBody);
        using (var req = new UnityWebRequest(serverUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Generate request failed: " + req.error + "\n" + req.downloadHandler.text);
                yield break;
            }

            var response = JsonUtility.FromJson<GenerateResponse>(req.downloadHandler.text);
            if (response == null || string.IsNullOrEmpty(response.image_url))
            {
                Debug.LogError("Invalid generate response: " + req.downloadHandler.text);
                yield break;
            }

            yield return StartCoroutine(DownloadTexture(response.image_url));
        }
    }

    private IEnumerator DownloadTexture(string imageUrl)
    {
        using (var req = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Download image failed: " + req.error);
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            targetImage.texture = tex;
            Debug.Log("Image generated and applied to RawImage.");
        }
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
