using System;
using UnityEngine;
public class GunInfo : MonoBehaviour
{
	public static string[] gunName = new string[20];

	public static int[] limitAmmo = new int[20];

	public static int[] limitMaxAmmo = new int[20];

	public static int[] burstCount = new int[20];

	public static float[] damage = new float[20];

	public static float[] rpm = new float[20];

	public static float[] accuracy = new float[20];

	public static float[] reloadTime = new float[20];

	public static float[] headshotBonus = new float[20];

	public static int[] zoom = new int[20];

	public static bool[] oneShot = new bool[20];

	public static bool[] handgun = new bool[20];

	public static bool[] grenade = new bool[20];


	private void Start()
	{
		for (int index=0; index<Flats.Core.WeaponCatalog.Count; index++)
		{
			var definition=Flats.Core.WeaponCatalog.GetDefault(index);
			gunName[index]=definition.gunName;
			limitAmmo[index]=definition.limitAmmo;
			limitMaxAmmo[index]=definition.limitMaxAmmo;
			burstCount[index]=definition.burstCount;
			damage[index]=definition.damage;
			rpm[index]=definition.rpm;
			accuracy[index]=definition.accuracy;
			reloadTime[index]=definition.reloadTime;
			headshotBonus[index]=definition.headshotBonus;
			zoom[index]=definition.zoom;
			oneShot[index]=definition.oneShot;
			handgun[index]=definition.handgun;
			grenade[index]=definition.grenade;
		}
	}

	public GunInfo()
	{
	}




}
