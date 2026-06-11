using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.ImgprocModule; // 用于图像绘制
using System.Collections.Generic;
using System;

public class ArUcoDetector : MonoBehaviour
{
    // ==================== 数据结构：用于对外输出坐标 ====================
    [Serializable]
    public struct MarkerData
    {
        public int id;
        [Tooltip("以摄像头画面左上角为原点 (0,0) 的像素坐标 (X向右, Y向下)")]
        public Vector2 pixelPosition;
    }

    /// <summary>
    /// 其他脚本可通过此属性，实时获取当前帧所有检测到的 ArUco 码及其坐标
    /// </summary>
    public List<MarkerData> DetectedMarkers { get; private set; } = new List<MarkerData>();
    // ==================================================================

    [Header("UI显示")]
    public RawImage displayImage;
    public LocalImageGenClient imageGenClient;

    [Tooltip("用于触发生成图片的按钮")]
    public Button generateButton;

    [Header("摄像头设置")]
    public string preferredCameraName = "HP True Vision FHD Camera";

    private const int FallbackCameraIndex = 0;
    private const int RequestedWidth = 1920;
    private const int RequestedHeight = 1080;
    private const int RequestedFps = 30;

    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Dictionary dictionary;
    private DetectorParameters detectorParams;
    private ArucoDetector arucoDetector;

    // 用于转换 Mat 到 Texture
    private Texture2D outputTexture;

    private List<int> pendingMarkerIds = new List<int>();

    [Header("方位与距离配置")]
    [Tooltip("物理方位映射。如果您相机旋转了90度（图一横向代表前后，图二纵向代表左右），请选择 X_is_FrontBack_Y_is_LeftRight。\n正常非旋转相机请选择 X_is_LeftRight_Y_is_FrontBack。")]
    public AxisMappingMode axisMapping = AxisMappingMode.X_is_FrontBack_Y_is_LeftRight;

    public enum AxisMappingMode
    {
        X_is_FrontBack_Y_is_LeftRight, // 对应您当前图片的情况
        X_is_LeftRight_Y_is_FrontBack  // 对应正常相机方向
    }

    [Tooltip("ArUco 标记卡片的实际物理边长（单位：厘米），用于精准估算真实距离")]
    public float markerPhysicalSizeCm = 10f;

    [Tooltip("如果方向颠倒了，可勾选此项来反转“前/后”判定")]
    public bool invertFrontBack = false;

    [Tooltip("如果方向颠倒了，可勾选此项来反转“左/右”判定")]
    public bool invertLeftRight = false;

    [Tooltip("（可选）绑定一个 UI Text 组件，用来实时显示文字结果与坐标")]
    public Text relationResultText;

