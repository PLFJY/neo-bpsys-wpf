using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 声明由 WPF 元素类型拥有的教程。
/// </summary>
/// <typeparam name="TSelf">所有者元素类型。</typeparam>
public interface ITutorialOwner<TSelf>
    where TSelf : FrameworkElement, ITutorialOwner<TSelf>
{
    /// <summary>获取此所有者的稳定教程键。</summary>
    static abstract string TutorialKey { get; }

    /// <summary>
    /// 注册此类型拥有的教程。
    /// </summary>
    /// <param name="builder">教程创作构建器。</param>
    static abstract void RegisterTutorials(ITutorialBuilder builder);
}

/// <summary>
/// 声明应用级教程流程。
/// </summary>
/// <typeparam name="TSelf">应用类型。</typeparam>
public interface IAppTutorial<TSelf>
    where TSelf : Application, IAppTutorial<TSelf>
{
    /// <summary>
    /// 注册应用级教程。
    /// </summary>
    /// <param name="builder">教程创作构建器。</param>
    static abstract void RegisterTutorials(ITutorialBuilder builder);
}

/// <summary>
/// 高级教程创作入口点。
/// </summary>
public interface ITutorialBuilder
{
    /// <summary>
    /// 开始页面级教程创作。
    /// </summary>
    /// <typeparam name="TOwner">页面所有者类型。</typeparam>
    /// <returns>所有者教程构建器。</returns>
    ITutorialOwnerBuilder<TOwner> ForPage<TOwner>()
        where TOwner : Page, ITutorialOwner<TOwner>;

    /// <summary>
    /// 开始窗口级教程创作。
    /// </summary>
    /// <typeparam name="TOwner">窗口所有者类型。</typeparam>
    /// <returns>所有者教程构建器。</returns>
    ITutorialOwnerBuilder<TOwner> ForWindow<TOwner>()
        where TOwner : Window, ITutorialOwner<TOwner>;

    /// <summary>
    /// 开始区域级教程创作。
    /// </summary>
    /// <typeparam name="TOwner">区域所有者类型。</typeparam>
    /// <returns>所有者教程构建器。</returns>
    ITutorialOwnerBuilder<TOwner> ForRegion<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>;

    /// <summary>
    /// 开始为某个元素类型拥有的特定教程键进行创作。
    /// </summary>
    /// <typeparam name="TOwner">所有者类型。</typeparam>
    /// <param name="tutorialKey">要注册的教程键。</param>
    /// <returns>所有者教程构建器。</returns>
    ITutorialOwnerBuilder<TOwner> ForKey<TOwner>(string tutorialKey)
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>;

    /// <summary>
    /// 开始流程创作。
    /// </summary>
    /// <param name="flowId">稳定的流程 id。</param>
    /// <returns>流程构建器。</returns>
    ITutorialFlowBuilder Flow(string flowId);

    /// <summary>
    /// 注册由所有者类型声明的教程。
    /// </summary>
    /// <typeparam name="TOwner">所有者类型。</typeparam>
    void RegisterOwner<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner>;

    /// <summary>
    /// 注册由应用声明的教程。
    /// </summary>
    /// <typeparam name="TApp">应用类型。</typeparam>
    void RegisterApp<TApp>()
        where TApp : Application, IAppTutorial<TApp>;
}

/// <summary>
/// 用于一个教程所有者的高级构建器。
/// </summary>
/// <typeparam name="TOwner">所有者类型。</typeparam>
public interface ITutorialOwnerBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// 开始一个由当前所有者拥有的包。
    /// </summary>
    /// <param name="package">包引用。</param>
    /// <returns>包构建器。</returns>
    ITutorialPackageBuilder<TOwner> Package(TutorialPackageRef package);

    /// <summary>
    /// 将已有的包引用添加到此所有者的运行序列。
    /// </summary>
    /// <param name="package">已有的包引用。</param>
    /// <returns>同一所有者构建器。</returns>
    ITutorialOwnerBuilder<TOwner> Use(TutorialPackageRef package);

}

