using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>固定布局來自 MeleeHUD prefab；只綁定狀態與既有輸入。</summary>
public sealed class RogueMeleeHUD : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Text Prompt;
    public GameObject TouchButton;
    RogueMelee melee;
    public void Bind(RogueMelee owner) { melee=owner; }
    void Update()
    {
        if(melee==null||!melee.Equipped){Destroy(gameObject);return;}
        TouchButton.SetActive(RogueInput.IsTouch);
        Prompt.text=melee.AxeThrown?RoguelikeController.T("Axe thrown · walk over it to pick it up"):melee.Guarding?RoguelikeController.T("Guarding · release {0} to stop",RogueInput.KeyText("Melee")):"";   // translated; the key follows the Melee binding
    }
    public void OnPointerDown(PointerEventData data)
    {
        if(melee==null)return;melee.TouchHeld(true);
        var fc=melee.GetComponent<FPSController>();
        if(fc==null||!fc.MeleeReady)return;
        RogueActionGate.NoteMeleeRequest(fc);
        if(Menu.network==0)fc.StartCoroutine("Smash");else fc.GetComponent<PhotonView>().RPC("Smash",PhotonTargets.All);
    }
    public void OnPointerUp(PointerEventData data) { if(melee!=null)melee.TouchHeld(false); }
    void OnDisable(){if(melee!=null)melee.TouchHeld(false);}
}
