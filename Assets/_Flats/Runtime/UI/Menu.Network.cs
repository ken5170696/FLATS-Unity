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

// Photon lobby, room, invitation and LAN sync callbacks.
public partial class Menu
{
	private void OnJoinedLobby()
	{
		if (!(current != "Multiplayer") || !(current != "Matching"))
		{
			return;
		}
		Debug.Log("Joined lobby");
		invitedRules = new List<int>();
		if (roomList == null || roomList.Length <= 0)
		{
			return;
		}
		for (int i = 0; i < roomList.Length; i++)
		{
			if (roomList[i].IsOpen)
			{
				int item = (int)roomList[i].CustomProperties["R"];
				invitedRules.Add(item);
			}
		}
	}

	private void OnReceivedRoomListUpdate()
	{
		if (!PhotonNetwork.inRoom && current != "Multiplayer" && current != "Matching")
		{
			roomList = PhotonNetwork.GetRoomList();
			if (mySettings.extra_notification == 1)
			{
				PhotonNetwork.Disconnect();
			}
		}
	}

	private IEnumerator ReceiveInvitation()
	{
		while (true)
		{
			yield return new WaitForSeconds(10f);
			if (offlineNotifications && network == 0 && canOpen && !waitBackground && current != "Matching")
			{
				if (PhotonNetwork.inRoom)
				{
					Debug.Log("You are in matchmaking, stop receiving invitation.");
				}
				else
				{
					gettingRoomList = true;
					Connect();
					Debug.Log("Called Connect() to get room list.");
				}
				yield return new WaitForSeconds(10f);
				if (!PhotonNetwork.connected && invitedRules.Count > 0)
				{
					int r = (currentInvitedRule = invitedRules[UnityEngine.Random.Range(0, invitedRules.Count)]);
					Debug.Log("Send invitation.");
					if (Input.GetJoystickNames().Length > 0)
					{
						notification.transform.GetChild(0).GetChild(1).GetComponent<Text>()
							.text = ruleTitleText[r] + "\nJoin: {control:Join}.";
					}
					else if (!Application.isMobilePlatform && Input.mousePresent)
					{
						notification.transform.GetChild(0).GetChild(1).GetComponent<Text>()
							.text = ruleTitleText[r] + "\nPress enter key to join";
					}
					else
					{
						notification.transform.GetChild(0).GetChild(1).GetComponent<Text>()
							.text = ruleTitleText[r] + "\nTap here to join";
					}
					Animator notificationAnim = notification.GetComponent<Animator>();
					notification.gameObject.SetActive(true);
					notificationAnim.Play("Invitation_On");
					yield return new WaitForSeconds(4f);
					notificationAnim.Play("Invitation_Off");
					yield return new WaitForSeconds(1f);
					notification.gameObject.SetActive(false);
				}
			}
			yield return new WaitForSeconds(0f);
		}
	}

	public void AcceptInvitation()
	{
		rule = currentInvitedRule;
		StopCoroutine("ReceiveInvitation");
		if (current == "Playing")
		{
			OpenMenu();
		}
		StartCoroutine("JoinFromInvitation");
	}

