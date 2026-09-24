using System;
using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    GameObject saveTransferDialog;
    GameObject saveTransferMessageDialog;
    Text saveTransferPreview, saveTransferMessageTitle, saveTransferMessageBody;
    ScrollRect saveTransferMessageScroll;
    RectTransform saveTransferMessageContent;
    Button saveTransferCopyPath;
    string exportedSavePath;
    InputField saveImportPath;
    Button saveImportConfirm, saveImportCancel;
    Button saveTransferImportButton,saveTransferExportButton;
    Text saveImportConfirmLabel, saveTransferTitle;
    bool saveImportIsJson, saveExportIsJson;
#if UNITY_WEBGL && !UNITY_EDITOR
    FlatsBrowserSaveTransfer browserSaveTransfer;
    bool browserImportAwaitingSave;
#endif
    FlatsLocalProfile.Profile pendingSaveImport;

    void InitializeSaveTransfer()
    {
        var sync = mt.Find("Character/Sync");
        if (sync == null) return;
        var original = sync.GetComponentInChildren<Text>(true);
        var ui = new ModCenterWidgets(original != null ? FlatsLocalizedText.GetSourceFont(original) : Resources.GetBuiltinResource<Font>("Arial.ttf"),
            () => PlayMenuSound(pressSE));
        var syncRect = (RectTransform)sync;
        syncRect.offsetMin = new Vector2(syncRect.offsetMin.x, syncRect.offsetMin.y - 64f);
        for (int i = 0; i < sync.childCount; i++)
        {
            if (sync.GetChild(i) is RectTransform child)
                child.anchoredPosition += new Vector2(0f, 32f);
        }
        var heading = ui.Text("SaveTransferHeading", sync, "Save files", 0, -131, 430, 25, 17, Color.white);
        heading.alignment = TextAnchor.MiddleCenter;
        var nativeControl = sync.Find("LANSync").GetComponent<Image>();
        var importButton = saveTransferImportButton = ui.Button("ImportOldSave", sync, "Import old save", -105, -168, 190, 34,
            ShowSaveImport, Color.white);
        var exportButton = saveTransferExportButton = ui.Button("ExportSave", sync, "Export save", 105, -168, 190, 34,
            ExportSave, Color.white);
        foreach (var button in new[] { importButton, exportButton })
        {
            var image = button.GetComponent<Image>();
            image.material = nativeControl.material;
            image.type = nativeControl.type;
            button.GetComponentInChildren<Text>().color = Color.white;
        }

        Color dialogColor = new Color(.31f, .24f, .29f);
        saveTransferDialog = ui.Panel("SaveTransferDialog", sync, 0, 32, 475, 340, dialogColor).gameObject;
        saveTransferTitle = ui.Text("Title", saveTransferDialog.transform, "Import old save", 0, 126, 435, 40, 24, Color.white);
        saveImportPath = ui.Input("SaveImportPath", saveTransferDialog.transform, "Absolute path to .dat or .json save", 0, 40, 435);
        saveImportPath.characterLimit = FlatsSaveTransfer.MaximumBytes;
        saveImportPath.lineType = InputField.LineType.MultiLineNewline;
        ((RectTransform)saveImportPath.transform).sizeDelta = new Vector2(435, 100);
        saveImportPath.gameObject.SetActive(false);
        saveTransferPreview = ui.Text("Preview", saveTransferDialog.transform, "", 0, -70, 435, 80, 16, Color.white);
        saveImportConfirm = ui.Button("ConfirmImport", saveTransferDialog.transform, "Replace current save", 113, -126, 215, 42, ConfirmSaveImport, ModCenterWidgets.Accent);
        saveImportConfirmLabel = saveImportConfirm.GetComponentInChildren<Text>();
        saveImportCancel = ui.Button("CancelImport", saveTransferDialog.transform, "Cancel", -113, -126, 215, 42, () =>
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
        }, ModCenterWidgets.Muted);
        saveTransferDialog.transform.Find("CancelImport/Label").GetComponent<Text>().color = Color.white;
        saveTransferDialog.SetActive(false);

        saveTransferMessageDialog = ui.Panel("SaveTransferMessage", sync, 0, 32, 475, 245, dialogColor).gameObject;
        saveTransferMessageTitle = ui.Text("Title", saveTransferMessageDialog.transform, "", 0, 84, 435, 40, 24, Color.white);
        saveTransferMessageScroll = ui.Scroll("MessageScroll", saveTransferMessageDialog.transform,
            0, 8, 435, 125, out saveTransferMessageContent);
        saveTransferMessageBody = ui.Text("Message", saveTransferMessageContent, "", 0, 0, 405, 125, 16, Color.white);
        var messageRect = saveTransferMessageBody.rectTransform;
        messageRect.anchorMin = new Vector2(.5f, 1f);
        messageRect.anchorMax = new Vector2(.5f, 1f);
        messageRect.pivot = new Vector2(.5f, 1f);
        saveTransferMessageBody.alignment = TextAnchor.UpperLeft;
        saveTransferCopyPath = ui.Button("CopyPath", saveTransferMessageDialog.transform, "Copy path", -113, -88, 215, 42,
            () => GUIUtility.systemCopyBuffer = exportedSavePath, ModCenterWidgets.Accent);
        ui.Button("Close", saveTransferMessageDialog.transform, "Close", 113, -88, 215, 42,
            () => saveTransferMessageDialog.SetActive(false), ModCenterWidgets.Muted);
        saveTransferMessageDialog.transform.Find("Close/Label").GetComponent<Text>().color = Color.white;
        saveTransferMessageDialog.SetActive(false);
#if UNITY_WEBGL && !UNITY_EDITOR
        browserSaveTransfer = FlatsBrowserSaveTransfer.Create(transform);
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
