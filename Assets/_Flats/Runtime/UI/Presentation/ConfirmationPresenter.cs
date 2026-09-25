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
    }
}
