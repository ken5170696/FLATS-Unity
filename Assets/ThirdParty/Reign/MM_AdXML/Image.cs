using System;
using System.Xml.Serialization;

namespace Reign.MM_AdXML
{
	public class Image
	{
		[XmlElement("url")]
		public TextContent url;

		[XmlElement("mime_type")]
		public TextContent mime_type;

		[XmlElement("height")]
		public TextContent height;

		[XmlElement("width")]
		public TextContent width;

		[XmlElement("altText")]
		public TextContent altText;

		public Image()
		{
		}


	}
}