	private IEnumerator JoinFromInvitation()
	{
        yield return StartCoroutine(EnsureMultiplayerConnection());
        if (!multiplayerReady) { fliping = false; anim.SetBool("Fade", false); yield break; }
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
		myButton.transform.GetChild(1).GetComponent<Text>().text = myCharacter.name;
		DetailInformation di = myButton.GetComponent<DetailInformation>();
		di.canvas = mt;
		di.backgroundColor = myButton.GetComponent<Image>().color;
		di.comment = myCharacter.comment;
		di.kill = myCharacter.kill;
		di.death = myCharacter.death;
		myButton.transform.SetParent(multiplayerList, false);
		Texture2D icon = new Texture2D(128, 128)
		{
			filterMode = FilterMode.Bilinear
		};
		byte[] bytes = System.IO.File.ReadAllBytes((FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath) + "/Flats_UserIcon.png");
		icon.LoadImage(bytes);
		ExitGames.Client.Photon.Hashtable playerProps = new ExitGames.Client.Photon.Hashtable();
		playerProps["K"] = myCharacter.kill;
		playerProps["D"] = myCharacter.death;
		playerProps["TC"] = myCharacter.color;
		playerProps["C"] = myCharacter.comment;
		playerProps["I"] = bytes;
        PublishRoomModules(playerProps);
		PhotonNetwork.SetPlayerCustomProperties(playerProps);
		PhotonNetwork.player.NickName = myCharacter.name;
		anim.SetBool("Fade", true);
		yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
		if (currentDetail != null)
		{
			currentDetail.SetActive(false);
		}
		quitButton.SetActive(false);
		anim.SetBool("Matching", true);
		anim.SetBool("Detail", false);
		anim.SetTrigger("SkipToMatching");
		current = "Matching";
		ExitGames.Client.Photon.Hashtable customProps = new ExitGames.Client.Photon.Hashtable();
		if (rule != 0)
		{
			customProps["R"] = rule;
		}
        RequireRoomModules(customProps);
        pendingRoomDeadline = Time.realtimeSinceStartup + 25f;
		PhotonNetwork.JoinRandomRoom(customProps, 0);
		preCheckToStayRoom = true;
	}

	private void Connect()
    {
        PhotonNetwork.automaticallySyncScene = false;
        PhotonNetwork.BackgroundTimeout = 60f;
        if (!FlatsPhotonConfiguration.Apply(out multiplayerFailure)) return;
        PhotonNetwork.offlineMode = false;
        if (!PhotonNetwork.ConnectUsingSettings(version.Substring(0, 3)))
            multiplayerFailure = "Photon rejected the connection request: " + PhotonNetwork.connectionStateDetailed;
    }

        private IEnumerator RunLanSync()
        {
            if (network == 1 || network == 2 || waitBackground || gameState != "Main")
            {
                ShowConfirm("Return to the main menu", "End your game or matchmaking before syncing saved scores.", null, "OK", null);
                yield break;
            }
            if (!LocalNetwork.Supported)
            {
                ShowConfirm("LAN Sync in this browser", "Browsers cannot use UDP LAN sync. Use Export save on the source device, then Import old save here. These controls are below LAN Sync.", null, "OK", null);
                yield break;
            }
            var lan = GetComponent<LocalNetwork>();
            if (lan == null)
            {
                ShowConfirm("LAN Sync unavailable", "The LAN component is missing. Use Export save / Import old save instead.", null, "OK", null);
                yield break;
            }
            syncData = "";
            syncing = false;
            pleaseWait.SetActive(true);
            lan.StartReceivingDataForSync();
            float end = Time.realtimeSinceStartup + 3f;
            bool cancelled = false;
            while (lan.IsReceiving && Time.realtimeSinceStartup < end)
            {
                if (gameState != "Main" || current != "Character" || Input.GetKeyDown(KeyCode.Escape) || InputManager.ActiveDevice.CommandWasPressed)
                { cancelled = true; break; }
                yield return null;
            }
            lan.CloseReceiver();
            pleaseWait.SetActive(false);
            if (cancelled) yield break;
            if (!string.IsNullOrEmpty(lan.Error))
            {
                ShowConfirm("LAN Sync failed", lan.Error, null, "OK", null);
                yield break;
            }
            Flats.Core.LanSyncRecord record;
            if (Flats.Core.LanSyncRecord.TryParse(lan.masterIP, out record))
            {
                syncData = lan.masterIP;
                ShowConfirm("Review received scores", "LAN sender ID: " + record.Id +
                    "\nKills / deaths: " + record.Kills + " / " + record.Deaths +
                    "\nSurvival / assortment / headshot: " + record.Survival + " / " + record.Assortment + " / " + record.Headshot +
                    "\nReplace only your ID and these five scores, then reload? Confirm only a sender you recognize.", SyncDataConfirm, "Replace scores", "Cancel");
                yield break;
            }
            syncing = true;
            lan.StartCoroutine(lan.StartSendingDataForSync());
            if (!lan.IsSending)
            {
                syncing = false;
                ShowConfirm("LAN Sync failed", lan.Error, null, "OK", null);
                yield break;
            }
            ShowConfirm("Broadcasting LAN scores", "Sending ID: " + myCharacter.id +
                "\nOpen LAN Sync on the receiving device now. Delivery is not confirmed here.\nBroadcast stops after 60 seconds, or when you close this dialog.", SyncDataConfirm, "Stop", null);
            while (syncing && lan.IsSending) yield return null;
            if (syncing)
            {
                syncing = false;
                ShowConfirm(string.IsNullOrEmpty(lan.Error) ? "LAN broadcast ended" : "LAN Sync failed",
                    lan.Status, null, "OK", null);
            }
        }

