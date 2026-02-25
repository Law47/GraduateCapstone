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
            m_Session.PlayerJoined += OnPlayerJoined;
            m_Session.PlayerLeaving += OnPlayerLeaving;
            UpdateCanPlayGame();
        }

        void OnPlayerJoined(IPlayer player)
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] Player joined: {player.Id}");
            UpdateCanPlayGame();
        }

        void OnPlayerLeaving(IPlayer player)
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] Player leaving: {player.Id}");
            UpdateCanPlayGame();
        }

        void UpdateCanPlayGame()
        {
            // Can play if we have a session with at least 2 players connected
            if (m_Session == null)
            {
                CanPlayGame = false;
                return;
            }

            int playerCount = m_Session.Players.Count;
            UnityEngine.Debug.Log($"[PlayGameViewModel] Player count: {playerCount}");
            CanPlayGame = playerCount >= 2;
        }

        public void PlayGame()
        {
            UnityEngine.Debug.Log($"[PlayGameViewModel] PlayGame called. CanPlayGame={m_CanPlayGame}");
            
            if (!m_CanPlayGame)
            {
                UnityEngine.Debug.LogError("Cannot start game. Need at least 2 players connected.");
                return;
            }

            UnityEngine.Debug.Log("[PlayGameViewModel] Loading Gameplay scene...");
            // Load the Gameplay scene
            SceneManager.LoadScene("Scenes/Test Scenes/Gameplay", LoadSceneMode.Single);
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
                m_Session.PlayerJoined -= OnPlayerJoined;
                m_Session.PlayerLeaving -= OnPlayerLeaving;
                m_Session = null;
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
