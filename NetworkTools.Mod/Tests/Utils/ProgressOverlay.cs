namespace NetworkTools.Tests {
    using Game;
    using Game.SceneFlow;

    using UnityEngine;

    /// <summary>
    ///     Shows how far a scenario has got, in three lines at the bottom left of the screen.
    ///     The first names the test under way and the time since the start.
    ///     The second shows the last check, the third the totals.
    ///     It keeps the totals once the scenario ends, until the game leaves the editor.
    /// </summary>
    internal class ProgressOverlay : MonoBehaviour {
        /// <summary>
        ///     Where the game's toolbar starts, as a fraction of the screen's height.
        /// </summary>
        private const float Toolbar = 0.82f;

        /// <summary>
        ///     The overlay on screen, which a new one replaces.
        ///     A reload from the editor into the editor leaves the last one in place.
        /// </summary>
        private static ProgressOverlay s_Current;

        /// <summary>
        ///     When the overlay was created, and when the scenario ended, or not a number.
        /// </summary>
        private float m_Started;
        private float m_Stopped = float.NaN;

        /// <summary>
        ///     The first two lines: the test under way, and the last check.
        /// </summary>
        private string m_Test  = string.Empty;
        private string m_Check = string.Empty;

        /// <summary>
        ///     Checks counted over every test so far.
        /// </summary>
        private int m_Passed;
        private int m_Failed;
        private int m_Known;

        /// <summary>
        ///     The box's style, made at the first draw.
        /// </summary>
        private GUIStyle m_Style;

        /// <summary>
        ///     Creates the overlay, its clock started, in place of the last run's.
        /// </summary>
        /// <returns>The overlay.</returns>
        public static ProgressOverlay Create() {
            if (s_Current != null) {
                Destroy(s_Current.gameObject);
            }

            s_Current = new GameObject(nameof(ProgressOverlay)).AddComponent<ProgressOverlay>();

            return s_Current;
        }

        /// <summary>
        ///     Shows the test under way.
        /// </summary>
        /// <param name="index">Index of the test, from one.</param>
        /// <param name="count">Number of tests.</param>
        /// <param name="name">Name of the test.</param>
        public void StartTest(int index, int count, string name) {
            m_Test = $"Test {index} of {count}: {name}";
        }

        /// <summary>
        ///     Counts a check and shows what it read, up to its first colon.
        /// </summary>
        /// <param name="result">PASSED, KNOWN, or FAILED.</param>
        /// <param name="text">What was checked and read.</param>
        public void Report(string result, string text) {
            var colon = text.IndexOf(':');

            m_Check = $"[{result}] {(colon < 0 ? text : text.Substring(0, colon))}";

            switch (result) {
                case "PASSED":
                    m_Passed++;
                    break;
                case "KNOWN":
                    m_Known++;
                    break;
                default:
                    m_Failed++;
                    break;
            }
        }

        /// <summary>
        ///     Stops the clock and shows that the scenario has ended.
        /// </summary>
        public void Stop() {
            m_Stopped = Time.realtimeSinceStartup;
            m_Test    = "Finished";
        }

        /// <summary>
        ///     Starts the clock.
        /// </summary>
        private void Awake() {
            m_Started = Time.realtimeSinceStartup;
        }

        /// <summary>
        ///     Removes the overlay once the game has left the editor.
        /// </summary>
        private void Update() {
            if (GameManager.instance.gameMode != GameMode.Editor) {
                Destroy(gameObject);
            }
        }

        /// <summary>
        ///     Draws the three lines in a box sized to them.
        /// </summary>
        private void OnGUI() {
            // The skin is only there while the GUI draws.
            // A label's text starts at the left, and the box behind it gives it a background.
            m_Style ??= new GUIStyle(GUI.skin.label) {
                fontSize = Screen.height / 50,
                padding  = new RectOffset(12, 12, 8, 8),
            };

            var now     = float.IsNaN(m_Stopped) ? Time.realtimeSinceStartup : m_Stopped;
            var seconds = (int)(now - m_Started);
            var text    = $"{m_Test}, {seconds / 60}:{seconds % 60:00}\n{m_Check}\n"
                          + $"{m_Passed} passed, {m_Failed} failed, {m_Known} known";
            var content = new GUIContent(text);
            var size    = m_Style.CalcSize(content);
            var area    = new Rect(
                16f,
                Screen.height * Toolbar - size.y,
                size.x,
                size.y);

            GUI.Box(area, GUIContent.none);
            GUI.Label(area, content, m_Style);
        }
    }
}
