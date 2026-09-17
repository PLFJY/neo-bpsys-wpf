using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using WPFLocalizeExtension.Engine;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// 测试 <see cref="DeleteConfirmationButton"/> 的确认交互。
/// </summary>
[Collection(WpfUiCollectionDefinition.Name)]
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

            Assert.True(control.CanConfirm);

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

            command.CanExecuteResult = false;
            command.NotifyCanExecuteChanged();
            trigger.InvokeClick();

            Assert.False(control.CanConfirm);
            Assert.False(control.IsConfirmationOpen);
        });
    }

    [Theory]
    [InlineData("zh-CN", "取消", "删除", "是否删除")]
    [InlineData("en-US", "Cancel", "Delete", "Are you sure to delete")]
    public void ConfirmationFlyout_ShouldResolveResourceText(
        string cultureName,
        string expectedCancelText,
        string expectedDeleteText,
        string expectedConfirmationText)
    {
        WpfTestThread.Run(() =>
        {
            var previousCulture = LocalizeDictionary.Instance.Culture;
            try
            {
                LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo(cultureName);

                var flyoutContent = Assert.IsType<StackPanel>(XamlReader.Parse("""
                    <StackPanel
                        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:lex="http://wpflocalizeextension.codeplex.com"
                        lex:LocalizeDictionary.Provider="{x:Static lex:ResxLocalizationProvider.Instance}"
                        lex:ResxLocalizationProvider.DefaultAssembly="neo-bpsys-wpf"
                        lex:ResxLocalizationProvider.DefaultDictionary="neo_bpsys_wpf.Locales.Common">
                        <Button Content="{lex:Loc Cancel}" />
                        <Button Content="{lex:Loc Delete}" />
                        <Button Content="{lex:Loc neo-bpsys-wpf:neo_bpsys_wpf.Locales.Team:AreYouSureToDelete}" />
                    </StackPanel>
                    """));
                var cancel = Assert.IsType<Button>(flyoutContent.Children[0]);
                var confirm = Assert.IsType<Button>(flyoutContent.Children[1]);
                var confirmationText = Assert.IsType<Button>(flyoutContent.Children[2]);

                Assert.Equal(expectedCancelText, cancel.Content);
                Assert.Equal(expectedDeleteText, confirm.Content);
                Assert.Equal(expectedConfirmationText, confirmationText.Content);
            }
            finally
            {
                LocalizeDictionary.Instance.Culture = previousCulture;
            }
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
        private EventHandler _canExecuteChanged;

        /// <summary>
        /// 获取或设置命令当前是否可执行。
        /// </summary>
        public bool CanExecuteResult { get; set; } = true;

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged
        {
            add => _canExecuteChanged += value;
            remove => _canExecuteChanged -= value;
        }

        /// <summary>
        /// 获取最后一次执行时的参数。
        /// </summary>
        public object LastParameter { get; private set; }

        /// <inheritdoc />
        public bool CanExecute(object parameter) => CanExecuteResult;

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            LastParameter = parameter;
        }

        /// <summary>
        /// 通知命令可执行状态发生变化。
        /// </summary>
        public void NotifyCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class TestButton : Button
    {
        /// <summary>
        /// 触发按钮点击。
        /// </summary>
        public void InvokeClick()
        {
            OnClick();
        }
    }
}
