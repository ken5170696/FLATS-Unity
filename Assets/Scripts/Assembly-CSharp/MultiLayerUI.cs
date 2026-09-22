using System;
using UnityEngine;
public class MultiLayerUI : MonoBehaviour
{
	public void SetAutoSelect(bool value)
	{
		EasyTouch.SetEnableAutoSelect(value);
	}

	public void SetAutoUpdate(bool value)
	{
		EasyTouch.SetAutoUpdatePickedObject(value);
	}

	public void Layer1(bool value)
	{
		LayerMask layerMask = EasyTouch.Get3DPickableLayer();
		if (value)
		{
			layerMask = (int)layerMask | 0x100;
		}
		else
		{
			layerMask = ~(int)layerMask;
			layerMask = ~((int)layerMask | 0x100);
		}
		EasyTouch.Set3DPickableLayer(layerMask);
	}

	public void Layer2(bool value)
	{
		LayerMask layerMask = EasyTouch.Get3DPickableLayer();
		if (value)
		{
			layerMask = (int)layerMask | 0x200;
		}
		else
		{
			layerMask = ~(int)layerMask;
			layerMask = ~((int)layerMask | 0x200);
		}
		EasyTouch.Set3DPickableLayer(layerMask);
	}

	public void Layer3(bool value)
	{
		LayerMask layerMask = EasyTouch.Get3DPickableLayer();
		if (value)
		{
			layerMask = (int)layerMask | 0x400;
		}
		else
		{
			layerMask = ~(int)layerMask;
			layerMask = ~((int)layerMask | 0x400);
		}
		EasyTouch.Set3DPickableLayer(layerMask);
	}

	public MultiLayerUI()
	{
	}




}
