using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
namespace Flats.UI {
public sealed partial class ConfirmationPresenter {
 private readonly GameObject confirm;
 private readonly Color[] originalTextColors;
 private readonly System.Func<GameObject,Text,Text> createBodyText;
 public ConfirmationPresenter(GameObject panel, System.Func<GameObject,Text,Text> textFactory = null) {
     confirm = panel;
     createBodyText = textFactory ?? ((target, source) => { var label=target.AddComponent<Text>();label.font=source.font;return label; });
     originalTextColors=System.Array.ConvertAll(panel.GetComponentsInChildren<Text>(true),t=>t.color);CaptureOriginalAppearance();
 }
		public void ShowConfirm(Color theme, string title, string message, UnityAction<bool> action, string positiveBtnText, string negativeBtnText)
		{
			RestoreAppearance();
            Color color = theme;
			var labels=confirm.GetComponentsInChildren<Text>(true);
			for(int i=0;i<labels.Length&&i<originalTextColors.Length;i++)labels[i].color=originalTextColors[i];
			confirm.transform.GetChild(0).GetComponent<Image>().color = new Color(color.r / 2f, color.g / 2f, color.b / 2f, 1f);
			confirm.transform.GetChild(1).GetComponent<Text>().text = title;
			confirm.transform.GetChild(2).GetComponent<Text>().text = message;
			confirm.transform.GetChild(3).GetChild(0).GetComponent<Text>()
				.text = positiveBtnText;
			confirm.transform.GetChild(4).GetChild(0).GetComponent<Text>()
				.text = negativeBtnText;
			confirm.transform.GetChild(5).GetChild(0).GetComponent<Text>()
				.text = positiveBtnText;
			GameObject gameObject = confirm.transform.GetChild(3).gameObject;
			GameObject gameObject2 = confirm.transform.GetChild(4).gameObject;
			GameObject gameObject3 = confirm.transform.GetChild(5).gameObject;
			gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
			gameObject2.GetComponent<Button>().onClick.RemoveAllListeners();
			gameObject3.GetComponent<Button>().onClick.RemoveAllListeners();
			if (negativeBtnText == null && action != null)
			{
				gameObject3.SetActive(true);
				gameObject3.GetComponent<Button>().onClick.AddListener(delegate
				{
					action(true);
					OnClickedConfirm();
				});
			}
			else if (negativeBtnText == null || action == null)
			{
				gameObject3.gameObject.SetActive(true);
			}
			else
			{
				gameObject.GetComponent<Button>().onClick.AddListener(delegate
				{
					action(true);
					OnClickedConfirm();
				});
				gameObject2.GetComponent<Button>().onClick.AddListener(delegate
				{
					action(false);
					OnClickedConfirm();
				});
				gameObject.gameObject.SetActive(true);
				gameObject2.gameObject.SetActive(true);
			}
			confirm.gameObject.SetActive(true);
            if(title=="Room modules differ")StyleRoomMismatch();
			EventSystem.current.SetSelectedGameObject(title=="Room modules differ"?gameObject2:null);
		}

		public void OnClickedConfirm()
		{
			GameObject positiveBtn = confirm.transform.GetChild(3).gameObject;
			GameObject negativeBtn = confirm.transform.GetChild(4).gameObject;
			GameObject alertBtn = confirm.transform.GetChild(5).gameObject;
			positiveBtn.GetComponent<Button>().onClick.RemoveAllListeners();
			negativeBtn.GetComponent<Button>().onClick.RemoveAllListeners();
			alertBtn.GetComponent<Button>().onClick.RemoveAllListeners();
			positiveBtn.GetComponent<Button>().onClick.AddListener(delegate
			{
				positiveBtn.SetActive(false);
				negativeBtn.SetActive(false);
				confirm.SetActive(false);
			});
			negativeBtn.GetComponent<Button>().onClick.AddListener(delegate
			{
				positiveBtn.SetActive(false);
				negativeBtn.SetActive(false);
				confirm.SetActive(false);
			});
			alertBtn.GetComponent<Button>().onClick.AddListener(delegate
			{
				alertBtn.SetActive(false);
				confirm.SetActive(false);
			});
		}

}
}