        private void SyncDataConfirm(bool result)
        {
            var lan = GetComponent<LocalNetwork>();
            if (lan != null) { lan.StopAllCoroutines(); lan.CloseSender(); lan.CloseReceiver(); }
            bool wasSending = syncing;
            syncing = false;
            string payload = syncData;
            syncData = "";
            if (!result || wasSending) return;
            Flats.Core.LanSyncRecord record;
            if (!Flats.Core.LanSyncRecord.TryParse(payload, out record) || gameState != "Main")
            {
                ShowConfirm("LAN Sync failed", "Received scores are no longer valid. Your scores were not changed.", null, "OK", null);
                return;
            }
            string oldId = myCharacter.id;
            int[] oldScores = { myCharacter.kill, myCharacter.death, myCharacter.survivalScore, myCharacter.assortmentScore, myCharacter.headshotScore };
            myCharacter.id = record.Id;
            myCharacter.kill = record.Kills;
            myCharacter.death = record.Deaths;
            myCharacter.survivalScore = record.Survival;
            myCharacter.assortmentScore = record.Assortment;
            myCharacter.headshotScore = record.Headshot;
            SaveDataController.Save();
            if (FlatsLocalProfile.LastSaveSucceeded) { LoadOfflineScene(0); return; }
            myCharacter.id = oldId;
            myCharacter.kill = oldScores[0];
            myCharacter.death = oldScores[1];
            myCharacter.survivalScore = oldScores[2];
            myCharacter.assortmentScore = oldScores[3];
            myCharacter.headshotScore = oldScores[4];
            ShowConfirm("Could not confirm save", "Current-session scores were restored. Check the storage error before retrying or restarting.", null, "OK", null);
        }

        private void ResetMatchReadiness()
        {
            ++readyOperation;
            readyStarted = false;
            StopCoroutine("Ready");
            RestoreReadyPause();
        }

        // Ready pauses a single-player game in progress; an abandoned attempt must resume it.
        private bool readyPausedGame;
        private void RestoreReadyPause()
        {
            if (readyPausedGame && gameState == "Singleplayer" && Time.timeScale == 0f) Time.timeScale = 1f;
            readyPausedGame = false;
        }

        private void OnLeftRoom()
        {
            ResetMatchReadiness();
        }

