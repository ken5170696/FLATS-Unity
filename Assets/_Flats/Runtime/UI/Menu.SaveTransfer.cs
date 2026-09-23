using System;
using UnityEngine;
using UnityEngine.UI;

public partial class Menu
{
    GameObject saveTransferDialog;
    Text saveTransferNotice, saveTransferPreview;
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
        ui.Button("ImportOldSave", sync, "Import old save", -125, -175, 215, 40, ShowSaveImport);
        ui.Button("ExportSave", sync, "Export save", 125, -175, 215, 40, ExportSave);
        saveTransferNotice = ui.Text("SaveTransferNotice", sync, "", 0, -209, 480, 28, 13);
        saveTransferNotice.alignment = TextAnchor.MiddleCenter;

        saveTransferDialog = ui.Panel("SaveTransferDialog", sync, 0, 0, 475, 260, ModCenterWidgets.Paper).gameObject;
        ui.Text("Title", saveTransferDialog.transform, "Import old save", 0, 84, 435, 40, 24);
        saveImportPath = ui.Input("SaveImportPath", saveTransferDialog.transform, "Absolute path to .dat or .json save", 0, 32, 435);
        saveImportPath.characterLimit = 4096;
        saveImportPath.gameObject.SetActive(false);
        saveTransferPreview = ui.Text("Preview", saveTransferDialog.transform, "", 0, -18, 435, 70, 16);
        saveImportConfirm = ui.Button("ConfirmImport", saveTransferDialog.transform, "Replace current save", 113, -96, 215, 42, ConfirmSaveImport, ModCenterWidgets.Accent);
        saveImportConfirmLabel = saveImportConfirm.GetComponentInChildren<Text>();
        ui.Button("CancelImport", saveTransferDialog.transform, "Cancel", -113, -96, 215, 42, () =>
        { pendingSaveImport = null; saveTransferDialog.SetActive(false); });
        saveTransferDialog.SetActive(false);
    }

    void ShowSaveImport()
    {
        if (gameState != "Main") { saveTransferNotice.text = "Return to the main menu before importing a save."; return; }
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
            else saveTransferNotice.text = "Import failed: " + e.Message;
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
            saveTransferNotice.text = "Exported to: " + FlatsSaveTransfer.Export();
        }
        catch (Exception e) { saveTransferNotice.text = "Export failed: " + e.Message; }
    }
}
