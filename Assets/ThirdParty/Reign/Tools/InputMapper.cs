using System;
using UnityEngine;
using UnityEngine.UI;
namespace Reign.Tools
{
	public class InputMapper : MonoBehaviour
	{
		public Text InputText;

		private void Update()
		{
			for (int i = 0; i != 20; i++)
			{
				float axis = Input.GetAxis("Axis" + (i + 1));
				if (Mathf.Abs(axis) >= 0.5f)
				{
					InputText.text = string.Format("Axis {0} of value {1}", new object[2]
					{
						i + 1,
						axis
					});
				}
			}
			for (int j = 0; j != 430; j++)
			{
				if (Input.GetKeyDown((KeyCode)j))
				{
					InputText.text = string.Format("Key/Button pressed {0}", new object[1] { (KeyCode)j });
				}
			}
		}

		public InputMapper()
		{
		}




	}
}
