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

        // Drag-to-reposition state (initiated from the handle only)
        private bool _dragDown = false;
        private Point _dragStartScreen;
        private double _dragStartLeft;
        private double _dragStartTop;

        // Click tracking for the main button
        private bool _clickDown = false;

        // Hover zone for cursor management
        private enum HoverZone { None, Handle, Button }
        private HoverZone _hoverZone = HoverZone.None;

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
            _mouseInput.LmbUp += OnLmbUp;
            _mouseInput.MouseMoved += OnMouseMoved;
        }

        private void DisableMouseInput()
        {
            if (_mouseInput == null) return;
            _mouseInput.LmbDown -= OnLmbDown;
            _mouseInput.LmbUp -= OnLmbUp;
            _mouseInput.MouseMoved -= OnMouseMoved;
            _mouseInput.Dispose();
            _mouseInput = null;
            _dragDown = false;
            _clickDown = false;

            // Restore cursor in case we left it as Hand or SizeAll
            _overlayButton?.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
                _hoverZone = HoverZone.None;
            });
        }

        private void OnLmbDown(object sender, EventArgs e)
        {
            if (_overlayButton == null || _overlayButton.Visibility != Visibility.Visible)
                return;

            var pos = User32.GetMousePos();
            var sp = new Point(pos.X, pos.Y);

            if (IsMouseOverHandle(sp))
            {
                _dragDown = true;
                _dragStartScreen = sp;
                _overlayButton.Dispatcher.Invoke(() =>
                {
                    _dragStartLeft = Canvas.GetLeft(_overlayButton);
                    _dragStartTop  = Canvas.GetTop(_overlayButton);
                });
            }
            else if (IsMouseOverMainButton(sp))
            {
                _clickDown = true;
            }
        }

        private void OnLmbUp(object sender, EventArgs e)
        {
            if (_dragDown)
            {
                _dragDown = false;
                _overlayButton.Dispatcher.Invoke(() =>
                {
                    PluginSettings.Instance.ButtonLeft = Canvas.GetLeft(_overlayButton);
                    PluginSettings.Instance.ButtonTop  = Canvas.GetTop(_overlayButton);
                    PluginSettings.Save();
                    Mouse.OverrideCursor = null;
                });
            }
            else if (_clickDown)
            {
                _clickDown = false;
                _overlayButton.Dispatcher.Invoke(() => BoardCapture.OpenCurrentBoard());
            }
        }

        private void OnMouseMoved(object sender, EventArgs e)
        {
            if (_overlayButton == null || _overlayButton.Visibility != Visibility.Visible)
                return;

            if (_dragDown)
            {
                var pos = User32.GetMousePos();
                double dx = pos.X - _dragStartScreen.X;
                double dy = pos.Y - _dragStartScreen.Y;
                _overlayButton.Dispatcher.Invoke(() =>
                {
                    double canvasW = Core.OverlayCanvas.ActualWidth;
                    double canvasH = Core.OverlayCanvas.ActualHeight;
                    double newLeft = Math.Max(0, Math.Min(_dragStartLeft + dx, canvasW - _overlayButton.ActualWidth));
                    double newTop  = Math.Max(0, Math.Min(_dragStartTop  + dy, canvasH - _overlayButton.ActualHeight));
                    Canvas.SetLeft(_overlayButton, newLeft);
                    Canvas.SetTop(_overlayButton, newTop);
                    Mouse.OverrideCursor = Cursors.SizeAll;
                });
                return;
            }

            // Update cursor based on which zone the mouse is in
            var mousePos = User32.GetMousePos();
            var sp = new Point(mousePos.X, mousePos.Y);

            HoverZone zone;
            if (IsMouseOverHandle(sp))          zone = HoverZone.Handle;
            else if (IsMouseOverMainButton(sp)) zone = HoverZone.Button;
            else                                zone = HoverZone.None;

            if (zone == _hoverZone) return;
            _hoverZone = zone;

            _overlayButton.Dispatcher.Invoke(() =>
                Mouse.OverrideCursor = zone == HoverZone.Handle ? Cursors.SizeAll
                                     : zone == HoverZone.Button ? Cursors.Hand
                                     : null);
        }

        private bool TryGetLocalPoint(Point screenPoint, out Point local)
        {
            try { local = _overlayButton.PointFromScreen(screenPoint); return true; }
            catch { local = default(Point); return false; }
        }

        private bool IsMouseOverHandle(Point screenPoint)
        {
            if (!TryGetLocalPoint(screenPoint, out var local)) return false;
            try
            {
                double hw = _overlayButton.HandleWidth;
                return local.X >= 0 && local.X <= hw
                    && local.Y >= 0 && local.Y <= _overlayButton.ActualHeight;
            }
            catch { return false; }
        }

        private bool IsMouseOverMainButton(Point screenPoint)
        {
            if (!TryGetLocalPoint(screenPoint, out var local)) return false;
            try
            {
                double hw = _overlayButton.HandleWidth;
                return local.X > hw && local.X <= _overlayButton.ActualWidth
                    && local.Y >= 0 && local.Y <= _overlayButton.ActualHeight;
            }
            catch { return false; }
        }

        private void InitViewPanel()
        {
            _overlayButton = new OverlayButton();
            _overlayButton.Visibility = Visibility.Collapsed;
            Core.OverlayCanvas.Children.Add(_overlayButton);

            Canvas.SetTop(_overlayButton, PluginSettings.Instance.ButtonTop);
            Canvas.SetLeft(_overlayButton, PluginSettings.Instance.ButtonLeft);
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
