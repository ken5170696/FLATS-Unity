using Reign.Plugin;

namespace Reign
{
	public interface IScores_UI
	{
		event ScoreFormatCallbackMethod ScoreFormatCallback;

		void Init(IScorePlugin plugin);

		void RequestLogin(AuthenticateCallbackMethod callback);

		void AutoLogin(AuthenticateCallbackMethod callback);

		void LoginCallback(bool succeeded, string errorMessage);

		void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback);

		void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback);
	}
}
