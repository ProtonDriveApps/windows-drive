using System.Windows;

namespace Proton.Drive.App.Windows.Views.SignIn;

internal partial class TwoFactorAuthenticationCodeBox
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        name: nameof(Code),
        propertyType: typeof(string),
        ownerType: typeof(TwoFactorAuthenticationCodeBox),
        new FrameworkPropertyMetadata(defaultValue: null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly RoutedEvent CodeCompletedEvent = EventManager.RegisterRoutedEvent(
        name: nameof(CodeCompleted),
        routingStrategy: RoutingStrategy.Bubble,
        handlerType: typeof(RoutedEventHandler),
        ownerType: typeof(TwoFactorAuthenticationCodeBox));

    public TwoFactorAuthenticationCodeBox()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler CodeCompleted
    {
        add => AddHandler(CodeCompletedEvent, value);
        remove => RemoveHandler(CodeCompletedEvent, value);
    }

    public string? Code
    {
        get => (string?)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public void RaiseCodeCompleted()
    {
        RaiseEvent(new RoutedEventArgs(CodeCompletedEvent));
    }
}
