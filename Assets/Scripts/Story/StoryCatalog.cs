using UnityEngine;

[DisallowMultipleComponent]
public class StoryCatalog : MonoBehaviour
{
    public StoryDefinition[] stories;

    public StoryDefinition[] ResolveStories()
    {
        if (stories != null && stories.Length > 0)
            return stories;
        return Resources.LoadAll<StoryDefinition>("Stories");
    }

    public static BrickPortfolioRoot.BrickWorkItem ToWorkItem(StoryDefinition def)
    {
        if (def == null)
            return null;
        return new BrickPortfolioRoot.BrickWorkItem
        {
            storyId = def.storyId,
            title = def.title,
            synopsisText = def.synopsisText,
            prologuePages = def.prologuePages,
            prologueSceneName = def.prologueSceneName,
            thumbnail = def.thumbnail,
        };
    }
}
