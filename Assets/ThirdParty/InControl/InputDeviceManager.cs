using System;
using System.Collections.Generic;
namespace InControl
{
	public abstract class InputDeviceManager
	{
		protected List<InputDevice> devices;

		public abstract void Update(ulong updateTick, float deltaTime);

		public virtual void Destroy()
		{
		}

		protected InputDeviceManager()
		{
			devices = new List<InputDevice>();

		}




	}
}
