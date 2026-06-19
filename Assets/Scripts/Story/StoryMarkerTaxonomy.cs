using System.Collections.Generic;

/// <summary>
/// ArUco ID 分区：角色 1–20。行为与故事元素均通过语音问答补全，不识别道具码。
/// </summary>
public struct StoryMarkerTaxonomy
{
    public int characterMin;
    public int characterMax;

    public static StoryMarkerTaxonomy Default => new StoryMarkerTaxonomy
    {
        characterMin = 1,
        characterMax = 20,
    };

    public static StoryMarkerTaxonomy FromStory(StoryDefinition story)
    {
        if (story == null)
            return Default;
        return new StoryMarkerTaxonomy
        {
            characterMin = story.characterMarkerMin,
            characterMax = story.characterMarkerMax,
        };
    }

    public bool IsCharacter(int id) => id >= characterMin && id <= characterMax;

    public List<int> FilterCharacterIds(IReadOnlyList<int> ids)
    {
        var list = new List<int>();
        if (ids == null)
            return list;
        foreach (int id in ids)
        {
            if (IsCharacter(id))
                list.Add(id);
        }
        list.Sort();
        return list;
    }
}
