using Avalonia.Input;
using Avalonia.Interactivity;

namespace TwitchDownloaderAvalonia.Behaviors
{
    public static class DragText
    {
        private const double DRAG_THRESHOLD = 4;

        public static readonly AttachedProperty<string?> PayloadProperty =
            AvaloniaProperty.RegisterAttached<Control, string?>("Payload", typeof(DragText));

        public static readonly AttachedProperty<bool> AcceptDropProperty =
            AvaloniaProperty.RegisterAttached<TextBox, bool>("AcceptDrop", typeof(DragText));

        private static readonly AttachedProperty<Point?> DragOriginProperty =
            AvaloniaProperty.RegisterAttached<Control, Point?>("DragOrigin", typeof(DragText));

        private static readonly AttachedProperty<PointerPressedEventArgs?> DragPressProperty =
            AvaloniaProperty.RegisterAttached<Control, PointerPressedEventArgs?>("DragPress", typeof(DragText));

        private static TextBox? _lastDropTarget;

        static DragText()
        {
            PayloadProperty.Changed.AddClassHandler<Control>(OnPayloadChanged);
            AcceptDropProperty.Changed.AddClassHandler<TextBox>(OnAcceptDropChanged);
        }

        public static string? GetPayload(Control control) => control.GetValue(PayloadProperty);
        public static void SetPayload(Control control, string? value) => control.SetValue(PayloadProperty, value);

        public static bool GetAcceptDrop(TextBox box) => box.GetValue(AcceptDropProperty);
        public static void SetAcceptDrop(TextBox box, bool value) => box.SetValue(AcceptDropProperty, value);

        private static void OnPayloadChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            DetachSourceHandlers(control);

            if (string.IsNullOrEmpty(args.GetNewValue<string?>()))
                return;

            control.AddHandler(InputElement.PointerPressedEvent, OnSourcePointerPressed, RoutingStrategies.Tunnel);
            control.AddHandler(InputElement.PointerMovedEvent, OnSourcePointerMoved, RoutingStrategies.Tunnel);
            control.AddHandler(InputElement.PointerReleasedEvent, OnSourcePointerReleased, RoutingStrategies.Tunnel);
            control.AddHandler(InputElement.PointerCaptureLostEvent, OnSourceCaptureLost, RoutingStrategies.Direct);
        }

        private static void DetachSourceHandlers(Control control)
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnSourcePointerPressed);
            control.RemoveHandler(InputElement.PointerMovedEvent, OnSourcePointerMoved);
            control.RemoveHandler(InputElement.PointerReleasedEvent, OnSourcePointerReleased);
            control.RemoveHandler(InputElement.PointerCaptureLostEvent, OnSourceCaptureLost);

            control.SetValue(DragOriginProperty, null);
            control.SetValue(DragPressProperty, null);
        }

        private static void OnSourcePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control)
                return;
            if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
                return;

            var payload = GetPayload(control);
            if (string.IsNullOrEmpty(payload))
                return;

            control.SetValue(DragOriginProperty, e.GetPosition(control));
            control.SetValue(DragPressProperty, e);

            e.Pointer.Capture(control);
        }

        private static void OnSourcePointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control)
                return;

            var origin = control.GetValue(DragOriginProperty);
            if (origin is null)
                return;

            var delta = e.GetPosition(control) - origin.Value;
            if (Math.Abs(delta.X) < DRAG_THRESHOLD && Math.Abs(delta.Y) < DRAG_THRESHOLD)
                return;

            var payload = GetPayload(control);
            var press = control.GetValue(DragPressProperty);
            control.SetValue(DragOriginProperty, null);
            control.SetValue(DragPressProperty, null);
            e.Pointer.Capture(null);
            if (press is null || string.IsNullOrEmpty(payload))
                return;

            e.Handled = true;
            _ = StartDragAsync(press, payload);
        }

        private static void OnSourcePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is not Control control)
                return;

            var origin = control.GetValue(DragOriginProperty);
            control.SetValue(DragOriginProperty, null);
            control.SetValue(DragPressProperty, null);
            e.Pointer.Capture(null);
            if (origin is null)
                return;

            var payload = GetPayload(control);
            if (string.IsNullOrEmpty(payload))
                return;

            InsertToken(payload);
            e.Handled = true;
        }

        private static void OnSourceCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (sender is not Control control)
                return;

            control.SetValue(DragOriginProperty, null);
            control.SetValue(DragPressProperty, null);
        }

        private static async Task StartDragAsync(PointerPressedEventArgs trigger, string payload)
        {
            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(payload));
                await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Copy);
            }
            catch (Exception)
            {
                // Drag-and-drop can fail if the pointer is released before the operation starts.
            }
        }

        private static void OnAcceptDropChanged(TextBox box, AvaloniaPropertyChangedEventArgs args)
        {
            var enabled = args.GetNewValue<bool>();
            DragDrop.SetAllowDrop(box, enabled);

            box.RemoveHandler(DragDrop.DragOverEvent, OnTargetDragOver);
            box.RemoveHandler(DragDrop.DragEnterEvent, OnTargetDragEnter);
            box.RemoveHandler(DragDrop.DragLeaveEvent, OnTargetDragLeave);
            box.RemoveHandler(DragDrop.DropEvent, OnTargetDrop);

            box.GotFocus -= OnDropTargetGotFocus;

            if (!enabled)
            {
                box.Classes.Set("dropTarget", false);
                if (ReferenceEquals(_lastDropTarget, box))
                    _lastDropTarget = null;

                return;
            }

            box.AddHandler(DragDrop.DragOverEvent, OnTargetDragOver);
            box.AddHandler(DragDrop.DragEnterEvent, OnTargetDragEnter);
            box.AddHandler(DragDrop.DragLeaveEvent, OnTargetDragLeave);
            box.AddHandler(DragDrop.DropEvent, OnTargetDrop);

            box.GotFocus += OnDropTargetGotFocus;
        }

        private static void OnDropTargetGotFocus(object? sender, EventArgs e)
        {
            if (sender is TextBox box)
                _lastDropTarget = box;
        }

        private static void OnTargetDragEnter(object? sender, DragEventArgs e)
        {
            if (sender is TextBox box && HasText(e))
                box.Classes.Set("dropTarget", true);
        }

        private static void OnTargetDragLeave(object? sender, DragEventArgs e)
        {
            if (sender is TextBox box)
                box.Classes.Set("dropTarget", false);
        }

        private static void OnTargetDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = HasText(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private static void OnTargetDrop(object? sender, DragEventArgs e)
        {
            if (sender is not TextBox box)
                return;

            box.Classes.Set("dropTarget", false);
            if (e.DataTransfer.TryGetText() is not { Length: > 0 } token)
                return;

            InsertToken(box, token);
            e.Handled = true;
        }

        private static void InsertToken(string token)
        {
            var target = _lastDropTarget is { } focused && GetAcceptDrop(focused)
                ? focused
                : null;

            if (target is null)
                return;

            InsertToken(target, token);
        }

        private static void InsertToken(TextBox box, string token)
        {
            var text = box.Text ?? string.Empty;
            var caret = box.IsFocused
                ? Math.Clamp(box.CaretIndex, 0, text.Length)
                : text.Length;

            box.Text = text.Insert(caret, token);
            box.CaretIndex = caret + token.Length;
            box.Focus();

            _lastDropTarget = box;
        }

        private static bool HasText(DragEventArgs e) => e.DataTransfer.TryGetText() is { Length: > 0 };
    }
}
