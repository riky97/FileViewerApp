using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FileViewerApp.Views.Controls;

public partial class SpinnerControl : UserControl
{
    private readonly DispatcherTimer _timer;
    private RotateTransform? _rt;
    private double _angle;

    public SpinnerControl()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) }; // ~25fps
        _timer.Tick += (_, __) => Rotate();
        _rt = (Ring.RenderTransform as RotateTransform);
        _timer.Start();
    }

    // Spinner always rotates; only visible when overlay shows
    // (simplifies since VisualTreeAttachmentEventArgs type resolution caused issues)

    private void Rotate()
    {
        if (_rt == null) return;
        _angle = (_angle + 20) % 360; // advance
        _rt.Angle = _angle;
    }
}