    void Start()
    {
        if (generateButton != null)
        {
            generateButton.gameObject.SetActive(false); // 默认隐藏
            generateButton.onClick.AddListener(OnGenerateButtonClick); // 绑定点击事件
        }
        else
        {
            Debug.LogError("ArUcoDetector: 未在 Inspector 中绑定 generateButton！");
        }

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

                    // ==================== 坐标数据处理与绘制 ====================
                    DetectedMarkers.Clear(); // 刷新当前帧数据

                    for (int i = 0; i < corners.Count; i++)
                    {
                        float[] pts = new float[8];
                        corners[i].get(0, 0, pts);
                        Point center = GetMarkerCenter(pts);
                        int id = idArray[i];

                        // 填充坐标数据（OpenCV 的 center 本身就是以左上角为原点计算出来的）
                        MarkerData markerData = new MarkerData();
                        markerData.id = id;
                        markerData.pixelPosition = new Vector2((float)center.x, (float)center.y);
                        DetectedMarkers.Add(markerData);

                        // 画面实时绘制坐标：在每个标记中心稍微往上绘制 "(X, Y)" 文字
                        string coordStr = $"ID {id}: ({Mathf.RoundToInt(markerData.pixelPosition.x)}, {Mathf.RoundToInt(markerData.pixelPosition.y)})";
                        Point textPos = new Point(center.x - 70, center.y - 25);
                        // 绘制带有黑边的文字（使用绿色显示，更加显眼）
                        Imgproc.putText(rgbaMat, coordStr, textPos, Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, new Scalar(0, 0, 0, 255), 3); // 黑色描边
                        Imgproc.putText(rgbaMat, coordStr, textPos, Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, new Scalar(0, 255, 0, 255), 1); // 绿色文字
                    }
                    // ==========================================================

                    // 收集当前帧看到的所有不重复 ID（供生图触发逻辑使用）
                    List<int> visibleIdsThisFrame = new List<int>();
                    HashSet<int> seen = new HashSet<int>();

                    foreach (int id in idArray)
                    {
                        if (seen.Add(id))
                        {
                            visibleIdsThisFrame.Add(id);
                        }
                    }

                    if (visibleIdsThisFrame.Count > 0)
                    {
                        pendingMarkerIds = visibleIdsThisFrame;

                        // 显示生图按钮
                        if (generateButton != null && !generateButton.gameObject.activeSelf)
                        {
                            generateButton.gameObject.SetActive(true);
                        }
                    }

                    // 关系检测与结果拼接
                    string relationshipMsg = "";
                    if (ids.total() >= 2)
                    {
                        relationshipMsg = AnalyzeMarkersRelationship(corners, idArray, rgbaMat);
                    }
                    else
                    {
                        relationshipMsg = "请放入至少两个 ArUco 码进行测距...";
                    }

                    // 拼接坐标信息一同输出到 UI
                    string coordinatesMsg = "当前检测到的坐标 (原点在左上角):\n";
                    foreach (var marker in DetectedMarkers)
                    {
                        coordinatesMsg += $"  • ID {marker.id} : ({marker.pixelPosition.x:F0}, {marker.pixelPosition.y:F0}) px\n";
                    }

                    if (relationResultText != null)
                    {
                        relationResultText.text = $"{relationshipMsg}\n\n{coordinatesMsg}";
                    }
                }
                else
                {
                    pendingMarkerIds.Clear();
                    DetectedMarkers.Clear();

                    if (generateButton != null && generateButton.gameObject.activeSelf)
                    {
                        generateButton.gameObject.SetActive(false);
                    }
                    if (relationResultText != null)
                    {
                        relationResultText.text = "未检测到任何 ArUco 码";
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

    private string AnalyzeMarkersRelationship(List<Mat> corners, int[] ids, Mat drawingMat)
    {
        if (corners.Count < 2 || ids.Length < 2) return "";

        Mat cornerMatA = corners[0];
        Mat cornerMatB = corners[1];
        int idA = ids[0];
        int idB = ids[1];

        float[] ptsA = new float[8];
        cornerMatA.get(0, 0, ptsA);
        Point centerA = GetMarkerCenter(ptsA);

        float[] ptsB = new float[8];
        cornerMatB.get(0, 0, ptsB);
        Point centerB = GetMarkerCenter(ptsB);

        double deltaX = centerB.x - centerA.x;
        double deltaY = centerB.y - centerA.y;
        double pixelDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        float pxSizeA = EstimateMarkerPixelSize(ptsA);
        float pxSizeB = EstimateMarkerPixelSize(ptsB);
        float avgPixelSize = (pxSizeA + pxSizeB) / 2f;

        float pixelToCmRatio = markerPhysicalSizeCm / avgPixelSize;
        float physicalDistanceCm = (float)pixelDistance * pixelToCmRatio;

        string relationType = "";
        string directionDetail = "";

        if (axisMapping == AxisMappingMode.X_is_FrontBack_Y_is_LeftRight)
        {
            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                relationType = "前后关系";
                bool isBInFront = deltaX > 0;
                if (invertFrontBack) isBInFront = !isBInFront;

                directionDetail = isBInFront
                    ? $"ID {idB} 在 ID {idA} 的前面"
                    : $"ID {idB} 在 ID {idA} 的后面";
            }
            else
            {
                relationType = "左右关系";
                bool isBOnRight = deltaY > 0;
                if (invertLeftRight) isBOnRight = !isBOnRight;

                directionDetail = isBOnRight
                    ? $"ID {idB} 在 ID {idA} 的右边"
                    : $"ID {idB} 在 ID {idA} 的左边";
            }
        }
        else
        {
            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                relationType = "左右关系";
                bool isBOnRight = deltaX > 0;
                if (invertLeftRight) isBOnRight = !isBOnRight;

                directionDetail = isBOnRight
                    ? $"ID {idB} 在 ID {idA} 的右边"
                    : $"ID {idB} 在 ID {idA} 的左边";
            }
            else
            {
                relationType = "前后关系";
                bool isBInFront = deltaY > 0;
                if (invertFrontBack) isBInFront = !isBInFront;

                directionDetail = isBInFront
                    ? $"ID {idB} 在 ID {idA} 的前面"
                    : $"ID {idB} 在 ID {idA} 的后面";
            }
        }

        // 绘制辅助图形
        Imgproc.circle(drawingMat, centerA, 8, new Scalar(255, 0, 0, 255), -1); // 蓝色中心
        Imgproc.circle(drawingMat, centerB, 8, new Scalar(0, 0, 255, 255), -1); // 红色中心
        Imgproc.line(drawingMat, centerA, centerB, new Scalar(0, 255, 0, 255), 3); // 绿色连线

        // 在线上标距离
        Point midPoint = new Point((centerA.x + centerB.x) / 2, (centerA.y + centerB.y) / 2);
        Imgproc.putText(drawingMat, $"{physicalDistanceCm:F1} cm", midPoint, Imgproc.FONT_HERSHEY_SIMPLEX, 0.8, new Scalar(0, 255, 0, 255), 2);

        return $"【{relationType}】\n{directionDetail}\n距离: {physicalDistanceCm:F1} 厘米 ({pixelDistance:F0} 像素)";
    }

