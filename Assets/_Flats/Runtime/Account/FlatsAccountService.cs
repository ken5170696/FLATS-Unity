using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using UnityEngine;

namespace Flats.Account
{
    // Google sign-in through Unity Player Accounts and a cloud mirror of the local save
    // in Cloud Save. The local atomic record stays authoritative: the service never
    // blocks play, and every cloud-to-local write goes through FlatsSaveTransfer.Import
    // (validation, backup, preference acceptance) exactly like a file import.
    public sealed class FlatsAccountService : MonoBehaviour
    {
        public enum State { Unconfigured, Unsupported, SignedOut, SigningIn, SignedIn, Syncing, Error }

        public const string ProfileKey = "flats.profile.v1";
        public const string AvatarKey = "flats.avatar.v1.png";
        const string DirtyKey = "account.v1.dirty";
        const string LastPlayerKey = "account.v1.lastPlayerId";
        const float UploadDelaySeconds = 3f;

        static FlatsAccountService instance;
        public static FlatsAccountService Instance
        {
            get
            {
                if (instance == null)
                {
                    var existing = FindObjectOfType<FlatsAccountService>();
                    if (existing != null) instance = existing;
                    else
                    {
                        var go = new GameObject("FlatsAccountService");
                        DontDestroyOnLoad(go);
                        instance = go.AddComponent<FlatsAccountService>();
                    }
                }
                return instance;
            }
        }

        public State Current { get; private set; } = State.SignedOut;
        public string PlayerId { get; private set; } = "";
        public string DisplayName { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public DateTime? LastSyncUtc { get; private set; }
        public bool Busy => Current == State.SigningIn || Current == State.Syncing;
        public bool IsSignedIn => Current == State.SignedIn || Current == State.Syncing;
        public event Action Changed;

        bool initialized, startupSyncDone, playerAccountHooked;
        string cloudWriteLock;
        Coroutine pendingUpload;
        // A cloud document that arrived while the player was not on the main menu.
        CloudProfileDocument deferredDownload;
        bool deferredConflict;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            if (FlatsPreferences.IsolatedRoot != null) Set(State.Unsupported, "Verification mode keeps saves local.");
            else if (string.IsNullOrEmpty(Application.cloudProjectId)) Set(State.Unconfigured, "");
            SaveDataController.Saved += OnLocalSaved;
        }

        void OnDestroy()
        {
            SaveDataController.Saved -= OnLocalSaved;
            if (instance == this) instance = null;
        }

        // Menu calls this once its profile has loaded. Restores a cached session silently
        // (no browser) and reconciles; also drains work deferred while in a match.
        public static void OnMainMenuReady()
        {
            var service = Instance;
            if (service.Current == State.Unconfigured || service.Current == State.Unsupported) return;
            if (service.deferredDownload != null || service.deferredConflict) { service.ResumeDeferred(); return; }
            if (!service.startupSyncDone) { service.startupSyncDone = true; service.RestoreSession(); }
        }

        public void SignIn()
        {
            if (Busy || Current == State.Unconfigured || Current == State.Unsupported) return;
            RunSignIn();
        }

        public void SignOut()
        {
            if (!IsSignedIn && Current != State.Error) return;
            try
            {
                if (AuthenticationService.Instance.IsSignedIn || AuthenticationService.Instance.SessionTokenExists) AuthenticationService.Instance.SignOut(true);
                PlayerAccountService.Instance.SignOut();
            }
            catch (Exception error) { Debug.LogWarning("FLATS_ACCOUNT sign-out: " + error.Message); }
            cloudWriteLock = null; PlayerId = ""; DisplayName = "";
            if (pendingUpload != null) { StopCoroutine(pendingUpload); pendingUpload = null; }
            Set(State.SignedOut, "");
        }

        public void SyncNow()
        {
            if (!IsSignedIn || Busy) return;
            RunReconcile();
        }

        // ---- sign-in -------------------------------------------------------------

        async void RestoreSession()
        {
            try
            {
                if (!await EnsureInitialized()) return;
                if (!AuthenticationService.Instance.SessionTokenExists) { Set(State.SignedOut, ""); return; }
                Set(State.SigningIn, "");
                // With a cached session token this resumes the same player; it never creates
                // an anonymous account because the token check above guards it.
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                await AfterAuthenticated();
            }
            catch (Exception error) { Fail(error); }
        }

