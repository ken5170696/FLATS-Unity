using System;
using System.Collections.Generic;
using System.IO;

namespace Flats.Profiles
{
    // Keeps the original Character/Settings/Current types and legacy wire order.
    // Decoding constructs a complete candidate before a caller can publish it.
    public static class LegacyProfileCodec
    {
        public static ProfilePayload Encode(ProfileSnapshot snapshot)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            Character myCharacter = snapshot.Character;
            string text = "";
            for (int i = 0; i < myCharacter.sightList.Count; i++)
            {
                text = ((i != 0) ? (text + "," + myCharacter.sightList[i]) : myCharacter.sightList[i].ToString());
            }
            string value = myCharacter.id + "$" + myCharacter.name + "$" + myCharacter.comment + "$" + myCharacter.color + "$" + myCharacter.kill + "$" + myCharacter.death + "$" + myCharacter.survivalScore + "$" + myCharacter.assortmentScore + "$" + myCharacter.headshotScore + "$" + myCharacter.primaryWeapon + "$" + myCharacter.secondaryWeapon + "$" + myCharacter.attack + "$" + myCharacter.defense + "$" + text;
            // Preserve the original field order and integer formatting.
            Settings mySettings = snapshot.Settings;
            string value2 = mySettings.sound_bgm + "$" + mySettings.sound_all + "$" + mySettings.graphics_aa + "$" + mySettings.graphics_dof + "$" + mySettings.graphics_motionBlur + "$" + mySettings.graphics_edgeRendering + "$" + mySettings.graphics_saturationFilter + "$" + mySettings.control_sensitivity + "$" + mySettings.control_handedness + "$" + mySettings.control_yAxis + "$" + mySettings.control_autoAim + "$" + mySettings.control_tapFiring + "$" + mySettings.vr_resolution + "$" + mySettings.vr_eyeDistance + "$" + mySettings.vr_headRotation + "$" + mySettings.extra_batterySaver + "$" + mySettings.extra_notification;

            Current myCurrent = snapshot.Current;
            string value3 = myCurrent.survival_Score + "$" + myCurrent.survival_Phase + "$" + myCurrent.assortment_Score + "$" + myCurrent.assortment_Phase + "$" + myCurrent.headshot_Score + "$" + myCurrent.headshot_Chain;

            return new ProfilePayload(value,value2,value3,snapshot.LegacyCharacter,snapshot.LegacySettings,snapshot.LegacyCurrent);
        }
        public static ProfileSnapshot Decode(ProfilePayload payload)
        {
            if(payload==null)throw new ArgumentNullException(nameof(payload));
            string text = payload.Character;
            string[] array = Fields(text,14,"character");
            string text2 = array[array.Length - 1];
            string[] array2 = text2.Split(new string[1] { "," }, StringSplitOptions.None);
            List<int> list = new List<int>();
            for (int i = 0; i < array2.Length; i++)
            {
                list.Add(int.Parse(array2[i]));
            }
            Character character = new Character();
            character.id = array[0];
            character.name = array[1];
            character.comment = array[2];
            character.color = int.Parse(array[3]);
            character.kill = int.Parse(array[4]);
            character.death = int.Parse(array[5]);
            character.survivalScore = int.Parse(array[6]);
            character.assortmentScore = int.Parse(array[7]);
            character.headshotScore = int.Parse(array[8]);
            character.primaryWeapon = int.Parse(array[9]);
            character.secondaryWeapon = int.Parse(array[10]);
            character.attack = int.Parse(array[11]);
            character.defense = int.Parse(array[12]);
            character.sightList = list;
            string text3 = payload.Settings;
            string[] array3 = Fields(text3,17,"settings");
            Settings settings = new Settings();
            settings.sound_bgm = int.Parse(array3[0]);
            settings.sound_all = int.Parse(array3[1]);
            settings.graphics_aa = int.Parse(array3[2]);
            settings.graphics_dof = int.Parse(array3[3]);
            settings.graphics_motionBlur = int.Parse(array3[4]);
            settings.graphics_edgeRendering = int.Parse(array3[5]);
            settings.graphics_saturationFilter = int.Parse(array3[6]);
            settings.control_sensitivity = int.Parse(array3[7]);
            settings.control_handedness = int.Parse(array3[8]);
            settings.control_yAxis = int.Parse(array3[9]);
            settings.control_autoAim = int.Parse(array3[10]);
            settings.control_tapFiring = int.Parse(array3[11]);
            settings.vr_resolution = int.Parse(array3[12]);
            settings.vr_eyeDistance = int.Parse(array3[13]);
            settings.vr_headRotation = int.Parse(array3[14]);
            settings.extra_batterySaver = int.Parse(array3[15]);
            settings.extra_notification = int.Parse(array3[16]);
            string text4 = payload.Current;
            string[] array4 = Fields(text4,6,"progress");
            Current current = new Current();
            current.survival_Score = int.Parse(array4[0]);
            current.survival_Phase = int.Parse(array4[1]);
            current.assortment_Score = int.Parse(array4[2]);
            current.assortment_Phase = int.Parse(array4[3]);
            current.headshot_Score = int.Parse(array4[4]);
            current.headshot_Chain = int.Parse(array4[5]);
            return new ProfileSnapshot(character,settings,current,payload.LegacyCharacter,payload.LegacySettings,payload.LegacyCurrent);
        }
        static string[] Fields(string text,int count,string label)
        {
            if(text==null)throw new InvalidDataException("Missing "+label+" payload");
            var fields=text.Split(new[]{'$'},StringSplitOptions.None);
            if(fields.Length!=count)throw new InvalidDataException("Invalid "+label+" field count");
            return fields;
        }
    }
}
