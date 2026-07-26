namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// 在设计器 v3 绑定目录中隐藏某个公开属性。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FrontedBindingIgnoreAttribute : Attribute;
