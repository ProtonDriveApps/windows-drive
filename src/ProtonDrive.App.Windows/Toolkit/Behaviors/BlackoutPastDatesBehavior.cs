using System;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace ProtonDrive.App.Windows.Toolkit.Behaviors;

internal sealed class BlackoutPastDatesBehavior : Behavior<DatePicker>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.SelectedDateChanged += OnSelectedDateChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.SelectedDateChanged -= OnSelectedDateChanged;
    }

    private void OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        var tomorrow = DateTime.Today.AddDays(1);

        AssociatedObject.BlackoutDates.Clear();

        var selectedDateIsPast = AssociatedObject.SelectedDate.HasValue && AssociatedObject.SelectedDate.Value.Date < DateTime.Today;

        AssociatedObject.DisplayDateStart = selectedDateIsPast ? AssociatedObject.SelectedDate : tomorrow;

        if (!selectedDateIsPast || AssociatedObject.SelectedDate is null)
        {
            return;
        }

        AssociatedObject.BlackoutDates.Add(new CalendarDateRange(AssociatedObject.SelectedDate.Value.Date.AddDays(1), tomorrow));
    }
}
