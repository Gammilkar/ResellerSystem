using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class InventoryView : UserControl
{
    // Avalonia's DataGrid has no built-in row-resize drag (only
    // column-resize — confirmed against the compiled assembly), so this
    // reimplements the same Excel-style interaction by hand on the "⇕"
    // grip cell. The handlers are attached with handledEventsToo:true
    // because DataGridCell/DataGridRow mark pointer events Handled during
    // their own (earlier, tunnelling) selection logic — without
    // handledEventsToo the grip's Bubble-phase handlers never fire and the
    // row simply never grows, which is exactly the bug this fixes. Delta is
    // tracked against the TopLevel (a fixed reference, since the row's own
    // bounds shift as its height changes mid-drag), writing straight into
    // the row's own RowHeight, which DataGridRow's Style is bound to.
    private Control? _dragGrip;
    private InventoryRowViewModel? _dragRow;
    private Point _dragStartPoint;
    private double _dragStartHeight;

    public InventoryView()
    {
        InitializeComponent();
    }

    private void RowHeightGrip_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not InputElement control) return;
        control.AddHandler(InputElement.PointerPressedEvent, RowHeightGrip_PointerPressed, handledEventsToo: true);
        control.AddHandler(InputElement.PointerMovedEvent, RowHeightGrip_PointerMoved, handledEventsToo: true);
        control.AddHandler(InputElement.PointerReleasedEvent, RowHeightGrip_PointerReleased, handledEventsToo: true);
    }

    private void RowHeightGrip_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not InputElement control) return;
        control.RemoveHandler(InputElement.PointerPressedEvent, RowHeightGrip_PointerPressed);
        control.RemoveHandler(InputElement.PointerMovedEvent, RowHeightGrip_PointerMoved);
        control.RemoveHandler(InputElement.PointerReleasedEvent, RowHeightGrip_PointerReleased);
    }

    private void RowHeightGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not InventoryRowViewModel row) return;
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel is null) return;

        e.Pointer.Capture(control);
        _dragGrip = control;
        _dragRow = row;
        _dragStartPoint = e.GetPosition(topLevel);
        _dragStartHeight = row.RowHeight;
        e.Handled = true;
    }

    private void RowHeightGrip_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragRow is null || _dragGrip is null || !ReferenceEquals(sender, _dragGrip)) return;
        var topLevel = TopLevel.GetTopLevel(_dragGrip);
        if (topLevel is null) return;

        var current = e.GetPosition(topLevel);
        var delta = current.Y - _dragStartPoint.Y;
        _dragRow.RowHeight = Math.Clamp(_dragStartHeight + delta, 16, 300);
        e.Handled = true;
    }

    private void RowHeightGrip_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragGrip is null) return;
        e.Pointer.Capture(null);
        _dragGrip = null;
        _dragRow = null;
        e.Handled = true;
    }
}
