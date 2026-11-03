using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace AlbionMarket.App;

public sealed class FilterOption : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public FilterOption(string label, string value, bool isChecked = false)
    {
        Label = label;
        Value = value;
        _isChecked = isChecked;
    }

    // Required by the WPF XAML loader for inline filter options.
    public FilterOption() { }

    public event PropertyChangedEventHandler? PropertyChanged;
}

[ContentProperty(nameof(Options))]
public partial class MultiSelectComboBox : UserControl
{
    public ObservableCollection<FilterOption> Options { get; } = new();
    public string Summary
    {
        get
        {
            var all = Options.FirstOrDefault(x => x.Value.Length == 0);
            if (all?.IsChecked == true) return all.Label;
            var selected = Options.Where(x => x.IsChecked && x.Value.Length > 0)
                .Select(x => x.Label).ToList();
            return selected.Count switch
            {
                0 => all?.Label ?? "Totes",
                <= 2 => string.Join(" + ", selected),
                _ => $"{selected.Count} seleccionats"
            };
        }
    }

    public MultiSelectComboBox()
    {
        InitializeComponent();
        Options.CollectionChanged += Options_CollectionChanged;
    }

    private void Options_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (FilterOption option in e.NewItems) option.PropertyChanged += Option_PropertyChanged;
        if (e.OldItems is not null)
            foreach (FilterOption option in e.OldItems) option.PropertyChanged -= Option_PropertyChanged;
        UpdateSummary();
    }

    private void Option_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FilterOption.IsChecked)) return;
        if (sender is FilterOption changed)
        {
            if (changed.Value.Length == 0 && changed.IsChecked)
            {
                foreach (var option in Options.Where(x => x != changed)) option.IsChecked = false;
            }
            else if (changed.Value.Length > 0 && changed.IsChecked)
            {
                var all = Options.FirstOrDefault(x => x.Value.Length == 0);
                if (all is not null) all.IsChecked = false;
            }
        }
        UpdateSummary();
    }

    private void UpdateSummary() =>
        Dispatcher.BeginInvoke(() =>
        {
            Button.Content = Summary;
        });

    private void Button_Click(object sender, RoutedEventArgs e) => Popup.IsOpen = true;

    public HashSet<string> SelectedValues() =>
        Options.Where(x => x.IsChecked && x.Value.Length > 0)
            .Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void SetSelectedValues(IEnumerable<string> values)
    {
        var selected = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var option in Options)
            option.IsChecked = option.Value.Length == 0
                ? selected.Count == 0
                : selected.Contains(option.Value);
        UpdateSummary();
    }
}
