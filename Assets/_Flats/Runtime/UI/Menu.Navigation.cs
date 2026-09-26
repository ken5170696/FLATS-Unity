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

// Menu page navigation, transitions and pointer input.
public partial class Menu
{
	public void OnDrag(int btn)
	{
		string text = "";
		switch (btn)
		{
		case 1:
			text = "Fire Button";
			break;
		case 2:
			text = "Reload Button";
			break;
		case 3:
			text = "Action Button";
			break;
		case 4:
			text = "Grenade&Aim Button";
			break;
		}
		RectTransform rectTransform = currentDetail.transform.GetChild(1).GetChild(1).GetChild(btn)
			.rectTransform();
		rectTransform.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
		currentDetail.transform.GetChild(1).GetChild(0).GetChild(2)
			.GetComponent<Text>()
			.text = text + "  X:" + rectTransform.anchoredPosition.x.ToString("F0") + " Y:" + rectTransform.anchoredPosition.y.ToString("F0");
		picking += 1f;
		Debug.Log("Picking..." + UnityEngine.Random.Range(0, 10));
	}

	public void OnClickedUp(int btn)
	{
		if (picking < 1f)
		{
			RectTransform rectTransform = currentDetail.transform.GetChild(1).GetChild(1).GetChild(btn)
				.rectTransform();
			if (rectTransform.localScale.x == 1f)
			{
				rectTransform.localScale = new Vector3(1.5f, 1.5f, rectTransform.localScale.z);
			}
			else if (rectTransform.localScale.x == 1.5f)
			{
				rectTransform.localScale = new Vector3(2f, 2f, rectTransform.localScale.z);
			}
			else
			{
				rectTransform.localScale = new Vector3(1f, 1f, rectTransform.localScale.z);
			}
		}
		else
		{
			Debug.Log("Just dragged...");
		}
		picking = 0f;
	}

	private void BackToMainMenu()
	{
		if (gameState == "Multiplayer")
		{
			bt[0].text = "Resume";
			bt[1].text = "Singleplayer";
		}
		else if (gameState == "Singleplayer")
		{
			bt[0].text = "Multiplayer";
			bt[1].text = "Resume";
		}
		else
		{
			bt[0].text = "Multiplayer";
			bt[1].text = "Singleplayer";
		}
		if (waitBackground)
		{
			bt[0].text = "Matchmaking...";
		}
		bt[2].text = "Character";
		bt[3].text = "Settings";
		bt[4].text = "Leaderboard";
		bt[5].text = "Information";
		buttons[0].sprite = images[0];
		buttons[1].sprite = images[1];
		buttons[2].sprite = images[2];
		buttons[3].sprite = images[3];
		buttons[4].sprite = images[4];
		buttons[5].sprite = images[5];
		anim.SetBool("Fade", false);
		current = "Main";
	}

	public void OpenMenu()
	{
        float nextTimeScale;
        if (!pauseNavigation.TryOpen(current, gameState, Time.timeScale, out nextTimeScale)) return;
		anim.SetTrigger("OpenMenu");
		current = "Main";
        ApplyListenerVolume();
		StartCoroutine("BackgroundColor", "OpenMenu");
		mt.parent.GetChild(1).GetComponent<Canvas>().enabled = false;
		mt.parent.GetChild(2).GetComponent<Canvas>().enabled = false;
		if (!VRmode)
		{
			FPSController.enableCamRotate = false;
		}
		if (gameState != "Multiplayer")
		{
			if (Singleplayer.rule == 1 && Singleplayer.currentAssortmentRule == 4)
			{
				GameObject grabbedObject = GameObject.Find("BlueTeamBase").GetComponent<TeamBase>().grabbedObject;
				if (grabbedObject != null && grabbedObject != null)
				{
					grabbedObject.transform.GetChild(1).gameObject.SetActive(false);
					grabbedObject.transform.GetChild(2).gameObject.SetActive(false);
				}
			}
            savedTimeScale = pauseNavigation.SavedTimeScale;
            Time.timeScale = nextTimeScale;
		}
		if ((Application.isMobilePlatform || !Input.mousePresent) && Input.GetJoystickNames().Length == 0)
		{
			EasyTouch.SetEnabled(false);
			ETCInput.SetControlActivated("Joystick", false);
			ETCInput.ResetAxis("Horizontal");
			ETCInput.ResetAxis("Vertical");
		}
		// Pause starts on Resume (the Singleplayer page shows it on the second tile), so A
		// continues the game instead of leaving for another mode.
		Selectable component = buttons[gameState == "Singleplayer" ? 1 : 0].transform.parent.GetComponent<Selectable>();
		lastMainTile = component.gameObject;
		if (Input.GetJoystickNames().Length > 0)
		{
			component.Select();
		}
		if (!Application.isMobilePlatform && Input.mousePresent)
		{
			Screen.lockCursor = false;
			UnityEngine.Cursor.visible = true;
		}
		if (!VRmode)
		{
			return;
		}
		if (Camera.main.gameObject != null)
		{
			mt.position = Camera.main.transform.position + Camera.main.transform.forward * 2.1f;
			mt.eulerAngles = new Vector3(Camera.main.transform.eulerAngles.x, Camera.main.transform.eulerAngles.y, 0f);
		}
		if ((bool)Camera.main.transform.GetChild(0).GetComponent<Camera>())
		{
			GameObject gameObject = Camera.main.transform.root.gameObject;
			if (gameObject.tag != "Player")
			{
				gameObject = Camera.main.transform.Find("Player").gameObject;
			}
			gameObject.GetComponent<IKController>().enabled = false;
			Camera.main.transform.GetChild(0).gameObject.SetActive(false);
		}
	}

