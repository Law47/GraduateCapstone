using System;
using Blocks.Common;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Blocks.Sessions.Common
{
    [UxmlElement]
    public partial class PlayGameButton : Button
    {
        const string k_PlayGameButtonText = "PLAY";

        [CreateProperty, UxmlAttribute]
        public string SessionType
        {
            get => m_SessionType;
            set
            {
                if (m_SessionType == value)
                {
                    return;
                }

                m_SessionType = value;
                if (panel != null)
                {
                    UpdateBindings();
                }
            }
        }

        string m_SessionType;

        DataBinding m_DataBinding;
        PlayGameViewModel m_ViewModel;

        public PlayGameButton()
        {
            text = k_PlayGameButtonText;

            AddToClassList(BlocksTheme.Button);
            m_DataBinding = new DataBinding() { dataSourcePath = new PropertyPath(nameof(PlayGameViewModel.CanPlayGame)), bindingMode = BindingMode.ToTarget };
            SetBinding(new BindingId(nameof(enabledSelf)), m_DataBinding);
            clicked += PlayGame;

            RegisterCallback<AttachToPanelEvent>(_ => UpdateBindings());
            RegisterCallback<DetachFromPanelEvent>(_ => CleanupBindings());
        }

        void PlayGame()
        {
            m_ViewModel.PlayGame();
        }

        void UpdateBindings()
        {
            CleanupBindings();
            m_ViewModel = new PlayGameViewModel(m_SessionType);
            m_DataBinding.dataSource = m_ViewModel;
        }

        void CleanupBindings()
        {
            if (m_DataBinding.dataSource is IDisposable disposable)
            {
                disposable.Dispose();
            }

            m_ViewModel = null;
            m_DataBinding.dataSource = null;
        }
    }
}
