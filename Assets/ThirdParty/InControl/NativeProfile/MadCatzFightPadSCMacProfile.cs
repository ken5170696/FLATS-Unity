using System;

namespace InControl.NativeProfile
{
	public class MadCatzFightPadSCMacProfile : Xbox360DriverMacProfile
	{
		public MadCatzFightPadSCMacProfile()
		{
			base.Name = "Mad Catz FightPad SC";
			base.Meta = "Mad Catz FightPad SC on Mac";
			Matchers = new NativeInputDeviceMatcher[1]
			{
				new NativeInputDeviceMatcher
				{
					VendorID = 7085,
					ProductID = 61503
				}
			};
		}


	}
}
