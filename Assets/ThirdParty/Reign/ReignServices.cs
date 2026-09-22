using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ReignServices : MonoBehaviour
{
	public delegate void FrameDoneCallbackMethod();

	public delegate void CaptureScreenShotCallbackMethod(byte[] data);

	public delegate void ScreenSizeChangedCallbackMethod(int oldWidth, int oldHeight, int newWidth, int newHeight);

	public delegate void ServiceMethod();

	private bool canDestroy;

	private static FrameDoneCallbackMethod frameDoneCallback;

	private static bool requestingFrame;

	private static CaptureScreenShotCallbackMethod captureScreenShotCallback;

	private static int lastScreenWidth;

	private static int lastScreenHeight;

	private static List<ServiceMethod>[] invokeOnUnityThreadCallbacks;

	private static int invokeSwap;

	public static ReignServices Singleton { get; private set; }

	public static event ScreenSizeChangedCallbackMethod ScreenSizeChangedCallback;

	private static event ServiceMethod updateService;

	private static event ServiceMethod onguiService;

	private static event ServiceMethod destroyService;

	public static void AddService(ServiceMethod update, ServiceMethod onGUI, ServiceMethod destroy)
	{
		if (update != null)
		{
			updateService -= update;
			updateService += update;
		}
		if (onGUI != null)
		{
			onguiService -= onGUI;
			onguiService += onGUI;
		}
		if (destroy != null)
		{
			destroyService -= destroy;
			destroyService += destroy;
		}
	}

	public static void RemoveService(ServiceMethod update, ServiceMethod onGUI, ServiceMethod destroy)
	{
		if (update != null)
		{
			updateService -= update;
		}
		if (onGUI != null)
		{
			onguiService -= onGUI;
		}
		if (destroy != null)
		{
			destroyService -= destroy;
		}
	}

	public static void InvokeOnUnityThread(ServiceMethod callback)
	{
		lock (Singleton)
		{
			invokeOnUnityThreadCallbacks[invokeSwap].Add(callback);
		}
	}

	public static void RequestEndOfFrame(FrameDoneCallbackMethod frameDoneCallback)
	{
		if (ReignServices.frameDoneCallback != null)
		{
			Debug.LogError("You must wait until RequestEndOfFrame has finished!");
			return;
		}
		ReignServices.frameDoneCallback = frameDoneCallback;
		requestingFrame = true;
	}

	public static void CaptureScreenShot(CaptureScreenShotCallbackMethod captureScreenShotCallback)
	{
		if (ReignServices.captureScreenShotCallback != null)
		{
			Debug.LogError("You must wait until CaptureScreenShot has finished!");
			return;
		}
		ReignServices.captureScreenShotCallback = captureScreenShotCallback;
		RequestEndOfFrame(captureScreenShotEndOfFrame);
	}

	private static void captureScreenShotEndOfFrame()
	{
		int width = Screen.width;
		int height = Screen.height;
		Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
		texture2D.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
		texture2D.Apply();
		byte[] data = texture2D.EncodeToPNG();
		UnityEngine.Object.Destroy(texture2D);
		if (captureScreenShotCallback != null)
		{
			captureScreenShotCallback(data);
		}
		captureScreenShotCallback = null;
	}

	static ReignServices()
	{
		lastScreenWidth = Screen.width;
		lastScreenHeight = Screen.height;
		invokeOnUnityThreadCallbacks = new List<ServiceMethod>[2];
		invokeOnUnityThreadCallbacks[0] = new List<ServiceMethod>();
		invokeOnUnityThreadCallbacks[1] = new List<ServiceMethod>();
	}

	public static void CheckStatus()
	{
		if (Singleton == null)
		{
			Debug.LogError("ReignServices Prefab or Script does NOT exist in your scene!");
		}
	}

	private void Awake()
	{
		if (Singleton != null)
		{
			canDestroy = false;
			UnityEngine.Object.Destroy(base.gameObject);
		}
		else
		{
			canDestroy = true;
			Singleton = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
	}

	private void OnDestroy()
	{
		if (canDestroy)
		{
			PlayerPrefs.Save();
			if (ReignServices.destroyService != null)
			{
				ReignServices.destroyService();
			}
			Singleton = null;
			ReignServices.updateService = null;
			ReignServices.onguiService = null;
			ReignServices.destroyService = null;
		}
	}

	private void Update()
	{
		if (ReignServices.updateService != null)
		{
			ReignServices.updateService();
		}
		if (requestingFrame)
		{
			requestingFrame = false;
			StartCoroutine(frameSync());
		}
		int width = Screen.width;
		int height = Screen.height;
		if ((width != lastScreenWidth || height != lastScreenHeight) && ReignServices.ScreenSizeChangedCallback != null)
		{
			ReignServices.ScreenSizeChangedCallback(lastScreenWidth, lastScreenHeight, width, height);
		}
		lastScreenWidth = Screen.width;
		lastScreenHeight = Screen.height;
		if (invokeOnUnityThreadCallbacks == null || invokeOnUnityThreadCallbacks[invokeSwap].Count == 0)
		{
			return;
		}
		invokeSwap = 1 - invokeSwap;
		foreach (ServiceMethod item in invokeOnUnityThreadCallbacks[1 - invokeSwap])
		{
			item();
		}
		invokeOnUnityThreadCallbacks[1 - invokeSwap].Clear();
	}

	private IEnumerator frameSync()
	{
		yield return new WaitForEndOfFrame();
		if (frameDoneCallback != null)
		{
			frameDoneCallback();
		}
		frameDoneCallback = null;
	}

	private void OnGUI()
	{
		if (ReignServices.onguiService != null)
		{
			ReignServices.onguiService();
		}
	}

	public ReignServices()
	{
	}




}
