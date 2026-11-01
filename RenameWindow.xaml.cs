using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GameTimer;

public partial class RenameWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly Rect _anchor;

    public RenameWindow(string name, Rect anchor)
    {
        InitializeComponent();
        _anchor = anchor;
        NameBox.Text = name;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        int on = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref on, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 오버레이 바로 아래 가운데에 두되 화면 밖으로 나가지 않게 한다.
        double vl = SystemParameters.VirtualScreenLeft;
        double vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth;
        double vb = vt + SystemParameters.VirtualScreenHeight;
        double left = _anchor.Left + (_anchor.Width - ActualWidth) / 2;
        double top = _anchor.Top + 30;
        Left = Math.Max(vl, Math.Min(left, vr - ActualWidth));
        Top = Math.Max(vt, Math.Min(top, vb - ActualHeight));

        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.Instance.RenameStopwatch(NameBox.Text)) Close();
    }
}
