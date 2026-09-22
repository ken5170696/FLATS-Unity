using System;
using System.Xml.Serialization;

namespace Reign.Plugin.XML
{
	public class WebResponse_Game
	{
		[XmlElement("ID")]
		public string ID;

		[XmlElement("Name")]
		public string Name;

		public WebResponse_Game()
		{
		}


	}
}
