using System;
using UnityEngine;
using UnityEngine.UI;
[Serializable]
public class ETCArea : MonoBehaviour
{
	public enum AreaPreset
	{
		Choose,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	public bool show;

	public ETCArea()
	{
		show = true;
	}

	public void Awake()
	{
		GetComponent<Image>().enabled = show;
	}

	public void ApplyPreset(AreaPreset preset)
	{
		RectTransform component = base.transform.parent.GetComponent<RectTransform>();
		switch (preset)
		{
		case AreaPreset.TopRight:
			this.rectTransform().anchoredPosition = new Vector2(component.rect.width / 4f, component.rect.height / 4f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, component.rect.width / 2f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, component.rect.height / 2f);
			this.rectTransform().anchorMin = new Vector2(1f, 1f);
			this.rectTransform().anchorMax = new Vector2(1f, 1f);
			this.rectTransform().anchoredPosition = new Vector2((0f - this.rectTransform().sizeDelta.x) / 2f, (0f - this.rectTransform().sizeDelta.y) / 2f);
			break;
		case AreaPreset.TopLeft:
			this.rectTransform().anchoredPosition = new Vector2((0f - component.rect.width) / 4f, component.rect.height / 4f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, component.rect.width / 2f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, component.rect.height / 2f);
			this.rectTransform().anchorMin = new Vector2(0f, 1f);
			this.rectTransform().anchorMax = new Vector2(0f, 1f);
			this.rectTransform().anchoredPosition = new Vector2(this.rectTransform().sizeDelta.x / 2f, (0f - this.rectTransform().sizeDelta.y) / 2f);
			break;
		case AreaPreset.BottomRight:
			this.rectTransform().anchoredPosition = new Vector2(component.rect.width / 4f, (0f - component.rect.height) / 4f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, component.rect.width / 2f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, component.rect.height / 2f);
			this.rectTransform().anchorMin = new Vector2(1f, 0f);
			this.rectTransform().anchorMax = new Vector2(1f, 0f);
			this.rectTransform().anchoredPosition = new Vector2((0f - this.rectTransform().sizeDelta.x) / 2f, this.rectTransform().sizeDelta.y / 2f);
			break;
		case AreaPreset.BottomLeft:
			this.rectTransform().anchoredPosition = new Vector2((0f - component.rect.width) / 4f, (0f - component.rect.height) / 4f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, component.rect.width / 2f);
			this.rectTransform().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, component.rect.height / 2f);
			this.rectTransform().anchorMin = new Vector2(0f, 0f);
			this.rectTransform().anchorMax = new Vector2(0f, 0f);
			this.rectTransform().anchoredPosition = new Vector2(this.rectTransform().sizeDelta.x / 2f, this.rectTransform().sizeDelta.y / 2f);
			break;
		}
	}




}