/// <summary>
/// <see cref="ITutorialOwnerBuilder{TOwner}"/> 的内部扩展，支持按需调度的包注册。
/// 由创作包构建器和运行时贡献者构建器使用。
/// </summary>
/// <typeparam name="TOwner">所有者类型。</typeparam>
internal interface ITutorialOwnerBuilderInternal<TOwner> : ITutorialOwnerBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// 向包注册表注册最终化的包定义，并可选地将其添加到序列。
    /// </summary>
    /// <param name="package">最终化的包定义。</param>
    /// <param name="isOnDemand">该包是否按需，不应出现在默认序列中。</param>
    void RegisterPackage(TutorialPackageDefinition package, bool isOnDemand);
}

/// <summary>
/// 用于一个所有者级包的高级构建器。
/// </summary>
/// <typeparam name="TOwner">所有者类型。</typeparam>
public interface ITutorialPackageBuilder<TOwner> : ITutorialOwnerBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// 添加一个教程步骤。
    /// </summary>
    /// <param name="title">步骤标题。</param>
    /// <returns>步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> Step(string title);

    /// <summary>
    /// 添加一个使用资源键作为标题的教程步骤。
    /// </summary>
    /// <param name="titleKey">步骤标题的资源键。</param>
    /// <returns>步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> StepKey(string titleKey);

    /// <summary>在包的当前位置添加对话。</summary>
    /// <param name="dialogue">要显示的对话。</param>
    /// <returns>同一包构建器。</returns>
    ITutorialPackageBuilder<TOwner> Dialogue(DialogueFlowItem dialogue);

    /// <summary>
    /// 将此包标记为按需：它注册在包注册表中，但排除在所有者的默认自动序列之外。
    /// 可以通过 <see cref="ITutorialRunner.RunPackageAsync"/> 显式启动。
    /// </summary>
    /// <returns>同一包构建器。</returns>
    ITutorialPackageBuilder<TOwner> OnDemand();

    /// <summary>
    /// 完成并注册当前包。
    /// </summary>
    /// <returns>所有者构建器。</returns>
    ITutorialOwnerBuilder<TOwner> Build();
}

