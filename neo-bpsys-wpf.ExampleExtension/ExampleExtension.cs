using neo_bpsys_wpf.Core.Extensions;
using neo_bpsys_wpf.ExampleExtension.UI;

namespace neo_bpsys_wpf.ExampleExtension;

public class ExampleExtension : IExtension
{
    public static ExampleExtension Instance { get; internal set; }

    public ExtensionManifest ExtensionManifest { get; } = new(
        "ExampleExtension",
        "示例扩展",
        "1.0.0",
        1,
        "天启",
        "这是一个示例扩展，用于演示如何创建和使用扩展。");

    public void Initialize()
    {
        Instance = this;
        ExtensionManager.Instance().RegisterUI(this, new ExampleUI().ExampleBorder);
    }

    public void Uninitialize()
    {
        ExtensionManager.Instance().UnregisterUI(this, new ExampleUI().ExampleBorder);
    }
}