        async void RunSignIn()
        {
            try
            {
                if (!await EnsureInitialized()) return;
                Set(State.SigningIn, "");
                if (!playerAccountHooked)
                {
                    PlayerAccountService.Instance.SignedIn += OnPlayerAccountSignedIn;
                    PlayerAccountService.Instance.SignInFailed += OnPlayerAccountFailed;
                    playerAccountHooked = true;
                }
#if UNITY_WEBGL && !UNITY_EDITOR
                // The browser leaves this page for the sign-in redirect. Persist pending saves to
                // IndexedDB first, otherwise unsynced changes die with the page.
                if (!await FlushBrowserStorage()) return;
#endif
                // Opens the system browser (or redirects on Web). Completion arrives via SignedIn.
                await PlayerAccountService.Instance.StartSignInAsync();
            }
            catch (Exception error) { Fail(error); }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        Task<bool> FlushBrowserStorage()
        {
            var completion = new TaskCompletionSource<bool>();
            try
            {
                var transfer = FlatsBrowserSaveTransfer.Create(transform);
                transfer.Flush((status, error) =>
                {
                    if (status == "saved") completion.TrySetResult(true);
                    else { Set(State.SignedOut, "Browser storage could not be saved before sign-in. " + error); completion.TrySetResult(false); }
                });
            }
            catch (Exception error) { Set(State.SignedOut, "Browser storage could not be saved before sign-in. " + error.Message); completion.TrySetResult(false); }
            return completion.Task;
        }
#endif

        async void OnPlayerAccountSignedIn()
        {
            try
            {
                string token = PlayerAccountService.Instance.AccessToken;
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
                await AuthenticationService.Instance.SignInWithUnityAsync(token);
                await AfterAuthenticated();
            }
            catch (Exception error) { Fail(error); }
        }

        void OnPlayerAccountFailed(RequestFailedException error) { Fail(error); }

        async Task AfterAuthenticated()
        {
            PlayerId = AuthenticationService.Instance.PlayerId ?? "";
            DisplayName = AuthenticationService.Instance.PlayerName ?? "";
            if (string.IsNullOrEmpty(DisplayName))
            {
                try { DisplayName = await AuthenticationService.Instance.GetPlayerNameAsync() ?? ""; }
                catch (Exception) { DisplayName = ""; }
            }
            Set(State.SignedIn, "");
            await Reconcile();
        }

        async Task<bool> EnsureInitialized()
        {
            if (initialized) return true;
            if (Current == State.Unconfigured || Current == State.Unsupported) return false;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                AuthenticationService.Instance.Expired += OnSessionExpired;
                initialized = true;
                return true;
            }
            catch (Exception error) { Fail(error); return false; }
        }

        void OnSessionExpired()
        {
            cloudWriteLock = null;
            Set(State.SignedOut, "Session expired. Sign in again to keep syncing.");
        }

        // ---- reconcile -----------------------------------------------------------

        async void RunReconcile() { try { await Reconcile(); } catch (Exception error) { Fail(error); } }

        async Task Reconcile()
        {
            if (!IsSignedIn) return;
            Set(State.Syncing, "");
            string localExport = LocalExport();
            var cloud = await LoadCloud();
            bool hasLocal = localExport != null, hasCloud = cloud != null;
            bool equal = hasLocal && hasCloud && FlatsCloudSyncPolicy.ContentEqual(localExport, cloud.export);
            string lastPlayer = FlatsPreferences.GetString(LastPlayerKey, "");
            bool accountChanged = lastPlayer.Length > 0 && lastPlayer != PlayerId;
            var action = FlatsCloudSyncPolicy.Decide(hasLocal, hasCloud, equal, FlatsPreferences.GetString(MarkerKey, null), cloud == null ? null : cloud.savedAtUtc,
                FlatsPreferences.GetInt(DirtyKey, 0) == 1, accountChanged);
            switch (action)
            {
                case SyncAction.Upload: await Upload(localExport); break;
                case SyncAction.Download: await ApplyCloud(cloud); break;
                case SyncAction.AskPlayer: AskPlayer(cloud, localExport); return;
                default:
                    if (cloud != null) MarkSynced(cloud.savedAtUtc);
                    await UploadAvatarIfChanged();
                    Set(State.SignedIn, "");
                    break;
            }
        }

