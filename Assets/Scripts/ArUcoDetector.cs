using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;

public class ArUcoDetector : MonoBehaviour
{
    [Header("UI显示")]
    public RawImage displayImage;
    public LocalImageGenClient imageGenClient;

    [Header("摄像头设置")]
    public string preferredCameraName = "HP True Vision FHD Camera";

    private const int FallbackCameraIndex = 0;
    private const int RequestedWidth = 1920;
    private const int RequestedHeight = 1080;
    private const int RequestedFps = 30;
    private const bool ReportEachIdOnlyOnce = true;

    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Dictionary dictionary;
    private DetectorParameters detectorParams;
    private ArucoDetector arucoDetector;
    private readonly HashSet<int> reportedIds = new HashSet<int>();

    // 用于转换 Mat 到 Texture
    private Texture2D outputTexture;

    void Start()
    {
        // 1. 初始化摄像头
        WebCamDevice[] devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"Camera {i}: {devices[i].name}, frontFacing={devices[i].isFrontFacing}");
        }

        string selectedDeviceName = null;
        if (!string.IsNullOrWhiteSpace(preferredCameraName))
        {
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].name == preferredCameraName)
                {
                    selectedDeviceName = devices[i].name;
                    break;
                }
            }
        }

        if (selectedDeviceName == null && devices.Length > 0)
        {
            int safeIndex = Mathf.Clamp(FallbackCameraIndex, 0, devices.Length - 1);
            selectedDeviceName = devices[safeIndex].name;
        }

        webCamTexture = selectedDeviceName != null
            ? new WebCamTexture(selectedDeviceName, RequestedWidth, RequestedHeight, RequestedFps)
            : new WebCamTexture(RequestedWidth, RequestedHeight, RequestedFps);
        webCamTexture.Play();

        // 2. 关键设置：对应网页上的 4x4 字典
        dictionary = Objdetect.getPredefinedDictionary(Objdetect.DICT_4X4_50);
        detectorParams = new DetectorParameters();
        arucoDetector = new ArucoDetector(dictionary, detectorParams);
    }

    void Update()
    {
        if (webCamTexture.didUpdateThisFrame)
        {
            // 3. 每一帧将摄像头画面转为 OpenCV 的 Mat 格式
            if (rgbaMat == null)
            {
                rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
                outputTexture = new Texture2D(rgbaMat.cols(), rgbaMat.rows(), TextureFormat.RGBA32, false);
                displayImage.texture = outputTexture;
            }

            OpenCVMatUtils.WebCamTextureToMat(webCamTexture, rgbaMat);

            // 4. 识别 ArUco 码
            List<Mat> corners = new List<Mat>();
            using (Mat ids = new Mat())
            {
                arucoDetector.detectMarkers(rgbaMat, corners, ids);

                if (ids.total() > 0)
                {
                    // 5. 提取识别到的 ID
                    int[] idArray = new int[ids.total()];
                    ids.get(0, 0, idArray);

                    foreach (int id in idArray)
                    {
                        if (!ReportEachIdOnlyOnce || reportedIds.Add(id))
                        {
                            Debug.Log($"<color=green>识别到 ArUco 码 ID:{id} (字典: DICT_4X4_50)</color>");

                            if (id == 0)
                            {
                                if (imageGenClient != null)
                                {
                                    imageGenClient.GenerateImage();
                                    Debug.Log("<color=green>ID:0 触发生图请求。</color>");
                                }
                                else
                                {
                                    Debug.LogWarning("检测到 ID:0，但未绑定 imageGenClient。");
                                }
                            }
                        }
                    }
                }
            }

            foreach (Mat corner in corners)
            {
                corner.Dispose();
            }

            // 6. 将处理后的画面显示在 UI 上
            OpenCVMatUtils.MatToTexture2D(rgbaMat, outputTexture);
        }
    }

    void OnDestroy()
    {
        if (webCamTexture != null) webCamTexture.Stop();
        if (rgbaMat != null) rgbaMat.Dispose();
        if (dictionary != null) dictionary.Dispose();
        if (detectorParams != null) detectorParams.Dispose();
        if (arucoDetector != null) arucoDetector.Dispose();
        if (outputTexture != null) Destroy(outputTexture);
        reportedIds.Clear();
    }
}