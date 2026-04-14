using AmalgadonPlugin.Controls;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.API;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Core = Hearthstone_Deck_Tracker.API.Core;

namespace AmalgadonPlugin
{
    /// <summary>
    /// Plugin logic class. Manages the overlay button and responds to game events.
    /// Mirrors the MyPlugin.cs pattern from the HDTPluginTemplate.
    ///
    /// The HDT overlay window is WS_EX_TRANSPARENT, so WPF hit-testing is bypassed.
    /// We use User32.MouseInput (a global low-level hook) to detect clicks and hover,
    /// exactly as the template's InputMoveManager does.
    /// </summary>
    public class AmalgadonPlugin : IDisposable
    {
        private OverlayButton _overlayButton;
        private User32.MouseInput _mouseInput;
        private bool _enabled = true;
        private bool _wasHovering = false;

        public AmalgadonPlugin()
        {
            InitViewPanel();

            GameEvents.OnGameStart.Add(GameTypeCheck);
            GameEvents.OnGameEnd.Add(HideOverlay);
        }

        /// <summary>
        /// Called by the menu item toggle — shows or hides the overlay.
        /// </summary>
        public void Toggle(bool enabled)
        {
            _enabled = enabled;
            if (!_enabled)
                HideOverlay();
            else
                GameTypeCheck();
        }

        private void GameTypeCheck()
        {
            if (_enabled && Core.Game.CurrentGameType == GameType.GT_BATTLEGROUNDS)
                ShowOverlay();
            else
                HideOverlay();
        }

        private void ShowOverlay()
        {
            _overlayButton.Visibility = Visibility.Visible;
            EnableMouseInput();
        }

        private void HideOverlay()
        {
            if (_overlayButton != null)
                _overlayButton.Visibility = Visibility.Collapsed;
            DisableMouseInput();
        }

        private void EnableMouseInput()
        {
            if (_mouseInput != null) return;
            _mouseInput = new User32.MouseInput();
            _mouseInput.LmbDown += OnLmbDown;
            _mouseInput.MouseMoved += OnMouseMoved;
        }

        private void DisableMouseInput()
        {
            if (_mouseInput == null) return;
            _mouseInput.LmbDown -= OnLmbDown;
            _mouseInput.MouseMoved -= OnMouseMoved;
            _mouseInput.Dispose();
            _mouseInput = null;

            // Restore cursor in case we left it as Hand
            _overlayButton?.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
                _wasHovering = false;
            });
        }

        private void OnLmbDown(object sender, EventArgs e)
        {
            if (_overlayButton == null || _overlayButton.Visibility != Visibility.Visible)
                return;

            if (IsMouseOverButton())
                _overlayButton.Dispatcher.Invoke(() => BoardCapture.OpenCurrentBoard());
        }

        private void OnMouseMoved(object sender, EventArgs e)
        {
            if (_overlayButton == null || _overlayButton.Visibility != Visibility.Visible)
                return;

            bool over = IsMouseOverButton();

            // Only dispatch when hover state changes to avoid flooding the UI thread
            if (over == _wasHovering) return;
            _wasHovering = over;

            _overlayButton.Dispatcher.Invoke(() =>
                Mouse.OverrideCursor = over ? Cursors.Hand : null);
        }

        private bool IsMouseOverButton()
        {
            var pos = User32.GetMousePos();
            var screenPoint = new Point(pos.X, pos.Y);
            try
            {
                var local = _overlayButton.PointFromScreen(screenPoint);
                return local.X >= 0 && local.X <= _overlayButton.ActualWidth
                    && local.Y >= 0 && local.Y <= _overlayButton.ActualHeight;
            }
            catch
            {
                return false;
            }
        }

        private void InitViewPanel()
        {
            _overlayButton = new OverlayButton();
            _overlayButton.Visibility = Visibility.Collapsed;
            Core.OverlayCanvas.Children.Add(_overlayButton);

            Canvas.SetTop(_overlayButton, 50);
            Canvas.SetLeft(_overlayButton, 10);
        }

        public void CleanUp()
        {
            DisableMouseInput();
            if (_overlayButton != null)
            {
                Core.OverlayCanvas.Children.Remove(_overlayButton);
                Dispose();
            }
        }

        public void Dispose()
        {
            _overlayButton = null;
        }
    }
}
