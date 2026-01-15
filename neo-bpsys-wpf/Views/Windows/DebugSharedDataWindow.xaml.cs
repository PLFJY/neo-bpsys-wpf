using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class DebugSharedDataWindow : Window
{
    private readonly ISharedDataService _sharedDataService;

    public DebugSharedDataWindow(ISharedDataService sharedDataService)
    {
        InitializeComponent();
        _sharedDataService = sharedDataService;
        Loaded += (s, e) => RefreshData();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshData();
    }

    private void RefreshData()
    {
        var rootNodes = new ObservableCollection<DebugNode>();
        if (_sharedDataService != null)
        {
            var root = new DebugNode("ISharedDataService", _sharedDataService, "Subject", Dispatcher);
            // Auto expand the root
            root.IsExpanded = true;
            rootNodes.Add(root);
        }
        DebugTree.ItemsSource = rootNodes;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F10)
        {
            Topmost = !Topmost;
        }
    }
}

public class DebugNode : INotifyPropertyChanged
{
    public string Name { get; }
    public string Type { get; }
    public string Value { get; private set; }

    public ObservableCollection<DebugNode> Children { get; } = new();

    private readonly object? _obj;
    private readonly Dispatcher _dispatcher;
    private bool _isExpanded;
    private bool _wasExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            if (_isExpanded && !_wasExpanded)
            {
                _wasExpanded = true;
                LoadChildren();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public DebugNode(string name, object? obj, string type, Dispatcher dispatcher)
    {
        Name = name;
        _obj = obj;
        Type = type;
        _dispatcher = dispatcher;
        Value = GetValueString(obj);

        if (CanExpand(obj))
        {
            Children.Add(new DebugNode("Loading...", null, "", dispatcher));
        }
    }

    private string GetValueString(object? obj)
    {
        if (obj == null) return "null";

        // Handle ImageSource specially (UI access)
        if (obj is ImageSource)
        {
            if (obj is BitmapImage bmp)
            {
                try
                {
                    if (bmp.CheckAccess()) return $"BitmapImage: {bmp.UriSource}";
                    return _dispatcher.Invoke(() => $"BitmapImage: {bmp.UriSource}");
                }
                catch
                {
                    return "BitmapImage (Error getting Uri)";
                }
            }
            return "ImageSource";
        }

        // Handle simple types
        var type = obj.GetType();
        if (IsSimpleType(type)) return obj.ToString() ?? "";

        // Collections
        if (obj is ICollection collection) return $"Count = {collection.Count}";
        if (obj is IEnumerable) return "IEnumerable";

        return obj.ToString() ?? type.Name;
    }

    private bool CanExpand(object? obj)
    {
        if (obj == null) return false;
        if (obj is string) return false; // string is IEnumerable but we treat as simple
        if (obj is ImageSource) return false; // Optimize: don't expand images

        var type = obj.GetType();
        if (type.IsValueType && IsSimpleType(type)) return false;

        return true;
    }

    // Check for "simple" types that are technically objects/structs but we display as value
    private bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(TimeSpan)
               || type == typeof(Guid);
    }

    private void LoadChildren()
    {
        Children.Clear();
        if (_obj == null) return;

        var type = _obj.GetType();

        // 1. If it's a collection/dictionary
        if (_obj is IEnumerable enumerable && type != typeof(string))
        {
            int index = 0;
            try
            {
                foreach (var item in enumerable)
                {
                    if (index >= 100)
                    {
                        Children.Add(new DebugNode("...", null, "Limit reached", _dispatcher));
                        break;
                    }

                    string childName = $"[{index}]";
                    // Special handling for DictionaryEntry or KeyValuePair if usually found
                    // But reflection handles generic enumeration fine.
                    // For Dictionary<K,V>, item is KeyValuePair<K,V>. 
                    // Reflection on KVP works fine (Key, Value properties).

                    Children.Add(new DebugNode(childName, item, item?.GetType().Name ?? "null", _dispatcher));
                    index++;
                }
            }
            catch (Exception ex)
            {
                Children.Add(new DebugNode("Error", null, ex.Message, _dispatcher));
            }
            return;
        }

        // 2. Reflection for properties
        try
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.GetIndexParameters().Length > 0) continue; // Skip indexers

                object? val = null;
                string typeName = prop.PropertyType.Name;
                try
                {
                    // Check thread access for DispatcherObjects
                    if (_obj is DispatcherObject dObj && !dObj.CheckAccess())
                    {
                        // Using Invoke to get property if strictly necessary? 
                        // Usually properties of Models are thread safe or we are on UI thread (Node expansion happens on UI)
                        // But if _obj is a UI Control, accessing properties might need check.
                        // But for SharedDataService, it's mostly Models.
                        val = _dispatcher.Invoke(() => prop.GetValue(_obj));
                    }
                    else
                    {
                        val = prop.GetValue(_obj);
                    }
                }
                catch (Exception ex)
                {
                    val = null; // or error string
                    typeName = "Error: " + ex.Message;
                }

                Children.Add(new DebugNode(prop.Name, val, typeName, _dispatcher));
            }
        }
        catch (Exception ex)
        {
            Children.Add(new DebugNode("Reflection Error", null, ex.Message, _dispatcher));
        }
    }
}
