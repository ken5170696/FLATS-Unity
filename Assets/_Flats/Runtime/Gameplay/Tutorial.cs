using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class Tutorial : MonoBehaviour
{
	public Text tutorialText;

	public Image tutorialImage;

	public GameObject enemy1;

	public GameObject enemy2;

	public GameObject block;

	public GameObject sniperRifle;

	public Transform blackCube;

	public Transform lookTarget;

	public Transform spawnPosition;

	public Sprite gamepad;

	public Sprite gamepad_Fire;

	public Sprite gamepad_Reload;

	public Sprite gamepad_Change;

	public Sprite gamepad_Grenade;

	public Sprite gamepad_Zoom;

	public Sprite gamepad_Jump;

	public Sprite touch_Fire;

	public Sprite touch_Reload;

	public Sprite touch_Grenade;

	public Sprite touch_Jump;

	public Sprite touch_Pick;

	public Sprite quit;

	public AudioClip changeSE;

	private Transform player;

	private Transform ct;

	private Animator anim;

	private int step;

	private Vector3 target;

	private Image low;

	private Image normal;

	private Image high;

	private Image enable;

	private Image disable;

	private int weaponIndex;

	private FPSController fc;

	private int currentAmmo;

	private Image reticle;

	private Text ammo;

	private bool mobileMouse;

	private void Update()
	{
		if (!mobileMouse && Input.GetJoystickNames().Length > 0 && !Input.mousePresent && Input.GetAxis("mouse x") != 0f)
		{
			mobileMouse = true;
		}
	}

	private IEnumerator Start()
	{
		GameObject p = (GameObject)UnityEngine.Object.Instantiate(Resources.Load("Flatman"), spawnPosition.position, spawnPosition.rotation);
		player = p.transform;
		anim = GetComponent<Animator>();
		ct = Camera.main.transform;
		target = lookTarget.position;
		fc = player.GetComponent<FPSController>();
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();
		fc.enableFire = false;
		yield return new WaitForSeconds(0.5f);
		while (true)
		{
			if (step == 0)
			{
				int t = 0;
				while (step == 0)
				{
					Quaternion rotY = Quaternion.LookRotation(target - player.position);
					rotY.x = 0f;
					rotY.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY, Time.deltaTime * 5f);
					t++;
					if (t == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				yield return new WaitForSeconds(1.5f);
				tutorialText.text = "Hi, welcome to the tutorial.";
				anim.Play("TextOn");
				yield return new WaitForSeconds(2.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					if (Menu.VRmode)
					{
						tutorialText.text = "First, turn your head\nand look around.";
					}
					else
					{
						tutorialText.text = "First, use right stick\nand look around.";
					}
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "First, use mouse \nand look around.";
				}
				else
				{
					tutorialText.text = "First, swipe screen\nand look around.";
				}
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(1.5f);
				step = 1;
				Quaternion camDelta = ct.parent.rotation;
				while (step == 1)
				{
					if (ct.parent.rotation != camDelta)
					{
						step = 2;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
			}
			else if (step == 2)
			{
				int t2 = 0;
				while (step == 2)
				{
					Quaternion rotY2 = Quaternion.LookRotation(target - player.position);
					rotY2.x = 0f;
					rotY2.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY2, Time.deltaTime * 5f);
					t2++;
					if (t2 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Walk to the black cube.\nUse left stick.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Walk to the black cube.\nUse W/A/S/D keys.";
				}
				else
				{
					tutorialText.text = "Walk to the black cube.\nUse stick on the screen.";
				}
				anim.Play("TextFadeIn");
				block.SetActive(true);
				yield return new WaitForSeconds(0.5f);
				blackCube.gameObject.SetActive(true);
				yield return new WaitForSeconds(0.5f);
				step = 3;
				while (step == 3)
				{
					if (Mathf.Abs(blackCube.position.x - player.position.x) < 10f && Mathf.Abs(blackCube.position.z - player.position.z) < 10f)
					{
						step = 4;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
			}
			else if (step == 4)
			{
				int t3 = 0;
				while (step == 4)
				{
					Quaternion rotY3 = Quaternion.LookRotation(target - player.position);
					rotY3.x = 0f;
					rotY3.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY3, Time.deltaTime * 5f);
					t3++;
					if (t3 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				if ((bool)block)
				{
					UnityEngine.Object.Destroy(blackCube.gameObject);
					UnityEngine.Object.Destroy(block);
				}
				tutorialText.text = "Good.";
				yield return new WaitForSeconds(2f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				tutorialText.text = "Now you can fire.";
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(2f);
				base.GetComponent<AudioSource>().PlayOneShot(changeSE);
				fc.enableFire = true;
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Pull right trigger to fire.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Left click to fire.";
				}
				else
				{
					tutorialText.text = "Press fire button.";
				}
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Fire;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item in tutorialImage.transform)
					{
						item.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Fire;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOn");
				}
				currentAmmo = player.GetComponent<FPSController>().primaryWeapon.GetComponent<Gun>().currentAmmo;
				step = 5;
			}
			else if (step == 5)
			{
				if (player.GetComponent<FPSController>().primaryWeapon.GetComponent<Gun>().currentAmmo < currentAmmo)
				{
					step = 6;
				}
			}
			else if (step == 6)
			{
				yield return new WaitForSeconds(1f);
				int t4 = 0;
				while (step == 6)
				{
					Quaternion rotY4 = Quaternion.LookRotation(target - player.position);
					rotY4.x = 0f;
					rotY4.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY4, Time.deltaTime * 5f);
					t4++;
					if (t4 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOff");
				}
				yield return new WaitForSeconds(0.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length > 0)
				{
					tutorialText.text = "Need to reload?\nUse this button.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Need to reload?\nPress R Key.";
				}
				else
				{
					tutorialText.text = "Need to reload?\nPress reload button.";
				}
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Reload;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item2 in tutorialImage.transform)
					{
						item2.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Reload;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOn");
				}
				step = 7;
			}
			else if (step == 7)
			{
				if (fc.primaryWeapon.gameObject.GetComponent<Gun>().currentAmmo == fc.primaryWeapon.gameObject.GetComponent<Gun>().limitAmmo)
				{
					step = 8;
				}
			}
			else if (step == 8)
			{
				int t5 = 0;
				while (step == 8)
				{
					Quaternion rotY5 = Quaternion.LookRotation(target - player.position);
					rotY5.x = 0f;
					rotY5.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY5, Time.deltaTime * 5f);
					t5++;
					if (t5 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Press this button\nto change weapons.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Press E key\nto change weapons.";
				}
				else
				{
					tutorialText.text = "Hold reload button\nto change weapons.";
				}
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Reload;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item3 in tutorialImage.transform)
					{
						item3.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Change;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				weaponIndex = fc.primaryWeaponIndex;
				step = 9;
			}
			else if (step == 9)
			{
				if (fc.primaryWeaponIndex != weaponIndex)
				{
					step = 10;
				}
			}
			else if (step == 10)
			{
				int t6 = 0;
				while (step == 10)
				{
					Quaternion rotY6 = Quaternion.LookRotation(target - player.position);
					rotY6.x = 0f;
					rotY6.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY6, Time.deltaTime * 5f);
					t6++;
					if (t6 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOff");
				}
				tutorialText.text = "You can set default weapons\nvia 'Character' in the menu.";
				yield return new WaitForSeconds(3.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				tutorialText.text = "Next, kill enemies.";
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(2.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Aim and shoot them\nor press this button\nto throw grenades.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Aim and shoot them\nor press G key\nto throw grenades.";
				}
				else
				{
					tutorialText.text = "Aim and shoot them\nor hold aim button\nto throw grenades.";
				}
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Grenade;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item4 in tutorialImage.transform)
					{
						item4.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Grenade;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOn");
				}
				enemy1 = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPosition.position, Quaternion.identity) as GameObject;
				enemy2 = UnityEngine.Object.Instantiate(Resources.Load("Flatman_Enemy"), spawnPosition.position, Quaternion.identity) as GameObject;
				step = 11;
			}
			else if (step == 11)
			{
				DamageReceiver.invincibility = true;
				if ((enemy1 == null || !enemy1.activeSelf) && (enemy2 == null || !enemy2.activeSelf))
				{
					step = 12;
				}
			}
			else if (step == 12)
			{
				int t7 = 0;
				while (step == 12)
				{
					Quaternion rotY7 = Quaternion.LookRotation(target - player.position);
					rotY7.x = 0f;
					rotY7.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY7, Time.deltaTime * 5f);
					t7++;
					if (t7 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				tutorialText.text = "Nice work!!";
				yield return new WaitForSeconds(1f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				tutorialText.text = "Headshot always makes\ngood results.";
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(2.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				tutorialText.text = "You can get ammo\nfrom dropped guns.";
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(2.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				t7 = 0;
				while (step == 12)
				{
					Quaternion rotY8 = Quaternion.LookRotation(target - player.position);
					rotY8.x = 0f;
					rotY8.z = 0f;
					player.rotation = Quaternion.Slerp(player.rotation, rotY8, Time.deltaTime * 5f);
					t7++;
					if (t7 == 30)
					{
						FPSController.enableCamRotate = true;
						break;
					}
					yield return new WaitForSeconds(0f);
				}
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Pick the sniper rifle on the floor.\nHold this button to pick up.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Pick the sniper rifle on the floor.\nPress Q key to pick up.";
				}
				else
				{
					tutorialText.text = "Pick the sniper rifle on the floor.\nLong tap to pick up.";
				}
				anim.Play("TextFadeIn");
				DroppedGun[] enemyguns = UnityEngine.Object.FindObjectsOfType<DroppedGun>();
				DroppedGun[] array = enemyguns;
				foreach (DroppedGun droppedGun in array)
				{
					UnityEngine.Object.Destroy(droppedGun.gameObject);
				}
				yield return new WaitForEndOfFrame();
				sniperRifle.SetActive(true);
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Pick;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item5 in tutorialImage.transform)
					{
						item5.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Change;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOn");
				}
				step = 13;
			}
			else if (step == 13)
			{
				while (!(sniperRifle == null))
				{
					yield return new WaitForSeconds(0f);
				}
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Hold left trigger\nfor zooming.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Right click\nfor zooming.";
				}
				else
				{
					tutorialText.text = "Press zoom button\nfor zooming.";
				}
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Grenade;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item6 in tutorialImage.transform)
					{
						item6.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Zoom;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				step = 14;
			}
			else if (step == 14)
			{
				while (!(Camera.main.transform.parent.name != "Camera"))
				{
					yield return new WaitForSeconds(0f);
				}
				step = 15;
			}
			else if (step == 15)
			{
				while (Camera.main.fieldOfView != 60f)
				{
					yield return new WaitForSeconds(0f);
				}
				step = 16;
			}
			else if (step == 16)
			{
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Press this button to jump.\nAnd hold it to sprint.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Press space key to jump.\nAnd hold shift to sprint.";
				}
				else
				{
					tutorialText.text = "Press jump button to jump.\nAnd hold it to sprint.";
				}
				if (Input.GetJoystickNames().Length == 0 && !Input.mousePresent)
				{
					tutorialImage.sprite = touch_Jump;
				}
				else if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialImage.sprite = gamepad;
					foreach (Transform item7 in tutorialImage.transform)
					{
						item7.gameObject.SetActive(false);
					}
					tutorialImage.transform.GetChild(0).GetComponent<Image>().sprite = gamepad_Jump;
					tutorialImage.transform.GetChild(0).gameObject.SetActive(true);
				}
				yield return new WaitForSeconds(8.5f);
				if ((Input.GetJoystickNames().Length > 0 || !Input.mousePresent) && !mobileMouse)
				{
					anim.Play("ImageOff");
				}
				yield return new WaitForSeconds(0.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				tutorialText.text = "OK. You've finished tutorial.";
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(3.5f);
				anim.Play("TextFadeOut");
				yield return new WaitForSeconds(0.5f);
				if (Input.GetJoystickNames().Length > 0 && !mobileMouse)
				{
					tutorialText.text = "Press start or select button.\nSelect reset button\nand exit the tutorial.";
				}
				else if (Input.mousePresent || mobileMouse)
				{
					tutorialText.text = "Press escape key.\nSelect reset button\nand exit the tutorial.";
				}
				else
				{
					tutorialText.text = "Press upper-left button.\nSelect reset button\nand exit the tutorial.";
				}
				anim.Play("TextFadeIn");
				yield return new WaitForSeconds(0.5f);
				tutorialImage.transform.GetChild(0).gameObject.SetActive(false);
				tutorialImage.sprite = quit;
				anim.Play("ImageOn");
				step = 17;
			}
			yield return new WaitForSeconds(0f);
		}
	}

	public Tutorial()
	{
	}




}
