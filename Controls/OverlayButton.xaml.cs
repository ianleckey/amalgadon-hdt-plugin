using System.Windows.Controls;

namespace AmalgadonPlugin.Controls
{
    public partial class OverlayButton : UserControl
    {
        public OverlayButton()
        {
            InitializeComponent();
        }

        private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            BoardCapture.OpenCurrentBoard();
        }
    }
}
