using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FileViewerApp.Views.Controls
{
    public partial class HistoryPanelControl : UserControl
    {
        public HistoryPanelControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}