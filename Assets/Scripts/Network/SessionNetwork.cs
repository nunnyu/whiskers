using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ZincWhiskers
{
    // Hosts and joins sessions over Relay. The session starts and stops the NetworkManager itself.
    [RequireComponent(typeof(NetworkManager))]
    public class SessionNetwork : MonoBehaviour
    {
        public const int MaxPlayers = 4;

        public static SessionNetwork Instance { get; private set; }

        public ISession Session { get; private set; }
        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "";

        NetworkManager networkManager;

        void Awake()
        {
            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            networkManager.OnClientStopped += OnClientStopped;
        }

        void OnApplicationQuit()
        {
            // Without this the lobby outlives the host and keeps handing out a dead join code.
            Session?.LeaveAsync();
            Session = null;
        }

        void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnClientStopped -= OnClientStopped;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public async void Host()
        {
            await Run("Hosting...", async () =>
            {
                await LeaveLeftoverSessions();
                var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            });
        }

        public async void Join(string code)
        {
            code = code.Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                Status = "Enter a join code";
                return;
            }
            await Run("Joining...", async () =>
            {
                await LeaveLeftoverSessions();
                Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            });
        }

        public async void Leave()
        {
            await Run("", LeaveSession);
        }

        async Task Run(string status, Func<Task> action)
        {
            if (IsBusy)
            {
                return;
            }
            IsBusy = true;
            Status = status;
            try
            {
                await SignIn();
                await action();
                Status = "";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Status = e is AggregateException aggregate ? aggregate.GetBaseException().Message : e.Message;
                Session = null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        async Task LeaveSession()
        {
            var session = Session;
            Session = null;
            if (session != null)
            {
                await session.LeaveAsync();
            }
        }

        // Sessions from an earlier run can linger when play mode keeps scripts loaded between runs.
        static async Task LeaveLeftoverSessions()
        {
            foreach (var leftover in MultiplayerService.Instance.Sessions.Values.ToList())
            {
                try
                {
                    await leftover.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Couldn't leave old session: {e.Message}");
                }
            }
        }

        async void OnClientStopped(bool wasHost)
        {
            if (Session == null)
            {
                return;
            }
            try
            {
                await LeaveSession();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Status = "Disconnected";
        }

        static async Task SignIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(ProfileName()));
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        // Multiplayer Play Mode players share this machine, so each one needs its own sign-in profile.
        static string ProfileName()
        {
#if UNITY_EDITOR
            string folder = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
            string name = Regex.Replace(folder, "[^a-zA-Z0-9_-]", "");
            if (name.Length > 30)
            {
                name = name.Substring(name.Length - 30);
            }
            return name.Length > 0 ? name : "default";
#else
            return "default";
#endif
        }
    }
}