/// <summary>
/// 教程步骤的流畅构建器。
/// </summary>
/// <typeparam name="TOwner">所有者类型。</typeparam>
public interface ITutorialStepBuilder<TOwner> : ITutorialPackageBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    /// <summary>
    /// 设置步骤描述文本。
    /// </summary>
    /// <param name="description">步骤描述。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> Text(string description);

    /// <summary>
    /// 设置用于在运行时解析步骤标题的资源键。
    /// </summary>
    /// <param name="titleKey">标题的资源键。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> StepKey(string titleKey);

    /// <summary>
    /// 设置用于在运行时解析步骤描述的资源键。
    /// </summary>
    /// <param name="descriptionKey">描述的资源键。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> TextKey(string descriptionKey);

    /// <summary>
    /// 以名称定位元素。
    /// </summary>
    /// <param name="targetName">目标元素名称。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> TargetName(string targetName);

    /// <summary>
    /// 以标签定位元素。
    /// </summary>
    /// <param name="targetTag">目标标签。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> TargetTag(string targetTag);

    /// <summary>
    /// 定位页面的导航项。
    /// </summary>
    /// <typeparam name="TPage">目标页面类型。</typeparam>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> TargetNavigation<TPage>()
        where TPage : Page;

    /// <summary>
    /// 定位可选宿主下某类型的第一后代。
    /// </summary>
    /// <param name="hostTargetName">可选的宿主目标元素名称。</param>
    /// <param name="targetType">目标后代类型。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> TargetDescendantType(string? hostTargetName, Type targetType);

    /// <summary>
    /// 清除目标解析，并将步骤显示为居中卡片。
    /// </summary>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> NoTarget();

    /// <summary>
    /// 设置交互模式。
    /// </summary>
    /// <param name="interactionMode">交互模式。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> Interaction(ProductTourInteractionMode interactionMode);

    /// <summary>
    /// 设置卡片放置位置。
    /// </summary>
    /// <param name="placement">卡片放置位置。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> Placement(ProductTourPlacement placement);

    /// <summary>
    /// 设置卡片偏移量。
    /// </summary>
    /// <param name="offset">卡片偏移量。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> CardOffset(Point offset);

    /// <summary>
    /// 设置头像放置位置。
    /// </summary>
    /// <param name="placement">头像放置位置。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> AvatarPlacement(ProductTourAvatarPlacement placement);

    /// <summary>
    /// 设置头像姿势。
    /// </summary>
    /// <param name="pose">头像姿势。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> AvatarPose(TutorialAvatarPose pose);

    /// <summary>
    /// 要求步骤完成前接收一个信号。
    /// </summary>
    /// <param name="signalId">信号 id。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> WaitFor(string signalId);

    /// <summary>
    /// 设置目标查找和信号等待的超时时间。
    /// </summary>
    /// <param name="timeout">超时时长。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> Timeout(TimeSpan timeout);

    /// <summary>
    /// 允许目标缺失时跳过此步骤。
    /// </summary>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> AllowMissingTarget();

    /// <summary>
    /// 追加在目标解析和覆盖层显示之前调用的操作。
    /// </summary>
    /// <param name="action">要追加的操作。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> PreStepAction(TutorialStepAction action);

    /// <summary>
    /// 追加一个命名的异步操作，在目标解析和覆盖层显示之前调用。
    /// </summary>
    /// <param name="name">诊断操作名称。</param>
    /// <param name="executeAsync">异步操作体。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 为空。</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync);

    /// <summary>
    /// 追加一个异步操作，在目标解析和覆盖层显示之前调用。
    /// 诊断名称从提供的 lambda 表达式自动捕获。
    /// </summary>
    /// <param name="executeAsync">异步操作体。</param>
    /// <param name="name">诊断操作名称，由编译器从 <paramref name="executeAsync"/> 捕获。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> 为 <see langword="null"/>。</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "");

    /// <summary>
    /// 追加一个命名的同步操作，在目标解析和覆盖层显示之前调用。
    /// </summary>
    /// <param name="name">诊断操作名称。</param>
    /// <param name="action">同步操作体。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 为空。</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(string name, Action<TutorialStepActionContext> action);

    /// <summary>
    /// 追加一个同步操作，在目标解析和覆盖层显示之前调用。
    /// 诊断名称从提供的 lambda 表达式自动捕获。
    /// </summary>
    /// <param name="action">同步操作体。</param>
    /// <param name="name">诊断操作名称，由编译器从 <paramref name="action"/> 捕获。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 <see langword="null"/>。</exception>
    ITutorialStepBuilder<TOwner> PreStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "");

    /// <summary>
    /// 追加在步骤完成且覆盖层关闭后调用的操作。
    /// </summary>
    /// <param name="action">要追加的操作。</param>
    /// <returns>同一步骤构建器。</returns>
    ITutorialStepBuilder<TOwner> PostStepAction(TutorialStepAction action);

    /// <summary>
    /// 追加一个命名的异步操作，在步骤完成且覆盖层关闭后调用。
    /// </summary>
    /// <param name="name">诊断操作名称。</param>
    /// <param name="executeAsync">异步操作体。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 为空。</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync);

    /// <summary>
    /// 追加一个异步操作，在步骤完成且覆盖层关闭后调用。
    /// 诊断名称从提供的 lambda 表达式自动捕获。
    /// </summary>
    /// <param name="executeAsync">异步操作体。</param>
    /// <param name="name">诊断操作名称，由编译器从 <paramref name="executeAsync"/> 捕获。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="executeAsync"/> 为 <see langword="null"/>。</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "");

    /// <summary>
    /// 追加一个命名的同步操作，在步骤完成且覆盖层关闭后调用。
    /// </summary>
    /// <param name="name">诊断操作名称。</param>
    /// <param name="action">同步操作体。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> 为空。</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(string name, Action<TutorialStepActionContext> action);

    /// <summary>
    /// 追加一个同步操作，在步骤完成且覆盖层关闭后调用。
    /// 诊断名称从提供的 lambda 表达式自动捕获。
    /// </summary>
    /// <param name="action">同步操作体。</param>
    /// <param name="name">诊断操作名称，由编译器从 <paramref name="action"/> 捕获。</param>
    /// <returns>同一步骤构建器。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 <see langword="null"/>。</exception>
    ITutorialStepBuilder<TOwner> PostStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "");
}

