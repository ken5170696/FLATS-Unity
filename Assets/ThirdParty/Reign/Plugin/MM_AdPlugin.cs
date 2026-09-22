using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Reign;
using System.Xml.Serialization;
using Reign.MM_AdXML;
using UnityEngine;
using UnityEngine.UI;
namespace Reign.Plugin
{
	public class MM_AdPlugin : IAdPlugin
	{
		private bool visible;

		private AdDesc desc;

		private GameObject adCanvas;

		private RectTransform adRect;

		private UnityEngine.UI.Image adImage;

		private Texture2D guiTexture;

		private Rect guiRect;

		private float uiScale;

		private bool testing;

		private AdEventCallbackMethod adEvent;

		private int refreshRate;

		private float refreshRateTic;

		private string deviceID;

		private string externalIP;

		private string apid;

		private string userAgent;

		private TextureGIF gifImage;

		private MonoBehaviour service;

		private Reign.MM_AdXML.Ad adMeta;

		private AdGravity gravity;

		private GUIStyle guiSytle;

		public bool Visible
		{
			get
			{
				return visible;
			}
			set
			{
				visible = value;
				if (!desc.UseClassicGUI)
				{
					adCanvas.SetActive(value);
				}
			}
		}

		public MM_AdPlugin(AdDesc desc, AdCreatedCallbackMethod createdCallback, MonoBehaviour service)
		{
			this.service = service;
			try
			{
				this.desc = desc;
				testing = desc.Testing;
				adEvent = desc.EventCallback;
				gravity = AdGravity.CenterScreen;
				refreshRate = desc.WinRT_MillennialMediaAdvertising_RefreshRate;
				apid = desc.WinRT_MillennialMediaAdvertising_APID;
				userAgent = "";
				gravity = desc.WinRT_MillennialMediaAdvertising_AdGravity;
				uiScale = desc.WinRT_AdScale;
				if (refreshRate < 60)
				{
					refreshRate = 60;
				}
				if (PlayerPrefs.HasKey("Reign_MMWebAds_DeviceID"))
				{
					deviceID = PlayerPrefs.GetString("Reign_MMWebAds_DeviceID");
				}
				else
				{
					deviceID = Guid.NewGuid().ToString().Replace("-", "0")
						.ToLower() + "0000";
					PlayerPrefs.SetString("Reign_MMWebAds_DeviceID", deviceID);
				}
				if (desc.UseClassicGUI)
				{
					guiSytle = new GUIStyle
					{
						stretchWidth = true,
						stretchHeight = true
					};
				}
				else
				{
					adCanvas = new GameObject("MM Ad");
					UnityEngine.Object.DontDestroyOnLoad(adCanvas);
					adCanvas.AddComponent<RectTransform>();
					Canvas canvas = adCanvas.AddComponent<Canvas>();
					canvas.renderMode = RenderMode.ScreenSpaceOverlay;
					canvas.sortingOrder = 1000;
					adCanvas.AddComponent<CanvasScaler>();
					adCanvas.AddComponent<GraphicRaycaster>();
					GameObject gameObject = new GameObject("AdButtonImage");
					gameObject.transform.parent = adCanvas.transform;
					adRect = gameObject.AddComponent<RectTransform>();
					adImage = gameObject.AddComponent<UnityEngine.UI.Image>();
					adImage.sprite = Resources.Load<Sprite>("Reign/Ads/AdLoading");
					adImage.preserveAspect = true;
					Button button = gameObject.AddComponent<Button>();
					button.onClick.AddListener(adClicked);
				}
				Visible = desc.Visible;
				SetGravity(gravity);
				service.StartCoroutine(init(createdCallback));
				ReignServices.ScreenSizeChangedCallback += ReignServices_ScreenSizeChangedCallback;
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.Message);
				if (createdCallback != null)
				{
					createdCallback(false);
				}
			}
		}

		private void ReignServices_ScreenSizeChangedCallback(int oldWidth, int oldHeight, int newWidth, int newHeight)
		{
			SetGravity(gravity);
		}

		private void adClicked()
		{
			if (testing)
			{
				Debug.Log("Ad Clicked!");
				if (gifImage != null)
				{
					Application.OpenURL("http://www.millennialmedia.com/");
				}
			}
			else
			{
				Debug.Log("Opening Ad at URL: " + adMeta.clickUrl.Content);
				if (adMeta != null && adMeta.clickUrl != null && !string.IsNullOrEmpty(adMeta.clickUrl.Content))
				{
					Application.OpenURL(adMeta.clickUrl.Content);
				}
				Debug.Log("Ad Clicked!");
			}
			if (adEvent != null)
			{
				adEvent(AdEvents.Clicked, null);
			}
		}

