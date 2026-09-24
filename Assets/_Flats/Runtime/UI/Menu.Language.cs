using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    Button languageButton;
    Text languageLabel;
    void InitializeLanguageButton()
    {
        foreach (var label in bt)
            if (label is FlatsLocalizedText localized) localized.UseMenuTypography();
        var ui = new ModCenterWidgets(FlatsLocalizedText.GetSourceFont(bt[0]), () => PlayMenuSound(pressSE));
        languageButton = ui.Button("Language", transform, "", 0, 0, 178, 48,
            () => { FlatsLocalization.SetLanguage(FlatsLocalization.IsChinese ? "en" : "zh-Hant"); RefreshLanguageButton(); },
            new Color(.31f,.24f,.29f,1));
        var rect = (RectTransform)languageButton.transform;
        languageButton.image.material = mainUI;
        languageButton.image.color = Color.white;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(24,24);
        var icon = ui.Rect("LanguageIcon", rect, -62, 0, 34, 34).gameObject.AddComponent<LanguageTileGraphic>();
        icon.color = Color.white; icon.raycastTarget = false;
        languageLabel = languageButton.GetComponentInChildren<Text>();
        languageLabel.color = Color.white;
        languageLabel.rectTransform.anchoredPosition = new Vector2(19,0);
        languageLabel.rectTransform.sizeDelta = new Vector2(128,44);
        RefreshLanguageButton();
    }
    void RefreshLanguageButton()
    {
        if (languageButton == null) return;
        bool show = gameState == "Main" && current == "Main" && !fliping && !anim.GetBool("Title")
            && !confirm.activeSelf && !errorMessage.activeSelf && !update.activeSelf;
        languageButton.gameObject.SetActive(show);
        languageLabel.text = FlatsLocalization.IsChinese ? "中文 / EN" : "EN / 中文";
        languageLabel.font = FlatsLocalization.ChineseFont;
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1;
        ((RectTransform)languageButton.transform).anchoredPosition = new Vector2(
            24 + Screen.safeArea.xMin / Mathf.Max(scale,.01f),
            24 + Screen.safeArea.yMin / Mathf.Max(scale,.01f));
    }
}