/// <summary>
/// 用于教程流程的高级构建器。
/// </summary>
public interface ITutorialFlowBuilder
{
    /// <summary>
    /// 设置流程版本。
    /// </summary>
    /// <param name="version">流程版本。</param>
    /// <returns>同一流程构建器。</returns>
    ITutorialFlowBuilder Version(int version);

    /// <summary>
    /// 添加一个包步骤并自动覆盖该包。
    /// </summary>
    /// <param name="package">包引用。</param>
    /// <returns>同一流程构建器。</returns>
    ITutorialFlowBuilder Step(TutorialPackageRef package);

    /// <summary>
    /// 添加一个流程项。
    /// </summary>
    /// <param name="item">流程项。</param>
    /// <returns>同一流程构建器。</returns>
    ITutorialFlowBuilder Step(TutorialFlowItem item);

    /// <summary>
    /// 构建并注册流程。
    /// </summary>
    /// <returns>流程定义。</returns>
    TutorialFlowDefinition Build();
}

/// <summary>
/// 默认的高级教程创作构建器。
/// </summary>
public sealed class TutorialBuilder : ITutorialBuilder
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;

    /// <summary>
    /// 初始化 <see cref="TutorialBuilder"/> 类的新实例。
    /// </summary>
    /// <param name="packageRegistry">包注册表。</param>
    /// <param name="sequenceRegistry">序列注册表。</param>
    /// <param name="flowRegistry">流程注册表。</param>
    public TutorialBuilder(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry)
    {
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _flowRegistry = flowRegistry;
    }

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForPage<TOwner>()
        where TOwner : Page, ITutorialOwner<TOwner> =>
        ForKey<TOwner>(TOwner.TutorialKey);

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForWindow<TOwner>()
        where TOwner : Window, ITutorialOwner<TOwner> =>
        ForKey<TOwner>(TOwner.TutorialKey);

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForRegion<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
        ForKey<TOwner>(TOwner.TutorialKey);

    /// <inheritdoc />
    public ITutorialOwnerBuilder<TOwner> ForKey<TOwner>(string tutorialKey)
        where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
        new TutorialOwnerBuilder<TOwner>(_packageRegistry, _sequenceRegistry, tutorialKey);

    /// <inheritdoc />
    public ITutorialFlowBuilder Flow(string flowId) => new TutorialAuthoringFlowBuilder(_packageRegistry, _flowRegistry, flowId);

    /// <inheritdoc />
    public void RegisterOwner<TOwner>()
        where TOwner : FrameworkElement, ITutorialOwner<TOwner> =>
        TOwner.RegisterTutorials(this);

    /// <inheritdoc />
    public void RegisterApp<TApp>()
        where TApp : Application, IAppTutorial<TApp> =>
        TApp.RegisterTutorials(this);
}

