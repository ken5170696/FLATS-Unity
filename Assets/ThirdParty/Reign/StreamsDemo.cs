using System;
using System.IO;
using Reign;
using UnityEngine;
using UnityEngine.UI;
public class StreamsDemo : MonoBehaviour
{
	public Sprite ReignLogo;

	public Sprite ReignLogo2;

	public Image ReignImage;

	public Button BackButton;

	public Button SaveFileButton;

	public Button LoadFileButton;

	public Button SaveToPicturesButton;

	public Button LoadFromPicturesButton;

	public Button CameraPickerButton;

	public Button ImagePickerButton;

	public Button SaveImageDlgButton;

	private void Start()
	{
		SaveFileButton.Select();
		BackButton.onClick.AddListener(backClicked);
		SaveFileButton.onClick.AddListener(saveFileClicked);
		LoadFileButton.onClick.AddListener(loadFileClicked);
		SaveToPicturesButton.onClick.AddListener(saveToPicturesClicked);
		LoadFromPicturesButton.onClick.AddListener(loadFromPicturesClicked);
		CameraPickerButton.onClick.AddListener(cameraPickerClicked);
		ImagePickerButton.onClick.AddListener(imagePickerClicked);
		SaveImageDlgButton.onClick.AddListener(saveImageDlgClicked);
	}

	private void saveImageDlgClicked()
	{
		byte[] data = ReignLogo.texture.EncodeToPNG();
		StreamManager.SaveFileDialog(data, FolderLocations.Pictures, new string[1] { ".png" }, imageSavedCallback);
	}

	private void imagePickerClicked()
	{
		StreamManager.LoadFileDialog(FolderLocations.Pictures, 128, 128, new string[3] { ".png", ".jpg", ".jpeg" }, imageLoadedCallback);
	}

	private void cameraPickerClicked()
	{
		StreamManager.LoadCameraPicker(CameraQuality.Med, 128, 128, imageLoadedCallback);
	}

	private void loadFromPicturesClicked()
	{
		disableButtons();
		StreamManager.LoadFile("TEST.png", FolderLocations.Pictures, imageLoadedCallback);
	}

	private void saveToPicturesClicked()
	{
		disableButtons();
		byte[] data = ReignLogo.texture.EncodeToPNG();
		StreamManager.SaveFile("TEST.png", data, FolderLocations.Pictures, imageSavedCallback);
	}

	private void loadFileClicked()
	{
		disableButtons();
		StreamManager.LoadFile("MyFile.data", FolderLocations.Storage, dataFileLoadedCallback);
	}

	private void saveFileClicked()
	{
		disableButtons();
		byte[] data = new byte[1] { (byte)UnityEngine.Random.Range(0, 255) };
		StreamManager.SaveFile("MyFile.data", data, FolderLocations.Storage, dataFileSavedCallback);
	}

	private void backClicked()
	{
		Application.LoadLevel("MainDemo");
	}

	private void disableButtons()
	{
		BackButton.enabled = false;
		SaveFileButton.enabled = false;
		LoadFileButton.enabled = false;
		SaveToPicturesButton.enabled = false;
		LoadFromPicturesButton.enabled = false;
		CameraPickerButton.enabled = false;
		ImagePickerButton.enabled = false;
		SaveImageDlgButton.enabled = false;
	}

	private void enableButtons()
	{
		BackButton.enabled = true;
		SaveFileButton.enabled = true;
		LoadFileButton.enabled = true;
		SaveToPicturesButton.enabled = true;
		LoadFromPicturesButton.enabled = true;
		CameraPickerButton.enabled = true;
		ImagePickerButton.enabled = true;
		SaveImageDlgButton.enabled = true;
	}

	private void dataFileSavedCallback(bool succeeded)
	{
		enableButtons();
		MessageBoxManager.Show("Data Status", "Data Saved: " + succeeded);
	}

	private void dataFileLoadedCallback(Stream stream, bool succeeded)
	{
		try
		{
			enableButtons();
			MessageBoxManager.Show("Image Status", "Data Loaded: " + succeeded);
			if (succeeded)
			{
				Debug.Log("Data Value: " + stream.ReadByte());
			}
		}
		catch (Exception ex)
		{
			MessageBoxManager.Show("Error", ex.Message);
		}
		finally
		{
			if (stream != null)
			{
				stream.Dispose();
			}
		}
	}

	private void imageSavedCallback(bool succeeded)
	{
		enableButtons();
		MessageBoxManager.Show("Image Status", "Image Saved: " + succeeded);
		if (succeeded)
		{
			ReignImage.sprite = ReignLogo2;
		}
	}

	private void imageLoadedCallback(Stream stream, bool succeeded)
	{
		enableButtons();
		MessageBoxManager.Show("Image Status", "Image Loaded: " + succeeded);
		if (!succeeded)
		{
			if (stream != null)
			{
				stream.Dispose();
			}
			return;
		}
		try
		{
			byte[] array = new byte[stream.Length];
			stream.Read(array, 0, array.Length);
			Texture2D texture2D = new Texture2D(4, 4);
			texture2D.LoadImage(array);
			texture2D.Apply();
			ReignImage.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
		}
		catch (Exception ex)
		{
			MessageBoxManager.Show("Error", ex.Message);
		}
		finally
		{
			if (stream != null)
			{
				stream.Dispose();
			}
		}
	}

	private void Update()
	{
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Application.Quit();
		}
	}

	public StreamsDemo()
	{
	}




}
