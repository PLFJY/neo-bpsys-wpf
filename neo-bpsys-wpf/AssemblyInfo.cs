using System.Reflection;
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page,
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
//(used if a resource is not found in the page,
// app, or any theme specific resource dictionaries)
)]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]
/// <summary>
/// 版本号迭代原则：
/// 首位仅在出现大型重构或又非常重大更改时（如官方规则改变、UI重构）才会跟进
/// 第二位出现重大的新Module更新/第三位满十才会跟进
/// 第三位是有新Feature就会跟进
/// 最后一位自动生成，用于BugFix和Improvement版本，不用手动迭代
/// </summary>
[assembly: AssemblyVersion("1.0.1.*")]