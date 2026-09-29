using System;
using Flats.Core.Roguelike;
using UnityEngine;
using UnityEngine.UI;

public class RogueMetaNode : MonoBehaviour
{
    public Text title, state, cost;
    public Image icon, stripe, connector;
    public Image stateIcon;
    public Button select;
    public Color learned, available, noPoints, prerequisite, exclusive;
    public int minimumNameSize=10, maximumNameSize=16;
    void LateUpdate()
    {
        var settings=title.GetGenerationSettings(Vector2.zero);settings.resizeTextForBestFit=false;settings.fontSize=maximumNameSize;
        float width=title.cachedTextGeneratorForLayout.GetPreferredWidth(title.text,settings)/title.pixelsPerUnit;
        title.resizeTextForBestFit=false;
        title.fontSize=Mathf.Clamp(Mathf.FloorToInt(maximumNameSize*title.rectTransform.rect.width/Mathf.Max(1,width)),minimumNameSize,maximumNameSize);
    }
    public void Bind(MetaProfile p, SkillDef n, Action click)
    {
        bool has = Array.IndexOf(p.Active.skills, n.Id) >= 0;
        string reason = SkillTree.CannotLearn(p.Active.skills, n.Id, MetaProfiles.AvailablePoints(p, p.Active));
        bool excluded = Array.IndexOf(p.Active.skills, n.Exclusive) >= 0;
        string caption = has ? "Learned" : excluded ? "Excludes the other capstone" : reason ?? "Available";
        RogueMetaUI.Put(title, n.Name); RogueMetaUI.Put(cost, MetaText.Value("{0}",n.Cost));
        RogueMetaUI.Put(state, has ? "Learned" : reason == null ? "Available" : reason == "Not enough skill points" ? "No points" : "Locked");
        RogueMetaUI.Image(stateIcon, RogueIcons.Get(has ? "Check" : reason == null ? "Plus" : reason == "Not enough skill points" ? "PointEmpty" : "Lock"));
        stripe.color = has ? learned : excluded ? exclusive : reason == null ? available : reason == "Not enough skill points" ? noPoints : prerequisite;
        RogueMetaUI.Image(icon, RogueMetaUI.SkillIcon(n));
        connector.gameObject.SetActive(n.Row > 0);
        connector.color = SkillTree.SpentIn(p.Active.skills, n.Branch) >= SkillTree.RowPointsRequired[n.Row] ? learned : prerequisite;
        select.onClick.RemoveAllListeners();
        select.onClick.AddListener(() => { RogueMetaUI.Sound(); click(); });
    }
}
