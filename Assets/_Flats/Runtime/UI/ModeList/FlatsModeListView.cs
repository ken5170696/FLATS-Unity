using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Single-column scrolling list of game modes (authored Resources/UI/ModeList prefab). The menu fills it with rows
/// that map onto the legacy tile actions, so a page can hold more modes than the six fixed tiles.
/// </summary>
public class FlatsModeListView : MonoBehaviour
{
    public RectTransform rowsContent;
    public ScrollRect scroll;
    public FlatsModeRowView rowTemplate;
    public Text heading;
    readonly List<FlatsModeRowView> rows = new List<FlatsModeRowView>();

    public static FlatsModeListView Open(Transform parent)
    {
        var prefab = Resources.Load<GameObject>("UI/ModeList");
        if (prefab == null) { Debug.LogWarning("FLATS_MODE_LIST missing Resources/UI/ModeList"); return null; }
        var go = Instantiate(prefab, parent, false);
        go.name = "ModeList";
        var view = go.GetComponent<FlatsModeListView>();
        if (view != null && view.rowTemplate != null) view.rowTemplate.gameObject.SetActive(false);
        return view;
    }

    public void Clear()
    {
        foreach (var r in rows) if (r != null) Destroy(r.gameObject);
        rows.Clear();
    }

    public FlatsModeRowView Add(Sprite iconSprite, string titleText, string descriptionText, string valueText, Color tint, Action onClick)
    {
        if (rowTemplate == null || rowsContent == null) return null;
        var row = Instantiate(rowTemplate, rowsContent, false);
        row.gameObject.SetActive(true);
        row.name = "Row-" + titleText;
        if (row.icon != null) { row.icon.sprite = iconSprite; row.icon.enabled = iconSprite != null; }
        if (row.iconBack != null) row.iconBack.color = tint;
        if (row.panel != null) row.panel.color = new Color(tint.r, tint.g, tint.b, 0.9f);
        if (row.title != null) row.title.text = titleText ?? "";
        if (row.description != null) row.description.text = descriptionText ?? "";
        if (row.value != null) { row.value.text = valueText ?? ""; row.value.gameObject.SetActive(!string.IsNullOrEmpty(valueText)); }
        if (row.button != null) { row.button.onClick.RemoveAllListeners(); row.button.onClick.AddListener(() => { if (onClick != null) onClick(); }); }
        rows.Add(row);
        return row;
    }

    public FlatsModeRowView Row(int index) { return index >= 0 && index < rows.Count ? rows[index] : null; }
    public int Count { get { return rows.Count; } }

    public void Focus(int index)
    {
        var row = Row(index);
        if (row != null && row.button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(row.button.gameObject);
    }

    void Update()
    {
        // keep a row selected for pad/keyboard users; the legacy tiles are hidden while the list is up
        if (EventSystem.current == null || rows.Count == 0) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(transform)) return;
        if (Input.GetAxisRaw("Vertical") != 0 || (InControl.InputManager.ActiveDevice != null && InControl.InputManager.ActiveDevice.LeftStickY.Value != 0)) Focus(0);
    }
}
