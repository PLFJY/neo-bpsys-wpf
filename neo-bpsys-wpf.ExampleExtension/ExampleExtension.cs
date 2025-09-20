using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ExampleExtension.UI;
using neo_bpsys_wpf.ExampleExtension.UI.View;

namespace neo_bpsys_wpf.ExampleExtension;

[ExtensionManifest("ExampleExtension",
    "示例扩展",
    "1.3.0",
    3,
    "天启",
    "这是一个示例扩展，用于演示如何创建和使用扩展。")]
public class ExampleExtension : IExtension
{
    public ExampleExtension()
    {
        Instance = this;
    }
    public static ExampleExtension Instance { get; private set; }

    public IExtensionService ExtensionService { get; set; }

    public void Initialize()
    {// 扩展被注册(Register)时调用
        ExtensionService.RegisterUi(this, new ExampleUI().ExampleBorder);
    }

    public void Uninitialize()
    {// 扩展被注销(Unregister)时调用
        ExtensionService.UnregisterUi(this, new ExampleUI().ExampleBorder);
    }
}