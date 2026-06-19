/// <summary>跨场景传递当前要浏览的已保存绘本 ID。</summary>
public static class CompletedStoryContext
{
    public static string SelectedSaveId { get; private set; } = "";

    public static bool HasSelection => !string.IsNullOrWhiteSpace(SelectedSaveId);

    public static void Select(string saveId)
    {
        SelectedSaveId = saveId?.Trim() ?? "";
    }

    public static void Clear()
    {
        SelectedSaveId = "";
    }
}