internal sealed class TutorialOwnerBuilder<TOwner> : ITutorialOwnerBuilderInternal<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly string _tutorialKey;
    private readonly List<string> _packageIds = [];
    private readonly List<TutorialPackageDefinition> _packages = [];

    public TutorialOwnerBuilder(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        string tutorialKey)
    {
        _packageRegistry = packageRegistry;
        _sequenceRegistry = sequenceRegistry;
        _tutorialKey = tutorialKey;
    }

    public ITutorialPackageBuilder<TOwner> Package(TutorialPackageRef package) =>
        new TutorialAuthoringPackageBuilder<TOwner>(this, _tutorialKey, package, _packageIds.Count + 1);

    public ITutorialOwnerBuilder<TOwner> Use(TutorialPackageRef package)
    {
        AddSequencePackage(package);
        return this;
    }

    internal void RegisterPackage(TutorialPackageDefinition package) =>
        RegisterPackage(package, isOnDemand: false);

    /// <inheritdoc />
    public void RegisterPackage(TutorialPackageDefinition package, bool isOnDemand)
    {
        ValidateDuplicateContent(package);
        _packageRegistry.Register(package);
        _packages.Add(package);
        if (!isOnDemand)
        {
            AddSequencePackage(new TutorialPackageRef(package.PackageId));
        }
    }

    private void AddSequencePackage(TutorialPackageRef package)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
        {
            throw new ArgumentException("Package id cannot be empty.", nameof(package));
        }

        _packageIds.Add(package.Id);
        RegisterSequence();
    }

    private void RegisterSequence() =>
        _sequenceRegistry.RegisterSequence(_tutorialKey, _packageIds);

    private void ValidateDuplicateContent(TutorialPackageDefinition package)
    {
        var mainStep = package.Steps.FirstOrDefault();
        if (mainStep == null
            || mainStep.TargetKind == TutorialTargetKind.NavigationItem
            || string.IsNullOrWhiteSpace(mainStep.TargetName))
        {
            return;
        }

        var mainStepIdentity = !string.IsNullOrWhiteSpace(mainStep.TitleKey)
            ? mainStep.TitleKey
            : mainStep.Title;
        if (string.IsNullOrWhiteSpace(mainStepIdentity))
        {
            return;
        }

        foreach (var existing in _packages)
        {
            var existingMainStep = existing.Steps.FirstOrDefault();
            if (existingMainStep == null
                || existingMainStep.TargetKind == TutorialTargetKind.NavigationItem
                || string.IsNullOrWhiteSpace(existingMainStep.TargetName))
            {
                continue;
            }

            var existingIdentity = !string.IsNullOrWhiteSpace(existingMainStep.TitleKey)
                ? existingMainStep.TitleKey
                : existingMainStep.Title;
            if (string.IsNullOrWhiteSpace(existingIdentity))
            {
                continue;
            }

            if (string.Equals(existingMainStep.TargetName, mainStep.TargetName, StringComparison.Ordinal)
                && string.Equals(existingIdentity, mainStepIdentity, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Tutorial package '{package.PackageId}' duplicates the main content of '{existing.PackageId}'.");
            }
        }
    }
}

