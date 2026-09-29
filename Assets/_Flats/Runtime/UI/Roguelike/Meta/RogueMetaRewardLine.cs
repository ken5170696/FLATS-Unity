using UnityEngine;
using UnityEngine.UI;
using Flats.Core.Roguelike;

public class RogueMetaRewardLine : MonoBehaviour
{
    public Image sourceIcon;
    public Text source, xp, merits;
    public CanvasGroup visibility;
    public RectTransform slide;
    public float slideDistance = 32;
    public void Bind(RewardLine line)
    {
        RogueMetaUI.Put(source,RogueMetaUI.RewardSource(line));
        RogueMetaUI.Put(xp,MetaText.Value("{0}",line.xp)); RogueMetaUI.Put(merits,MetaText.Value("{0}",line.merits));
        RogueMetaUI.Image(sourceIcon,RogueMetaUI.RewardIcon(line.source)); Reveal(0);
    }
    public void Reveal(float t) { visibility.alpha=t; slide.anchoredPosition=new Vector2((1-t)*slideDistance,0); }
}
