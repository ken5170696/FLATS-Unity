using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
namespace FxProNS
{
	public class RenderTextureManager : IDisposable
	{
		private static RenderTextureManager instance;

		private List<RenderTexture> allRenderTextures;

		private List<RenderTexture> availableRenderTextures;

		public static RenderTextureManager Instance
		{
			get
			{
				return instance ?? (instance = new RenderTextureManager());
			}
		}

		public RenderTexture RequestRenderTexture(int _width, int _height, int _depth, RenderTextureFormat _format)
		{
			if (allRenderTextures == null)
			{
				allRenderTextures = new List<RenderTexture>();
			}
			if (availableRenderTextures == null)
			{
				availableRenderTextures = new List<RenderTexture>();
			}
			RenderTexture renderTexture = null;
			foreach (RenderTexture availableRenderTexture in availableRenderTextures)
			{
				if (!(null == availableRenderTexture) && availableRenderTexture.width == _width && availableRenderTexture.height == _height && availableRenderTexture.depth == _depth && availableRenderTexture.format == _format)
				{
					renderTexture = availableRenderTexture;
				}
			}
			if (null != renderTexture)
			{
				MakeRenderTextureNonAvailable(renderTexture);
				renderTexture.DiscardContents();
				return renderTexture;
			}
			renderTexture = CreateNewTexture(_width, _height, _depth, _format);
			MakeRenderTextureNonAvailable(renderTexture);
			return renderTexture;
		}

		public RenderTexture ReleaseRenderTexture(RenderTexture _tex)
		{
			if (null == _tex || availableRenderTextures == null)
			{
				return null;
			}
			if (availableRenderTextures.Contains(_tex))
			{
				return null;
			}
			availableRenderTextures.Add(_tex);
			return null;
		}

		public void SafeAssign(ref RenderTexture a, RenderTexture b)
		{
			if (!(a == b))
			{
				ReleaseRenderTexture(a);
				a = b;
			}
		}

		public void MakeRenderTextureNonAvailable(RenderTexture _tex)
		{
			if (availableRenderTextures.Contains(_tex))
			{
				availableRenderTextures.Remove(_tex);
			}
		}

		private RenderTexture CreateNewTexture(int _width, int _height, int _depth, RenderTextureFormat _format)
		{
			RenderTexture renderTexture = new RenderTexture(_width, _height, _depth, _format);
			renderTexture.Create();
			allRenderTextures.Add(renderTexture);
			availableRenderTextures.Add(renderTexture);
			return renderTexture;
		}

		public void PrintRenderTextureStats()
		{
			string text = "<color=blue>availableRenderTextures: </color>" + availableRenderTextures.Count + "\n";
			foreach (RenderTexture availableRenderTexture in availableRenderTextures)
			{
				text = text + "\t" + RenderTexToString(availableRenderTexture) + "\n";
			}
			Debug.Log(text);
			text = "<color=green>allRenderTextures:</color>" + allRenderTextures.Count + "\n";
			foreach (RenderTexture allRenderTexture in allRenderTextures)
			{
				text = text + "\t" + RenderTexToString(allRenderTexture) + "\n";
			}
			Debug.Log(text);
		}

		private string RenderTexToString(RenderTexture _rt)
		{
			if (null == _rt)
			{
				return "null";
			}
			return _rt.width + " x " + _rt.height + "\t" + _rt.depth + "\t" + _rt.format;
		}

		private void PrintRenderTexturesCount(string _prefix = "")
		{
			Debug.Log(_prefix + ": " + (allRenderTextures.Count - availableRenderTextures.Count) + "/" + allRenderTextures.Count);
		}

		public void ReleaseAllRenderTextures()
		{
			if (allRenderTextures == null)
			{
				return;
			}
			foreach (RenderTexture allRenderTexture in allRenderTextures)
			{
				if (!availableRenderTextures.Contains(allRenderTexture))
				{
					ReleaseRenderTexture(allRenderTexture);
				}
			}
		}

		public void PrintBalance()
		{
			Debug.Log("RenderTextures balance: " + (allRenderTextures.Count - availableRenderTextures.Count) + "/" + allRenderTextures.Count);
		}

		public void Dispose()
		{
			if (allRenderTextures != null)
			{
				foreach (RenderTexture allRenderTexture in allRenderTextures)
				{
					allRenderTexture.Release();
				}
				allRenderTextures.Clear();
			}
			if (availableRenderTextures != null)
			{
				availableRenderTextures.Clear();
			}
		}

		public RenderTextureManager()
		{
		}




	}
}
