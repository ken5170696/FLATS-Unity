using System;
using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    [SerializeField] GameObject saveTransferDialog;
    [SerializeField] GameObject saveTransferMessageDialog;
    [SerializeField] Text saveTransferPreview, saveTransferMessageTitle, saveTransferMessageBody;
    [SerializeField] ScrollRect saveTransferMessageScroll;
    [SerializeField] RectTransform saveTransferMessageContent;
    [SerializeField] Button saveTransferCopyPath;
    [SerializeField] Button saveTransferMessageClose;
    string exportedSavePath;
    [SerializeField] InputField saveImportPath;
    [SerializeField] Button saveImportConfirm, saveImportCancel;
    [SerializeField] Button saveTransferImportButton,saveTransferExportButton;
    [SerializeField] Text saveImportConfirmLabel, saveTransferTitle;
    bool saveImportIsJson, saveExportIsJson;
#if UNITY_WEBGL && !UNITY_EDITOR
    FlatsBrowserSaveTransfer browserSaveTransfer;
    bool browserImportAwaitingSave;
#endif
    FlatsLocalProfile.Profile pendingSaveImport;

    public void ShowLegacyCloudStatus()
    {
        PlayMenuSound(pressSE);
        ShowConfirm("Legacy cloud unavailable", "Original service data cannot be accessed. No cloud data was loaded.\nUse Export save / Import old save files, or LAN Sync between desktop devices.", null, "OK", null);
    }

    void InitializeSaveTransfer()
    {
        saveTransferImportButton.onClick.AddListener(()=>{PlayMenuSound(pressSE);ShowSaveImport();});
        saveTransferExportButton.onClick.AddListener(()=>{PlayMenuSound(pressSE);ExportSave();});
        saveImportConfirm.onClick.AddListener(()=>{PlayMenuSound(pressSE);ConfirmSaveImport();});
        saveImportCancel.onClick.AddListener(()=>{PlayMenuSound(cancelSE);CancelSaveImport();});
        saveTransferCopyPath.onClick.AddListener(()=>{PlayMenuSound(pressSE);GUIUtility.systemCopyBuffer=exportedSavePath;});
        saveTransferMessageClose.onClick.AddListener(()=>{PlayMenuSound(cancelSE);saveTransferMessageDialog.SetActive(false);});
#if UNITY_WEBGL && !UNITY_EDITOR
        browserSaveTransfer = FlatsBrowserSaveTransfer.Create(transform);
#endif
    }
    void CancelSaveImport()
    {
            pendingSaveImport = null;
            saveTransferDialog.SetActive(false);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (browserImportAwaitingSave)
            {
                FlatsStorageNotice.Show("Imported profile is active for this session, but browser persistence was not confirmed. Export a backup and retry saving from the browser storage notice.", false);
                Application.LoadLevel(0);
            }
#endif
    }

    void ShowSaveTransferMessage(string title, string message, string path = null)
    {
        exportedSavePath = path;
        saveTransferMessageTitle.text = title;
        saveTransferMessageBody.text = message;
        saveTransferCopyPath.gameObject.SetActive(!string.IsNullOrEmpty(path));
        var bodyHeight = Mathf.Max(125f, saveTransferMessageBody.preferredHeight + 8f);
        saveTransferMessageBody.rectTransform.sizeDelta = new Vector2(405f, bodyHeight);
        saveTransferMessageContent.sizeDelta = new Vector2(0f, bodyHeight);
        saveTransferMessageScroll.verticalNormalizedPosition = 1f;
        saveTransferMessageDialog.SetActive(true);
        saveTransferMessageDialog.transform.SetAsLastSibling();
    }

    void ShowSaveImport()
    {
        if (gameState != "Main") { ShowSaveTransferMessage("Import unavailable", "Return to the main menu before importing a save."); return; }
#if UNITY_WEBGL && !UNITY_EDITOR
        if(browserImportAwaitingSave){PersistBrowserImport();return;}
#endif
        pendingSaveImport = null;
        saveExportIsJson = false;
        saveImportIsJson = false;
        saveImportPath.readOnly = false;
        saveImportConfirm.interactable = true;
        saveTransferTitle.text = "Import old save";
        saveImportCancel.GetComponentInChildren<Text>().text = "Cancel";
#if UNITY_WEBGL && !UNITY_EDITOR
        browserSaveTransfer.Upload((bytes, filename) =>
        {
            try { PreviewSaveProfile(FlatsSaveTransfer.ReadBytes(bytes, filename)); }
            catch (Exception error) { ShowSaveTransferMessage("Import failed", error.Message); }
        }, (status, error) =>
        {
            if (status == "error") ShowSaveTransferMessage("Import failed", error);
        });
#elif UNITY_EDITOR || UNITY_STANDALONE_WIN
        string path = LocalModFilePicker.ChooseSave();
        if (!string.IsNullOrEmpty(path)) PreviewSaveImport(path);
#else
        saveImportIsJson = Application.isMobilePlatform;
        saveImportPath.gameObject.SetActive(true);
        saveImportPath.text = "";
        saveImportPath.placeholder.GetComponent<Text>().text = saveImportIsJson ? "Paste exported FLATS JSON" : "Absolute path to .dat or .json save";
        saveTransferPreview.text = saveImportIsJson
            ? "Paste an exported FLATS JSON save. Review the character before replacing your current save."
            : "Paste the absolute path to an old playerprefs.dat or an exported FLATS JSON save.";
        saveImportConfirmLabel.text = saveImportIsJson ? "Review JSON" : "Load file";
        saveTransferDialog.SetActive(true);
        saveTransferDialog.transform.SetAsLastSibling();
        saveImportPath.ActivateInputField();
#endif
    }

    void PreviewSaveProfile(FlatsLocalProfile.Profile profile)
    {
        if (gameState != "Main") throw new InvalidOperationException("Return to the main menu before importing a save.");
        pendingSaveImport = profile;
        string name = profile.character.Split('$')[1];
        saveTransferPreview.text = "Character: " + name + "\n\nThe current save will be backed up before replacement.";
        saveImportPath.gameObject.SetActive(false);
        saveImportConfirmLabel.text = "Replace current save";
        saveTransferDialog.SetActive(true);
        saveTransferDialog.transform.SetAsLastSibling();
    }

    void PreviewSaveImport(string text)
    {
        try { PreviewSaveProfile(saveImportIsJson ? FlatsSaveTransfer.ReadJson(text) : FlatsSaveTransfer.Read(text)); }
        catch (Exception error)
        {
            pendingSaveImport = null;
            if (saveTransferDialog.activeSelf) saveTransferPreview.text = "Import failed: " + error.Message;
            else ShowSaveTransferMessage("Import failed", error.Message);
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    void PersistBrowserImport()
    {
        if(browserSaveTransfer.IsFlushing)return;
        saveTransferImportButton.interactable=false;
        saveTransferExportButton.interactable=false;
        saveImportConfirm.interactable = false;
        saveImportCancel.interactable = false;
        saveTransferDialog.SetActive(true);
        saveTransferDialog.transform.SetAsLastSibling();
        saveTransferPreview.text = "Saving imported profile to browser storage...";
        Action<string,string> completion=(status, error) =>
        {
            saveImportConfirm.interactable = true;
            saveImportCancel.interactable = true;
            if (status == "saved")
            {
                browserImportAwaitingSave = false;
                saveTransferDialog.SetActive(false);
                Application.LoadLevel(0);
            }
            else
            {
                saveTransferPreview.text = "Imported in this session, but browser storage failed. " + error;
                saveImportConfirmLabel.text = "Retry browser save";
                saveImportCancel.GetComponentInChildren<Text>().text = "Use this session";
            }
        };
        try { browserSaveTransfer.Flush(completion); }
        catch(Exception error) { completion("error",error.Message); }
    }
#endif

    void ConfirmSaveImport()
    {
        if (saveExportIsJson)
        {
            try
            {
                GUIUtility.systemCopyBuffer = saveImportPath.text;
                saveTransferPreview.text = GUIUtility.systemCopyBuffer == saveImportPath.text
                    ? "JSON copied. Paste it into a text file or another device's Import old save field."
                    : "Clipboard unavailable. Select and copy the JSON above, then save it outside the app.";
            }
            catch (Exception) { saveTransferPreview.text = "Clipboard unavailable. Select and copy the JSON above."; }
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        if (browserImportAwaitingSave) { PersistBrowserImport(); return; }
#endif
        if (pendingSaveImport == null)
        {
            PreviewSaveImport(saveImportPath.text.Trim());
            return;
        }
        try
        {
            if (gameState != "Main") throw new InvalidOperationException("Return to the main menu before importing a save.");
#if UNITY_WEBGL && !UNITY_EDITOR
            // Parse before committing, then publish the complete imported session
            // synchronously so ordinary Menu saves cannot write the old snapshot.
            var importedSnapshot=Flats.Profiles.LegacyProfileCodec.Decode(new Flats.Profiles.ProfilePayload(pendingSaveImport.character,pendingSaveImport.settings,pendingSaveImport.current));
#endif
            FlatsSaveTransfer.Import(pendingSaveImport);
#if UNITY_WEBGL && !UNITY_EDITOR
            myCharacter=importedSnapshot.Character;mySettings=importedSnapshot.Settings;myCurrent=importedSnapshot.Current;
#endif
            pendingSaveImport = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            browserImportAwaitingSave = true;
            PersistBrowserImport();
#else
            saveTransferDialog.SetActive(false);
            Application.LoadLevel(0);
#endif
        }
        catch (Exception e) { saveTransferPreview.text = "Import failed: " + e.Message; }
    }

    void ExportSave()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if(browserImportAwaitingSave){PersistBrowserImport();return;}
#endif
        try
        {
            SaveDataController.Save();
            if (!FlatsLocalProfile.LastSaveSucceeded) throw new InvalidOperationException("Current save could not be written.");
#if UNITY_WEBGL && !UNITY_EDITOR
            browserSaveTransfer.Download("FLATS-save-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json", FlatsSaveTransfer.ExportJson(), (status, error) =>
            {
                if (status == "error") ShowSaveTransferMessage("Export failed", error);
                else if (status == "download") ShowSaveTransferMessage("Download requested", "Check your browser downloads. Keep the JSON file outside browser storage to retain a backup.");
            });
#else
            if (Application.isMobilePlatform)
            {
                saveExportIsJson = true;
                pendingSaveImport = null;
                saveTransferTitle.text = "Export save JSON";
                saveImportPath.gameObject.SetActive(true);
                saveImportPath.text = FlatsSaveTransfer.ExportJson();
                saveImportPath.readOnly = true;
                saveImportConfirmLabel.text = "Copy JSON";
                saveImportConfirm.interactable = true;
                saveTransferPreview.text = "Copy this JSON and save it in a text file or paste it into Import old save on another device.";
                saveTransferDialog.SetActive(true);
                saveTransferDialog.transform.SetAsLastSibling();
            }
            else
            {
                string path = FlatsSaveTransfer.Export();
                ShowSaveTransferMessage("Save exported", "Your save was written to:\n" + path, path);
            }
#endif
        }
        catch (Exception e) { ShowSaveTransferMessage("Export failed", e.Message); }
    }
}
