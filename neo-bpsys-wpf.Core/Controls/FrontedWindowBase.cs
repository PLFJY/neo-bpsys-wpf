using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Controls;

/// <summary>
/// 前台窗口基类
/// </summary>
public abstract class FrontedWindowBase : Window
{
    private bool _isInternalContentChange = false;
    /// <summary>
    /// 前台窗口基类构造
    /// </summary>
    public FrontedWindowBase()
    {
        WeakReferenceMessenger.Default.Register<DesignerModeChangedMessage>(this, OnDesignerModeChanged);
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <inheritdoc/>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        // 防止递归调用：如果是我们自己包装 Viewbox 导致的 Content 改变，直接返回
        if (_isInternalContentChange || newContent is Viewbox vb && vb.Name == "InternalAutoViewbox")
        {
            base.OnContentChanged(oldContent, newContent);
            return;
        }

        _isInternalContentChange = true;

        try
        {
            // 1. 创建 Viewbox 并配置属性
            var viewbox = new Viewbox
            {
                Name = "InternalAutoViewbox",
                Stretch = Stretch.Fill
            };

            // 2. 创建 Binding (等价于你 XAML 里的 RelativeSource Binding)
            Binding widthBinding = new("Width")
            {
                Source = this, // 直接指向当前 Window
                Mode = BindingMode.OneWay
            };
            Binding heightBinding = new("Height")
            {
                Source = this,
                Mode = BindingMode.OneWay
            };

            viewbox.SetBinding(Viewbox.WidthProperty, widthBinding);
            viewbox.SetBinding(Viewbox.HeightProperty, heightBinding);

            // 3. 将原本的内容移交给 Viewbox
            // 注意：需要先将 Content 置空，否则 newContent 仍然属于 Window 的 Logical Tree，
            // 直接赋值给 Viewbox.Child 会报错
            this.Content = null;
            viewbox.Child = newContent as UIElement;

            // 4. 重新将 Viewbox 设为 Window 的 Content
            this.Content = viewbox;
        }
        finally
        {
            _isInternalContentChange = false;
        }

        base.OnContentChanged(oldContent, this.Content);
    }

    private void OnDesignerModeChanged(object recipient, DesignerModeChangedMessage message)
    {
        if (message.IsDesignerMode)
            MouseLeftButtonDown -= OnMouseLeftButtonDown;
        else
            MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <inheritdoc/>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
