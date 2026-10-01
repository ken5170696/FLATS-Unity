using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Headquarters play bar: the next run's settings (difficulty, map) and the primary Start Run button.
/// Layout, palette and icons live in the RogueMetaHub prefab; RogueMetaHub binds the data (see RogueMetaHub.PlaySetup).
/// </summary>
public class RogueMetaPlayBar : MonoBehaviour
{
    public GameObject info;
    public Text title, detail;
    public Button difficulty, map, play;
    public Text difficultyCaption, difficultyValue, mapCaption, mapValue, playLabel;
    [Tooltip("Portrait screens are too narrow for the summary next to three buttons: the summary hides there.")]
    public bool hideInfoInPortrait = true;

    void LateUpdate()
    {
        if (info == null || !hideInfoInPortrait) return;
        bool show = Screen.width >= Screen.height;
        if (info.activeSelf != show) info.SetActive(show);
    }

    /// <summary>Sets a label only when it changed, so a periodic refresh does not rebuild the layout every time.</summary>
    public static void Put(Text label, string text)
    {
        if (label != null && label.text != text) label.text = text;
    }
}
