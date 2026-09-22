using UnityEngine;

namespace Reign.Plugin
{
	public interface IScorePlugin
	{
		bool IsAuthenticated { get; }

		string Username { get; }

		void Authenticate(AuthenticateCallbackMethod callback, MonoBehaviour services);

		void Logout();

		void ManualLogin(string username, string password, AuthenticateCallbackMethod callback, MonoBehaviour services);

		void ManualCreateUser(string username, string password, AuthenticateCallbackMethod callback, MonoBehaviour services);

		void ReportScore(string leaderboardID, long score, ReportScoreCallbackMethod callback, MonoBehaviour services);

		void RequestScores(string leaderboardID, int offset, int range, RequestScoresCallbackMethod callback, MonoBehaviour services);

		void ReportAchievement(string achievementID, float percentComplete, ReportAchievementCallbackMethod callback, MonoBehaviour services);

		void RequestAchievements(RequestAchievementsCallbackMethod callback, MonoBehaviour services);

		void ShowNativeScoresPage(string leaderboardID, ShowNativeViewDoneCallbackMethod callback, MonoBehaviour services);

		void ShowNativeAchievementsPage(ShowNativeViewDoneCallbackMethod callback, MonoBehaviour services);

		void ResetUserAchievementsProgress(ResetUserAchievementsCallbackMethod callback, MonoBehaviour services);

		void Update();
	}
}
