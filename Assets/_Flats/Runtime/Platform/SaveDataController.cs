using System;
using System.Collections.Generic;
using UnityEngine;
public class SaveDataController : MonoBehaviour
{
	private static readonly string EncryptKey = "Z5KJ7RmdhZ27EQensHmupJQ9ePr7Vm5W";

	private static readonly int EncryptPasswordCount = 16;

	private static readonly string PasswordChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

	private static readonly int PasswordCharsLength = PasswordChars.Length;

	private static string SavePath { get { return (FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserData.bytes"; } }

	public static void Save()
	{
		Character myCharacter = Menu.myCharacter;
		string text = "";
		for (int i = 0; i < myCharacter.sightList.Count; i++)
		{
			text = ((i != 0) ? (text + "," + myCharacter.sightList[i]) : myCharacter.sightList[i].ToString());
		}
		string value = myCharacter.id + "$" + myCharacter.name + "$" + myCharacter.comment + "$" + myCharacter.color + "$" + myCharacter.kill + "$" + myCharacter.death + "$" + myCharacter.survivalScore + "$" + myCharacter.assortmentScore + "$" + myCharacter.headshotScore + "$" + myCharacter.primaryWeapon + "$" + myCharacter.secondaryWeapon + "$" + myCharacter.attack + "$" + myCharacter.defense + "$" + text;
		// Commit all three legacy payloads together below.
		Settings mySettings = Menu.mySettings;
		string value2 = mySettings.sound_bgm + "$" + mySettings.sound_all + "$" + mySettings.graphics_aa + "$" + mySettings.graphics_dof + "$" + mySettings.graphics_motionBlur + "$" + mySettings.graphics_edgeRendering + "$" + mySettings.graphics_saturationFilter + "$" + mySettings.control_sensitivity + "$" + mySettings.control_handedness + "$" + mySettings.control_yAxis + "$" + mySettings.control_autoAim + "$" + mySettings.control_tapFiring + "$" + mySettings.vr_resolution + "$" + mySettings.vr_eyeDistance + "$" + mySettings.vr_headRotation + "$" + mySettings.extra_batterySaver + "$" + mySettings.extra_notification;
		
		Current myCurrent = Menu.myCurrent;
		string value3 = myCurrent.survival_Score + "$" + myCurrent.survival_Phase + "$" + myCurrent.assortment_Score + "$" + myCurrent.assortment_Phase + "$" + myCurrent.headshot_Score + "$" + myCurrent.headshot_Chain;
		
		FlatsLocalProfile.Commit(value, value2, value3);
	}

	public static void Load()
	{
		string text = FlatsPreferences.GetString("characterData");
		string[] array = text.Split(new string[1] { "$" }, StringSplitOptions.None);
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
		Menu.myCharacter = character;
		string text3 = FlatsPreferences.GetString("settingsData");
		string[] array3 = text3.Split(new string[1] { "$" }, StringSplitOptions.None);
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
		Menu.mySettings = settings;
		string text4 = FlatsPreferences.GetString("currentData");
		string[] array4 = text4.Split(new string[1] { "$" }, StringSplitOptions.None);
		Current current = new Current();
		current.survival_Score = int.Parse(array4[0]);
		current.survival_Phase = int.Parse(array4[1]);
		current.assortment_Score = int.Parse(array4[2]);
		current.assortment_Phase = int.Parse(array4[3]);
		current.headshot_Score = int.Parse(array4[4]);
		current.headshot_Chain = int.Parse(array4[5]);
		Menu.myCurrent = current;
		if (FlatsPreferences.GetString("character") != null)
		{
			Menu.oldCharacter = FlatsPreferences.GetString("character");
			Menu.oldCurrent = FlatsPreferences.GetString("current");
			Menu.oldSettings = FlatsPreferences.GetString("settings");
		}
	}

	public SaveDataController()
	{
	}




}
