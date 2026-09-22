using System;
using System.Xml.Serialization;

namespace Reign.MM_AdXML
{
	[XmlRoot("ad")]
	public class Ad
	{
		[XmlElement("bodyType")]
		public TextContent bodyType;

		[XmlElement("clickUrl")]
		public TextContent clickUrl;

		[XmlElement("image")]
		public Image image;

		[XmlElement("text")]
		public TextContent text;

		public Ad()
		{
		}


	}
}
