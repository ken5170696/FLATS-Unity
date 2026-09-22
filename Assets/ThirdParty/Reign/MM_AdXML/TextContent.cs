using System;
using System.Xml.Serialization;

namespace Reign.MM_AdXML
{
	public class TextContent
	{
		[XmlText]
		public string Content;

		public TextContent()
		{
		}


	}
}
