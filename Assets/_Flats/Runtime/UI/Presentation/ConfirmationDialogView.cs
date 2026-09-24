using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Authored modal contents. Only the message's preferred height is content-driven.
public sealed class ConfirmationDialogView : MonoBehaviour
{
    public Image paper;
    public Text title, message;
    public Button positive, negative, alert;
    public ScrollRect body;
    public LayoutElement bodyLayout;
    public float minimumBodyHeight=32, maximumBodyHeight=196;
    public void RefreshMessageLayout()
    {
        Canvas.ForceUpdateCanvases();
        bodyLayout.preferredHeight=Mathf.Clamp(message.preferredHeight,minimumBodyHeight,maximumBodyHeight);
        Canvas.ForceUpdateCanvases();
        body.verticalNormalizedPosition=1;
    }
    GameObject previousSelection;
    void OnEnable()
    {
        previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
    }
    void OnDisable()
    {
        if(EventSystem.current!=null && previousSelection!=null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
    }
}