    private Point GetMarkerCenter(float[] pts)
    {
        double cx = (pts[0] + pts[2] + pts[4] + pts[6]) / 4.0;
        double cy = (pts[1] + pts[3] + pts[5] + pts[7]) / 4.0;
        return new Point(cx, cy);
    }

    private float EstimateMarkerPixelSize(float[] pts)
    {
        double top = Math.Sqrt(Math.Pow(pts[2] - pts[0], 2) + Math.Pow(pts[3] - pts[1], 2));
        double right = Math.Sqrt(Math.Pow(pts[4] - pts[2], 2) + Math.Pow(pts[5] - pts[3], 2));
        double bottom = Math.Sqrt(Math.Pow(pts[6] - pts[4], 2) + Math.Pow(pts[7] - pts[5], 2));
        double left = Math.Sqrt(Math.Pow(pts[0] - pts[6], 2) + Math.Pow(pts[1] - pts[7], 2));
        return (float)((top + right + bottom + left) / 4.0);
    }

    private void OnGenerateButtonClick()
    {
        if (imageGenClient != null && pendingMarkerIds.Count > 0)
        {
            imageGenClient.GenerateImageForMarker(pendingMarkerIds);
        }

        pendingMarkerIds.Clear();
        if (generateButton != null)
        {
            generateButton.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(OnGenerateButtonClick);
        }

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }

        if (arucoDetector != null)
        {
            arucoDetector.Dispose();
            arucoDetector = null;
        }

        if (detectorParams != null)
        {
            detectorParams.Dispose();
            detectorParams = null;
        }

        if (dictionary != null)
        {
            dictionary.Dispose();
            dictionary = null;
        }

        if (rgbaMat != null)
        {
            rgbaMat.Dispose();
            rgbaMat = null;
        }

        if (outputTexture != null)
        {
            Destroy(outputTexture);
            outputTexture = null;
        }
    }
}