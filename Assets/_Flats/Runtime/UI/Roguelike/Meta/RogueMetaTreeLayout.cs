using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive placement of the authored tree, branch tabs and inspector.</summary>
public class RogueMetaTreeLayout : MonoBehaviour
{
    public RectTransform board, detail, branchTabs;
    public RectTransform[] columns;
    public GridLayoutGroup[] grids;
    public Text[] alternatives;
    public RectTransform[] alternativeBackdrops;
    public float detailWidth = 350, portraitDetailHeight = 330, gap = 22, headerHeight = 70, maximumTile = 148, rowGap = 26, pairGap = 26, tabHeight = 48;
    public int SelectedBranch { get; private set; }
    public bool Portrait { get { return Screen.height > Screen.width; } }
    public void SelectBranch(int index) { SelectedBranch = (index + columns.Length) % columns.Length; Arrange(); }
    Vector2 lastSize; bool lastPortrait;
    void LateUpdate() { var size=((RectTransform)transform).rect.size; if(size==lastSize&&Portrait==lastPortrait)return;lastSize=size;lastPortrait=Portrait;Arrange(); }
    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h);
    }
    void Arrange()
    {
        var r = (RectTransform)transform; float w = r.rect.width, h = r.rect.height;
        if (w <= 0 || h <= 0) return;
        bool portrait = Portrait, mobile=RogueInput.IsTouch&&!portrait;int visible=portrait?1:mobile?2:4; branchTabs.gameObject.SetActive(portrait||mobile);
        float bh = portrait ? h - portraitDetailHeight - gap - tabHeight - gap : mobile?h-tabHeight-gap:h;
        float bw = portrait ? w : w - detailWidth - gap;
        Place(board,0,portrait||mobile ? tabHeight+gap : 0,bw,bh);
        Place(branchTabs,0,0,w,tabHeight);
        Place(detail,portrait ? 0 : bw+gap,portrait ? h-portraitDetailHeight : 0,portrait ? w : detailWidth,portrait ? portraitDetailHeight : h);
        float cw = (bw-gap*(visible-1))/visible;
        float tile = portrait ? (cw-pairGap)/2 : Mathf.Min(maximumTile, (cw-pairGap)/2);
        float tileHeight = (bh-headerHeight-rowGap*3)/4;
        for (int i=0;i<columns.Length;i++)
        {
            columns[i].gameObject.SetActive(portrait?i==SelectedBranch:!mobile||i/2==SelectedBranch/2);
            Place(columns[i],portrait ? 0 : (mobile?i%2:i)*(cw+gap),0,cw,bh);
            var grid = grids[i]; grid.cellSize = new Vector2(tile,tileHeight); grid.spacing = new Vector2(pairGap,rowGap);
            float width = tile*2+pairGap;
            Place((RectTransform)grid.transform,(cw-width)/2,headerHeight,width,tileHeight*4+rowGap*3);
            Place(alternatives[i].rectTransform,0,headerHeight+3*(tileHeight+rowGap)-24,cw,24);
            if(alternativeBackdrops!=null&&i<alternativeBackdrops.Length)Place(alternativeBackdrops[i],0,headerHeight+3*(tileHeight+rowGap)-24,cw,24);
        }
    }
}
