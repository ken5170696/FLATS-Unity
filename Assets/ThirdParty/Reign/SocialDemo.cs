using System;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class SocialDemo : MonoBehaviour
{
	public Button ShareButton;

	public Button BackButton;

	public Sprite ReignLogo;

	public GameObject BB10_ShareSelectorUI;

	public Text BB10_ShareSelectorTitle;

	public Button BB10_CloseButton;

	public Button BB10_ShareSelectorBBM;

	public Button BB10_ShareSelectorFacebook;

	public Button BB10_ShareSelectorTwitter;

	private void Start()
	{
		ShareButton.Select();
		ShareButton.onClick.AddListener(shareClicked);
		BackButton.onClick.AddListener(backClicked);
		SocialDesc socialDesc = new SocialDesc();
		socialDesc.BB10_ShareSelectorUI = BB10_ShareSelectorUI;
		socialDesc.BB10_ShareSelectorTitle = BB10_ShareSelectorTitle;
		socialDesc.BB10_CloseButton = BB10_CloseButton;
		socialDesc.BB10_ShareSelectorBBM = BB10_ShareSelectorBBM;
		socialDesc.BB10_ShareSelectorFacebook = BB10_ShareSelectorFacebook;
		socialDesc.BB10_ShareSelectorTwitter = BB10_ShareSelectorTwitter;
		SocialDesc desc = socialDesc;
		SocialManager.Init(desc);
	}

	private void shareClicked()
	{
		byte[] data = ReignLogo.texture.EncodeToPNG();
		SocialManager.Share(data, "ReignSocialImage", "Demo Text", "Reign Demo", "Reign Demo Desc", SocialShareDataTypes.Image_PNG);
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public SocialDemo()
	{
	}




}