	public void CloseMenu()
	{
        float nextTimeScale;
        if (!pauseNavigation.TryClose(current, gameState, Time.timeScale, savedTimeScale, out nextTimeScale)) return;
		anim.SetTrigger("CloseMenu");
		anim.SetBool("Fade", false);
		current = "Playing";
        ApplyListenerVolume();
		StartCoroutine("BackgroundColor", "CloseMenu");
        Time.timeScale = nextTimeScale;
		FPSController.enableCamRotate = true;
		EventSystem.current.SetSelectedGameObject(null);
		mt.parent.GetChild(1).GetComponent<Canvas>().enabled = true;
		mt.parent.GetChild(2).GetComponent<Canvas>().enabled = true;
		if (Singleplayer.rule == 1 && Singleplayer.currentAssortmentRule == 4)
		{
			GameObject grabbedObject = GameObject.Find("BlueTeamBase").GetComponent<TeamBase>().grabbedObject;
			if (grabbedObject != null)
			{
				grabbedObject.transform.GetChild(1).gameObject.SetActive(true);
				grabbedObject.transform.GetChild(2).gameObject.SetActive(true);
			}
		}
		if (!Application.isMobilePlatform && Input.mousePresent)
		{
			Screen.lockCursor = true;
			UnityEngine.Cursor.visible = false;
		}
		if ((Application.isMobilePlatform || !Input.mousePresent) && Input.GetJoystickNames().Length == 0)
		{
			EasyTouch.SetEnabled(true);
			ETCInput.SetControlActivated("Joystick", true);
		}
		ETCInput.ResetAxis("Vertical");
		ETCInput.ResetAxis("Horizontal");
		if (VRmode)
		{
			GameObject gameObject = Camera.main.transform.root.gameObject;
			if (gameObject.tag != "Player")
			{
				gameObject = Camera.main.transform.Find("Player").gameObject;
			}
			gameObject.GetComponent<IKController>().enabled = true;
			Camera.main.transform.GetChild(0).gameObject.SetActive(true);
			Camera.main.BroadcastMessage("UpdateStereoValues", SendMessageOptions.DontRequireReceiver);
		}
	}

	public void Fade(int button)
	{
        if (saveTransferMessageDialog != null && saveTransferMessageDialog.activeSelf)
        { if (button == -1) saveTransferMessageClose.onClick.Invoke(); return; }
        if (saveTransferDialog != null && saveTransferDialog.activeSelf)
        { if (button == -1) saveImportCancel.onClick.Invoke(); return; }
        if (HandleControlNavigation(button)) return;
        if (localMatchPanel != null && localMatchPanel.activeSelf)
        { if (button == -1) CloseLocalMatch(); return; }
        if (current == "Multiplayer" && button == 2 && !fliping && !multiplayerConnecting)
        { OpenLocalMatch(); return; }
        if (multiplayerConnecting)
        {
            if (button == -1)
            {
                ++multiplayerOperation;
                multiplayerFailure = "Connection cancelled";
                multiplayerReady = false;
                PhotonNetwork.Disconnect();
                pleaseWait.SetActive(false);
                fliping = false;
                anim.SetBool("Fade", false);
            }
            return;
        }
        if (HandleModNavigation(button)) return;
        if (button == -1 && current == "Multiplayer" && anim.GetBool("RoomCreation"))
        {
            SetRoomCreationVisible(false); anim.SetBool("Detail", true);
            anim.Play("Detail Fade In", 0, 0f);
            if (currentDetail != null) currentDetail.SetActive(true);
            backButton.SetActive(true); fliping = false; return;
        }
        Debug.Log("FLATS_MENU_ACTION current=" + current + " button=" + button);
        if (current == "Main" && button == 90 && Array.IndexOf(Environment.GetCommandLineArgs(), "-flats-bot-sandbox") >= 0)
        { rule = 1; objective = 1; botCount = 3; current = "OfflineMatch"; backButton.SetActive(true); RefreshOfflineMatch(); return; }
        if (current == "OfflineMatch")
        {
            if (!fliping) StartCoroutine(OfflineMatchMenu(button));
            return;
        }

		if (button <= 5 && button >= -1 && current != "Playing" && (current == "Main" || current == "Multiplayer" || current == "Singleplayer" || current == "Character" || current == "Settings") && current != "Map" && (!(current == "Singleplayer") || button != 5) && (!(gameState == "Main") || !(current == "Main") || button != -1))
		{
			anim.SetBool("Fade", true);
		}
		if (button != -1 || current != "Playing" || canOpen)
		{
			StartCoroutine("MenuController", button);
		}
		if (button == -1)
		{
			PlayMenuSound(cancelSE);
		}
		else
		{
			PlayMenuSound(pressSE);
		}
	}

