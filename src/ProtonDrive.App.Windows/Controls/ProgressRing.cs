/*
MIT License

Copyright (c) .NET Foundation and Contributors. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ProtonDrive.App.Windows.Controls;

[TemplateVisualState(Name = "Large", GroupName = "SizeStates")]
[TemplateVisualState(Name = "Small", GroupName = "SizeStates")]
[TemplateVisualState(Name = "Inactive", GroupName = "ActiveStates")]
[TemplateVisualState(Name = "Active", GroupName = "ActiveStates")]
public class ProgressRing : Control
{
    /// <summary>Identifies the <see cref="EllipseDiameterScale"/> dependency property.</summary>
    public static readonly DependencyProperty EllipseDiameterScaleProperty
        = DependencyProperty.Register(
            nameof(EllipseDiameterScale),
            typeof(double),
            typeof(ProgressRing),
            new PropertyMetadata(1D));

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
    public static readonly DependencyProperty IsActiveProperty
        = DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(ProgressRing),
            new PropertyMetadata(true, OnIsActivePropertyChanged));

    /// <summary>Identifies the <see cref="IsLarge"/> dependency property.</summary>
    public static readonly DependencyProperty IsLargeProperty
        = DependencyProperty.Register(
            nameof(IsLarge),
            typeof(bool),
            typeof(ProgressRing),
            new PropertyMetadata(true, OnIsLargePropertyChanged));

    /// <summary>Identifies the <see cref="BindableWidth"/> dependency property.</summary>
    private static readonly DependencyPropertyKey BindableWidthPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(BindableWidth),
            typeof(double),
            typeof(ProgressRing),
            new PropertyMetadata(default(double), OnBindableWidthPropertyChanged));

    /// <summary>Identifies the <see cref="MaxSideLength"/> dependency property.</summary>
    private static readonly DependencyPropertyKey MaxSideLengthPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(MaxSideLength),
            typeof(double),
            typeof(ProgressRing),
            new PropertyMetadata(default(double)));

    /// <summary>Identifies the <see cref="EllipseDiameter"/> dependency property.</summary>
    private static readonly DependencyPropertyKey EllipseDiameterPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(EllipseDiameter),
            typeof(double),
            typeof(ProgressRing),
            new PropertyMetadata(default(double)));

    /// <summary>Identifies the <see cref="EllipseOffset"/> dependency property.</summary>
    private static readonly DependencyPropertyKey EllipseOffsetPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(EllipseOffset),
            typeof(Thickness),
            typeof(ProgressRing),
            new PropertyMetadata(default(Thickness)));

    /// <summary>Identifies the <see cref="BindableWidth"/> dependency property.</summary>
    public static readonly DependencyProperty BindableWidthProperty = BindableWidthPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="MaxSideLength"/> dependency property.</summary>
    public static readonly DependencyProperty MaxSideLengthProperty = MaxSideLengthPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="EllipseDiameter"/> dependency property.</summary>
    public static readonly DependencyProperty EllipseDiameterProperty = EllipseDiameterPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="EllipseOffset"/> dependency property.</summary>
    public static readonly DependencyProperty EllipseOffsetProperty = EllipseOffsetPropertyKey.DependencyProperty;

    private List<Action>? _deferredActions = new();

    static ProgressRing()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ProgressRing), new FrameworkPropertyMetadata(typeof(ProgressRing)));
        VisibilityProperty.OverrideMetadata(
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(
                (ringObject, e) =>
                {
                    if (e.NewValue != e.OldValue)
                    {
                        var ring = ringObject as ProgressRing;

                        ring?.SetCurrentValue(IsActiveProperty, (Visibility)e.NewValue == Visibility.Visible);
                    }
                }));
    }

    public ProgressRing()
    {
        SizeChanged += OnSizeChanged;
    }

    public double BindableWidth
    {
        get => (double)GetValue(BindableWidthProperty);
        protected set => SetValue(BindableWidthPropertyKey, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsLarge
    {
        get => (bool)GetValue(IsLargeProperty);
        set => SetValue(IsLargeProperty, value);
    }

    public double EllipseDiameterScale
    {
        get => (double)GetValue(EllipseDiameterScaleProperty);
        set => SetValue(EllipseDiameterScaleProperty, value);
    }

    public double EllipseDiameter
    {
        get => (double)GetValue(EllipseDiameterProperty);
        protected set => SetValue(EllipseDiameterPropertyKey, value);
    }

    public double MaxSideLength
    {
        get => (double)GetValue(MaxSideLengthProperty);
        protected set => SetValue(MaxSideLengthPropertyKey, value);
    }

    public Thickness EllipseOffset
    {
        get => (Thickness)GetValue(EllipseOffsetProperty);
        protected set => SetValue(EllipseOffsetPropertyKey, value);
    }

    public override void OnApplyTemplate()
    {
        // make sure the states get updated
        UpdateLargeState();
        UpdateActiveState();
        base.OnApplyTemplate();
        if (_deferredActions != null)
        {
            foreach (var action in _deferredActions)
            {
                action();
            }
        }

        _deferredActions = null;
    }

    private static void OnBindableWidthPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        if (dependencyObject is not ProgressRing ring)
        {
            return;
        }

        var action = new Action(
            () =>
            {
                ring.SetEllipseDiameter((double)dependencyPropertyChangedEventArgs.NewValue);
                ring.SetEllipseOffset((double)dependencyPropertyChangedEventArgs.NewValue);
                ring.SetMaxSideLength((double)dependencyPropertyChangedEventArgs.NewValue);
            });

        if (ring._deferredActions != null)
        {
            ring._deferredActions.Add(action);
        }
        else
        {
            action();
        }
    }

    private static void OnIsActivePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        var ring = dependencyObject as ProgressRing;

        ring?.UpdateActiveState();
    }

    private static void OnIsLargePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        var ring = dependencyObject as ProgressRing;

        ring?.UpdateLargeState();
    }

    private void SetMaxSideLength(double width)
    {
        SetValue(MaxSideLengthPropertyKey, width <= 16d ? 16d : width); // Modified from 20d to 16d to have the possibility to have a 16x16 progress ring
    }

    private void SetEllipseDiameter(double width)
    {
        SetValue(EllipseDiameterPropertyKey, width / 8 * EllipseDiameterScale);
    }

    private void SetEllipseOffset(double width)
    {
        SetValue(EllipseOffsetPropertyKey, new Thickness(0, width / 2, 0, 0));
    }

    private void UpdateLargeState()
    {
        Action action;

        if (IsLarge)
        {
            action = () => VisualStateManager.GoToState(this, "Large", true);
        }
        else
        {
            action = () => VisualStateManager.GoToState(this, "Small", true);
        }

        if (_deferredActions != null)
        {
            _deferredActions.Add(action);
        }
        else
        {
            action();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
    {
        SetValue(BindableWidthPropertyKey, ActualWidth);
    }

    private void UpdateActiveState()
    {
        Action action = IsActive
            ? () => VisualStateManager.GoToState(this, "Active", true)
            : () => VisualStateManager.GoToState(this, "Inactive", true);

        if (_deferredActions != null)
        {
            _deferredActions.Add(action);
        }
        else
        {
            action();
        }
    }
}
