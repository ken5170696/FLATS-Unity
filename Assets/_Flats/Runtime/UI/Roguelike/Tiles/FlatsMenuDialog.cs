using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authored modal; cancel owns initial focus. One activation per press, with release protection.</summary>
public sealed class FlatsMenuDialog : MonoBehaviour
{
    public Text title, body;
    public Button primary, alternate, cancel;
    public ScrollRect scroll;
    public CanvasGroup group;
    public float activationDelay=.35f, scrollSpeed=.5f;
    static FlatsMenuDialog active;
    static float blockUntil;
    static bool waitForRelease;
    public static bool BlocksMenuInput { get { if(waitForRelease&&!Held())waitForRelease=false;return active!=null || Time.unscaledTime<blockUntil || waitForRelease; } }
    static bool Held() { var p=InControl.InputManager.ActiveDevice;return Input.GetKey(KeyCode.Escape)||Input.GetKeyUp(KeyCode.Escape)||Input.GetKey(KeyCode.Return)||Input.GetMouseButton(0)||p!=null&&(p.Action1.IsPressed||p.Action2.IsPressed||p.CommandIsPressed); }
    public static void Guard() { blockUntil=Time.unscaledTime+.35f;waitForRelease=true; }
    string heading, message, primaryKey, alternateKey;
    Action yes, other, no;
    GameObject previous;
    float acceptAfter;
    bool closed, released;
    public static FlatsMenuDialog Show(string heading,string message,string primary,Action yes,string alternate=null,Action other=null,Action no=null)
    {
        if(active!=null)active.Dismiss(true);   // a newer question replaces the open one, which counts as cancelled
        var view=Instantiate(Resources.Load<FlatsMenuDialog>("UI/Roguelike/Tiles/FlatsMenuDialog"));
        active=view;view.heading=heading;view.message=message;view.primaryKey=primary;view.alternateKey=alternate;
        view.yes=yes;view.other=other;view.no=no;
        view.previous=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        view.primary.onClick.AddListener(()=>view.Choose(1));view.alternate.onClick.AddListener(()=>view.Choose(2));view.cancel.onClick.AddListener(()=>view.Choose(0));
        view.primary.gameObject.SetActive(yes!=null);view.alternate.gameObject.SetActive(other!=null);
        var actions=new System.Collections.Generic.List<Button>{view.cancel};if(other!=null)actions.Add(view.alternate);if(yes!=null)actions.Add(view.primary);
        for(int i=0;i<actions.Count;i++)actions[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=actions[(i+actions.Count-1)%actions.Count],selectOnLeft=actions[(i+actions.Count-1)%actions.Count],selectOnDown=actions[(i+1)%actions.Count],selectOnRight=actions[(i+1)%actions.Count]};
        view.Refresh();FlatsLocalization.Changed+=view.Refresh;
        PointerFocusPolicy.Hold(view);FlatsCursor.Push(view);view.acceptAfter=Time.unscaledTime+view.activationDelay;
        view.FocusCancel();return view;
    }
    /// <summary>Closes the open dialog without a choice: its owner (the pause, the squad room) is gone, so Leave or Abandon
    /// would act on whatever page is up now.</summary>
    public static void CloseAll() { if(active!=null)active.Dismiss(false); }
    void Dismiss(bool cancelled)
    {
        if(closed)return;closed=true;var action=cancelled?no:null;Release();gameObject.SetActive(false);Destroy(gameObject);if(action!=null)action();
    }
    void Refresh() { RogueMetaUI.Put(title,heading);RogueMetaUI.Put(body,message);RogueMetaUI.Put(primary.GetComponentInChildren<Text>(),primaryKey);RogueMetaUI.Put(alternate.GetComponentInChildren<Text>(),alternateKey);RogueMetaUI.Put(cancel.GetComponentInChildren<Text>(),yes==null?"Back":"Cancel"); }
    void FocusCancel() { if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(cancel.gameObject); }
    public void Choose(int choice)
    {
        if(closed||Time.unscaledTime<acceptAfter)return;
        closed=true;var action=choice==1?yes:choice==2?other:no;RogueAudio.Click();Release();Guard();gameObject.SetActive(false);Destroy(gameObject);if(action!=null)action();
    }
    void Update()
    {
        if(active!=this)return;
        var p=InControl.InputManager.ActiveDevice;
        if(Time.unscaledTime>=acceptAfter && (Input.GetKeyUp(KeyCode.Escape)||p!=null&&p.Action2.WasPressed)) {Choose(0);return;}
        var es=EventSystem.current;if(es!=null&&(es.currentSelectedGameObject==null||!es.currentSelectedGameObject.transform.IsChildOf(transform)))FocusCancel();
        if(p!=null&&scroll!=null)scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+p.RightStickY.Value*scrollSpeed*Time.unscaledDeltaTime);
    }
    void Release() { if(released)return;released=true;if(active==this)active=null;PointerFocusPolicy.Release(this);FlatsCursor.Pop(this);if(EventSystem.current!=null&&(previous==null||previous.activeInHierarchy))EventSystem.current.SetSelectedGameObject(previous); }
    void OnDestroy() { FlatsLocalization.Changed-=Refresh;Release(); }
}
