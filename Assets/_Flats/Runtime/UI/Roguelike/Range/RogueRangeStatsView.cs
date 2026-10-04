using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The shooting range's statistics panel on the HUD (an authored prefab): damage per second over the last seconds, total
/// damage, kills, hits and headshots, the best single hit, knock-outs, and on a phone the button that opens the range menu.</summary>
public sealed class RogueRangeStatsView : MonoBehaviour
{
    public const string ResourcePath = "UI/Roguelike/Range/RogueRangeStats";
    public Text title, dpsLabel, dpsValue, totalLabel, totalValue, killsLabel, killsValue, hitsLabel, hitsValue, bestLabel, bestValue, enemiesLabel, enemiesValue, hint;
    public Button menuButton;
    public Text menuLabel;
    public RectTransform panel;
    [Tooltip("Top-left corner of the panel inside the HUD, in the HUD canvas's units (landscape and portrait).")]
    public Vector2 landscapePosition = new Vector2(24, -300), portraitPosition = new Vector2(16, -360);
    bool chinese, touch, first = true;
    Action openMenu;

    public static RogueRangeStatsView Open(Transform hud, Action openMenu)
    {
        var prefab = Resources.Load<RogueRangeStatsView>(ResourcePath);
        if (prefab == null || hud == null) return null;
        var view = Instantiate(prefab, hud, false);
        view.name = "RogueRangeStats";
        view.openMenu = openMenu;
        if (view.menuButton != null) view.menuButton.onClick.AddListener(() => { if (view.openMenu != null) view.openMenu(); });
        return view;
    }

    // no-break spaces: a value never wraps onto a second (cut) line, it shrinks to fit instead
    const string Dot = "\u00A0·\u00A0", Slash = "\u00A0/\u00A0";
    static string Number(double value) { return Math.Round(value).ToString("N0", CultureInfo.InvariantCulture); }

    public void Bind(float dps, double total, int kills, int hits, int headshots, float best, int knockouts, int enemies)
    {
        bool zh = FlatsLocalization.IsChinese, isTouch = RogueInput.IsTouch;
        if (first || zh != chinese || isTouch != touch)
        {
            first = false; chinese = zh; touch = isTouch;
            Func<string, string> T = FlatsLocalization.Translate;
            Put(title, T("Range statistics"));
            Put(dpsLabel, string.Format(T("Damage per second ({0} s)"), RogueRangeMeter.WindowSeconds.ToString("0", CultureInfo.InvariantCulture)));
            Put(totalLabel, T("Total damage"));
            Put(killsLabel, T("Kills"));
            Put(hitsLabel, T("Hits · headshots"));
            Put(bestLabel, T("Best hit"));
            Put(enemiesLabel, T("Enemies on the field"));
            Put(menuLabel, T("Range menu"));
            if (menuButton != null) menuButton.gameObject.SetActive(isTouch);
            if (hint != null)
            {
                hint.gameObject.SetActive(!isTouch);
                Put(hint, string.Format(T("{0}: range menu"), RogueInput.KeyText("Shop")));
            }
        }
        if (panel != null)
        {
            var wanted = Screen.height > Screen.width ? portraitPosition : landscapePosition;
            if (panel.anchoredPosition != wanted) panel.anchoredPosition = wanted;
        }
        Put(dpsValue, Number(dps));
        Put(totalValue, Number(total));
        Put(killsValue, kills.ToString(CultureInfo.InvariantCulture));
        Put(hitsValue, hits.ToString(CultureInfo.InvariantCulture) + Dot + headshots.ToString(CultureInfo.InvariantCulture));
        Put(bestValue, Number(best));
        Put(enemiesValue, enemies.ToString(CultureInfo.InvariantCulture) + Slash + Flats.Core.Roguelike.RogueRange.EnemyCap.ToString(CultureInfo.InvariantCulture));
    }

    static void Put(Text text, string value) { if (text != null && text.text != value) text.text = value; }
}
