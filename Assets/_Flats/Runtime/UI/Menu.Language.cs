using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    [SerializeField] Button languageButton;
    [SerializeField] Text languageLabel;
    [SerializeField] RectTransform languageAlignmentReference;
    readonly Vector3[] languageReferenceCorners = new Vector3[4];
    bool mainMenuInitialized;
    void InitializeLanguageButton()
    {
        foreach (var label in bt)
            if (label is FlatsLocalizedText localized) localized.UseMenuTypography();
        languageButton.onClick.AddListener(() =>
        {
            PlayMenuSound(pressSE);
            FlatsLocalization.SetLanguage(FlatsLocalization.IsChinese ? "en" : "zh-Hant");
            RefreshLanguageButton();
        });
        RefreshLanguageButton();
    }
    void RefreshLanguageButton()
    {
        if (languageButton == null) return;
        bool show = mainMenuInitialized && gameState == "Main" && current == "Main" && !fliping && !anim.GetBool("Title")
            && languageAlignmentReference != null && languageAlignmentReference.gameObject.activeInHierarchy
            && !confirm.activeSelf && !errorMessage.activeSelf && !update.activeSelf;
        languageButton.gameObject.SetActive(show);
        languageLabel.text = FlatsLocalization.IsChinese ? "中文 / EN" : "EN / 中文";
        languageLabel.font = FlatsLocalization.ChineseFont;
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1;
        var rect = (RectTransform)languageButton.transform;
        var parent = (RectTransform)rect.parent;
        if (languageAlignmentReference != null)
        {
            // Use the existing footer's bounds, including its parent transform,
            // so all three controls share top/bottom edges at every canvas scale.
            languageAlignmentReference.GetWorldCorners(languageReferenceCorners);
            Vector3 bottom = parent.InverseTransformPoint(languageReferenceCorners[0]);
            Vector3 top = parent.InverseTransformPoint(languageReferenceCorners[2]);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, top.y - bottom.y);
            rect.anchoredPosition = new Vector2(
                parent.rect.xMax - top.x + Screen.safeArea.xMin / Mathf.Max(scale,.01f),
                Mathf.Max(bottom.y - parent.rect.yMin, Screen.safeArea.yMin / Mathf.Max(scale,.01f)));
        }
    }
}
