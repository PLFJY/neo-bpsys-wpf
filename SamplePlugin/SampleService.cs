namespace SamplePlugin;

/// <summary>
/// 示例服务接口
/// </summary>
public interface ISampleService
{
    /// <summary>
    /// 获取问候语
    /// </summary>
    string GetGreeting(string name);

    /// <summary>
    /// 执行示例操作
    /// </summary>
    Task<int> PerformCalculationAsync(int a, int b);
}

/// <summary>
/// 示例服务实现
/// </summary>
public class SampleService : ISampleService
{
    /// <inheritdoc/>
    public string GetGreeting(string name)
    {
        return $"你好，{name}！这是来自示例插件的问候。";
    }

    /// <inheritdoc/>
    public async Task<int> PerformCalculationAsync(int a, int b)
    {
        // 模拟异步操作
        await Task.Delay(100);
        return a + b;
    }
}
