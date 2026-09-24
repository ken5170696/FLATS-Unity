using System;
using UnityEngine;
using UnityEngine.UI;
using Flats.Modules;
using Flats.UI;

public sealed partial class ModuleManagementPage
{
    [SerializeField, Tooltip("Page heading. Referenced directly so the Header group can be reorganized.")]
    Text pageTitle;
    [Header("Responsive canvas")]
    [SerializeField, Min(1), Tooltip("Design height in UI units. Layout follows the canvas aspect ratio, not desktop monitor pixels.")]
    float referenceHeight=720;
    [SerializeField] Vector2 minimumLayoutSize=new Vector2(960,680);
    [SerializeField] Vector2 edgePadding=new Vector2(30,24);
    [SerializeField, Min(960)] float maximumLayoutWidth=1440;
    [SerializeField, Min(960)] float splitViewWidth=1120;
    [Header("Repeated content prefabs")]
    [SerializeField] ModuleListRowView installedRowPrefab;
    [SerializeField] ModuleListRowView downloadRowPrefab;
    [SerializeField] ModuleCatalogueCardView catalogueCardPrefab;
    [SerializeField] ModuleProfileCardView profileCardPrefab;
    [SerializeField] ModuleEmptyStateView emptyStatePrefab;
    [SerializeField] RectTransform loadingCardPrefab;
    // The saved ModulesScreen hierarchy owns visual composition. This method
    // connects session behavior without rebuilding or replacing authored objects.
    void BindViewActions()
    {
        Listen(back, Close);
        Listen(explore, () => Switch("Explore"));
        Listen(installed, () => Switch("Installed"));
        Listen(downloads, () => Switch("Downloads"));
        search.onValueChanged.AddListener(_ => debounce=Time.unscaledTime+.35f);
        Listen(category, CycleCategory);
        Listen(filter, () => { if(tab=="Explore")compatible=!compatible;else CycleFilter();offset=0;Reload(); });
        Listen(sort, () => { sortValue=sortValue=="name"?(tab=="Installed"?"name-desc":"updated"):"name";offset=0;Reload(); });
        Listen(enable, Primary);
        Listen(secondary, UpdateSelected);
        Listen(remove, ReviewRemoval);
        Listen(previous, () => { offset=Math.Max(0,offset-20);savedScroll=1;restoreScroll=true;Reload(); });
        Listen(next, () => { offset+=20;savedScroll=1;restoreScroll=true;Reload(); });
        Listen(retryInitialization, RetryLocalModules);
        Listen(overviewTab, () => { detailSection="Overview";ShowDetail(); });
        Listen(versionsTab, () => { detailSection="Versions";ShowDetail(); });
        Listen(dependenciesTab, () => { detailSection="Dependencies";ShowDetail(); });
        var filters=new[]{"All","Enabled","Updates","Problems"};
        for(int i=0;i<statusTabs.Length;i++)
        {
            string value=filters[i];
            Listen(statusTabs[i], () => { localFilter=value;offset=0;Reload(); });
        }
        Listen(quickConfigure, OpenSettings);
        Listen(quickDetails, OpenDetail);
        Listen(importButton, ShowImport);
        Listen(profilesButton, () => Switch("Profiles"));
        Listen(newProfileButton, () => NameProfile("New profile",name=>Profiles.Create(name)));
        Listen(profileNamePanel.transform, "SaveProfileName", () =>
        {
            try { saveProfileName(profileNameInput.text);HideModal();RenderProfiles();notice.text="Profile saved."; }
            catch(Exception e) { notice.text=e.Message; }
        });
        Listen(profileNamePanel.transform, "CancelProfileName", HideModal);
        Listen(confirmPanel.transform, "ConfirmAction", () => { HideModal();var action=confirmation;confirmation=null;action?.Invoke(); });
        Listen(confirmPanel.transform, "CancelAction", () => { if(!busy)HideModal(); });
        Listen(crosshairPanel.transform, "LightPreview", () => previewBackdrop.color=new Color(.75f,.72f,.75f));
        Listen(crosshairPanel.transform, "DarkPreview", () => previewBackdrop.color=new Color(.25f,.23f,.26f));
        // Legacy crosshair controls are optional once the settings-driven form is authored.
        if(styles!=null)for(int i=0;i<styles.Length;i++)
        {
            var value=(CrosshairStyle)i;
            Listen(styles[i], () => Configure(value,draft.size));
        }
        if(sizeSlider!=null)sizeSlider.onValueChanged.AddListener(value=>Configure(draft.style,value));
        if(smaller!=null)Listen(smaller, () => Configure(draft.style,draft.size-2));
        if(larger!=null)Listen(larger, () => Configure(draft.style,draft.size+2));
        Listen(reset, () => Ask("Restore Crosshair defaults?\n\nThis changes only this mod's draft. Save changes to apply it. Other mods and game settings are not affected.",
            ()=>{if(Generic)settingsForm.ResetToDefaults();else Configure(CrosshairStyle.Cross,24);}));
        Listen(saveDraft, () => SaveDraft(true));
        Listen(cancelDraft, LeaveSettings);
        Listen(importPanel.transform,"ReviewImport",ReviewImport);
        Listen(importPanel.transform,"ChooseImport",()=>{var path=LocalModFilePicker.Choose();if(!string.IsNullOrEmpty(path))importPath.text=path;});
        Listen(importPanel.transform,"CancelImport",HideModal);
        Listen(webImportPanel.transform,"ReviewData",ReviewWebData);
        Listen(webImportPanel.transform,"CancelImport",HideModal);
    }

    void Listen(Button button, Action action)
    {
        button.onClick.AddListener(() => { menu.PlayMenuSound(menu.pressSE);action(); });
    }

    void Listen(Transform parent, string child, Action action)
    {
        Listen(parent.Find(child).GetComponent<Button>(), action);
    }
}