		private IEnumerator Ready()
		{
            // Full-room callbacks and buffered StartNow votes can arrive in one frame.
            // Each client must send exactly one Sync for this room attempt.
            if (readyStarted || !PhotonNetwork.inRoom) yield break;
            readyStarted = true;
            int operation = readyOperation;
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            float deadline = Time.realtimeSinceStartup + 60f;
			MonoBehaviour.print("Start syncing...");
			roomTexts[4].text = "Syncing... up to a minute.";
			PhotonNetwork.room.IsOpen = false;
			PhotonNetwork.room.IsVisible = false;
			if (waitBackground && current != "Matching")
			{
				ShowConfirm("Multiplayer Ready", "If you are playing singleplayer, current score will be saved.", null, "OK", null);
				Time.timeScale = 0f;
				readyPausedGame = true;
				if (gameState == "Singleplayer")
				{
					myCurrent.survival_Score = currentSurvivalScore;
					myCurrent.survival_Phase = currentSurvivalPhase;
					myCurrent.assortment_Score = currentAssortmentScore;
					myCurrent.assortment_Phase = currentAssortmentPhase;
					myCurrent.headshot_Score = currentHeadshotScore;
					myCurrent.headshot_Chain = currentHeadshotChain;
					SaveDataController.Save();
				}
				waitBackground = false;
			}
			else
			{
				backButton.SetActive(false);
				startNow.interactable = false;
				Debug.Log("Stopped start now function.");
			}
			syncedPlayer = 0;
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(5f));
            if (operation != readyOperation || !PhotonNetwork.inRoom ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != scene) { RestoreReadyPause(); yield break; }
			base.gameObject.GetPhotonView().RPC("Sync", PhotonTargets.AllBuffered);
			while (true)
			{
                if (operation != readyOperation || !PhotonNetwork.inRoom ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != scene) { RestoreReadyPause(); yield break; }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    // Invalidate this attempt before disconnecting so delayed buffered RPCs
                    // cannot start map voting while the recovery message is visible.
                    ++readyOperation;
                    readyStarted = false;
                    wasInRoom = false;
                    pendingRoomDeadline = 0;
                    localHostedRoom = null;
                    if (localDiscovery != null) localDiscovery.Stop();
                    PhotonNetwork.Disconnect();
                    pleaseWait.SetActive(false);
                    fliping = false;
                    roomTexts[4].text = "Player synchronization timed out.";
                    ShowConfirm("Match could not start",
                        "Not all players completed synchronization within 60 seconds. The room connection was closed. Return to the main menu, then host or join again.",
                        result => { if (result && this != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle == scene) Reset(true); },
                        "Return to menu", null);
                    RestoreReadyPause();
                    yield break;
                }
				if (PhotonNetwork.isMasterClient && syncedPlayer >= PhotonNetwork.room.PlayerCount)
				{
					Debug.Log("I'm the master");
					base.gameObject.GetPhotonView().RPC("DecideMap", PhotonTargets.AllBuffered);
					break;
				}
				if (!(gameState == "Multiplayer"))
				{
					yield return new WaitForSeconds(0f);
					continue;
				}
				break;
			}
			// The match is starting; the pause now belongs to the match transition.
			readyPausedGame = false;
		}

		[PunRPC]
		private void Sync()
		{
            if (!readyStarted || !PhotonNetwork.inRoom) return;
			syncedPlayer++;
		}

		[PunRPC]
		private IEnumerator DecideMap()
		{
            if (!readyStarted || !PhotonNetwork.inRoom) yield break;
			if (current != "Matching")
			{
				backButton.SetActive(false);
				anim.SetTrigger("SkipToMatching");
				current = "Matching";
			}
			if (rule != 1 && rule != 6 && rule != 8)
			{
				roomTexts[4].text = "Adjusting team members...";
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
			}
			if (PhotonNetwork.isMasterClient && PunTeams.PlayersPerTeam[PunTeams.Team.red].Count != PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count)
			{
				Debug.Log("Adjusting team count...");
				if (PunTeams.PlayersPerTeam[PunTeams.Team.red].Count < PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count)
				{
					PhotonPlayer player = PunTeams.PlayersPerTeam[PunTeams.Team.blue][0];
					player.SetTeam(PunTeams.Team.red);
				}
				else
				{
					PhotonPlayer player2 = PunTeams.PlayersPerTeam[PunTeams.Team.red][0];
					player2.SetTeam(PunTeams.Team.blue);
				}
			}
			waitBackground = false;
			anim.SetBool("Matching", false);
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
			anim.SetBool("Fade", false);
			current = "Map";
			gameState = "Multiplayer";
			voteMap.text = "Vote map";
			voteMap.gameObject.SetActive(true);
			int time = 10;
			while (time > 0)
			{
				voteMap.text = "Vote map " + time;
				bt[0].text = stageName[0] + " : " + vote[0].mapValue;
				bt[1].text = stageName[1] + " : " + vote[1].mapValue;
				bt[2].text = stageName[2] + " : " + vote[2].mapValue;
				bt[3].text = stageName[3] + " : " + vote[3].mapValue;
				bt[4].text = stageName[4] + " : " + vote[4].mapValue;
				bt[5].text = stageName[5] + " : " + vote[5].mapValue;
				buttons[0].sprite = images[12];
				buttons[1].sprite = images[13];
				buttons[2].sprite = images[14];
				buttons[3].sprite = images[15];
				buttons[4].sprite = images[16];
				buttons[5].sprite = images[17];
				yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(1f));
				time--;
				if (time <= 0)
				{
					break;
				}
				yield return new WaitForSeconds(0f);
			}
			anim.SetBool("Fade", true);
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(fade.length));
			voteMap.gameObject.SetActive(false);
			network = 2;
			Debug.Log("Network mode:" + network);
			while (!PhotonNetwork.isMasterClient)
			{
				yield return new WaitForSeconds(0f);
			}
			vote.Sort((Map x, Map y) => y.mapValue.CompareTo(x.mapValue));
			int num;
			if (vote[0].mapValue == 0)
			{
				num = vote[UnityEngine.Random.Range(0, 6)].mapKey + 2;
			}
			else
			{
				int num2 = 0;
				for (int num3 = 1; num3 < vote.Count; num3++)
				{
					if (vote[num3].mapValue == vote[0].mapValue)
					{
						num2 = num3;
					}
				}
				num = ((num2 != 0) ? (vote[UnityEngine.Random.Range(0, num2 + 1)].mapKey + 2) : (vote[0].mapKey + 2));
			}
			base.gameObject.GetPhotonView().RPC("LoadMap", PhotonTargets.AllBuffered, num);
		}

		[PunRPC]
		private void VoteMap(int map)
		{
			vote[map].mapValue++;
		}

		[PunRPC]
		private IEnumerator LoadMap(int map)
		{
			StartCoroutine("BackgroundColor", "FadeIn");
			yield return StartCoroutine(CoroutineUtil.WaitForRealSeconds(2f));
			LoadOfflineScene(map);
		}

		private void OnJoinedRoom()
		{
            ResetMatchReadiness();
            pendingRoomDeadline = 0;
            if (startingOfflineMatch) return;
            if (RejectCancelledLocalRoom()) return;
            if (!CheckRoomModules()) { LocalRoomFailed("Room modules do not match. Inspect MOD before retrying."); return; }
            LocalRoomJoined();
			Debug.Log("Joined!");
			if ((int)PhotonNetwork.room.CustomProperties["R"] == -1)
			{
				chat.GetChild(5).gameObject.SetActive(false);
				chat.GetChild(3).GetComponent<Button>().interactable = true;
				base.gameObject.GetPhotonView().RPC("Chat", PhotonTargets.MasterClient, PhotonNetwork.player.NickName + " joined chat.");
				return;
			}
			wasInRoom = true;
			pleaseWait.SetActive(false);
			backButton.SetActive(true);
			anim.SetBool("Matching", true);
			current = "Matching";
			stayRoom.interactable = true;
			startNow.interactable = true;
			rule = (int)PhotonNetwork.room.CustomProperties["R"];
			objective = (int)PhotonNetwork.room.CustomProperties["O"];
			playerCount = PhotonNetwork.room.MaxPlayers;
			roomTexts[0].text = ruleTitleText[rule];
			roomTexts[1].text = ruleExpText[rule];
			roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
			roomTexts[3].text = "Player Count: " + playerCount;
			roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
			int num = playerCount;
			if (num % 2 == 1)
			{
				num++;
			}
			if (num <= 2)
			{
				num = 4;
			}
			if (playerCount <= 2 || rule == 1 || rule == 6 || rule == 8)
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Start Now! " + startNowPlayer + "/" + num / 2;
			}
			else
			{
				startNow.transform.GetChild(0).GetComponent<Text>().text = "Add bot and Start Now! " + startNowPlayer + "/" + num / 2;
			}
			if (myButton == null)
			{
				myButton = (GameObject)UnityEngine.Object.Instantiate(playerButton);
				myButton.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(myCharacter.color)
					.GetComponent<Image>()
					.color;
				myButton.transform.GetChild(0).GetComponent<Image>().sprite = characterScreen.GetChild(0).GetChild(0)
					.GetChild(0)
					.GetComponent<Image>()
					.sprite;
				myButton.transform.GetChild(1).GetComponent<Text>().text = PhotonNetwork.player.NickName;
				DetailInformation component = myButton.GetComponent<DetailInformation>();
				component.canvas = mt;
				component.backgroundColor = myButton.GetComponent<Image>().color;
				component.comment = myCharacter.comment;
				component.kill = myCharacter.kill;
				component.death = myCharacter.death;
				myButton.transform.SetParent(multiplayerList, false);
			}
			if (rule == 1 || rule == 6 || rule == 8)
			{
				PhotonNetwork.player.SetTeam(PunTeams.Team.none);
			}
			else if ((bool)myButton)
			{
				if (PunTeams.PlayersPerTeam[PunTeams.Team.red].Count <= PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count)
				{
					PhotonNetwork.player.SetTeam(PunTeams.Team.red);
					myButton.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
				}
				else
				{
					PhotonNetwork.player.SetTeam(PunTeams.Team.blue);
					myButton.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
				}
				if (preCheckToStayRoom)
				{
					stayRoom.isOn = true;
				}
			}
			else
			{
				stayRoom.interactable = false;
				startNow.interactable = false;
				PhotonNetwork.Disconnect();
			}
			MonoBehaviour.print("Red Team:" + PunTeams.PlayersPerTeam[PunTeams.Team.red].Count + " Blue Team:" + PunTeams.PlayersPerTeam[PunTeams.Team.blue].Count);
			PhotonPlayer[] otherPlayers = PhotonNetwork.otherPlayers;
			foreach (PhotonPlayer photonPlayer in otherPlayers)
			{
				byte[] data = (byte[])photonPlayer.CustomProperties["I"];
				Texture2D texture2D = new Texture2D(128, 128);
				texture2D.LoadImage(data);
				GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(playerButton);
				if (rule == 1 || rule == 6 || rule == 8)
				{
					int index = (int)photonPlayer.CustomProperties["TC"];
					gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(index)
						.GetComponent<Image>()
						.color;
				}
				else if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
						.GetComponent<Image>()
						.color;
				}
				else
				{
					gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
						.GetComponent<Image>()
						.color;
				}
				gameObject.transform.GetChild(0).GetComponent<Image>().sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
				gameObject.transform.GetChild(1).GetComponent<Text>().text = photonPlayer.NickName;
				DetailInformation component2 = gameObject.GetComponent<DetailInformation>();
				component2.canvas = base.transform;
				component2.backgroundColor = gameObject.GetComponent<Image>().color;
				component2.comment = (string)photonPlayer.CustomProperties["C"];
				component2.kill = (int)photonPlayer.CustomProperties["K"];
				component2.death = (int)photonPlayer.CustomProperties["D"];
				component2.id = photonPlayer.ID;
				gameObject.transform.SetParent(multiplayerList, false);
				if (photonPlayer.GetTeam() == PunTeams.Team.red)
				{
					gameObject.transform.SetAsFirstSibling();
				}
			}
			if (PhotonNetwork.room.PlayerCount >= PhotonNetwork.room.MaxPlayers)
			{
                // The existing master alone requests the handoff in OnPhotonPlayerConnected.
				StartCoroutine("Ready");
			}
		}

		private void OnPhotonPlayerConnected(PhotonPlayer newPlayer)
		{
            if (PhotonNetwork.isMasterClient && !CheckPeerModules(newPlayer)) return;
			if ((int)PhotonNetwork.room.CustomProperties["R"] == -1)
			{
				chat.GetChild(5).gameObject.SetActive(false);
				chat.GetChild(3).GetComponent<Button>().interactable = true;
				return;
			}
			Debug.Log("Someone joined!");
			byte[] data = (byte[])newPlayer.CustomProperties["I"];
			Texture2D texture2D = new Texture2D(128, 128);
			texture2D.LoadImage(data);
			GameObject gameObject = (GameObject)UnityEngine.Object.Instantiate(playerButton);
			if (rule == 1 || rule == 6 || rule == 8)
			{
				int index = (int)newPlayer.CustomProperties["TC"];
				gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(index)
					.GetComponent<Image>()
					.color;
			}
			else if (newPlayer.GetTeam() == PunTeams.Team.red)
			{
				gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(9)
					.GetComponent<Image>()
					.color;
			}
			else
			{
				gameObject.GetComponent<Image>().color = characterScreen.GetChild(1).GetChild(7)
					.GetComponent<Image>()
					.color;
			}
			gameObject.transform.GetChild(0).GetComponent<Image>().sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f));
			gameObject.transform.GetChild(1).GetComponent<Text>().text = newPlayer.NickName;
			DetailInformation component = gameObject.GetComponent<DetailInformation>();
			component.canvas = base.transform;
			component.backgroundColor = gameObject.GetComponent<Image>().color;
			component.comment = (string)newPlayer.CustomProperties["C"];
			component.kill = (int)newPlayer.CustomProperties["K"];
			component.death = (int)newPlayer.CustomProperties["D"];
			component.id = newPlayer.ID;
			gameObject.transform.SetParent(multiplayerList, false);
			if (newPlayer.GetTeam() == PunTeams.Team.red)
			{
				gameObject.transform.SetAsFirstSibling();
			}
			if (PhotonNetwork.room.PlayerCount >= PhotonNetwork.room.MaxPlayers)
			{
                if (PhotonNetwork.isMasterClient) PhotonNetwork.SetMasterClient(newPlayer);
				StartCoroutine("Ready");
			}
		}

		private void OnPhotonPlayerDisconnected(PhotonPlayer otherPlayer)
		{
			foreach (Transform multiplayer in multiplayerList)
			{
				if (multiplayer.GetComponent<DetailInformation>().id == otherPlayer.ID)
				{
					UnityEngine.Object.Destroy(multiplayer.gameObject);
				}
			}
			PunTeams.PlayersPerTeam[otherPlayer.GetTeam()].Remove(otherPlayer);
			otherPlayer.CustomProperties["team"] = (byte)PunTeams.Team.none;
		}

		private void OnPhotonCreateRoomFailed(object[] codeAndMsg)
		{
            if (LocalRoomFailed("Room creation failed. Try hosting a new room.")) return;
            pendingRoomDeadline = 0;
			pleaseWait.SetActive(false);
			errorMessage.SetActive(true);
			Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
			if (Input.GetJoystickNames().Length > 0)
			{
				component.Select();
			}
		}

		private void OnPhotonRandomJoinFailed(object[] codeAndMsg)
		{
			Debug.Log("There is no room that matches your conditions. Created a new room instead.");
			if (rule == 0)
			{
				rule = UnityEngine.Random.Range(1, 8);
			}
			if (objective == 0)
			{
				objective = 1;
			}
			if (playerCount == 0)
			{
				playerCount = 4;
			}
			roomTexts[0].text = ruleTitleText[rule];
			roomTexts[1].text = ruleExpText[rule];
			roomTexts[2].text = "Objective: " + objectiveText[rule + "-" + objective];
			roomTexts[3].text = "Player Count: " + playerCount;
			roomTexts[4].text = "Matchmaking... Wait or press Start Now.";
			ExitGames.Client.Photon.Hashtable hashtable = new ExitGames.Client.Photon.Hashtable();
			hashtable["R"] = rule;
			hashtable["O"] = objective;
            PublishRoomModules(hashtable);
			RoomOptions roomOptions = new RoomOptions();
			roomOptions.MaxPlayers = (byte)playerCount;
			roomOptions.CustomRoomProperties = hashtable;
			roomOptions.CustomRoomPropertiesForLobby = new string[3] { "R", "O", Flats.Modules.SessionModules.DigestProperty };
			string roomName = "pub-" + StringUtils.GeneratePassword(8);
			PhotonNetwork.CreateRoom(roomName, roomOptions, null);
		}

		private void OnPhotonJoinRoomFailed(object[] codeAndMsg)
		{
            if (LocalRoomFailed("Room unavailable. Check host, region, code and free slots.")) return;
            pendingRoomDeadline = 0;
            if (currentDetail == null) return;
            backButton.SetActive(true);
			if (currentDetail.name != "ChatRoom")
			{
				Debug.Log("There is no room that matches your room name.");
				pleaseWait.SetActive(false);
				rule = 1;
				objective = 1;
				playerCount = 4;
				roomCreation.transform.GetChild(0).GetChild(1).GetComponent<Text>()
					.text = ruleTitleText[rule];
				roomCreation.transform.GetChild(1).GetChild(1).GetComponent<Text>()
					.text = objectiveText[rule + "-" + objective];
				roomCreation.transform.GetChild(2).GetChild(1).GetComponent<Text>()
					.text = playerCount.ToString();
				SetRoomCreationVisible(true);
				Selectable component = roomCreation.transform.GetChild(3).GetComponent<Selectable>();
				if (Input.GetJoystickNames().Length > 0)
				{
					component.Select();
				}
			}
		}

		private void OnDisconnectedFromPhoton()
		{
            ResetMatchReadiness();
			if (gettingRoomList)
			{
				Debug.Log("I got the room list, disconnected from Photon.");
				gettingRoomList = false;
				return;
			}
			if (wasInRoom)
			{
				errorMessage.SetActive(true);
				Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
				if (Input.GetJoystickNames().Length > 0)
				{
					component.Select();
				}
			}
			if (network == 2)
			{
				network = 0;
			}
			startNowPlayer = 0;
			startNowPressed = false;
			wasInRoom = false;
			waitBackground = false;
			stayRoom.isOn = false;
			stayRoom.interactable = false;
			startNow.interactable = false;
            PhotonNetwork.player.CustomProperties["team"] = (byte)PunTeams.Team.none;
			if (multiplayerList.childCount > 0)
			{
				foreach (Transform multiplayer in multiplayerList)
				{
					UnityEngine.Object.Destroy(multiplayer.gameObject);
				}
			}
			if (gameState == "Multiplayer" && current != "Singleplayer" && current != "Result")
			{
				LoadOfflineScene(0);
			}
		}

		private void OnFailedToConnectToPhoton(DisconnectCause cause)
		{
            multiplayerFailure = "Photon connection failed: " + cause;
            if (multiplayerConnecting) return;
			if (!gettingRoomList)
			{
				errorMessage.SetActive(true);
				pleaseWait.SetActive(false);
				Selectable component = errorMessage.transform.GetChild(2).GetComponent<Selectable>();
				if (Input.GetJoystickNames().Length > 0)
				{
					component.Select();
				}
			}
		}
}