        void AskPlayer(CloudProfileDocument cloud, string localExport)
        {
            var menu = Menu.Current;
            if (menu == null || Menu.gameState != "Main") { deferredDownload = cloud; deferredConflict = true; Set(State.SignedIn, ""); return; }
            ProfileSummary local, remote = null;
            try { local = FlatsCloudSyncPolicy.Summarize(localExport, null); if (cloud != null) remote = FlatsCloudSyncPolicy.Summarize(cloud.export, cloud.SavedAt); }
            catch (Exception error) { Fail(error); return; }
            Set(State.SignedIn, "");
            if (remote == null)
            {
                // A different account was used on this device before and the new account has no cloud
                // save yet. Never upload silently; the player decides what this save belongs to.
                menu.ShowConfirm("Upload this device's save?", "This device last synced with a different account.\n\n" +
                    "This device: " + local.Name + ", " + local.Kills + " kills, survival " + local.SurvivalScore + "\n\n" +
                    "Upload keeps it as this account's cloud save. Not now leaves the cloud empty; the device save is unchanged.",
                    accepted => { if (accepted) RunUpload(localExport, force: true); }, "Upload", "Not now");
                return;
            }
            string message = "This device and your account have different saves.\n\n" +
                "Cloud: " + remote.Name + ", " + remote.Kills + " kills, survival " + remote.SurvivalScore + ", saved " + Describe(remote.SavedAtUtc) + "\n" +
                "This device: " + local.Name + ", " + local.Kills + " kills, survival " + local.SurvivalScore + "\n\n" +
                "Use cloud replaces this device's save (a backup is kept). Keep device uploads this save to the cloud.";
            menu.ShowConfirm("Which save do you want to keep?", message, accepted => { if (accepted) RunApplyCloud(cloud); else RunUpload(localExport, force: true); },
                "Use cloud", "Keep device");
        }

        static string Describe(DateTime? utc) => utc.HasValue ? utc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "unknown time";

        void ResumeDeferred()
        {
            deferredConflict = false;
            var cloud = deferredDownload; deferredDownload = null;
            if (cloud == null) return;
            RunReconcile();
        }

        async void RunApplyCloud(CloudProfileDocument cloud) { try { Set(State.Syncing, ""); await ApplyCloud(cloud); } catch (Exception error) { Fail(error); } }
        async void RunUpload(string localExport, bool force) { try { Set(State.Syncing, ""); if (force) cloudWriteLock = null; await Upload(localExport); } catch (Exception error) { Fail(error); } }

        async Task ApplyCloud(CloudProfileDocument cloud)
        {
            var menu = Menu.Current;
            if (menu == null || Menu.gameState != "Main") { deferredDownload = cloud; Set(State.SignedIn, "Cloud save is ready; it will be applied on the main menu."); return; }
            FlatsSaveTransfer.Preference[] preferences;
            var profile = FlatsSaveTransfer.ReadJson(cloud.export, out preferences);
            byte[] avatar = null;
            try { avatar = await CloudSaveService.Instance.Files.Player.LoadBytesAsync(AvatarKey); }
            catch (CloudSaveException) { avatar = null; }
            MarkSynced(cloud.savedAtUtc);
            if (avatar != null) RememberAvatar(avatar);
            LastSyncUtc = DateTime.UtcNow;
            Set(State.SignedIn, "");
            // Imports, backs up the previous save and reloads the menu like a file import.
            menu.ImportCloudProfile(profile, preferences, avatar);
        }

