using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 在布局包页面和文件关联导入入口之间共享导入状态。
/// </summary>
public sealed class BpuiPackageImportState : ObservableObject
{
    private int _importCount;
    private bool _isImporting;

    /// <summary>
    /// 获取是否有布局包正在导入。
    /// </summary>
    public bool IsImporting
    {
        get => _isImporting;
        private set => SetProperty(ref _isImporting, value);
    }

    /// <summary>
    /// 在 UI 线程开始一次导入，并返回用于结束该次导入的作用域。
    /// </summary>
    /// <returns>导入结束时应释放的作用域。</returns>
    public IDisposable BeginImport()
    {
        _importCount++;
        IsImporting = true;
        return new ImportScope(this);
    }

    private sealed class ImportScope(BpuiPackageImportState owner) : IDisposable
    {
        private BpuiPackageImportState? _owner = owner;

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null)
            {
                return;
            }

            owner._importCount--;
            owner.IsImporting = owner._importCount > 0;
        }
    }
}
