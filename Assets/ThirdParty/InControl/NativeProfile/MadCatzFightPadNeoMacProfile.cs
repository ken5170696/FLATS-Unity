using System;

namespace InControl.NativeProfile
{
	public class MadCatzFightPadNeoMacProfile : Xbox360DriverMacProfile
	{
		public MadCatzFightPadNeoMacProfile()
		{
			base.Name = "Mad Catz FightPad Neo";
			base.Meta = "Mad Catz FightPad Neo on Mac";
			Matchers = new NativeInputDeviceMatcher[1]
			{
				new NativeInputDeviceMatcher
				{
					VendorID = 7085,
					ProductID = 61498
				}
			};
		}


	}
}
