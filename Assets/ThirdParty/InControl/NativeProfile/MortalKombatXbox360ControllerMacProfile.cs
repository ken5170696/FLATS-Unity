using System;

namespace InControl.NativeProfile
{
	public class MortalKombatXbox360ControllerMacProfile : Xbox360DriverMacProfile
	{
		public MortalKombatXbox360ControllerMacProfile()
		{
			base.Name = "Mortal Kombat Xbox 360 Controller";
			base.Meta = "Mortal Kombat Xbox 360 Controller on Mac";
			Matchers = new NativeInputDeviceMatcher[1]
			{
				new NativeInputDeviceMatcher
				{
					VendorID = 7085,
					ProductID = 63750
				}
			};
		}


	}
}
