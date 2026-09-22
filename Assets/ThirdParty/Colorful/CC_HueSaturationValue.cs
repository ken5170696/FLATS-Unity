using System;
using UnityEngine;
[AddComponentMenu("Colorful/Hue, Saturation, Value")]
[ExecuteInEditMode]
public class CC_HueSaturationValue : CC_Base
{
	[Range(-180f, 180f)]
	public float masterHue;

	[Range(-100f, 100f)]
	public float masterSaturation;

	[Range(-100f, 100f)]
	public float masterValue;

	[Range(-180f, 180f)]
	public float redsHue;

	[Range(-100f, 100f)]
	public float redsSaturation;

	[Range(-100f, 100f)]
	public float redsValue;

	[Range(-180f, 180f)]
	public float yellowsHue;

	[Range(-100f, 100f)]
	public float yellowsSaturation;

	[Range(-100f, 100f)]
	public float yellowsValue;

	[Range(-180f, 180f)]
	public float greensHue;

	[Range(-100f, 100f)]
	public float greensSaturation;

	[Range(-100f, 100f)]
	public float greensValue;

	[Range(-180f, 180f)]
	public float cyansHue;

	[Range(-100f, 100f)]
	public float cyansSaturation;

	[Range(-100f, 100f)]
	public float cyansValue;

	[Range(-180f, 180f)]
	public float bluesHue;

	[Range(-100f, 100f)]
	public float bluesSaturation;

	[Range(-100f, 100f)]
	public float bluesValue;

	[Range(-180f, 180f)]
	public float magentasHue;

	[Range(-100f, 100f)]
	public float magentasSaturation;

	[Range(-100f, 100f)]
	public float magentasValue;

	public bool advanced;

	public int currentChannel;

	public float hue
	{
		get
		{
			return masterHue;
		}
		set
		{
			masterHue = value;
		}
	}

	public float saturation
	{
		get
		{
			return masterSaturation;
		}
		set
		{
			masterSaturation = value;
		}
	}

	public float value
	{
		get
		{
			return masterValue;
		}
		set
		{
			masterValue = value;
		}
	}

	[ImageEffectTransformsToLDR]
	protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		base.material.SetVector("_Master", new Vector3(masterHue / 360f, masterSaturation * 0.01f, masterValue * 0.01f));
		if (advanced)
		{
			base.material.SetVector("_Reds", new Vector3(redsHue / 360f, redsSaturation * 0.01f, redsValue * 0.01f));
			base.material.SetVector("_Yellows", new Vector3(yellowsHue / 360f, yellowsSaturation * 0.01f, yellowsValue * 0.01f));
			base.material.SetVector("_Greens", new Vector3(greensHue / 360f, greensSaturation * 0.01f, greensValue * 0.01f));
			base.material.SetVector("_Cyans", new Vector3(cyansHue / 360f, cyansSaturation * 0.01f, cyansValue * 0.01f));
			base.material.SetVector("_Blues", new Vector3(bluesHue / 360f, bluesSaturation * 0.01f, bluesValue * 0.01f));
			base.material.SetVector("_Magentas", new Vector3(magentasHue / 360f, magentasSaturation * 0.01f, magentasValue * 0.01f));
			Graphics.Blit(source, destination, base.material, 1);
		}
		else
		{
			Graphics.Blit(source, destination, base.material, 0);
		}
	}

	public CC_HueSaturationValue()
	{
	}




}
