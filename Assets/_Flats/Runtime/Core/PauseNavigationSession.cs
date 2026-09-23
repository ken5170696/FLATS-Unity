namespace Flats.Core
{
    /// <summary>Per-menu ownership of pause navigation and simulation restoration.</summary>
    public sealed class PauseNavigationSession
    {
        public bool IsOpen { get; private set; }
        public float SavedTimeScale { get; private set; } = 1f;
        bool frozeSimulation;

        public bool TryOpen(string screen, string game, float timeScale, out float nextTimeScale)
        {
            nextTimeScale = timeScale;
            if (IsOpen || screen != "Playing" || game == "Main") return false;
            IsOpen = true;
            frozeSimulation = game != "Multiplayer";
            if (frozeSimulation)
            {
                SavedTimeScale = timeScale;
                nextTimeScale = 0f;
            }
            return true;
        }

        public bool TryClose(string screen, string game, float timeScale, float legacyResumeScale, out float nextTimeScale)
        {
            nextTimeScale = timeScale;
            if (screen == "Playing" || screen == "Result" || game == "Main") return false;
            // A scene can expose Resume before OpenMenu was called on this Menu.
            // Preserve that entry, using its existing resume scale only offline.
            if (game != "Multiplayer") nextTimeScale = IsOpen && frozeSimulation ? SavedTimeScale : legacyResumeScale;
            IsOpen = false;
            frozeSimulation = false;
            return true;
        }

        public static float ListenerVolume(float configuredVolume, bool playing)
        {
            return configuredVolume * (playing ? 1f : 0.5f);
        }
    }
}
