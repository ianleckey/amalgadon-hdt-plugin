using System.Windows.Controls;

namespace AmalgadonPlugin.Controls
{
    public partial class OverlayButton : UserControl
    {
        public OverlayButton()
        {
            InitializeComponent();
        }

        public double HandleWidth => DragHandle.ActualWidth;

        private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            BoardCapture.OpenCurrentBoard();
        }
    }
}
