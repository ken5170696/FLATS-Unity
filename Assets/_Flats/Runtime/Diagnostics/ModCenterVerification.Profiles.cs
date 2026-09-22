using System;
using System.Linq;
using System.Threading.Tasks;
using Flats.Modules;
using Flats.UI;
using UnityEngine.UI;

public sealed partial class ModCenterVerification
{
    async Task ProfilesUI()
    {
        var owner=BuiltinModules.Instance;var profiles=owner.Profiles;
        if(phase=="ProfilesMigration")
        {
            Check("invalid legacy settings do not block library initialization",profiles!=null&&Service.Downloads!=null);
            Check("validated default migrated instead of corrupt legacy JSON",owner.ConfiguredCrosshair.style==CrosshairStyle.Cross&&owner.ConfiguredCrosshair.size==24);
            return;
        }
        if(phase=="ProfilesRestart")
        {
            Check("selected profile becomes running on restart",profiles.SelectedId==profiles.RunningId&&!profiles.RestartRequired&&profiles.Selected.name=="Clean");
            Check("selected settings load after restart",owner.Crosshair.Settings.style==CrosshairStyle.Ring&&owner.Crosshair.Settings.size==26);
            await MainClick(1,"Modules");await Click("Profiles");await Capture("profiles-after-restart");return;
        }
        var running=profiles.RunningId;var runtimeStyle=owner.Crosshair.Settings.style;var runtimeSize=owner.Crosshair.Settings.size;
        await MainClick(1,"Modules");await Click("Profiles");await Click("NewProfile");await SetInput("ProfileName","Clean");await Click("SaveProfileName");
        var clean=profiles.Snapshot().Single(p=>p.name=="Clean");Check("new profile starts without enabled mods",clean.modules.Length==0);
        await Click("DuplicateProfile-"+running);await SetInput("ProfileName","Practice");await Click("SaveProfileName");
        var practice=profiles.Snapshot().Single(p=>p.name=="Practice");
        await Click("RenameProfile-"+practice.id);await SetInput("ProfileName","Renamed practice");await Click("SaveProfileName");
        Check("rename persisted",profiles.Snapshot().Single(p=>p.id==practice.id).name=="Renamed practice");
        await Click("SelectProfile-"+clean.id);await Click("ConfirmAction");await Until(()=>profiles.SelectedId==clean.id);
        Check("selection preserves runtime identity",profiles.RunningId==running&&profiles.RestartRequired);
        await Capture("profiles-selected-restart");
        await Click("SelectProfile-"+clean.id);await Click("Details-"+CrosshairModule.Id);await Click("Secondary");
        await Click("StyleRing");await Click("CrosshairLarger");await Click("SaveDraft");
        Check("settings saved in selected profile",owner.ConfiguredCrosshair.style==CrosshairStyle.Ring&&owner.ConfiguredCrosshair.size==26);
        Check("running settings remain unchanged until restart",owner.Crosshair.Settings.style==runtimeStyle&&owner.Crosshair.Settings.size==runtimeSize);
        await Click("Profiles");await Click("DeleteProfile-"+practice.id);await Click("ConfirmAction");
        Check("inactive profile deleted",!profiles.Snapshot().Any(p=>p.id==practice.id));
        Check("running profile deletion blocked",!Button("DeleteProfile-"+running).interactable);
        Check("package library shared",Service.Installed.Length==0);
        await Capture("profiles-saved");
    }
}
