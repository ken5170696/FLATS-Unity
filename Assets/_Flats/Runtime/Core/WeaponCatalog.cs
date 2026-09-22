namespace Flats.Core {
public sealed class WeaponDefinition {
 public readonly string gunName;
 public readonly int limitAmmo;
 public readonly int limitMaxAmmo;
 public readonly int burstCount;
 public readonly float damage;
 public readonly float rpm;
 public readonly float accuracy;
 public readonly float reloadTime;
 public readonly float headshotBonus;
 public readonly int zoom;
 public readonly bool oneShot;
 public readonly bool handgun;
 public readonly bool grenade;
 public WeaponDefinition(string gunName, int limitAmmo, int limitMaxAmmo, int burstCount, float damage, float rpm, float accuracy, float reloadTime, float headshotBonus, int zoom, bool oneShot, bool handgun, bool grenade) {
 this.gunName=gunName;
 this.limitAmmo=limitAmmo;
 this.limitMaxAmmo=limitMaxAmmo;
 this.burstCount=burstCount;
 this.damage=damage;
 this.rpm=rpm;
 this.accuracy=accuracy;
 this.reloadTime=reloadTime;
 this.headshotBonus=headshotBonus;
 this.zoom=zoom;
 this.oneShot=oneShot;
 this.handgun=handgun;
 this.grenade=grenade;
 }
}
public static class WeaponCatalog {
 private static readonly WeaponDefinition[] defaults = {
 new WeaponDefinition("SMG 1", 20, 300, 3, 120f, 680f, 100f, 0.4f, 2f, 2, false, false, false),
 new WeaponDefinition("SMG 2", 20, 300, 3, 100f, 900f, 80f, 0.4f, 2f, 2, false, false, false),
 new WeaponDefinition("SMG 3", 20, 300, 3, 130f, 700f, 90f, 0.6f, 2f, 2, false, false, false),
 new WeaponDefinition("SMG 4", 20, 300, 3, 100f, 800f, 90f, 0.3f, 2f, 2, false, false, false),
 new WeaponDefinition("Assault Rifle 1", 30, 500, 3, 150f, 550f, 100f, 1f, 1.5f, 3, false, false, false),
 new WeaponDefinition("Assault Rifle 2", 30, 500, 3, 160f, 650f, 70f, 0.7f, 1.5f, 3, false, false, false),
 new WeaponDefinition("Assault Rifle 3", 40, 500, 3, 170f, 580f, 80f, 1.2f, 1.5f, 3, false, false, false),
 new WeaponDefinition("Assault Rifle 4", 40, 500, 3, 180f, 520f, 90f, 1.2f, 1.5f, 3, false, false, false),
 new WeaponDefinition("Shotgun 1", 30, 200, 3, 200f, 100f, 60f, 0.7f, 1.2f, 2, true, false, false),
 new WeaponDefinition("Shotgun 2", 30, 200, 5, 150f, 50f, 40f, 1f, 1.2f, 2, true, false, false),
 new WeaponDefinition("Sniper Rifle 1", 20, 200, 1, 250f, 80f, 100f, 1.5f, 5f, 5, true, false, false),
 new WeaponDefinition("Sniper Rifle 2", 20, 200, 1, 300f, 60f, 100f, 1.8f, 5f, 5, true, false, false),
 new WeaponDefinition("Handgun 1", 12, 600, 1, 120f, 150f, 100f, 0f, 5f, 2, false, true, false),
 new WeaponDefinition("Handgun 2", 12, 600, 1, 150f, 100f, 100f, 0.2f, 5f, 2, false, true, false),
 new WeaponDefinition("Light Machine Gun", 100, 800, 5, 150f, 400f, 50f, 2.2f, 1.2f, 3, false, false, false),
 new WeaponDefinition("Grenade Launcher", 6, 50, 1, 300f, 50f, 100f, 2.2f, 1.2f, 2, true, false, true),
 };
 public static int Count { get { return defaults.Length; } }
 public static WeaponDefinition GetDefault(int index) { return defaults[index]; }
}
}
