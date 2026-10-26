using UnityEngine;

namespace ZincWhiskers
{
    // Bare-bones host/join screen, to be replaced by a proper UI later.
    public class LobbyMenu : MonoBehaviour
    {
        const float ReferenceHeight = 720f;

        string joinCode = "";

        void OnGUI()
        {
            var network = SessionNetwork.Instance;
            if (network == null)
            {
                return;
            }

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (network.Session == null)
            {
                DrawMainMenu(network);
            }
            else if (Cursor.lockState == CursorLockMode.Locked)
            {
                GUI.Label(new Rect(16f, 16f, 300f, 24f), "Esc for menu");
            }
            else
            {
                DrawPauseMenu(network);
            }
        }

        void DrawMainMenu(SessionNetwork network)
        {
            GUILayout.BeginArea(new Rect(16f, 16f, 260f, 260f), GUI.skin.box);
            GUILayout.Label("Zinc Whiskers");
            GUI.enabled = !network.IsBusy;
            if (GUILayout.Button("Host"))
            {
                network.Host();
            }
            GUILayout.Space(8f);
            GUILayout.Label("Join code");
            joinCode = GUILayout.TextField(joinCode, 12);
            if (GUILayout.Button("Join"))
            {
                network.Join(joinCode);
            }
            GUI.enabled = true;
            DrawStatus(network);
            GUILayout.EndArea();
        }

        void DrawPauseMenu(SessionNetwork network)
        {
            var session = network.Session;
            GUILayout.BeginArea(new Rect(16f, 16f, 260f, 260f), GUI.skin.box);
            GUILayout.Label($"Join code: {session.Code}");
            GUILayout.Label($"Cats: {session.PlayerCount}/{session.MaxPlayers}");
            if (GUILayout.Button("Copy code"))
            {
                GUIUtility.systemCopyBuffer = session.Code;
            }
            if (GUILayout.Button("Resume"))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            GUI.enabled = !network.IsBusy;
            if (GUILayout.Button("Leave"))
            {
                network.Leave();
            }
            GUI.enabled = true;
            DrawStatus(network);
            GUILayout.EndArea();
        }

        static void DrawStatus(SessionNetwork network)
        {
            if (!string.IsNullOrEmpty(network.Status))
            {
                GUILayout.Label(network.Status);
            }
        }
    }
}
