using System;
using System.Xml.Serialization;

namespace Reign.Plugin.XML
{
	public class WebResponse_Achievement
	{
		[XmlElement("ID")]
		public string ID;

		[XmlElement("AchievementID")]
		public string AchievementID;

		[XmlElement("PercentComplete")]
		public float PercentComplete;

		public WebResponse_Achievement()
		{
		}


	}
}
