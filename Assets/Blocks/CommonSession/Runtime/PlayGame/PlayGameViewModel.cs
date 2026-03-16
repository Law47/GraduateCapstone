using System;
using System.Runtime.CompilerServices;
using Unity.Properties;
using Unity.Services.Multiplayer;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using JetBrains.Annotations;

namespace Blocks.Sessions.Common
{
    public class PlayGameViewModel : IDisposable, INotifyBindablePropertyChanged
    {
        const string k_GameplayScenePath = "Scenes/Test Scenes/Gameplay";

        public static event Action GameplayStartRequested;

        private SessionObserver m_SessionObserver;
        private ISession m_Session;

        [CreateProperty]
        public bool CanPlayGame
        {
            get => m_CanPlayGame;
            private set
            {
                if (m_CanPlayGame == value)
                {
                    return;
                }

                m_CanPlayGame = value;
                Notify();
            }
        }

        bool m_CanPlayGame;

        public PlayGameViewModel(string sessionType)
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] Creating view model for sessionType: {sessionType}");
            m_SessionObserver = new SessionObserver(sessionType);
            m_SessionObserver.SessionAdded += OnSessionAdded;
            if (m_SessionObserver.Session != null)
            {
                UnityEngine.Debug.Log("[PlayGameViewModel] Session already exists in observer");
                OnSessionAdded(m_SessionObserver.Session);
            }
            else
            {
                UnityEngine.Debug.Log("[PlayGameViewModel] Waiting for session to be added");
            }
            UpdateCanPlayGame();
        }

        void OnSessionAdded(ISession newSession)
        {
            UnityEngine.Debug.Log("[PlayGameViewModel] OnSessionAdded called");
            m_Session = newSession;
            m_Session.SessionHostChanged += OnSessionHostChanged;
            m_Session.RemovedFromSession += OnSessionRemoved;
            m_Session.Deleted += OnSessionRemoved;
            m_Session.PlayerJoined += OnPlayerJoined;
            m_Session.PlayerHasLeft += OnPlayerLeaving;
            UpdateCanPlayGame();
        }

        void OnSessionHostChanged(string hostPlayerId)
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] Session host changed: {hostPlayerId}");
            UpdateCanPlayGame();
        }

        void OnPlayerJoined(string playerId)
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] Player joined: {playerId}");
            UpdateCanPlayGame();
        }

        void OnPlayerLeaving(string playerId)
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] Player leaving: {playerId}");
            UpdateCanPlayGame();
        }

        void OnSessionRemoved()
        {
            CleanupSession();
            UpdateCanPlayGame();
        }

        void UpdateCanPlayGame()
        {
            if (m_Session == null)
            {
                CanPlayGame = false;
                return;
            }

            int playerCount = m_Session.Players.Count;
            bool isHost = m_Session.IsHost;
            UnityEngine.Debug.Log($"[PlayGameViewModel] Player count: {playerCount}, IsHost: {isHost}");

            // Only the session host should be able to trigger a synchronized scene transition.
            CanPlayGame = playerCount >= 2 && isHost;
        }

        public void PlayGame()
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] PlayGame called. CanPlayGame={m_CanPlayGame}");
            
            if (!m_CanPlayGame)
            {
                UnityEngine.Debug.LogError("Cannot start game. Need at least 2 players connected.");
                return;
            }

            if (GameplayStartRequested != null)
            {
                UnityEngine.Debug.Log("[PlayGameViewModel] Requesting synchronized gameplay start...");
                GameplayStartRequested.Invoke();
                return;
            }

            UnityEngine.Debug.LogWarning("[PlayGameViewModel] No network scene loader is active. Falling back to a local scene load.");
            SceneManager.LoadScene(k_GameplayScenePath, LoadSceneMode.Single);
        }

        void CleanupSession()
        {
            if (m_Session == null)
            {
                return;
            }

            m_Session.SessionHostChanged -= OnSessionHostChanged;
            m_Session.RemovedFromSession -= OnSessionRemoved;
            m_Session.Deleted -= OnSessionRemoved;
            m_Session.PlayerJoined -= OnPlayerJoined;
            m_Session.PlayerHasLeft -= OnPlayerLeaving;
            m_Session = null;
        }

        public void Dispose()
        {
            if (m_SessionObserver != null)
            {
                m_SessionObserver.Dispose();
                m_SessionObserver = null;
            }

            if (m_Session != null)
            {
                CleanupSession();
            }
        }

        /// <summary>
        /// Suggested implementation of INotifyBindablePropertyChanged from UIToolkit.
        /// </summary>
        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        void Notify([CallerMemberName] string property = null)
        {
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }
    }
}
