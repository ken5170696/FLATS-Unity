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
    Button saveImportConfirm;
    Text saveImportConfirmLabel;
    FlatsLocalProfile.Profile pendingSaveImport;

    void InitializeSaveTransfer()
    {
        var sync = mt.Find("Character/Sync");
        if (sync == null) return;
        var original = sync.GetComponentInChildren<Text>(true);
        var ui = new ModCenterWidgets(original != null ? original.font : Resources.GetBuiltinResource<Font>("Arial.ttf"),
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
        var importButton = ui.Button("ImportOldSave", sync, "Import old save", -105, -168, 190, 34,
            ShowSaveImport, Color.white);
        var exportButton = ui.Button("ExportSave", sync, "Export save", 105, -168, 190, 34,
            ExportSave, Color.white);
        foreach (var button in new[] { importButton, exportButton })
        {
            var image = button.GetComponent<Image>();
            image.material = nativeControl.material;
            image.type = nativeControl.type;
            button.GetComponentInChildren<Text>().color = Color.white;
        }

        Color dialogColor = new Color(.31f, .24f, .29f);
        saveTransferDialog = ui.Panel("SaveTransferDialog", sync, 0, 32, 475, 260, dialogColor).gameObject;
        ui.Text("Title", saveTransferDialog.transform, "Import old save", 0, 84, 435, 40, 24, Color.white);
        saveImportPath = ui.Input("SaveImportPath", saveTransferDialog.transform, "Absolute path to .dat or .json save", 0, 32, 435);
        saveImportPath.characterLimit = 4096;
        saveImportPath.gameObject.SetActive(false);
        saveTransferPreview = ui.Text("Preview", saveTransferDialog.transform, "", 0, -18, 435, 70, 16, Color.white);
        saveImportConfirm = ui.Button("ConfirmImport", saveTransferDialog.transform, "Replace current save", 113, -96, 215, 42, ConfirmSaveImport, ModCenterWidgets.Accent);
        saveImportConfirmLabel = saveImportConfirm.GetComponentInChildren<Text>();
        ui.Button("CancelImport", saveTransferDialog.transform, "Cancel", -113, -96, 215, 42, () =>
        { pendingSaveImport = null; saveTransferDialog.SetActive(false); }, ModCenterWidgets.Muted);
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
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        pendingSaveImport = null;
        saveImportPath.gameObject.SetActive(true);
        saveImportPath.text = "";
        saveTransferPreview.text = "Paste the absolute path to an old playerprefs.dat or an exported FLATS JSON save.";
        saveImportConfirmLabel.text = "Load file";
        saveTransferDialog.SetActive(true);
        saveTransferDialog.transform.SetAsLastSibling();
        saveImportPath.ActivateInputField();
#else
        string path = LocalModFilePicker.ChooseSave();
        if (string.IsNullOrEmpty(path)) return;
        PreviewSaveImport(path);
#endif
    }

    void PreviewSaveImport(string path)
    {
        try
        {
            pendingSaveImport = FlatsSaveTransfer.Read(path);
            string name = pendingSaveImport.character.Split('$')[1];
            saveTransferPreview.text = "Character: " + name + "\n\nThe current save will be backed up. The imported save will load after returning to the main menu.";
            saveImportPath.gameObject.SetActive(false);
            saveImportConfirmLabel.text = "Replace current save";
            saveTransferDialog.SetActive(true);
            saveTransferDialog.transform.SetAsLastSibling();
        }
        catch (Exception e)
        {
            pendingSaveImport = null;
            if (saveTransferDialog.activeSelf) saveTransferPreview.text = "Import failed: " + e.Message;
            else ShowSaveTransferMessage("Import failed", e.Message);
        }
    }

    void ConfirmSaveImport()
    {
        if (pendingSaveImport == null)
        {
            PreviewSaveImport(saveImportPath.text.Trim());
            return;
        }
        try
        {
            FlatsSaveTransfer.Import(pendingSaveImport);
            pendingSaveImport = null;
            saveTransferDialog.SetActive(false);
            Application.LoadLevel(0);
        }
        catch (Exception e) { saveTransferPreview.text = "Import failed: " + e.Message; }
    }

    void ExportSave()
    {
        try
        {
            SaveDataController.Save();
            if (!FlatsLocalProfile.LastSaveSucceeded) throw new InvalidOperationException("Current save could not be written.");
            string path = FlatsSaveTransfer.Export();
            ShowSaveTransferMessage("Save exported", "Your save was written to:\n" + path, path);
        }
        catch (Exception e) { ShowSaveTransferMessage("Export failed", e.Message); }
    }
}
