using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CluckWise;

/// <summary>
/// Drag-to-reorder for items shown by an ItemsControl bound to an ObservableCollection.
/// Wire an item's root element to these handlers and set AllowDrop="True" on it.
/// </summary>
public static class DragReorder
{
    private static Point _start;
    private static object? _dragged;
    private static Brush? _savedBrush;

    public static void MouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(null);
        _dragged = ((FrameworkElement)sender).DataContext;
    }

    /// <summary>Forget any pending drag (e.g. when a button inside the item was pressed).</summary>
    public static void Cancel() => _dragged = null;

    public static void MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragged == null || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(null) - _start;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var source = (FrameworkElement)sender;
        source.Opacity = 0.5;
        try { DragDrop.DoDragDrop(source, new DataObject(typeof(object), _dragged), DragDropEffects.Move); }
        finally { source.Opacity = 1; _dragged = null; }
    }

    public static void DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        if (sender is Border b && _savedBrush == null)
        {
            _savedBrush = b.BorderBrush;
            b.BorderBrush = (Brush)Application.Current.FindResource("Yolk");
        }
        e.Handled = true;
    }

    public static void DragLeave(object sender, DragEventArgs e) => Restore(sender);

    /// <summary>Moves the dragged item to the drop target's position. Returns true if the order changed.</summary>
    public static bool Drop(object sender, DragEventArgs e)
    {
        Restore(sender);
        var dragged = e.Data.GetData(typeof(object));
        var target = ((FrameworkElement)sender).DataContext;
        var owner = ItemsOwner((DependencyObject)sender);
        if (dragged == null || target == null || dragged == target || owner?.ItemsSource is not System.Collections.IList list) return false;

        int from = list.IndexOf(dragged), to = list.IndexOf(target);
        if (from < 0 || to < 0) return false;
        var move = list.GetType().GetMethod(nameof(ObservableCollection<object>.Move));
        if (move != null) move.Invoke(list, new object[] { from, to });
        else { list.RemoveAt(from); list.Insert(to, dragged); }
        return true;
    }

    private static void Restore(object sender)
    {
        if (sender is Border b && _savedBrush != null) b.BorderBrush = _savedBrush;
        _savedBrush = null;
    }

    private static ItemsControl? ItemsOwner(DependencyObject d)
    {
        for (; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is ItemsControl ic) return ic;
        return null;
    }
}