		private IEnumerator init(AdCreatedCallbackMethod createdCallback)
		{
			if (testing)
			{
				WWW www = new WWW("http://media.mydas.mobi/images/rich/T/test_mm/collapsed.gif");
				yield return www;
				byte[] data = www.bytes;
				if (data == null || data.Length == 0)
				{
					Debug.LogError("Test Ad failed to loadb");
					if (createdCallback != null)
					{
						createdCallback(false);
					}
					yield break;
				}
				gifImage = new TextureGIF(data, frameUpdatedCallback);
				if (desc.UseClassicGUI)
				{
					guiTexture = gifImage.CurrentFrame.Texture;
				}
				else
				{
					adImage.sprite = gifImage.CurrentFrame.Sprite;
				}
				SetGravity(gravity);
				Texture2D texture = gifImage.CurrentFrame.Texture;
				Debug.Log(string.Format("Ad Image Size: {0}x{1}", new object[2] { texture.width, texture.height }));
				if (createdCallback != null)
				{
					createdCallback(true);
				}
				if (adEvent != null)
				{
					adEvent(AdEvents.Refreshed, null);
				}
				yield break;
			}
			WWW ipWWW = new WWW("http://checkip.dyndns.org/");
			yield return ipWWW;
			Match match = Regex.Match(ipWWW.text, "Current IP Address\\: (\\d*\\.\\d*\\.\\d*\\.\\d*)");
			if (!match.Success)
			{
				if (createdCallback != null)
				{
					createdCallback(false);
				}
			}
			else
			{
				externalIP = match.Groups[1].Value;
				Debug.Log("External IP: " + externalIP);
				service.StartCoroutine(asyncRefresh(createdCallback));
			}
		}

		private void frameUpdatedCallback(TextureGIFFrame frame)
		{
			if (desc.UseClassicGUI)
			{
				guiTexture = frame.Texture;
			}
			else
			{
				adImage.sprite = frame.Sprite;
			}
		}

		public void Dispose()
		{
			ReignServices.ScreenSizeChangedCallback -= ReignServices_ScreenSizeChangedCallback;
			if (gifImage != null)
			{
				gifImage.Dispose();
				gifImage = null;
			}
		}

		public void SetGravity(AdGravity gravity)
		{
			if (desc.UseClassicGUI)
			{
				if (!(guiTexture == null))
				{
					float num = Screen.width;
					float num2 = Screen.height;
					float num3 = new Vector2(num, num2).magnitude / new Vector2(1280f, 720f).magnitude * uiScale;
					float num4 = (float)guiTexture.width * num3;
					float num5 = (float)guiTexture.height * num3;
					switch (gravity)
					{
					case AdGravity.CenterScreen:
						guiRect = new Rect(num / 2f - num4 / 2f, num2 / 2f - num5 / 2f, num4, num5);
						break;
					case AdGravity.BottomCenter:
						guiRect = new Rect(num / 2f - num4 / 2f, num2 - num5, num4, num5);
						break;
					case AdGravity.BottomLeft:
						guiRect = new Rect(0f, num2 - num5, num4, num5);
						break;
					case AdGravity.BottomRight:
						guiRect = new Rect(num - num4, num2 - num5, num4, num5);
						break;
					case AdGravity.TopCenter:
						guiRect = new Rect(num / 2f - num4 / 2f, 0f, num4, num5);
						break;
					case AdGravity.TopLeft:
						guiRect = new Rect(0f, 0f, num4, num5);
						break;
					case AdGravity.TopRight:
						guiRect = new Rect(num - num4, 0f, num4, num5);
						break;
					default:
						Debug.LogError("Unsuported Gravity: " + gravity);
						break;
					}
				}
			}
			else if (!(adImage.sprite == null))
			{
				float num6 = Screen.width;
				float num7 = Screen.height;
				float num8 = new Vector2(num6, num7).magnitude / new Vector2(1280f, 720f).magnitude * uiScale;
				Texture2D texture = adImage.sprite.texture;
				float num9 = (float)texture.width / num6 * num8;
				float num10 = (float)texture.height / num7 * num8;
				switch (gravity)
				{
				case AdGravity.CenterScreen:
					adRect.anchorMin = new Vector2(0f - num9 * 0.5f + 0.5f, 0f - num10 * 0.5f + 0.5f);
					adRect.anchorMax = new Vector2(0f - num9 * 0.5f + 0.5f + num9, 0f - num10 * 0.5f + 0.5f + num10);
					break;
				case AdGravity.BottomCenter:
					adRect.anchorMin = new Vector2(0f - num9 * 0.5f + 0.5f, 0f);
					adRect.anchorMax = new Vector2(0f - num9 * 0.5f + 0.5f + num9, num10);
					break;
				case AdGravity.BottomLeft:
					adRect.anchorMin = new Vector2(0f, 0f);
					adRect.anchorMax = new Vector2(num9, num10);
					break;
				case AdGravity.BottomRight:
					adRect.anchorMin = new Vector2(1f - num9, 0f);
					adRect.anchorMax = new Vector2(1f, num10);
					break;
				case AdGravity.TopCenter:
					adRect.anchorMin = new Vector2(0f - num9 * 0.5f + 0.5f, 1f - num10);
					adRect.anchorMax = new Vector2(0f - num9 * 0.5f + 0.5f + num9, 1f);
					break;
				case AdGravity.TopLeft:
					adRect.anchorMin = new Vector2(0f, 1f - num10);
					adRect.anchorMax = new Vector2(num9, 1f);
					break;
				case AdGravity.TopRight:
					adRect.anchorMin = new Vector2(1f - num9, 1f - num10);
					adRect.anchorMax = new Vector2(1f, 1f);
					break;
				default:
					Debug.LogError("Unsuported Gravity: " + gravity);
					break;
				}
				adRect.offsetMin = Vector2.zero;
				adRect.offsetMax = Vector2.zero;
			}
		}

