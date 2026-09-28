using UnityEngine;
using UnityEngine.UI;

/// <summary>One offer/choice row. Reference bag for the authored RogueOfferRow prefab (Resources/UI/Roguelike).</summary>
public class RogueOfferRowView : MonoBehaviour
{
    public Text title, effect, price, rarity, status, actionLabel;
    public Button action;
    public Image panel;
}
