using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Reign.Plugin.XML
{
	[XmlRoot("ClientResponse")]
	public class WebResponse
	{
		[XmlAttribute("Type")]
		public ResponseTypes Type;

		[XmlElement("ErrorMessage")]
		public string ErrorMessage;

		[XmlElement("ClientID")]
		public string ClientID;

		[XmlElement("UserID")]
		public string UserID;

		[XmlElement("Username")]
		public string Username;

		[XmlElement("Score")]
		public List<WebResponse_Score> Scores;

		[XmlElement("Achievement")]
		public List<WebResponse_Achievement> Achievements;

		[XmlElement("Games")]
		public List<WebResponse_Game> Games;

		public WebResponse()
		{
		}

		public WebResponse(ResponseTypes type)
		{
			Type = type;
		}


	}
}
