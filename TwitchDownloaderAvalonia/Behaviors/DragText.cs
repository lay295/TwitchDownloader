using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TwitchDownloaderAvalonia.Behaviors
{
    public static class DragText
    {
        public static readonly AttachedProperty<string?> PayloadProperty =
            AvaloniaProperty.RegisterAttached<Control, string?>("Payload", typeof(DragText));

        public static readonly AttachedProperty<bool> AcceptDropProperty =
            AvaloniaProperty.RegisterAttached<TextBox, bool>("AcceptDrop", typeof(DragText));

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
            control.RemoveHandler(InputElement.PointerPressedEvent, OnSourcePointerPressed);

            if (string.IsNullOrEmpty(args.GetNewValue<string?>()))
                return;

            control.AddHandler(InputElement.PointerPressedEvent, OnSourcePointerPressed, RoutingStrategies.Tunnel);
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

            e.Handled = true;
            _ = StartDragAsync(e, payload);
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

            if (!enabled)
            {
                box.Classes.Set("dropTarget", false);
                return;
            }

            box.AddHandler(DragDrop.DragOverEvent, OnTargetDragOver);
            box.AddHandler(DragDrop.DragEnterEvent, OnTargetDragEnter);
            box.AddHandler(DragDrop.DragLeaveEvent, OnTargetDragLeave);
            box.AddHandler(DragDrop.DropEvent, OnTargetDrop);
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

            var text = box.Text ?? string.Empty;
            var caret = box.IsFocused
                ? Math.Clamp(box.CaretIndex, 0, text.Length)
                : text.Length;

            box.Text = text.Insert(caret, token);
            box.CaretIndex = caret + token.Length;
            box.Focus();
            e.Handled = true;
        }

        private static bool HasText(DragEventArgs e) => e.DataTransfer.TryGetText() is { Length: > 0 };
    }
}
