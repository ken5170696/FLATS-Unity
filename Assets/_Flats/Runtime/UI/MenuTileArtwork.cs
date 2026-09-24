using System;
using UnityEngine;
using UnityEngine.UI;

// Per-artwork framing preserves each source illustration's transparent margins.
// These values belong to MainMenuScreen, alongside the card and label geometry.
[ExecuteAlways]
public sealed class MenuTileArtwork : MonoBehaviour
{
    [Serializable]
    public struct Framing
    {
        public Sprite sprite;
        public Vector2 size;
        public Vector2 position;
    }
    public Image[] images;
    public Text[] labels;
    public Image[] readingSurfaces;
    public GameObject modIcon;
    public Framing[] artwork;
    [Header("Desktop Settings: centered lower row")]
    public RectTransform displayCard, extraSettingsCard;
    public Vector2 displaySettingsPosition=new Vector2(-110,-80);
    public Vector2 extraSettingsPosition=new Vector2(110,-80);
    bool compactSettings;
    Vector2 displayOriginalPosition, extraOriginalPosition;
    public void SetDesktopSettingsLayout(bool compact)
    {
        if(compactSettings==compact || displayCard==null || extraSettingsCard==null)return;
        compactSettings=compact;
        if(compact)
        {
            displayOriginalPosition=displayCard.anchoredPosition;
            extraOriginalPosition=extraSettingsCard.anchoredPosition;
            displayCard.anchoredPosition=displaySettingsPosition;
            extraSettingsCard.anchoredPosition=extraSettingsPosition;
        }
        else
        {
            displayCard.anchoredPosition=displayOriginalPosition;
            extraSettingsCard.anchoredPosition=extraOriginalPosition;
        }
    }
    Sprite[] shown;
    void OnValidate(){shown=null;}
    void LateUpdate()
    {
        if(images==null)return;
        if(shown==null || shown.Length!=images.Length)shown=new Sprite[images.Length];
        for(int i=0;i<images.Length;i++)
        {
            var image=images[i];if(image==null || image.sprite==null || shown[i]==image.sprite)continue;
            shown[i]=image.sprite;
            foreach(var frame in artwork)
            {
                if(frame.sprite!=image.sprite)continue;
                var r=image.rectTransform;
                r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);
                r.sizeDelta=frame.size;r.anchoredPosition=frame.position;
                break;
            }
        }
    }
}
