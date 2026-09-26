using System;
using UnityEngine;
using UnityEngine.UI;

namespace Flats.Account
{
    // Binds the authored account block (Menu/Character/Sync/Account in GameInterface)
    // to FlatsAccountService. Labels are FlatsLocalizedText, so English source strings set
    // here are translated on display. Layout, typography and button styling stay in the prefab.
    public sealed class FlatsAccountPanel : MonoBehaviour
    {
        [SerializeField] Text status;
        [SerializeField] Text detail;
        [SerializeField] Button signIn;
        [SerializeField] Button signOut;
        [SerializeField] Button syncNow;
        [SerializeField] AudioSource pressSound;

        bool bound;
        FlatsAccountService service;

        void OnEnable()
        {
            Bind();
            service = FlatsAccountService.Instance;
            service.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            // Use the captured reference: the Instance getter would create a new service while
            // the scene is being torn down.
            if (service != null) service.Changed -= Refresh;
            service = null;
        }

        void Bind()
        {
            if (bound) return;
            bound = true;
            if (signIn != null && signIn.onClick.GetPersistentEventCount() == 0) signIn.onClick.AddListener(() => { Click(); FlatsAccountService.Instance.SignIn(); });
            if (signOut != null && signOut.onClick.GetPersistentEventCount() == 0) signOut.onClick.AddListener(() => { Click(); FlatsAccountService.Instance.SignOut(); });
            if (syncNow != null && syncNow.onClick.GetPersistentEventCount() == 0) syncNow.onClick.AddListener(() => { Click(); FlatsAccountService.Instance.SyncNow(); });
        }

        void Click() { if (pressSound != null) pressSound.Play(); }

        public void Refresh()
        {
            var service = this.service ?? FlatsAccountService.Existing;
            if (service == null) return;
            // Known app messages are translated before they become a template parameter;
            // unknown server text stays as data.
            string error = FlatsLocalization.Translate(service.LastError);
            string title, body = "";
            bool canSignIn = false, canSignOut = false, canSync = false;
            switch (service.Current)
            {
                case FlatsAccountService.State.Unconfigured:
                    title = "Cloud account not configured";
                    body = "This build has no Unity Cloud project, so saves stay on this device.";
                    break;
                case FlatsAccountService.State.Unsupported:
                    title = "Cloud account unavailable";
                    body = error;
                    break;
                case FlatsAccountService.State.SigningIn:
                    title = "Signing in...";
                    body = "Finish signing in with Google in your browser, then return here.";
                    break;
                case FlatsAccountService.State.Syncing:
                    title = "Syncing...";
                    canSignOut = true;
                    break;
                case FlatsAccountService.State.SignedIn:
                    title = string.IsNullOrEmpty(service.DisplayName) ? "Signed in" : "Signed in as " + service.DisplayName;
                    body = string.IsNullOrEmpty(service.LastError)
                        ? (service.LastSyncUtc.HasValue ? "Last sync: " + service.LastSyncUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "Your save syncs to this account.")
                        : "Sync problem: " + error;
                    canSignOut = true; canSync = true;
                    break;
                case FlatsAccountService.State.Error:
                    title = "Sign-in failed";
                    body = error;
                    canSignIn = true;
                    break;
                default:
                    title = "Not signed in";
                    body = "Sign in with Google to keep your profile, progress and settings on every device.";
                    canSignIn = true;
                    break;
            }
            if (status != null) status.text = title;
            if (detail != null) detail.text = body;
            Show(signIn, canSignIn); Show(signOut, canSignOut); Show(syncNow, canSync);
        }

        static void Show(Button button, bool visible)
        {
            if (button == null) return;
            button.gameObject.SetActive(visible);
            button.interactable = visible;
        }
    }
}
