using System;
using UnityEngine;
using UnityEngine.UI;

public class RogueMetaLocale : MonoBehaviour
{
    [Serializable] public class Label { public Text text; public string key; }
    public Font latinFont;
    public Label[] labels;
    void OnEnable() { Apply(); FlatsLocalization.Changed += Apply; }
    void OnDisable() { FlatsLocalization.Changed -= Apply; }
    public void Apply()
    {
        foreach (var text in GetComponentsInChildren<Text>(true)) text.font = FlatsLocalization.IsChinese ? FlatsLocalization.ChineseFont : latinFont;
        if (labels != null) foreach (var label in labels) if (label.text != null) RogueMetaUI.Put(label.text, label.key);
    }
}
