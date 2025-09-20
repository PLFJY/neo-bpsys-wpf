using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ExampleExtension.UI;
using neo_bpsys_wpf.ExampleExtension.UI.View;

namespace neo_bpsys_wpf.ExampleExtension;

[ExtensionManifest("ExampleExtension",
    "示例扩展",
    "1.2.0",
    3,
    "天启",
    "这是一个示例扩展，用于演示如何创建和使用扩展。")]
public class ExampleExtension : IExtension
{
    public static ExampleExtension Instance { get; internal set; }

    public IExtensionService ExtensionService { get; set; }

    public void Initialize()
    {// 扩展被注册(Register)时调用
        Instance = this;
        ExtensionService.RegisterUI(this, new ExampleUI().ExampleBorder);
    }

    public void Uninitialize()
    {// 扩展被注销(Unregister)时调用
        ExtensionService.UnregisterUI(this, new ExampleUI().ExampleBorder);
    }
}