using EngineeringSupporter.Business.TodoView;
using EngineeringSupporter.DB;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;

namespace EngineeringSupporter.Controls;

public partial class TodoView : ContentView
{
    private readonly TodoViewViewModel _viewModel;

    public TodoView()
    {
        InitializeComponent();
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var dbContext = services?.GetService<AppDbContext>();
        _viewModel = new TodoViewViewModel(dbContext);
        BindingContext = _viewModel;
        SizeChanged += (_, _) => QueueColumnWidthUpdate();
        Loaded += (_, _) =>
        {
            QueueColumnWidthUpdate();
            var loadedServices = Application.Current?.Handler?.MauiContext?.Services;
            var loadedDbContext = loadedServices?.GetService<AppDbContext>();
            if (loadedDbContext is not null)
            {
                _viewModel.SetDbContext(loadedDbContext);
            }
        };
        _viewModel.Rows.CollectionChanged += OnRowsCollectionChanged;
    }

    protected override void OnBindingContextChanged()
    {
        if (BindingContext != _viewModel)
        {
            BindingContext = _viewModel;
        }

        base.OnBindingContextChanged();
    }

    private async void OnProgressEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        if (entry.BindingContext is not TodoViewCell cell)
        {
            return;
        }

        var row = FindRow(entry);
        if (row is null)
        {
            return;
        }

        await _viewModel.CommitProgressAsync(row, cell);
    }

    private static TodoViewRow? FindRow(Element element)
    {
        var current = element.Parent;
        while (current is not null)
        {
            if (current is BindableObject bindable && bindable.BindingContext is TodoViewRow row)
            {
                return row;
            }

            current = current.Parent;
        }

        return null;
    }

    private void QueueColumnWidthUpdate()
    {
        Dispatcher.Dispatch(UpdateColumnWidths);
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            LayoutRoot?.InvalidateMeasure();
        });
    }

    private void UpdateColumnWidths()
    {
        if (TableGrid is null)
        {
            return;
        }

        var availableWidth = TableGrid.Width - TableGrid.Padding.Left - TableGrid.Padding.Right;
        if (availableWidth <= 0)
        {
            return;
        }

        _viewModel.UpdateColumnWidths(availableWidth);
    }

}
