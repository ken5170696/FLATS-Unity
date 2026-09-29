using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace Flats.UI
{
    public sealed partial class ConfirmationPresenter
    {
        readonly GameObject confirm;
        readonly ConfirmationDialogView view;
        public ConfirmationPresenter(GameObject panel)
        {
            confirm=panel;view=panel.GetComponent<ConfirmationDialogView>();
        }
        public void ShowConfirm(Color theme,string title,string message,UnityAction<bool> action,string positiveBtnText,string negativeBtnText)
        {
            view.title.text=title;view.message.text=message;
            bool choice=negativeBtnText!=null;
            Bind(view.positive,positiveBtnText,true,action);
            Bind(view.negative,negativeBtnText,false,action);
            Bind(view.alert,positiveBtnText,true,action);
            view.positive.gameObject.SetActive(choice);view.negative.gameObject.SetActive(choice);view.alert.gameObject.SetActive(!choice);
            confirm.SetActive(true);
            KeepOnTop();
            ResetMessageScroll();
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(choice?view.negative.gameObject:view.alert.gameObject);
        }
        void Bind(Button button,string label,bool accepted,UnityAction<bool> action)
        {
            button.GetComponentInChildren<Text>(true).text=label??"";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>{OnClickedConfirm();action?.Invoke(accepted);});
        }
        public void OnClickedConfirm(){confirm.SetActive(false);}
        // Screens opened later under the same Menu (the Roguelike run screen and overview) are later siblings and would
        // cover the dialog and take its clicks; the dialog's authored nested canvas (GameInterface.prefab) sorts it above them.
        // A nested canvas only applies its sorting once active, so it is re-asserted each time the dialog opens.
        void KeepOnTop()
        {
            var canvas=confirm.GetComponent<Canvas>();
            if(canvas!=null)canvas.overrideSorting=true;
        }
    }
}
