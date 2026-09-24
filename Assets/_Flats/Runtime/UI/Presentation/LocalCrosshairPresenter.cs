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
        [SerializeField, Tooltip("Authored child graphic. It is enabled while a module replaces the original reticle.")]
        CrosshairGraphic custom;
        bool replacing;
        public bool IsCustom { get { return replacing; } }
        public void BindLocalOwner(Object localOwner) { owner = localOwner; }
        public void SetVisible(bool visible)
        {
            // The controller retains this through an interface. During scene
            // teardown that managed reference can outlive the native UI object.
            if(this != null)gameObject.SetActive(visible);
        }
        void Awake() { original = GetComponentsInChildren<Image>(true); originalEnabled = new bool[original.Length]; }
        void LateUpdate()
        {
            var appearance = CrosshairPresentation.Appearance;
            bool wanted = owner != null && appearance != null && custom != null;
            if (wanted && !replacing)
            {
                for(int i=0;i<original.Length;i++) { originalEnabled[i]=original[i].enabled; original[i].enabled=false; }
                custom.gameObject.SetActive(true); replacing=true;
            }
            if (!wanted) { Restore(); return; }
            // Legacy DesktopCrosshairSize may scale the parent. Module size is in HUD canvas units.
            var scale=transform.localScale;
            custom.rectTransform.localScale=new Vector3(1/Mathf.Max(.001f,scale.x),1/Mathf.Max(.001f,scale.y),1);
            custom.Set(appearance);
        }
        void Restore()
        {
            if(!replacing)return;
            for(int i=0;i<original.Length;i++)if(original[i]!=null)original[i].enabled=originalEnabled[i];
            if(custom!=null)custom.gameObject.SetActive(false);
            replacing=false;
        }
        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }
    }
}
