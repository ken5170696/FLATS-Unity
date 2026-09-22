using System;
using System.Xml.Serialization;

namespace Reign.Plugin.XML
{
	public class WebResponse_Score
	{
		[XmlElement("ID")]
		public string ID;

		[XmlElement("UserID")]
		public string UserID;

		[XmlElement("Username")]
		public string Username;

		[XmlElement("Score")]
		public long Score;

		public WebResponse_Score()
		{
		}


	}
}