	public IEnumerator MenuController(int button)
	{
        if (current == "Multiplayer" && (button == 3 || button == 10 || button == 11 || button == 15))
        {
            if (button == 11 && string.IsNullOrWhiteSpace(invitationRoomName.text))
            { ShowConfirm("Invitation Match", "Enter a room name.", null, "OK", null); yield break; }
            yield return StartCoroutine(EnsureMultiplayerConnection());
            if (!multiplayerReady) { fliping = false; anim.SetBool("Fade", false); yield break; }
            pendingRoomDeadline = Time.realtimeSinceStartup + 25f;
        }

		if (current != "Playing")
		{
			fliping = true;
		}
		if (current == "Playing")
		{
			if (button == -1)
			{
				OpenMenu();
			}
		}
		else if (current == "Main")
		{
			switch (button)
			{
			case -1:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (gameState != "Main")
				{
					CloseMenu();
				}
				else
				{
						ShowConfirm("Quit Application", "Quit Flats.", Quit, "Quit", "Cancel");
				}
				break;
			case 0:
			{
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (gameState == "Multiplayer")
				{
					CloseMenu();
					break;
				}
				if (waitBackground)
				{
					anim.SetBool("Matching", true);
					backButton.SetActive(true);
					anim.SetTrigger("SkipToMatching");
					current = "Matching";
					break;
				}
				bt[0].text = "Open Match";
				bt[1].text = "Invitation Match";
				bt[2].text = "Local Match";
				bt[3].text = "Chat Room";
				bt[4].text = "Server Region";
				bt[5].text = "Online Version: " + version.Substring(0, 3);
				buttons[0].sprite = images[36];
				buttons[1].sprite = images[37];
				buttons[2].sprite = images[38];
				buttons[3].sprite = images[39];
				buttons[4].sprite = images[40];
				buttons[5].sprite = images[41];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Multiplayer";
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.filterMode = FilterMode.Bilinear;
				byte[] array = FlatsUserIcon.Read(defaultIcon);
				texture2D.LoadImage(array);
				ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
				hashtable["K"] = myCharacter.kill;
				hashtable["D"] = myCharacter.death;
				hashtable["TC"] = myCharacter.color;
				hashtable["C"] = myCharacter.comment;
				hashtable["I"] = array;
                PublishRoomModules(hashtable);
				PhotonNetwork.SetPlayerCustomProperties(hashtable);
				PhotonNetwork.player.NickName = myCharacter.name;
				break;
			}
			case 1:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (gameState == "Singleplayer")
				{
					CloseMenu();
					break;
				}
				bt[0].text = "Survival";
				bt[1].text = "Assortment";
				bt[2].text = "Headshot Challenge";
				bt[3].text = "Training";
				bt[4].text = "Tutorial";
				bt[5].text = "Stage Select";
				buttons[0].sprite = images[18];
				buttons[1].sprite = images[19];
				buttons[2].sprite = images[20];
				buttons[3].sprite = images[21];
				buttons[4].sprite = images[22];
				buttons[5].sprite = images[23];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Singleplayer";
				break;
			case 2:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				bt[0].text = "Profile";
				bt[1].text = "Color";
				bt[2].text = "Score";
				bt[3].text = "Weapons";
				bt[4].text = "Stats";
				bt[5].text = "Sync";
				buttons[0].sprite = images[24];
				buttons[1].sprite = images[25];
				buttons[2].sprite = images[26];
				buttons[3].sprite = images[27];
				buttons[4].sprite = images[28];
				buttons[5].sprite = images[29];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Character";
				break;
			case 3:
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				bt[0].text = "Sound";
				bt[1].text = "Graphics";
				bt[2].text = "Control";
				bt[3].text = VRmode ? "VR Image" : "Display";
				bt[4].text = "Button Mapping";
				bt[5].text = "Extra Settings";
				buttons[0].sprite = images[30];
				buttons[1].sprite = images[31];
				buttons[2].sprite = images[32];
				buttons[3].sprite = images[33];
				buttons[4].sprite = images[34];
				buttons[5].sprite = images[35];
				backButton.SetActive(true);
				EventSystem.current.SetSelectedGameObject(null);
				anim.SetBool("Fade", false);
				current = "Settings";
				break;
			case 4:
				leaderboardLoading.SetActive(true);
				StartCoroutine("Leaderboard", false);
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				currentDetail = leaderboardScreen.GetChild(0).gameObject;
				currentDetail.SetActive(true);
				backButton.SetActive(true);
				anim.SetBool("Detail", true);
				current = "Leaderboard";
				break;
			case 5:
				StartCoroutine("Information");
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				currentDetail = informationScreen.GetChild(0).gameObject;
				currentDetail.SetActive(true);
				backButton.SetActive(true);
				anim.SetBool("Detail", true);
				current = "Information";
				break;
			case -2:
				if (VRController.device == "cardboard")
				{
					ShowConfirm("Cardboard mode", "Use gamepads to play VR mode.", EnableVR, "Enable", "Disable");
				}
				break;
			case -3:
				ShowConfirm("Reset", "Exit from current game and reboot.", Reset, "Restart", "Cancel");
				break;
			case -4:
				ShowConfirm("Quit Application", "Quit Flats.", Quit, "Quit", "Cancel");
				break;
			}
			if (button >= 0)
			{
				page = button + 3;
			}
		}
		else if (current == "Multiplayer" || current == "Singleplayer" || current == "Character" || current == "Settings" || current == "Leaderboard" || current == "Information")
		{
			switch (button)
			{
			case -1:
				if (currentDetail == null && current != "Leaderboard" && current != "Information")
				{
					backButton.SetActive(false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					EventSystem.current.SetSelectedGameObject(null);
					bool returnToPlay = gameState == "Main" && (current == "Singleplayer" || current == "Multiplayer");
					BackToMainMenu();
                    if (returnToPlay) { current="Play"; backButton.SetActive(true); RefreshPlayTiles(); EventSystem.current.SetSelectedGameObject(buttons[0].transform.parent.gameObject); }
					break;
				}
				if (current == "Multiplayer")
				{
					if (currentDetail.name == "ChatRoom")
					{
						if (PhotonNetwork.inRoom)
						{
							base.gameObject.GetPhotonView().RPC("Chat", PhotonTargets.MasterClient, PhotonNetwork.player.NickName + " left chat.");
						}
						foreach (Transform item in currentDetail.transform.GetChild(4))
						{
							UnityEngine.Object.Destroy(item.gameObject);
						}
						currentDetail.transform.GetChild(2).GetComponent<InputField>().text = "";
						currentDetail.transform.GetChild(3).GetComponent<Button>().interactable = false;
						currentDetail.transform.GetChild(5).gameObject.SetActive(true);
						PhotonNetwork.LeaveRoom();
					}
				}
				else if (current == "Character")
				{
					SaveDataController.Save();
					if (FlatsLocalProfile.LastSaveSucceeded) MonoBehaviour.print("Character data has been saved.");
				}
				else if (current == "Settings")
				{
					// Save whenever the touch layout editor was open, even if a controller connected meanwhile.
					if (currentDetail.name == "ButtonMapping" && currentDetail.transform.GetChild(1).gameObject.activeSelf)
					{
						Vector2[] array2 = new Vector2[4];
						float[] array3 = new float[4];
						for (int i = 1; i < 5; i++)
						{
							array2[i - 1] = new Vector2(Mathf.Abs(currentDetail.transform.GetChild(1).GetChild(1).GetChild(i)
								.rectTransform()
								.anchoredPosition.x) - mt.parent.GetChild(1).GetChild(i).rectTransform()
								.sizeDelta.x / 2f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(i)
								.rectTransform()
								.anchoredPosition.y);
							mt.parent.GetChild(1).GetChild(i).GetComponent<ETCButton>()
								.anchorOffet = array2[i - 1];
							Vector3 localScale = currentDetail.transform.GetChild(1).GetChild(1).GetChild(i)
								.rectTransform()
								.localScale;
							array3[i - 1] = localScale.x;
							mt.parent.GetChild(1).GetChild(i).rectTransform()
								.localScale = new Vector3(localScale.x, localScale.y, localScale.z);
						}
						FlatsPreferences.SetString("touchmapping", FormatTouchMapping(new[] { array2[0].x, array2[0].y, array2[1].x, array2[1].y, array2[2].x, array2[2].y, array2[3].x, array2[3].y, array3[0], array3[1], array3[2], array3[3] }));
						FlatsPreferences.Save();
						Debug.Log("Touch mapping has been saved.");
					}
					SaveDataController.Save();
					changedSettings = true;
					if (FlatsLocalProfile.LastSaveSucceeded) MonoBehaviour.print("Settings data has been saved.");
				}
				else if (current == "Leaderboard" || current == "Information")
				{
					current = "Main";
					backButton.SetActive(false);
					EventSystem.current.SetSelectedGameObject(null);
				}
				uploadButton.SetActive(false);
				leaderboardLoading.SetActive(false);
				anim.SetBool("Detail", false);
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				if (currentDetail != null)
				{
					currentDetail.SetActive(false);
				}
				currentDetail = null;
				anim.SetBool("Fade", false);
				break;
			case 0:
			case 1:
			case 2:
			case 3:
			case 4:
			case 5:
			{
				if (current == "Multiplayer")
				{
					if (button == 0)
					{
						rule = 0;
						objective = 0;
						playerCount = 0;
						botCount = 0;
						multiplayerScreen.GetChild(0).GetChild(0)
							.GetChild(1)
							.GetComponent<Text>()
							.text = ruleTitleText[rule];
						multiplayerScreen.GetChild(0).GetChild(1)
							.GetChild(1)
							.GetComponent<Text>()
							.text = objectiveText[rule + "-" + objective];
						multiplayerScreen.GetChild(0).GetChild(2)
							.GetChild(1)
							.GetComponent<Text>()
							.text = "Any";
					}
					if (button == 1)
					{
						rule = 0;
						objective = 0;
						playerCount = 0;
						botCount = 0;
					}
					if (button == 2)
					{
						OpenLocalMatch();
					}
					if (button == 3)
					{
						foreach (Transform item2 in chat.GetChild(4))
						{
							UnityEngine.Object.Destroy(item2.gameObject);
						}
						if (!PhotonNetwork.connected)
						{
							Connect();
						}
						if (PhotonNetwork.inRoom)
						{
							while (PhotonNetwork.inRoom)
							{
								yield return new WaitForSeconds(0f);
							}
						}
						while (!PhotonNetwork.connectedAndReady)
						{
							yield return new WaitForSeconds(0f);
						}
						ExitGames.Client.Photon.Hashtable customProps = new ExitGames.Client.Photon.Hashtable();
						customProps["R"] = -1;
						PhotonNetwork.JoinOrCreateRoom("ChatRoom", new RoomOptions
						{
							IsVisible = false,
							CustomRoomProperties = customProps,
							CustomRoomPropertiesForLobby = new string[1] { "R" }
						}, null);
					}
					int num2 = button;
				}
				if (current != "Singleplayer")
				{
					if (current == "Multiplayer" && button == 2)
					{
						anim.SetBool("Detail", false);
						yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
						backButton.SetActive(true);
						anim.SetBool("Fade", false);
					}
					else
					{
						yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
						currentDetail = DetailScreenFor(page).GetChild(button).gameObject;
						currentDetail.SetActive(true);
						RefreshRegionLabel();
						backButton.SetActive(true);
						anim.SetBool("Detail", true);
						FocusDetailForController();
					}
				}
				if (!(current == "Settings") || button != 4)
				{
					break;
				}
				if ((!Application.isMobilePlatform && Input.mousePresent) || Input.GetJoystickNames().Length > 0)
				{
					currentDetail.GetComponent<Image>().enabled = true;
					currentDetail.transform.GetChild(0).gameObject.SetActive(true);
					break;
				}
				currentDetail.GetComponent<Image>().enabled = false;
				currentDetail.transform.GetChild(1).gameObject.SetActive(true);
				currentDetail.transform.GetChild(1).GetChild(0).GetChild(2)
					.GetComponent<Text>()
					.text = "";
				RectTransform rectTransform = currentDetail.transform.GetChild(1).GetChild(0).rectTransform();
				RectTransform rectTransform2 = currentDetail.transform.GetChild(1).GetChild(1).rectTransform();
				if (mySettings.control_handedness == 0)
				{
					rectTransform.anchoredPosition = new Vector2(0f - Mathf.Abs(rectTransform.anchoredPosition.x), rectTransform.anchoredPosition.y);
					rectTransform2.anchoredPosition = new Vector2(Mathf.Abs(rectTransform2.anchoredPosition.x), rectTransform2.anchoredPosition.y);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMin = new Vector2(1f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMax = new Vector2(1f, 0.5f);
				}
				else
				{
					rectTransform.anchoredPosition = new Vector2(Mathf.Abs(rectTransform.anchoredPosition.x), rectTransform.anchoredPosition.y);
					rectTransform2.anchoredPosition = new Vector2(0f - Mathf.Abs(rectTransform2.anchoredPosition.x), rectTransform2.anchoredPosition.y);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMin = new Vector2(0f, 0.5f);
					currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
						.rectTransform()
						.anchorMax = new Vector2(0f, 0.5f);
				}
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(1).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(2).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(3).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
					.rectTransform()
					.anchoredPosition = mt.parent.GetChild(1).GetChild(4).rectTransform()
					.anchoredPosition;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(1).rectTransform()
					.localScale;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(2).rectTransform()
					.localScale;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(3).rectTransform()
					.localScale;
				currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
					.rectTransform()
					.localScale = mt.parent.GetChild(1).GetChild(4).rectTransform()
					.localScale;
				break;
			}
			}
			if (current == "Multiplayer")
			{
				switch (button)
				{
				case 10:
				{
					stayRoom.gameObject.SetActive(true);
					ipButton.SetActive(false);
					roomTexts[0].text = ruleTitleText[rule];
					roomTexts[1].text = ruleExpText[rule];
					roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
					roomTexts[3].text = "Player Count: " + playerCount;
					roomTexts[4].text = "Searching for a room...";
					myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
					myButton.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(myCharacter.color)
						.GetComponent<Image>()
						.color;
					myButton.transform.GetChild(0).GetComponent<Image>().sprite = characterScreen.GetChild(0).GetChild(0)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					myButton.transform.GetChild(1).GetComponent<Text>().text = PhotonNetwork.player.NickName;
					DetailInformation di2 = myButton.GetComponent<DetailInformation>();
					di2.canvas = mt;
					di2.backgroundColor = myButton.GetComponent<Image>().color;
					di2.comment = myCharacter.comment;
					di2.kill = myCharacter.kill;
					di2.death = myCharacter.death;
					myButton.transform.SetParent(multiplayerList, false);
					anim.SetBool("Detail", false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					if (currentDetail != null)
					{
						currentDetail.SetActive(false);
					}
					anim.SetBool("Matching", true);
					if (!PhotonNetwork.connected)
					{
						Connect();
					}
					if (PhotonNetwork.inRoom)
					{
						while (PhotonNetwork.inRoom)
						{
							yield return new WaitForSeconds(0f);
						}
					}
					while (!PhotonNetwork.connectedAndReady)
					{
						yield return new WaitForSeconds(0f);
					}
					ExitGames.Client.Photon.Hashtable customProps3 = new ExitGames.Client.Photon.Hashtable();
					if (rule != 0)
					{
						customProps3["R"] = rule;
					}
					if (objective != 0)
					{
						customProps3["O"] = objective;
					}
					RequireRoomModules(customProps3);
					PhotonNetwork.JoinRandomRoom(customProps3, (byte)playerCount);
					break;
				}
				case 11:
					stayRoom.gameObject.SetActive(true);
					ipButton.SetActive(false);
					if (!(invitationRoomName.text != ""))
					{
						break;
					}
					anim.SetBool("Detail", false);
					backButton.SetActive(false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					if (currentDetail != null)
					{
						currentDetail.SetActive(false);
					}
					pleaseWait.SetActive(true);
					if (!PhotonNetwork.connected)
					{
						Connect();
					}
					if (PhotonNetwork.inRoom)
					{
						while (PhotonNetwork.inRoom)
						{
							yield return new WaitForSeconds(0f);
						}
					}
					while (!PhotonNetwork.connectedAndReady)
					{
						yield return new WaitForSeconds(0f);
					}
					PhotonNetwork.JoinRoom(invitationRoomName.text);
					break;
				case 15:
					if (currentDetail.name == "InvitationMatch")
					{
						roomTexts[0].text = ruleTitleText[rule];
						roomTexts[1].text = ruleExpText[rule];
						roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
						roomTexts[3].text = "Player Count: " + playerCount;
						roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
						myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
						myButton.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(myCharacter.color)
							.GetComponent<Image>()
							.color;
						myButton.transform.GetChild(0).GetComponent<Image>().sprite = characterScreen.GetChild(0).GetChild(0)
							.GetChild(0)
							.GetComponent<Image>()
							.sprite;
						myButton.transform.GetChild(1).GetComponent<Text>().text = PhotonNetwork.player.NickName;
						DetailInformation di = myButton.GetComponent<DetailInformation>();
						di.canvas = mt;
						di.backgroundColor = myButton.GetComponent<Image>().color;
						di.comment = myCharacter.comment;
						di.kill = myCharacter.kill;
						di.death = myCharacter.death;
						myButton.transform.SetParent(multiplayerList, false);
						SetRoomCreationVisible(false);
						backButton.SetActive(false);
						yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
						pleaseWait.SetActive(true);
						ExitGames.Client.Photon.Hashtable customProps2 = new ExitGames.Client.Photon.Hashtable();
						customProps2["R"] = rule;
						customProps2["O"] = objective;
                        PublishRoomModules(customProps2);
						RoomOptions options = new RoomOptions
						{
							MaxPlayers = (byte)playerCount,
							CustomRoomProperties = customProps2,
							CustomRoomPropertiesForLobby = new string[3] { "R", "O", Flats.Modules.SessionModules.DigestProperty },
							IsVisible = false
						};
						string roomName = invitationRoomName.text;
						PhotonNetwork.CreateRoom(roomName, options, null);
					}
					else if (!(currentDetail.name == "LocalMatch"))
					{
					}
					break;
				case 16:
				{
					string text = PhotonNetwork.player.NickName + ":" + multiplayerScreen.GetChild(3).GetChild(2)
						.GetComponent<InputField>()
						.text;
					base.gameObject.GetPhotonView().RPC("Chat", PhotonTargets.MasterClient, text);
					multiplayerScreen.GetChild(3).GetChild(2)
						.GetComponent<InputField>()
						.text = "";
					break;
				}
				}
			}
			else if (current == "Singleplayer")
			{
				if (button >= 0 && button != 5)
				{
					backButton.SetActive(false);
					if (!waitBackground && PhotonNetwork.connected)
					{
						PhotonNetwork.Disconnect();
					}
					anim.SetBool("Detail", false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					StartCoroutine("BackgroundColor", "FadeIn");
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
					if (button != 4)
					{
						if (stage == -1)
						{
							stage = UnityEngine.Random.Range(2, 8);
						}
						else
						{
							stage += 2;
						}
					}
				}
				else if (button != 5)
				{
					stage = -1;
				}
				switch (button)
				{
				case 0:
					Singleplayer.rule = 0;
					LoadOfflineScene(stage);
					gameState = "Singleplayer"; Singleplayer.ResetSharedMatchState();
					break;
				case 1:
					Singleplayer.rule = 1;
					LoadOfflineScene(stage);
					gameState = "Singleplayer"; Singleplayer.ResetSharedMatchState();
					break;
				case 2:
					Singleplayer.rule = 2;
					LoadOfflineScene(stage);
					gameState = "Singleplayer"; Singleplayer.ResetSharedMatchState();
					break;
				case 3:
					Singleplayer.rule = 3;
					LoadOfflineScene(stage);
					gameState = "Singleplayer"; Singleplayer.ResetSharedMatchState();
					break;
				case 4:
					Singleplayer.rule = 4;
					LoadOfflineScene("Tutorial");
					gameState = "Singleplayer"; Singleplayer.ResetSharedMatchState();
					break;
				case 5:
					stage++;
					if (stage > 5)
					{
						stage = 0;
					}
					else if (stage < 0)
					{
						stage = 5;
					}
					buttons[5].sprite = images[stage + 12];
					bt[5].text = stageName[stage];
					break;
				}
			}
			else if (current == "Character")
			{
				switch (button)
				{
				case 10:
					StreamManager.LoadFileDialog(FolderLocations.Pictures, new string[3] { ".png", ".jpg", ".jpeg" }, imageLoadedCallback);
					break;
				case 11:
					myCharacter.color = IntParseFast(EventSystem.current.currentSelectedGameObject.name.Replace("Color", ""));
					StartCoroutine("BackgroundColor", "Change");
					if (Application.loadedLevel == 0)
					{
						Color color = characterScreen.GetChild(1).GetChild(myCharacter.color)
							.GetComponent<Image>()
							.color;
						ParticleSystem particleSystem = GameObject.Find("BackgroundParticle").GetComponent<ParticleSystem>();
						particleSystem.startColor = color;
					}
					break;
				case 12:
				{
					int primaryWeapon = myCharacter.primaryWeapon;
					Transform child = characterScreen.GetChild(3).GetChild(3)
						.GetChild(0);
					child.GetChild(0).GetComponent<Image>().sprite = characterScreen.GetChild(3).GetChild(2)
						.GetChild(primaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					child.GetChild(1).GetComponent<Text>().text = characterScreen.GetChild(3).GetChild(2)
						.GetChild(primaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					if (child.GetChild(1).GetComponent<Text>().text.Contains("Shotgun"))
					{
						child.GetChild(2).GetComponent<Text>().text = GunInfo.damage[primaryWeapon] + " x " + GunInfo.burstCount[primaryWeapon];
					}
					else if (child.GetChild(1).GetComponent<Text>().text.Contains("Grenade"))
					{
						child.GetChild(2).GetComponent<Text>().text = GunInfo.damage[primaryWeapon] + " + explosion";
					}
					else
					{
						child.GetChild(2).GetComponent<Text>().text = GunInfo.damage[primaryWeapon].ToString();
					}
					child.GetChild(3).GetComponent<Text>().text = GunInfo.rpm[primaryWeapon].ToString();
					child.GetChild(4).GetComponent<Text>().text = GunInfo.limitAmmo[primaryWeapon].ToString();
					child.GetChild(5).GetComponent<Text>().text = GunInfo.limitMaxAmmo[primaryWeapon].ToString();
					child.GetChild(6).GetComponent<Text>().text = GunInfo.accuracy[primaryWeapon] + "%";
					child.GetChild(7).GetComponent<Text>().text = GunInfo.reloadTime[primaryWeapon] + 1.2f + "sec";
					child.GetChild(8).GetComponent<Text>().text = GunInfo.headshotBonus[primaryWeapon] + "x";
					child.GetChild(9).GetComponent<Text>().text = sightDictionary[myCharacter.sightList[myCharacter.primaryWeapon]];
					if (myCharacter.sightList[myCharacter.primaryWeapon] == 0)
					{
						child.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.primaryWeapon]] + "\n(reload speed bonus)";
					}
					else
					{
						child.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.primaryWeapon]];
					}
					child.parent.gameObject.SetActive(true);
					EventSystem.current.SetSelectedGameObject(child.GetChild(11).gameObject);
					savedWeapon = primaryWeapon;
					break;
				}
				case 13:
				{
					int secondaryWeapon = myCharacter.secondaryWeapon;
					Transform child3 = characterScreen.GetChild(3).GetChild(3)
						.GetChild(0);
					child3.GetChild(0).GetComponent<Image>().sprite = characterScreen.GetChild(3).GetChild(2)
						.GetChild(secondaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					child3.GetChild(1).GetComponent<Text>().text = characterScreen.GetChild(3).GetChild(2)
						.GetChild(secondaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					if (child3.GetChild(1).GetComponent<Text>().text.Contains("Shotgun"))
					{
						child3.GetChild(2).GetComponent<Text>().text = GunInfo.damage[secondaryWeapon] + " x " + GunInfo.burstCount[secondaryWeapon];
					}
					else if (child3.GetChild(1).GetComponent<Text>().text.Contains("Grenade"))
					{
						child3.GetChild(2).GetComponent<Text>().text = GunInfo.damage[secondaryWeapon] + " + explosion";
					}
					else
					{
						child3.GetChild(2).GetComponent<Text>().text = GunInfo.damage[secondaryWeapon].ToString();
					}
					child3.GetChild(3).GetComponent<Text>().text = GunInfo.rpm[secondaryWeapon].ToString();
					child3.GetChild(4).GetComponent<Text>().text = GunInfo.limitAmmo[secondaryWeapon].ToString();
					child3.GetChild(5).GetComponent<Text>().text = GunInfo.limitMaxAmmo[secondaryWeapon].ToString();
					child3.GetChild(6).GetComponent<Text>().text = GunInfo.accuracy[secondaryWeapon] + "%";
					child3.GetChild(7).GetComponent<Text>().text = GunInfo.reloadTime[secondaryWeapon] + 1.2f + "sec";
					child3.GetChild(8).GetComponent<Text>().text = GunInfo.headshotBonus[secondaryWeapon] + "x";
					child3.GetChild(9).GetComponent<Text>().text = sightDictionary[myCharacter.sightList[myCharacter.secondaryWeapon]];
					if (myCharacter.sightList[myCharacter.secondaryWeapon] == 0)
					{
						child3.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.secondaryWeapon]] + "\n(reload speed bonus)";
					}
					else
					{
						child3.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[myCharacter.secondaryWeapon]];
					}
					child3.parent.gameObject.SetActive(true);
					EventSystem.current.SetSelectedGameObject(child3.GetChild(11).gameObject);
					savedWeapon = secondaryWeapon;
					break;
				}
				case 14:
				{
					int num = IntParseFast(EventSystem.current.currentSelectedGameObject.name.Replace("Weapon", ""));
					Debug.Log("picked weapon:" + num);
					Transform child2 = characterScreen.GetChild(3).GetChild(3)
						.GetChild(0);
					child2.GetChild(0).GetComponent<Image>().sprite = characterScreen.GetChild(3).GetChild(2)
						.GetChild(num)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					child2.GetChild(1).GetComponent<Text>().text = characterScreen.GetChild(3).GetChild(2)
						.GetChild(num)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					if (child2.GetChild(1).GetComponent<Text>().text.Contains("Shotgun"))
					{
						child2.GetChild(2).GetComponent<Text>().text = GunInfo.damage[num] + " x " + GunInfo.burstCount[num];
					}
					else if (child2.GetChild(1).GetComponent<Text>().text.Contains("Grenade"))
					{
						child2.GetChild(2).GetComponent<Text>().text = GunInfo.damage[num] + " + explosion";
					}
					else
					{
						child2.GetChild(2).GetComponent<Text>().text = GunInfo.damage[num].ToString();
					}
					child2.GetChild(3).GetComponent<Text>().text = GunInfo.rpm[num].ToString();
					child2.GetChild(4).GetComponent<Text>().text = GunInfo.limitAmmo[num].ToString();
					child2.GetChild(5).GetComponent<Text>().text = GunInfo.limitMaxAmmo[num].ToString();
					child2.GetChild(6).GetComponent<Text>().text = GunInfo.accuracy[num] + "%";
					child2.GetChild(7).GetComponent<Text>().text = GunInfo.reloadTime[num] + 2f + "sec";
					child2.GetChild(8).GetComponent<Text>().text = GunInfo.headshotBonus[num] + "x";
					child2.GetChild(9).GetComponent<Text>().text = sightDictionary[myCharacter.sightList[num]];
					if (myCharacter.sightList[num] == 0)
					{
						child2.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[num]] + "\n(reload speed bonus)";
					}
					else
					{
						child2.GetChild(13).GetChild(0).GetComponent<Text>()
							.text = "Sight: " + sightDictionary[myCharacter.sightList[num]];
					}
					child2.parent.gameObject.SetActive(true);
					EventSystem.current.SetSelectedGameObject(child2.GetChild(11).gameObject);
					savedWeapon = num;
					break;
				}
				case 15:
					if (myCharacter.secondaryWeapon == savedWeapon)
					{
						myCharacter.secondaryWeapon = myCharacter.primaryWeapon;
						characterScreen.GetChild(3).GetChild(1)
							.GetChild(1)
							.GetComponent<Image>()
							.sprite = characterScreen.GetChild(3).GetChild(2)
							.GetChild(myCharacter.secondaryWeapon)
							.GetChild(0)
							.GetComponent<Image>()
							.sprite;
						characterScreen.GetChild(3).GetChild(1)
							.GetChild(2)
							.GetComponent<Text>()
							.text = characterScreen.GetChild(3).GetChild(2)
							.GetChild(myCharacter.secondaryWeapon)
							.GetChild(1)
							.GetComponent<Text>()
							.text;
					}
					myCharacter.primaryWeapon = savedWeapon;
					characterScreen.GetChild(3).GetChild(0)
						.GetChild(1)
						.GetComponent<Image>()
						.sprite = characterScreen.GetChild(3).GetChild(2)
						.GetChild(myCharacter.primaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					characterScreen.GetChild(3).GetChild(0)
						.GetChild(2)
						.GetComponent<Text>()
						.text = characterScreen.GetChild(3).GetChild(2)
						.GetChild(myCharacter.primaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					break;
				case 16:
					if (myCharacter.primaryWeapon == savedWeapon)
					{
						myCharacter.primaryWeapon = myCharacter.secondaryWeapon;
						characterScreen.GetChild(3).GetChild(0)
							.GetChild(1)
							.GetComponent<Image>()
							.sprite = characterScreen.GetChild(3).GetChild(2)
							.GetChild(myCharacter.primaryWeapon)
							.GetChild(0)
							.GetComponent<Image>()
							.sprite;
						characterScreen.GetChild(3).GetChild(0)
							.GetChild(2)
							.GetComponent<Text>()
							.text = characterScreen.GetChild(3).GetChild(2)
							.GetChild(myCharacter.primaryWeapon)
							.GetChild(1)
							.GetComponent<Text>()
							.text;
					}
					myCharacter.secondaryWeapon = savedWeapon;
					characterScreen.GetChild(3).GetChild(1)
						.GetChild(1)
						.GetComponent<Image>()
						.sprite = characterScreen.GetChild(3).GetChild(2)
						.GetChild(myCharacter.secondaryWeapon)
						.GetChild(0)
						.GetComponent<Image>()
						.sprite;
					characterScreen.GetChild(3).GetChild(1)
						.GetChild(2)
						.GetComponent<Text>()
						.text = characterScreen.GetChild(3).GetChild(2)
						.GetChild(myCharacter.secondaryWeapon)
						.GetChild(1)
						.GetComponent<Text>()
						.text;
					break;
				case 17:
					if (myCharacter.sightList[savedWeapon] < GunInfo.zoom[savedWeapon])
					{
						myCharacter.sightList[savedWeapon] = myCharacter.sightList[savedWeapon] + 1;
					}
					else
					{
						myCharacter.sightList[savedWeapon] = 0;
					}
					if (myCharacter.sightList[savedWeapon] == 0)
					{
						characterScreen.GetChild(3).GetChild(3)
							.GetChild(0)
							.GetChild(13)
							.GetChild(0)
							.GetComponent<Text>()
							.text = sightDictionary[myCharacter.sightList[savedWeapon]] + "\n(reload speed bonus)";
					}
					else
					{
						characterScreen.GetChild(3).GetChild(3)
							.GetChild(0)
							.GetChild(13)
							.GetChild(0)
							.GetComponent<Text>()
							.text = sightDictionary[myCharacter.sightList[savedWeapon]];
					}
					characterScreen.GetChild(3).GetChild(3)
						.GetChild(0)
						.GetChild(9)
						.GetComponent<Text>()
						.text = sightDictionary[myCharacter.sightList[savedWeapon]];
					break;
				case 18:
					EventSystem.current.SetSelectedGameObject(characterScreen.GetChild(3).GetChild(2)
						.GetChild(savedWeapon)
						.gameObject);
						characterScreen.GetChild(3).GetChild(3)
							.gameObject.SetActive(false);
						break;
					default:
						switch (button)
						{
                        case 18:
                            ShowConfirm("Legacy cloud unavailable", "Original service data cannot be accessed. No cloud data was loaded.\nUse Export save / Import old save files, or LAN Sync between desktop devices.", null, "OK", null);
                            break;
                        case 19:
                            yield return RunLanSync();
                            break;
						}
						break;
					}
					InputDevice inputDevice = InputManager.ActiveDevice;
					if (characterScreen.GetChild(3).GetChild(3)
						.gameObject.activeSelf && (Input.GetKeyUp(KeyCode.Escape) || inputDevice.CommandWasPressed || inputDevice.Action2.WasPressed))
					{
						characterScreen.GetChild(3).GetChild(3)
							.gameObject.SetActive(false);
						backButton.SetActive(true);
						MonoBehaviour.print("Back with B");
					}
				}
				else if (current == "Settings")
				{
					if (button == 10)
					{
						Debug.Log("Touch button mapping has been reset.");
						currentDetail.transform.GetChild(1).GetChild(0).GetChild(2)
							.GetComponent<Text>()
							.text = "";
						if (mySettings.control_handedness == 0)
						{
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMin = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMax = new Vector2(1f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - fireButtonPosition.x, fireButtonPosition.y);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - reloadButtonPosition.x, reloadButtonPosition.y);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - actionButtonPosition.x, actionButtonPosition.y);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchoredPosition = new Vector2(0f - grenadeButtonPosition.x, grenadeButtonPosition.y);
						}
						else
						{
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMin = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchorMax = new Vector2(0f, 0.5f);
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
								.rectTransform()
								.anchoredPosition = fireButtonPosition;
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
								.rectTransform()
								.anchoredPosition = reloadButtonPosition;
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
								.rectTransform()
								.anchoredPosition = actionButtonPosition;
							currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
								.rectTransform()
								.anchoredPosition = grenadeButtonPosition;
						}
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(1)
							.rectTransform()
							.localScale.z);
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(2)
							.rectTransform()
							.localScale.z);
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(3)
							.rectTransform()
							.localScale.z);
						currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
							.rectTransform()
							.localScale = new Vector3(1f, 1f, currentDetail.transform.GetChild(1).GetChild(1).GetChild(4)
							.rectTransform()
							.localScale.z);
					}
				}
				else if (current == "Leaderboard")
				{
					if (button == 10)
					{
						leaderboardLoading.SetActive(true);
						StartCoroutine("Leaderboard", true);
					}
				}
				else if (current == "Information")
				{
					switch (button)
					{
					case 10:
                        ShareGame();
                        break;
					case 11:
					{
						aboutUs.SetActive(true);
						Selectable component = aboutUs.transform.GetChild(4).GetComponent<Selectable>();
						if (Input.GetJoystickNames().Length > 0)
						{
							component.Select();
						}
						break;
					}
					case 12:
						if (VRController.device == "oculus")
						{
							ShowConfirm("Not available", "Sorry, currently not available.", null, "OK", null);
							break;
						}
						if (fireTV)
						{
							ShowConfirm("Not available from TV", "Sorry, currently this option is unavalable.", null, "OK", null);
							break;
						}
						if (adFree)
						{
							GetComponent<InAppPurchase>().Buy("beer");
						}
						else
						{
							GetComponent<InAppPurchase>().Buy("adremover");
						}
						Debug.Log("Opening in-app purchase...");
						break;
					case 13:
					{
						MarketingDesc marketingDesc = new MarketingDesc();
						marketingDesc.Editor_URL = "http://foliagegames.com/";
						marketingDesc.Win8_PackageFamilyName = "FoliageGamesLLC.Flats_arh4z6sc73q8a";
						marketingDesc.WP8_AppID = "9wzdncrdh8vm";
						marketingDesc.iOS_AppID = "833603987";
						marketingDesc.BB10_AppID = "";
						marketingDesc.Android_MarketingStore = MarketingStores.GooglePlay;
						marketingDesc.Android_GooglePlay_BundleID = "com.foliagegames.flats";
						marketingDesc.Android_Amazon_BundleID = "com.foliagegames.flats";
						marketingDesc.Android_Samsung_BundleID = "com.foliagegames.flats";
						MarketingManager.OpenStoreForReview(marketingDesc);
						break;
					}
					case 14:
						Application.OpenURL("http://foliagegames.com");
						break;
					case 15:
						Application.OpenURL("https://www.facebook.com/foliagegames");
						break;
					case 16:
						Application.OpenURL("https://twitter.com/foliagegames");
						break;
					case 17:
						Application.OpenURL("https://plus.google.com/107882397860279823163");
						break;
					}
				}
			}
			else if (current == "Matching")
			{
				switch (button)
				{
				case -1:
					anim.SetBool("Matching", false);
					yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
					if (stayRoom.isOn)
					{
						waitBackground = true;
						backButton.SetActive(false);
						BackToMainMenu();
						break;
					}
					bt[0].text = "Open Match";
					bt[1].text = "Invitation Match";
					bt[2].text = "Local Match";
					bt[3].text = "Chat Room";
					bt[4].text = "Server Region";
					bt[5].text = "Online Version: " + version.Substring(0, 3);
					buttons[0].sprite = images[36];
					buttons[1].sprite = images[37];
					buttons[2].sprite = images[38];
					buttons[3].sprite = images[39];
					buttons[4].sprite = images[40];
					buttons[5].sprite = images[41];
					waitBackground = false;
					stayRoom.interactable = false;
					startNow.interactable = false;
					UnityEngine.Object.Destroy(myButton);
					wasInRoom = false;
					PhotonNetwork.Disconnect();
					currentDetail = null;
					anim.SetBool("Detail", false);
					anim.SetBool("Fade", false);
					current = "Multiplayer";
					break;
				case 10:
					if (!startNowPressed)
					{
						base.gameObject.GetPhotonView().RPC("StartNow", PhotonTargets.AllBuffered);
						startNowPressed = true;
					}
					break;
				case 12:
					if (!VRmode)
					{
						if (!PhotonNetwork.inRoom)
						{
						}
					}
					else
					{
						ShowConfirm("This option is unavailable.", "Sorry, currently this option is not supported in VR mode.", null, "OK", null);
					}
					break;
				}
			}
			else if (current == "Map")
			{
				if (!voted)
				{
					base.gameObject.GetPhotonView().RPC("VoteMap", PhotonTargets.AllBuffered, button);
					voted = true;
				}
			}
			else if (current == "Result" && button == -1)
			{
				backButton.SetActive(false);
				Time.timeScale = 1f;
				if (gameState == "Multiplayer")
				{
					wasInRoom = false;
					PhotonNetwork.Disconnect();
				}
				if (adForWin != null && adForWin.Visible)
				{
					adForWin.Visible = false;
				}
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
				anim.SetBool("Fade", false);
				resultsScreen.gameObject.SetActive(false);
				StartCoroutine("BackgroundColor", "FadeIn");
			}
			if (current == "Main")
			{
				quitButton.SetActive(true);
			}
			else
			{
				quitButton.SetActive(false);
			}
			if (currentDetail != null)
			{
				Debug.Log("current:" + current + " currentDetail:" + currentDetail.name);
			}
			else
			{
				Debug.Log("current:" + current + " currentDetail: null");
			}
			fliping = false;
		}
}