        async Task Upload(string localExport)
        {
            if (localExport == null) { Set(State.SignedIn, ""); return; }
            string json = FlatsCloudSyncPolicy.BuildDocument(localExport, DateTime.UtcNow, Application.version, Application.platform.ToString());
            var document = CloudProfileDocument.Parse(json);
            try
            {
                Dictionary<string, string> locks;
                if (cloudWriteLock != null)
                    locks = await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, SaveItem> { { ProfileKey, new SaveItem(json, cloudWriteLock) } });
                else
                    locks = await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { ProfileKey, json } });
                string newLock;
                if (locks != null && locks.TryGetValue(ProfileKey, out newLock)) cloudWriteLock = newLock;
            }
            catch (CloudSaveException error) when (error.Reason == CloudSaveExceptionReason.Conflict)
            {
                // Another device wrote in between: reload and let the policy decide again.
                cloudWriteLock = null;
                Set(State.SignedIn, "");
                await Reconcile();
                return;
            }
            MarkSynced(document.savedAtUtc);
            await UploadAvatarIfChanged();
            LastSyncUtc = DateTime.UtcNow;
            Set(State.SignedIn, "");
        }

        async Task<CloudProfileDocument> LoadCloud()
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { ProfileKey });
            Item item;
            if (result == null || !result.TryGetValue(ProfileKey, out item) || item == null) { cloudWriteLock = null; return null; }
            cloudWriteLock = item.WriteLock;
            string json = null;
            try { json = item.Value.GetAs<string>(); } catch (Exception) { json = null; }
            var document = CloudProfileDocument.Parse(json);
            if (document == null) Debug.LogWarning("FLATS_ACCOUNT cloud profile is unreadable; treating it as absent.");
            return document;
        }

        // ---- avatar ----------------------------------------------------------------

        async Task UploadAvatarIfChanged()
        {
            byte[] png = ReadLocalAvatar();
            if (png == null) return;
            string hash = Sha256(png);
            if (FlatsPreferences.GetString(AvatarMarkerKey, "") == hash) return;
            try
            {
                await CloudSaveService.Instance.Files.Player.SaveAsync(AvatarKey, png);
                FlatsPreferences.SetString(AvatarMarkerKey, hash); FlatsPreferences.Save();
            }
            catch (CloudSaveException error) { Debug.LogWarning("FLATS_ACCOUNT avatar upload: " + error.Message); }
        }

        void RememberAvatar(byte[] png) { FlatsPreferences.SetString(AvatarMarkerKey, Sha256(png)); FlatsPreferences.Save(); }

        static byte[] ReadLocalAvatar()
        {
            try { return FlatsUserIcon.Exists() ? File.ReadAllBytes(FlatsUserIcon.FilePath) : null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        // ---- local state -----------------------------------------------------------

        string MarkerKey => "account.v1." + PlayerId + ".cloudStamp";
        string AvatarMarkerKey => "account.v1." + PlayerId + ".avatarSha256";

        void MarkSynced(string cloudStamp)
        {
            FlatsPreferences.SetString(MarkerKey, cloudStamp ?? "");
            FlatsPreferences.SetString(LastPlayerKey, PlayerId ?? "");
            FlatsPreferences.SetInt(DirtyKey, 0);
            FlatsPreferences.Save();
            LastSyncUtc = DateTime.UtcNow;
        }

        static string LocalExport()
        {
            try { return FlatsSaveTransfer.ExportJson(); }
            catch (InvalidDataException) { return null; }  // no profile saved yet
        }

        void OnLocalSaved()
        {
            if (Current == State.Unconfigured || Current == State.Unsupported) return;
            FlatsPreferences.SetInt(DirtyKey, 1); FlatsPreferences.Save();
            if (!IsSignedIn) return;
            if (pendingUpload != null) StopCoroutine(pendingUpload);
            pendingUpload = StartCoroutine(UploadSoon());
        }

        IEnumerator UploadSoon()
        {
            yield return new WaitForSecondsRealtime(UploadDelaySeconds);
            pendingUpload = null;
            if (!IsSignedIn || Busy) yield break;
            RunReconcile();
        }

        void Set(State state, string error)
        {
            Current = state; LastError = error ?? "";
            if (!string.IsNullOrEmpty(LastError)) Debug.LogWarning("FLATS_ACCOUNT " + state + ": " + LastError);
            Changed?.Invoke();
        }

        void Fail(Exception error)
        {
            string message = error is RequestFailedException failed ? failed.Message : error.Message;
            Set(IsSignedIn || AuthenticationService.Instance.IsSignedIn ? State.SignedIn : State.Error, message);
            if (Current == State.SignedIn) { LastError = message; Changed?.Invoke(); }
        }
    }
}