internal sealed class TutorialAuthoringPackageBuilder<TOwner> :
    ITutorialPackageBuilder<TOwner>,
    ITutorialStepBuilder<TOwner>
    where TOwner : FrameworkElement, ITutorialOwner<TOwner>
{
    private readonly ITutorialOwnerBuilderInternal<TOwner> _ownerBuilder;
    private readonly string _tutorialKey;
    private readonly TutorialPackageRef _package;
    private readonly int _sequence;
    private readonly List<TutorialPackageItem> _items = [];
    private ProductTourStep? _currentStep;
    private bool _isOnDemand;

    public TutorialAuthoringPackageBuilder(
        ITutorialOwnerBuilderInternal<TOwner> ownerBuilder,
        string tutorialKey,
        TutorialPackageRef package,
        int sequence)
    {
        _ownerBuilder = ownerBuilder;
        _tutorialKey = tutorialKey;
        _package = package;
        _sequence = sequence;
    }

    public ITutorialStepBuilder<TOwner> Step(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Step title cannot be empty.", nameof(title));
        }

        var step = new ProductTourStep
        {
            Title = title,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _items.Add(new TutorialPackageStepItem { Step = step });
        _currentStep = step;
        return this;
    }

    public ITutorialPackageBuilder<TOwner> Dialogue(DialogueFlowItem dialogue)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        _items.Add(new TutorialPackageDialogueItem { Dialogue = dialogue });
        _currentStep = null;
        return this;
    }

    public ITutorialPackageBuilder<TOwner> OnDemand()
    {
        _isOnDemand = true;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Text(string description)
    {
        EnsureCurrentStep().Description = description;
        return this;
    }

    public ITutorialStepBuilder<TOwner> StepKey(string titleKey)
    {
        if (string.IsNullOrWhiteSpace(titleKey))
        {
            throw new ArgumentException("Step title key cannot be empty.", nameof(titleKey));
        }

        var step = new ProductTourStep
        {
            TitleKey = titleKey,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _items.Add(new TutorialPackageStepItem { Step = step });
        _currentStep = step;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TextKey(string descriptionKey)
    {
        if (string.IsNullOrWhiteSpace(descriptionKey))
        {
            throw new ArgumentException("Step description key cannot be empty.", nameof(descriptionKey));
        }

        EnsureCurrentStep().DescriptionKey = descriptionKey;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetName(string targetName)
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.Name;
        step.TargetName = targetName;
        step.TargetKey = null;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetTag(string targetTag)
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.ElementTag;
        step.TargetName = null;
        step.TargetKey = targetTag;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetNavigation<TPage>()
        where TPage : Page
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.NavigationItem;
        step.TargetName = null;
        step.TargetKey = typeof(TPage).FullName;
        return this;
    }

    public ITutorialStepBuilder<TOwner> TargetDescendantType(string? hostTargetName, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.DescendantType;
        step.TargetName = hostTargetName;
        step.TargetKey = targetType.FullName;
        return this;
    }

    public ITutorialStepBuilder<TOwner> NoTarget()
    {
        var step = EnsureCurrentStep();
        step.TargetKind = TutorialTargetKind.None;
        step.TargetName = null;
        step.TargetKey = null;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Interaction(ProductTourInteractionMode interactionMode)
    {
        EnsureCurrentStep().InteractionMode = interactionMode;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Placement(ProductTourPlacement placement)
    {
        EnsureCurrentStep().Placement = placement;
        return this;
    }

    public ITutorialStepBuilder<TOwner> CardOffset(Point offset)
    {
        EnsureCurrentStep().CardOffset = offset;
        return this;
    }

    public ITutorialStepBuilder<TOwner> AvatarPlacement(ProductTourAvatarPlacement placement)
    {
        EnsureCurrentStep().AvatarPlacement = placement;
        return this;
    }

    public ITutorialStepBuilder<TOwner> AvatarPose(TutorialAvatarPose pose)
    {
        EnsureCurrentStep().AvatarPose = pose;
        return this;
    }

    public ITutorialStepBuilder<TOwner> WaitFor(string signalId)
    {
        var step = EnsureCurrentStep();
        step.WaitForSignalId = signalId;
        step.ExpectedAction = TutorialExpectedAction.SignalReceived;
        step.InteractionMode = step.InteractionMode == ProductTourInteractionMode.BlockAll
            ? ProductTourInteractionMode.AllowTargetOnly
            : step.InteractionMode;
        return this;
    }

    public ITutorialStepBuilder<TOwner> Timeout(TimeSpan timeout)
    {
        EnsureCurrentStep().Timeout = timeout;
        return this;
    }

    public ITutorialStepBuilder<TOwner> AllowMissingTarget()
    {
        EnsureCurrentStep().AllowMissingTarget = true;
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(TutorialStepAction action)
    {
        EnsureCurrentStep().PreStepActions.Add(action);
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PreStepActions.Add(new TutorialStepAction(name, executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PreStepActions.Add(
            new TutorialStepAction(NormalizeActionName(name), executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(string name, Action<TutorialStepActionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PreStepActions.Add(WrapSynchronous(name, action));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PreStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PreStepActions.Add(WrapSynchronous(NormalizeActionName(name), action));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(TutorialStepAction action)
    {
        EnsureCurrentStep().PostStepActions.Add(action);
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(
        string name,
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PostStepActions.Add(new TutorialStepAction(name, executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(
        Func<TutorialStepActionContext, CancellationToken, Task> executeAsync,
        [CallerArgumentExpression(nameof(executeAsync))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
        EnsureCurrentStep().PostStepActions.Add(
            new TutorialStepAction(NormalizeActionName(name), executeAsync));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(string name, Action<TutorialStepActionContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PostStepActions.Add(WrapSynchronous(name, action));
        return this;
    }

    public ITutorialStepBuilder<TOwner> PostStepAction(
        Action<TutorialStepActionContext> action,
        [CallerArgumentExpression(nameof(action))] string name = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureCurrentStep().PostStepActions.Add(WrapSynchronous(NormalizeActionName(name), action));
        return this;
    }

    private static TutorialStepAction WrapSynchronous(string name, Action<TutorialStepActionContext> action) =>
        new(name, (context, _) =>
        {
            action(context);
            return Task.CompletedTask;
        });

    private static string NormalizeActionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Lambda";
        }

        var compressed = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compressed.Length <= 80 ? compressed : compressed[..77] + "...";
    }

    public ITutorialOwnerBuilder<TOwner> Build()
    {
        _ownerBuilder.RegisterPackage(new TutorialPackageDefinition
        {
            PackageId = _package.Id,
            PageKey = _tutorialKey,
            Sequence = _sequence,
            Items = _items.ToArray()
        }, _isOnDemand);
        return _ownerBuilder;
    }

    public ITutorialPackageBuilder<TOwner> Package(TutorialPackageRef package)
    {
        Build();
        return _ownerBuilder.Package(package);
    }

    public ITutorialOwnerBuilder<TOwner> Use(TutorialPackageRef package)
    {
        Build();
        return _ownerBuilder.Use(package);
    }

    private ProductTourStep EnsureCurrentStep()
    {
        if (_currentStep == null)
        {
            throw new InvalidOperationException("No tutorial step is being configured.");
        }

        return _currentStep;
    }
}

internal sealed class TutorialAuthoringFlowBuilder : ITutorialFlowBuilder
{
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialFlowRegistry _flowRegistry;
    private readonly string _flowId;
    private readonly List<TutorialFlowItem> _items = [];
    private readonly List<string> _coveredPackageIds = [];
    private int _version = 1;

    public TutorialAuthoringFlowBuilder(
        ITutorialPackageRegistry packageRegistry,
        ITutorialFlowRegistry flowRegistry,
        string flowId)
    {
        _packageRegistry = packageRegistry;
        _flowRegistry = flowRegistry;
        _flowId = flowId;
    }

    public ITutorialFlowBuilder Version(int version)
    {
        _version = version;
        return this;
    }

    public ITutorialFlowBuilder Step(TutorialPackageRef package)
    {
        if (string.IsNullOrWhiteSpace(package.Id))
        {
            throw new ArgumentException("Package id cannot be empty.", nameof(package));
        }

        _items.Add(new PackageFlowItem { PackageId = package.Id });
        if (!_coveredPackageIds.Contains(package.Id, StringComparer.Ordinal))
        {
            _coveredPackageIds.Add(package.Id);
        }

        return this;
    }

    public ITutorialFlowBuilder Step(TutorialFlowItem item)
    {
        _items.Add(item);
        return this;
    }

    public TutorialFlowDefinition Build()
    {
        ValidatePackageReferences();
        var flow = new TutorialFlowDefinition
        {
            FlowId = _flowId,
            Version = _version,
            IncludedPackageIds = _coveredPackageIds.ToArray(),
            Items = _items.ToArray()
        };
        _flowRegistry.Register(flow);
        return flow;
    }

    private void ValidatePackageReferences()
    {
        foreach (var packageId in _coveredPackageIds)
        {
            var package = _packageRegistry.GetPackage(packageId);
            if (package == null)
            {
                throw new InvalidOperationException($"Tutorial flow '{_flowId}' references missing package '{packageId}'.");
            }

            if (package.Steps.Any(step => IsFallbackStep(step)))
            {
                throw new InvalidOperationException($"Tutorial flow '{_flowId}' references fallback package '{packageId}'.");
            }
        }
    }

    private static bool IsFallbackStep(ProductTourStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.TitleKey)
            && step.TitleKey.StartsWith("Fallback.", StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(step.DescriptionKey)
            && step.DescriptionKey.StartsWith("Fallback.", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(step.Title, "功能教学", StringComparison.Ordinal)
            || step.Description.Contains("详细教学将在", StringComparison.Ordinal);
    }
}
