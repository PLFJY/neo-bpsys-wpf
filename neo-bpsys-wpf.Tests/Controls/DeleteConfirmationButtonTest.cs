using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// 测试 <see cref="DeleteConfirmationButton"/> 的确认交互。
/// </summary>
public sealed class DeleteConfirmationButtonTest
{
    [Fact]
    public void ConfirmingDeletion_ShouldExecuteCommandAndCloseConfirmation()
    {
        WpfTestThread.Run(() =>
        {
            var command = new RecordingCommand();
            var control = new DeleteConfirmationButton
            {
                Command = command,
                CommandParameter = "custom-window",
                Template = CreateTemplate()
            };

            control.ApplyTemplate();
            var trigger = Assert.IsType<TestButton>(
                control.Template.FindName("PART_TriggerButton", control));
            var confirm = Assert.IsType<TestButton>(
                control.Template.FindName("PART_ConfirmButton", control));
            var cancel = Assert.IsType<TestButton>(
                control.Template.FindName("PART_CancelButton", control));

            trigger.InvokeClick();

            Assert.True(control.IsConfirmationOpen);
            Assert.Null(command.LastParameter);

            cancel.InvokeClick();

            Assert.False(control.IsConfirmationOpen);
            Assert.Null(command.LastParameter);

            trigger.InvokeClick();
            confirm.InvokeClick();

            Assert.False(control.IsConfirmationOpen);
            Assert.Equal("custom-window", command.LastParameter);
        });
    }

    private static ControlTemplate CreateTemplate()
    {
        var template = new ControlTemplate(typeof(DeleteConfirmationButton));
        var root = new FrameworkElementFactory(typeof(Grid));

        root.AppendChild(CreateButton("PART_TriggerButton"));
        root.AppendChild(CreateButton("PART_CancelButton"));

        var confirm = CreateButton("PART_ConfirmButton");
        confirm.SetBinding(
            ButtonBase.CommandProperty,
            new Binding(nameof(DeleteConfirmationButton.Command))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
        confirm.SetBinding(
            ButtonBase.CommandParameterProperty,
            new Binding(nameof(DeleteConfirmationButton.CommandParameter))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
        root.AppendChild(confirm);

        template.VisualTree = root;
        return template;
    }

    private static FrameworkElementFactory CreateButton(string name)
    {
        return new FrameworkElementFactory(typeof(TestButton))
        {
            Name = name
        };
    }

    private sealed class RecordingCommand : ICommand
    {
        /// <inheritdoc />
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// 获取最后一次执行时的参数。
        /// </summary>
        public object LastParameter { get; private set; }

        /// <inheritdoc />
        public bool CanExecute(object parameter) => true;

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            LastParameter = parameter;
        }
    }

    private sealed class TestButton : Button
    {
        public void InvokeClick()
        {
            OnClick();
        }
    }
}
