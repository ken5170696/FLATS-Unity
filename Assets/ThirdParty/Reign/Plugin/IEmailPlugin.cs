namespace Reign.Plugin
{
	public interface IEmailPlugin
	{
		void Send(string to, string subject, string body);
	}
}
