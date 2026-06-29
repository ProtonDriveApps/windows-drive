using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using Proton.Drive.App.Windows.Views.SignIn;

namespace Proton.Drive.App.Windows.Toolkit.Behaviors;

internal sealed class TotpDigitsBehavior : Behavior<TwoFactorAuthenticationCodeBox>
{
    private readonly List<TextBox> _digitBoxes = [];
    private bool _isUpdatingDigitsFromCode;
    private bool _isLoadedHooked;
    private string? _lastCompletedCode;
    private bool _lastChangeWasPaste;
    private bool _lastChangeFromLastBoxTyping;

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.Loaded += OnAssociatedObjectLoaded;
        _isLoadedHooked = true;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();

        UnhookDigitEvents();
        _digitBoxes.Clear();

        if (!_isLoadedHooked)
        {
            return;
        }

        AssociatedObject.Loaded -= OnAssociatedObjectLoaded;
        _isLoadedHooked = false;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void OnDigitGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.SelectAll();
        }
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private void OnAssociatedObjectLoaded(object sender, RoutedEventArgs e)
    {
        CollectDigitBoxes();
        HookDigitEvents();

        UpdateDigitsFromCode(AssociatedObject.Code);
    }

    private void CollectDigitBoxes()
    {
        _digitBoxes.Clear();
        _digitBoxes.AddRange(FindVisualChildren<TextBox>(AssociatedObject)
            .OrderBy(Grid.GetRow)
            .ThenBy(Grid.GetColumn));
    }

    private void HookDigitEvents()
    {
        foreach (var box in _digitBoxes)
        {
            box.PreviewTextInput += OnDigitPreviewTextInput;
            box.PreviewKeyDown += OnDigitPreviewKeyDown;
            box.GotFocus += OnDigitGotFocus;
            DataObject.AddPastingHandler(box, OnPasting);
            CommandManager.AddPreviewExecutedHandler(box, OnPreviewExecuted);
        }
    }

    private void UnhookDigitEvents()
    {
        foreach (var box in _digitBoxes)
        {
            box.PreviewTextInput -= OnDigitPreviewTextInput;
            box.PreviewKeyDown -= OnDigitPreviewKeyDown;
            box.GotFocus -= OnDigitGotFocus;
            DataObject.RemovePastingHandler(box, OnPasting);
            CommandManager.RemovePreviewExecutedHandler(box, OnPreviewExecuted);
        }
    }

    private void OnDigitPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox current)
        {
            return;
        }

        var digit = e.Text.FirstOrDefault(IsAsciiDigit);

        if (digit == 0)
        {
            e.Handled = true;
            return;
        }

        current.Text = digit.ToString();
        current.CaretIndex = current.Text.Length;
        e.Handled = true;

        _lastChangeFromLastBoxTyping = _digitBoxes.Count > 0 && ReferenceEquals(current, _digitBoxes[^1]);

        MoveFocusToNext(current);
        UpdateCodeFromDigits();
    }

    private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (sender is not TextBox current)
        {
            return;
        }

        if (e.Command != ApplicationCommands.Paste)
        {
            return;
        }

        HandlePaste(current);
        e.Handled = true;
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox current)
        {
            return;
        }

        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText) && !e.DataObject.GetDataPresent(DataFormats.Text))
        {
            return;
        }

        var text = (string?)e.DataObject.GetData(DataFormats.UnicodeText) ?? (string?)e.DataObject.GetData(DataFormats.Text);

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        HandlePaste(current, text);
        e.CancelCommand();
    }

    private void OnDigitPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox current)
        {
            return;
        }

        bool isCtrlV = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V;
        if (isCtrlV)
        {
            HandlePaste(current);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Back:
                if (!string.IsNullOrEmpty(current.Text))
                {
                    current.Clear();
                }
                else
                {
                    MoveFocusToPrevious(current, clear: true);
                }

                UpdateCodeFromDigits();
                e.Handled = true;
                break;

            case Key.Delete:
                current.Clear();
                UpdateCodeFromDigits();
                e.Handled = true;
                break;

            case Key.Left:
                MoveFocusToPrevious(current, clear: false);
                e.Handled = true;
                break;

            case Key.Right:
                MoveFocusToNext(current);
                e.Handled = true;
                break;

            case Key.Home:
                if (_digitBoxes.Count > 0)
                {
                    _digitBoxes[0].Focus();
                    _digitBoxes[0].SelectAll();
                }

                e.Handled = true;
                break;

            case Key.End:
                if (_digitBoxes.Count > 0)
                {
                    var last = _digitBoxes[^1];
                    last.Focus();
                    last.SelectAll();
                }

                e.Handled = true;
                break;
        }
    }

    private void HandlePaste(TextBox startBox)
    {
        string? text;
        try
        {
            text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch
        {
            text = null;
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        HandlePaste(startBox, text);
    }

    private void HandlePaste(TextBox startBox, string text)
    {
        var digits = text.Where(IsAsciiDigit).Take(_digitBoxes.Count).ToArray();
        if (digits.Length == 0)
        {
            return;
        }

        int startIndex = _digitBoxes.IndexOf(startBox);
        if (startIndex < 0)
        {
            startIndex = 0;
        }

        int di = 0;
        for (int i = startIndex; i < _digitBoxes.Count && di < digits.Length; i++)
        {
            _digitBoxes[i].Text = digits[di].ToString();
            di++;
        }

        // Do not wrap-around; stop when out of boxes
        var nextEmpty = _digitBoxes.FirstOrDefault(b => string.IsNullOrEmpty(b.Text));
        (nextEmpty ?? _digitBoxes[Math.Min(startIndex + digits.Length - 1, _digitBoxes.Count - 1)]).Focus();

        _lastChangeWasPaste = true;
        _lastChangeFromLastBoxTyping = false;
        UpdateCodeFromDigits();
    }

    private void MoveFocusToNext(TextBox current)
    {
        int index = _digitBoxes.IndexOf(current);
        if (index < 0 || index >= _digitBoxes.Count - 1)
        {
            return;
        }

        _digitBoxes[index + 1].Focus();
        _digitBoxes[index + 1].SelectAll();
    }

    private void MoveFocusToPrevious(TextBox current, bool clear)
    {
        int index = _digitBoxes.IndexOf(current);
        if (index <= 0)
        {
            return;
        }

        var prev = _digitBoxes[index - 1];
        prev.Focus();

        if (clear)
        {
            prev.Clear();
        }

        prev.SelectAll();
    }

    private void UpdateCodeFromDigits()
    {
        var code = string.Concat(_digitBoxes.Select(b => b.Text).Where(s => !string.IsNullOrEmpty(s)));

        _isUpdatingDigitsFromCode = true;
        AssociatedObject.Code = string.IsNullOrEmpty(code) ? null : code;
        _isUpdatingDigitsFromCode = false;

        TryRaiseCodeCompleted(code);
    }

    private void UpdateDigitsFromCode(string? newCode)
    {
        if (_isUpdatingDigitsFromCode)
        {
            return;
        }

        string code = newCode ?? string.Empty;
        for (int i = 0; i < _digitBoxes.Count; i++)
        {
            _digitBoxes[i].Text = i < code.Length ? code[i].ToString() : string.Empty;
        }
    }

    private void TryRaiseCodeCompleted(string code)
    {
        if (!_lastChangeWasPaste && !_lastChangeFromLastBoxTyping)
        {
            return;
        }

        _lastChangeWasPaste = false;
        _lastChangeFromLastBoxTyping = false;

        if (code.Length != _digitBoxes.Count)
        {
            _lastCompletedCode = null;
            return;
        }

        if (code == _lastCompletedCode)
        {
            return;
        }

        _lastCompletedCode = code;
        AssociatedObject.RaiseCodeCompleted();
    }
}
