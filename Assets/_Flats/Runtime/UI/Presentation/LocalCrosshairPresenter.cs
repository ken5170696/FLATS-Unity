using UnityEngine;
using UnityEngine.UI;

namespace Flats.UI
{
    // Attached to the original reticle. Its active state, parents and all old references survive.
    public sealed class LocalCrosshairPresenter : MonoBehaviour, ICrosshairVisibility
    {
        Object owner;
        Image[] original;
        bool[] originalEnabled;
        CrosshairGraphic custom;
        bool replacing;
        public bool IsCustom { get { return replacing; } }
        public void BindLocalOwner(Object localOwner) { owner = localOwner; }
        public void SetVisible(bool visible) { gameObject.SetActive(visible); }
        void Awake() { original = GetComponentsInChildren<Image>(true); originalEnabled = new bool[original.Length]; }
        void LateUpdate()
        {
            var appearance = CrosshairPresentation.Appearance;
            bool wanted = owner != null && appearance != null;
            if (wanted && !replacing)
            {
                for(int i=0;i<original.Length;i++) { originalEnabled[i]=original[i].enabled; original[i].enabled=false; }
                if(custom == null)
                {
                    var go=new GameObject("ModuleCrosshair",typeof(RectTransform),typeof(CanvasRenderer),typeof(CrosshairGraphic));
                    go.transform.SetParent(transform,false); custom=go.GetComponent<CrosshairGraphic>(); custom.raycastTarget=false;
                    custom.rectTransform.sizeDelta=new Vector2(100,100);
                }
                custom.gameObject.SetActive(true); replacing=true;
            }
            if (!wanted) { Restore(); return; }
            // Legacy DesktopCrosshairSize may scale the parent. Module size is in HUD canvas units.
            var scale=transform.localScale;
            custom.rectTransform.localScale=new Vector3(1/Mathf.Max(.001f,scale.x),1/Mathf.Max(.001f,scale.y),1);
            custom.Set(appearance.Style,appearance.Size);
        }
        void Restore()
        {
            if(!replacing)return;
            for(int i=0;i<original.Length;i++)if(original[i]!=null)original[i].enabled=originalEnabled[i];
            if(custom!=null) { custom.gameObject.SetActive(false); Destroy(custom.gameObject); custom=null; }
            replacing=false;
        }
        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }
    }
}