		public void Refresh()
		{
			Debug.Log("Refreshing Ad");
			if (!testing)
			{
				service.StartCoroutine(asyncRefresh(null));
			}
			else if (adEvent != null)
			{
				adEvent(AdEvents.Refreshed, null);
			}
		}

		private IEnumerator asyncRefresh(AdCreatedCallbackMethod createdCallback)
		{
			if (!Visible)
			{
				yield break;
			}
			string url = "http://ads.mp.mydas.mobi/getAd?";
			url = url + "&apid=" + apid;
			url = url + "&auid=" + deviceID;
			url = url + "&ua=" + userAgent;
			url = url + "&uip=" + externalIP;
			Debug.Log("Ad Request URL: " + url);
			WWW www = new WWW(url);
			yield return www;
			if (!string.IsNullOrEmpty(www.error))
			{
				Debug.LogError(www.error);
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				else if (adEvent != null)
				{
					adEvent(AdEvents.Error, www.error);
				}
				yield break;
			}
			if (www.text.Contains("\"error\""))
			{
				Debug.LogError(www.text);
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				else if (adEvent != null)
				{
					adEvent(AdEvents.Error, www.text);
				}
				yield break;
			}
			if (string.IsNullOrEmpty(www.text))
			{
				string text = "Invalid server responce! No data!";
				Debug.LogError(text);
				if (createdCallback != null)
				{
					createdCallback(false);
				}
				else if (adEvent != null)
				{
					adEvent(AdEvents.Error, text);
				}
				yield break;
			}
			try
			{
				XmlSerializer xmlSerializer = new XmlSerializer(typeof(Reign.MM_AdXML.Ad));
				string s = System.Text.Reign.Encoding.Singleton.GetString(www.bytes, 0, www.bytes.Length).Replace("[:_mm_campaignid:]&[:_mm_advertiserid:]?", "");
				using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(s)))
				{
					adMeta = (Reign.MM_AdXML.Ad)xmlSerializer.Deserialize(stream);
				}
			}
			catch (Exception ex)
			{
				Debug.LogError(ex.Message);
				Debug.LogError("Responce Text: " + www.text);
				if (adEvent != null)
				{
					adEvent(AdEvents.Error, ex.Message);
				}
				yield break;
			}
			string imageURL = adMeta.image.url.Content;
			Debug.Log("MMWeb Ad ImageURL: " + imageURL);
			www = new WWW(imageURL);
			yield return www;
			if (gifImage != null)
			{
				gifImage.Dispose();
				gifImage = null;
			}
			gifImage = new TextureGIF(www.bytes, frameUpdatedCallback);
			if (desc.UseClassicGUI)
			{
				guiTexture = gifImage.CurrentFrame.Texture;
			}
			else
			{
				adImage.sprite = gifImage.CurrentFrame.Sprite;
			}
			Texture2D texture = gifImage.CurrentFrame.Texture;
			Debug.Log(string.Format("Ad Image Size: {0}x{1}", new object[2] { texture.width, texture.height }));
			SetGravity(gravity);
			if (adEvent != null)
			{
				adEvent(AdEvents.Refreshed, null);
			}
		}

		public void Update()
		{
			if (gifImage != null)
			{
				gifImage.Update();
			}
			refreshRateTic += Time.deltaTime;
			if (refreshRateTic >= (float)refreshRate)
			{
				refreshRateTic = 0f;
				Refresh();
			}
		}

		public void OnGUI()
		{
			if (!desc.GUIOverrideEnabled)
			{
				onGUI();
			}
		}

		public void OverrideOnGUI()
		{
			if (desc.GUIOverrideEnabled)
			{
				onGUI();
			}
		}

		private void onGUI()
		{
			if (desc.UseClassicGUI && visible && guiTexture != null)
			{
				GUI.DrawTexture(guiRect, guiTexture, ScaleMode.StretchToFill);
				if (GUI.Button(guiRect, "", guiSytle))
				{
					adClicked();
				}
			}
		}




	}
}
