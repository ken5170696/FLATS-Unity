using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExitGames.Client.Photon;
using InControl;
using Reign;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// Plus/Minus option rows (settings, character, room creation) and text inputs.
public partial class Menu
{
    // The value text of a Settings row. Pages keep their order; rows are found by the
    // names PlusMinus already relies on, so authored rows can be added to a page.
    Text SettingValue(int page, string row) => settingsScreen.GetChild(page).Find(row).GetChild(1).GetComponent<Text>();

		public void NameInput(string newName)
		{
			newName = newName.Replace("$", "");
			newName = newName.Replace("|", "");
			newName = newName.Replace("*", "");
			newName = newName.Replace("/", "");
			myCharacter.name = newName;
			Debug.Log("New name: " + myCharacter.name);
		}

		public void CommentInput(string newComment)
		{
			newComment = newComment.Replace("$", "");
			newComment = newComment.Replace("|", "");
			newComment = newComment.Replace("*", "");
			newComment = newComment.Replace("/", "");
			myCharacter.comment = newComment;
			Debug.Log("New comment: " + myCharacter.comment);
		}

		public void PlusMinus()
		{
			Transform parent = EventSystem.current.currentSelectedGameObject.transform.parent;
			int num = ((EventSystem.current.currentSelectedGameObject.name == "Plus") ? 1 : (-1));
			if (parent.name.StartsWith("Desktop")) { GetComponent<FlatsDesktopSettings>().Change(parent,num); return; }
			if (ChangePersonalRow(parent)) return;
			if (parent.name == "Rule")
			{
				rule += num;
				if (parent.parent.name == "RoomCreation")
				{
					if (rule < 1)
					{
						rule = ruleTitleText.Count - 1;
					}
					else if (rule > ruleTitleText.Count - 1)
					{
						rule = 1;
					}
				}
				else if (rule < 0)
				{
					rule = ruleTitleText.Count - 1;
				}
				else if (rule > ruleTitleText.Count - 1)
				{
					rule = 0;
				}
				parent.GetChild(1).GetComponent<Text>().text = ruleTitleText[rule];
				if (parent.parent.name == "RoomCreation")
				{
					objective = 1;
				}
				else
				{
					objective = 0;
				}
				parent.parent.GetChild(1).GetChild(1).GetComponent<Text>()
					.text = objectiveText[rule + "-" + objective];
				if (parent.parent.name == "RoomCreation")
				{
					playerCount = 4;
					parent.parent.GetChild(2).GetChild(1).GetComponent<Text>()
						.text = "4";
				}
				else
				{
					playerCount = 0;
					parent.parent.GetChild(2).GetChild(1).GetComponent<Text>()
						.text = "Any";
				}
			}
			else if (parent.name == "Objective")
			{
				if (rule < 1)
				{
					objective = 0;
				}
				else
				{
					objective += num;
					if (parent.parent.name == "RoomCreation")
					{
						if (objective < 1)
						{
							objective = 3;
						}
						else if (objective > 3)
						{
							objective = 1;
						}
					}
					else if (objective < 0)
					{
						objective = 3;
					}
					else if (objective > 3)
					{
						objective = 0;
					}
				}
				parent.GetChild(1).GetComponent<Text>().text = objectiveText[rule + "-" + objective];
			}
			else if (parent.name == "PlayerCount")
			{
				playerCount += num;
				if (playerCount == 1)
				{
					if (num < 0)
					{
						playerCount = 0;
					}
					else
					{
						playerCount = 2;
					}
				}
				if (parent.parent.name == "RoomCreation")
				{
					if (playerCount < 2)
					{
						playerCount = 8;
					}
					else if (playerCount > 8)
					{
						playerCount = 2;
					}
				}
				else if (playerCount < 0)
				{
					playerCount = 8;
				}
				else if (playerCount > 8)
				{
					playerCount = 0;
				}
				if (rule != 1 && rule != 6 && rule != 8)
				{
					if (num < 0)
					{
						if (playerCount == 3)
						{
							playerCount = 2;
						}
						else if (playerCount == 5)
						{
							playerCount = 4;
						}
						else if (playerCount == 7)
						{
							playerCount = 6;
						}
					}
					else if (playerCount == 3)
					{
						playerCount = 4;
					}
					else if (playerCount == 5)
					{
						playerCount = 6;
					}
					else if (playerCount == 7)
					{
						playerCount = 8;
					}
				}
				if (playerCount == 0)
				{
					parent.GetChild(1).GetComponent<Text>().text = "Any";
				}
				else
				{
					parent.GetChild(1).GetComponent<Text>().text = playerCount.ToString();
				}
			}
			else if (parent.name == "Region")
			{
                if (PhotonNetwork.inRoom)
                { ShowConfirm("Server Region", "Leave the current room before changing region.", null, "OK", null); return; }
				if (PhotonNetwork.PhotonServerSettings.PreferredRegion == CloudRegionCode.us)
				{
					if (num > 0)
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.eu;
					}
					else
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.asia;
					}
				}
				else if (PhotonNetwork.PhotonServerSettings.PreferredRegion == CloudRegionCode.eu)
				{
					if (num > 0)
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.asia;
					}
					else
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.us;
					}
				}
				else if (PhotonNetwork.PhotonServerSettings.PreferredRegion == CloudRegionCode.asia)
				{
					if (num > 0)
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.us;
					}
					else
					{
						PhotonNetwork.PhotonServerSettings.PreferredRegion = CloudRegionCode.eu;
					}
				}
				parent.GetChild(1).GetComponent<Text>().text = PhotonNetwork.PhotonServerSettings.PreferredRegion.ToString().ToUpper();
                if (PhotonNetwork.connected) PhotonNetwork.Disconnect();
			}
			else if (parent.name == "Attack")
			{
				if ((num < 0 && myCharacter.attack > 0) || (num > 0 && myCharacter.attack + myCharacter.defense < 10))
				{
					myCharacter.attack += num;
				}
				parent.GetChild(1).GetComponent<Text>().text = myCharacter.attack.ToString();
				parent.parent.GetChild(1).GetComponent<Text>().text = myCharacter.attack + myCharacter.defense + "/10";
			}
			else if (parent.name == "Defense")
			{
				if ((num < 0 && myCharacter.defense > 0) || (num > 0 && myCharacter.attack + myCharacter.defense < 10))
				{
					myCharacter.defense += num;
				}
				parent.GetChild(1).GetComponent<Text>().text = myCharacter.defense.ToString();
				parent.parent.GetChild(1).GetComponent<Text>().text = myCharacter.attack + myCharacter.defense + "/10";
			}
            else if (parent.name == "Volume-BGM")
            {
                SetVolume(true, mySettings.sound_bgm + num, false);
            }
            else if (parent.name == "Volume-All")
            {
                SetVolume(false, mySettings.sound_all + num, false);
            }
			else if (parent.name == "Anti-Aliasing")
			{
				if (mySettings.graphics_aa == 0)
				{
					mySettings.graphics_aa = 1;
				}
				else
				{
					mySettings.graphics_aa = 0;
				}
				FPSController.aa = IntToBool(mySettings.graphics_aa);
				parent.GetChild(1).GetComponent<Text>().text = aaText[mySettings.graphics_aa];
			}
			else if (parent.name == "DepthOfField")
			{
				if (mySettings.graphics_dof == 0)
				{
					mySettings.graphics_dof = 1;
				}
				else
				{
					mySettings.graphics_dof = 0;
				}
				FPSController.dof = IntToBool(mySettings.graphics_dof);
				parent.GetChild(1).GetComponent<Text>().text = dofText[mySettings.graphics_dof];
			}
			else if (parent.name == "MotionBlur")
			{
				if (mySettings.graphics_motionBlur == 0)
				{
					mySettings.graphics_motionBlur = 1;
				}
				else
				{
					mySettings.graphics_motionBlur = 0;
				}
				FPSController.motionBlur = IntToBool(mySettings.graphics_motionBlur);
				parent.GetChild(1).GetComponent<Text>().text = motionBlurText[mySettings.graphics_motionBlur];
			}
			else if (parent.name == "EdgeRendering")
			{
				if (mySettings.graphics_edgeRendering == 0)
				{
					mySettings.graphics_edgeRendering = 1;
				}
				else
				{
					mySettings.graphics_edgeRendering = 0;
				}
				FPSController.edgeRendering = IntToBool(mySettings.graphics_edgeRendering);
				parent.GetChild(1).GetComponent<Text>().text = edgeRenderingText[mySettings.graphics_edgeRendering];
			}
			else if (parent.name == "SaturationFilter")
			{
				if (mySettings.graphics_saturationFilter == 0)
				{
					mySettings.graphics_saturationFilter = 1;
				}
				else
				{
					mySettings.graphics_saturationFilter = 0;
				}
				FPSController.saturationFilter = IntToBool(mySettings.graphics_saturationFilter);
				parent.GetChild(1).GetComponent<Text>().text = saturationFilterText[mySettings.graphics_saturationFilter];
			}
			else if (parent.name == "CameraSensitivity")
			{
				mySettings.control_sensitivity += num;
				if (mySettings.control_sensitivity > 2)
				{
					mySettings.control_sensitivity = 0;
				}
				else if (mySettings.control_sensitivity < 0)
				{
					mySettings.control_sensitivity = 2;
				}
				FPSController.sensitivity = mySettings.control_sensitivity + 1;
				parent.GetChild(1).GetComponent<Text>().text = sensitivityText[mySettings.control_sensitivity];
			}
			else if (parent.name == "Handedness")
			{
				mySettings.control_handedness += num;
				if (mySettings.control_handedness > 1)
				{
					mySettings.control_handedness = 0;
				}
				else if (mySettings.control_handedness < 0)
				{
					mySettings.control_handedness = 1;
				}
				Transform child = mt.parent.GetChild(1);
				if (mySettings.control_handedness == 0)
				{
					stick.joystickArea = ETCJoystick.JoystickArea.Left;
					child.GetChild(1).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(2).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(3).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(4).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterRight;
					child.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(0f - Mathf.Abs(child.GetChild(5).rectTransform().anchoredPosition3D.x), child.GetChild(5).rectTransform().anchoredPosition3D.y, child.GetChild(5).rectTransform().anchoredPosition3D.z);
					child.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(0f - Mathf.Abs(child.GetChild(6).rectTransform().anchoredPosition3D.x), child.GetChild(6).rectTransform().anchoredPosition3D.y, child.GetChild(6).rectTransform().anchoredPosition3D.z);
				}
				else
				{
					stick.joystickArea = ETCJoystick.JoystickArea.Right;
					child.GetChild(1).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(2).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(3).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(4).GetComponent<ETCButton>().anchor = ETCBase.RectAnchor.CenterLeft;
					child.GetChild(5).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(child.GetChild(5).rectTransform().anchoredPosition3D.x), child.GetChild(5).rectTransform().anchoredPosition3D.y, child.GetChild(5).rectTransform().anchoredPosition3D.z);
					child.GetChild(6).rectTransform().anchoredPosition3D = new Vector3(Mathf.Abs(child.GetChild(6).rectTransform().anchoredPosition3D.x), child.GetChild(6).rectTransform().anchoredPosition3D.y, child.GetChild(6).rectTransform().anchoredPosition3D.z);
				}
				FPSController.handedness = mySettings.control_handedness;
				parent.GetChild(1).GetComponent<Text>().text = handednessText[mySettings.control_handedness];
			}
			else if (parent.name == "Y-Axis")
			{
				if (mySettings.control_yAxis == 0)
				{
					mySettings.control_yAxis = 1;
				}
				else
				{
					mySettings.control_yAxis = 0;
				}
				FPSController.invertY = IntToBool(mySettings.control_yAxis);
				parent.GetChild(1).GetComponent<Text>().text = yAxisText[mySettings.control_yAxis];
			}
			else if (parent.name == "AutoAim")
			{
				if (mySettings.control_autoAim == 0)
				{
					mySettings.control_autoAim = 1;
				}
				else
				{
					mySettings.control_autoAim = 0;
				}
				FPSController.autoAim = IntToBool(mySettings.control_autoAim);
				parent.GetChild(1).GetComponent<Text>().text = autoAimText[mySettings.control_autoAim];
			}
			else if (parent.name == "TapFiring")
			{
				if (mySettings.control_tapFiring == 0)
				{
					mySettings.control_tapFiring = 1;
				}
				else
				{
					mySettings.control_tapFiring = 0;
				}
				FPSController.tapFiring = IntToBool(mySettings.control_tapFiring);
				parent.GetChild(1).GetComponent<Text>().text = tapFiringText[mySettings.control_tapFiring];
			}
			else if (parent.name == "Resolution")
			{
				mySettings.vr_resolution += num;
				if (mySettings.vr_resolution > 2)
				{
					mySettings.vr_resolution = 0;
				}
				else if (mySettings.vr_resolution < 0)
				{
					mySettings.vr_resolution = 2;
				}
				if (mySettings.vr_resolution == 2)
				{
					if (VRmode && !(VRController.device == "cardboard") && !(VRController.device == "oculus"))
					{
					}
				}
				else if (mySettings.vr_resolution == 1)
				{
					if (VRmode && !(VRController.device == "cardboard") && !(VRController.device == "oculus"))
					{
					}
				}
				else if (VRmode && !(VRController.device == "cardboard"))
				{
					bool flag = VRController.device == "oculus";
				}
				parent.GetChild(1).GetComponent<Text>().text = resolutionText[mySettings.vr_resolution];
			}
			else if (parent.name == "EyeDistance")
			{
				mySettings.vr_eyeDistance += num;
				if (mySettings.vr_eyeDistance > 2)
				{
					mySettings.vr_eyeDistance = 0;
				}
				else if (mySettings.vr_eyeDistance < 0)
				{
					mySettings.vr_eyeDistance = 2;
				}
				string text = ((mySettings.vr_eyeDistance != 0) ? ("+" + (float)mySettings.vr_eyeDistance * 0.5f) : "Default");
				VRController.offset = (float)mySettings.vr_eyeDistance * 0.5f;
				parent.GetChild(1).GetComponent<Text>().text = text;
			}
			else if (parent.name == "HeadRotation")
			{
				if (mySettings.vr_headRotation == 0)
				{
					mySettings.vr_headRotation = 1;
				}
				else
				{
					mySettings.vr_headRotation = 0;
				}
				parent.GetChild(1).GetComponent<Text>().text = headRotationText[mySettings.vr_headRotation];
			}
			else if (parent.name == "BatterySaver")
			{
				if (mySettings.extra_batterySaver == 0)
				{
					mySettings.extra_batterySaver = 1;
				}
				else
				{
					mySettings.extra_batterySaver = 0;
				}
				Application.targetFrameRate = 60 - 30 * mySettings.extra_batterySaver;
				parent.GetChild(1).GetComponent<Text>().text = batteryText[mySettings.extra_batterySaver];
			}
			else if (parent.name == "Notification")
			{
				if (mySettings.extra_notification == 0)
				{
					mySettings.extra_notification = 1;
				}
				else
				{
					mySettings.extra_notification = 0;
				}
				parent.GetChild(1).GetComponent<Text>().text = notificationText[mySettings.extra_notification];
			}
            // These settings apply immediately; closing the player or browser
            // without navigating Back must not discard the accepted value.
            if (current == "Settings") SaveDataController.Save();
		}
}
