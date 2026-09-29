using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive placement of the authored tree, branch tabs and inspector.</summary>
public class RogueMetaTreeLayout : MonoBehaviour
{
    public RectTransform board, detail, branchTabs;
    public RectTransform[] columns;
    public GridLayoutGroup[] grids;
    public Text[] alternatives;
    public float detailWidth = 350, portraitDetailHeight = 330, gap = 22, headerHeight = 70, maximumTile = 148, rowGap = 26, pairGap = 26, tabHeight = 48;
    public int SelectedBranch { get; private set; }
    public bool Portrait { get { return Screen.height > Screen.width; } }
    public void SelectBranch(int index) { SelectedBranch = (index + columns.Length) % columns.Length; Arrange(); }
    void LateUpdate() { Arrange(); }
    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,1);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h);
    }
    void Arrange()
    {
        var r = (RectTransform)transform; float w = r.rect.width, h = r.rect.height;
        if (w <= 0 || h <= 0) return;
        bool portrait = Portrait; branchTabs.gameObject.SetActive(portrait);
        float bh = portrait ? h - portraitDetailHeight - gap - tabHeight - gap : h;
        float bw = portrait ? w : w - detailWidth - gap;
        Place(board,0,portrait ? tabHeight+gap : 0,bw,bh);
        Place(branchTabs,0,0,w,tabHeight);
        Place(detail,portrait ? 0 : bw+gap,portrait ? h-portraitDetailHeight : 0,portrait ? w : detailWidth,portrait ? portraitDetailHeight : h);
        float cw = portrait ? bw : (bw-gap*(columns.Length-1))/columns.Length;
        float tile = Mathf.Min(maximumTile, (cw-pairGap)/2, (bh-headerHeight-rowGap*3)/4);
        for (int i=0;i<columns.Length;i++)
        {
            columns[i].gameObject.SetActive(!portrait || i==SelectedBranch);
            Place(columns[i],portrait ? 0 : i*(cw+gap),0,cw,bh);
            var grid = grids[i]; grid.cellSize = new Vector2(tile,tile); grid.spacing = new Vector2(pairGap,rowGap);
            float width = tile*2+pairGap;
            Place((RectTransform)grid.transform,(cw-width)/2,headerHeight,width,tile*4+rowGap*3);
            Place(alternatives[i].rectTransform,(cw-pairGap)/2,headerHeight+3*(tile+rowGap)+tile*.45f,pairGap,24);
        }
    }
}
