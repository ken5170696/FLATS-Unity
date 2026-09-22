#if !UNITY_5_3_OR_NEWER
using System;
using System.Runtime.InteropServices;

namespace UnityEngine.SceneManagement
{
	public class SceneManager
	{
		public static void LoadScene(string name)
		{
			Application.LoadLevel(name);
		}

		public static void LoadScene(int buildIndex)
		{
			Application.LoadLevel(buildIndex);
		}

		public SceneManager()
		{
		}




	}
}

#endif
