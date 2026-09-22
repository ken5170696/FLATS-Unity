using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Reign.Plugin.XML;
using UnityEngine;
namespace Reign.Plugin
{
	internal class ReignScores_ServicesHelper
	{
		public delegate void CallbackMethod(bool succeeded, WebResponse response);

		private string reignScoresURL;

		private string userAPIKey;

		private string gameAPIKey;

		public ReignScores_ServicesHelper(ScoreDesc desc)
		{
			reignScoresURL = desc.ReignScores_ServicesURL;
			gameAPIKey = desc.ReignScores_GameKey;
			userAPIKey = desc.ReignScores_UserKey;
		}

		private string convertType(ReignScores_ServiceTypes type)
		{
			switch (type)
			{
			case ReignScores_ServiceTypes.Games:
				return "Games";
			case ReignScores_ServiceTypes.Users:
				return "Users";
			default:
				throw new Exception("Invalid type: " + type);
			}
		}

		private string convertAPI_Key(ReignScores_ServiceTypes type)
		{
			switch (type)
			{
			case ReignScores_ServiceTypes.Games:
				return gameAPIKey;
			case ReignScores_ServiceTypes.Users:
				return userAPIKey;
			default:
				throw new Exception("Invalid type: " + type);
			}
		}

		private WebResponse generateResponse(string response)
		{
			try
			{
				XmlSerializer xmlSerializer = new XmlSerializer(typeof(WebResponse));
				using (StringReader textReader = new StringReader(response))
				{
					return xmlSerializer.Deserialize(textReader) as WebResponse;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("generateResponse Failed: " + ex.Message);
			}
			return null;
		}

		public void InvokeServiceMethod(ReignScores_ServiceTypes type, string method, CallbackMethod callback, MonoBehaviour services, params string[] args)
		{
			if (callback != null)
			{
				string text = string.Format("{0}{1}/{2}.cshtml", new object[3]
				{
					reignScoresURL,
					convertType(type),
					method
				});
				Debug.Log("Invoking URL: " + text);
				services.StartCoroutine(invokeWebMethod(convertAPI_Key(type), text, callback, args));
			}
		}

		private IEnumerator invokeWebMethod(string api_key, string url, CallbackMethod callback, params string[] args)
		{
			WWWForm form = new WWWForm();
			form.AddField("api_key", api_key);
			foreach (string text in args)
			{
				string[] array = text.Split('=');
				form.AddField(array[0], array[1]);
			}
			WWW www = new WWW(url, form);
			yield return www;
			if (!string.IsNullOrEmpty(www.error))
			{
				Debug.LogError(www.error);
				callback(false, null);
				yield break;
			}
			WebResponse response = generateResponse(www.text);
			if (response != null)
			{
				if (response.Type == ResponseTypes.Error)
				{
					Debug.LogError("Failed: " + response.ErrorMessage);
					callback(false, response);
				}
				else
				{
					callback(true, response);
				}
			}
			else
			{
				Debug.LogError("response is null");
				callback(false, null);
			}
		}




	}
}
