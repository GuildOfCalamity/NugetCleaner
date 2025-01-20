using System;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace NugetCleaner;

/// <summary>
/// An InfoBar that closes itself after an interval.
/// </summary>
public class AutoCloseInfoBar : InfoBar
{
    long? _iopToken;
    long? _mpToken;
    DispatcherTimer? _timer;

    /// <summary>
    /// Gets or sets the auto-close interval, in milliseconds.
    /// </summary>
    public int AutoCloseInterval { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the animation time span.
    /// </summary>
    public double AnimationSeconds { get; set; } = 1.5d;

    /// <summary>
    /// Gets or sets the animation direction.
    /// </summary>
    public bool SlideUp { get; set; } = true;

    public AutoCloseInfoBar() : base()
    {
        this.Loaded += AutoCloseInfoBar_Loaded;
        this.Unloaded += AutoCloseInfoBar_Unloaded;
    }

    #region [Control Events]
    void AutoCloseInfoBar_Loaded(object sender, RoutedEventArgs e)
    {
        _iopToken = this.RegisterPropertyChangedCallback(IsOpenProperty, IsOpenChanged);
        _mpToken = this.RegisterPropertyChangedCallback(MessageProperty, MessageChanged);

        if (IsOpen)
            Open();
    }

    /// <summary>
    /// Clean-up procedure
    /// </summary>
    void AutoCloseInfoBar_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_iopToken != null)
            this.UnregisterPropertyChangedCallback(IsOpenProperty, (long)_iopToken);

        if (_mpToken != null)
            this.UnregisterPropertyChangedCallback(MessageProperty, (long)_mpToken);
    }

    /// <summary>
    /// Triggered once the <see cref="AutoCloseInterval"/> elapses.
    /// </summary>
    void Timer_Tick(object? sender, object e)
    {
        this.IsOpen = false;
    }

    /// <summary>
    /// Callback for our control's property change.
    /// </summary>
    void MessageChanged(DependencyObject o, DependencyProperty p)
    {
        var obj = o as AutoCloseInfoBar;
        if (obj == null)
            return;

        if (p != MessageProperty)
            return;

        if (obj.IsOpen)
        {
            // If the message has changed we should reset the timer.
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Interval = TimeSpan.FromMilliseconds(AutoCloseInterval);
                _timer.Start();
            }
        }
        else
        {
            Debug.WriteLine($"'{obj.GetType()}' is not open, skipping timer reset.");
        }
    }

    /// <summary>
    /// Callback for our control's property change.
    /// </summary>
    void IsOpenChanged(DependencyObject o, DependencyProperty p)
    {
        var obj = o as AutoCloseInfoBar;
        if (obj == null)
            return;

        if (p != IsOpenProperty)
            return;

        if (obj.IsOpen)
        {
            AnimateUIElementOpacity(0, 1, TimeSpan.FromSeconds(AnimationSeconds), obj);
            if (obj.ActualHeight != double.NaN && obj.ActualHeight != 0)
            {
                Debug.WriteLine($"[INFO] Reported {obj.GetType().Name} width by height: {obj.ActualHeight} pixels by {obj.ActualWidth} pixels");
                AnimateUIElementOffset(new Windows.Foundation.Point(0, obj.ActualHeight), TimeSpan.FromSeconds(AnimationSeconds).Multiply(0.5), obj, SlideUp ? Microsoft.UI.Composition.AnimationDirection.Normal : Microsoft.UI.Composition.AnimationDirection.Reverse);
            }
            else
            {
                AnimateUIElementOffset(new Windows.Foundation.Point(0, 60), TimeSpan.FromSeconds(AnimationSeconds).Multiply(0.5), obj, SlideUp ? Microsoft.UI.Composition.AnimationDirection.Normal : Microsoft.UI.Composition.AnimationDirection.Reverse);
            }
            obj.Open();
        }
        else
            obj.Close();
    }
    #endregion

    #region [Control Methods]
    void Open()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += Timer_Tick;
        _timer.Interval = TimeSpan.FromMilliseconds(AutoCloseInterval);
        _timer.Start();
    }

    void Close()
    {
        if (_timer == null)
            return;

        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }
    #endregion

    /// <summary>
    /// Opacity animation using <see cref="Microsoft.UI.Composition.ScalarKeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementOpacity(double from, double to, TimeSpan duration, UIElement target, Microsoft.UI.Composition.AnimationDirection direction = Microsoft.UI.Composition.AnimationDirection.Normal)
    {
        Microsoft.UI.Composition.CompositionEasingFunction easer;
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        if (targetVisual is null) { return; }
        var compositor = targetVisual.Compositor;
        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Direction = direction;
        opacityAnimation.Duration = duration;
        opacityAnimation.Target = "Opacity";
        //easer = compositor.CreateLinearEasingFunction();
        easer = CreatePennerEquation(compositor, "QuadEaseOut");
        opacityAnimation.InsertKeyFrame(0.0f, (float)from, easer);
        opacityAnimation.InsertKeyFrame(1.0f, (float)to, easer);
        targetVisual.StartAnimation("Opacity", opacityAnimation);
    }

    /// <summary>
    /// Offset animation using <see cref="Microsoft.UI.Composition.Vector3KeyFrameAnimation"/>
    /// </summary>
    void AnimateUIElementOffset(Windows.Foundation.Point to, TimeSpan duration, UIElement target, Microsoft.UI.Composition.AnimationDirection direction = Microsoft.UI.Composition.AnimationDirection.Normal)
    {
        Microsoft.UI.Composition.CompositionEasingFunction easer;
        var targetVisual = ElementCompositionPreview.GetElementVisual(target);
        if (targetVisual is null) { return; }
        var compositor = targetVisual.Compositor;
        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Direction = direction;
        offsetAnimation.Duration = duration;
        offsetAnimation.Target = "Offset";
        //easer = compositor.CreateLinearEasingFunction();
        easer = CreatePennerEquation(compositor, "QuadEaseOut");
        offsetAnimation.InsertKeyFrame(0.0f, new System.Numerics.Vector3((float)to.X, (float)to.Y, 0), easer);
        offsetAnimation.InsertKeyFrame(1.0f, new System.Numerics.Vector3(0), easer);
        targetVisual.StartAnimation("Offset", offsetAnimation);
    }

    /// <summary>
    /// This should be moved to a shared module, but I want to keep these behaviors portable.
    /// </summary>
    static Microsoft.UI.Composition.CompositionEasingFunction CreatePennerEquation(Microsoft.UI.Composition.Compositor compositor, string pennerType = "SineEaseInOut")
    {
        System.Numerics.Vector2 controlPoint1;
        System.Numerics.Vector2 controlPoint2;
        switch (pennerType)
        {
            case "SineEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.47f, 0.0f);
                controlPoint2 = new System.Numerics.Vector2(0.745f, 0.715f);
                break;
            case "SineEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.39f, 0.575f);
                controlPoint2 = new System.Numerics.Vector2(0.565f, 1.0f);
                break;
            case "SineEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.445f, 0.05f);
                controlPoint2 = new System.Numerics.Vector2(0.55f, 0.95f);
                break;
            case "QuadEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.55f, 0.085f);
                controlPoint2 = new System.Numerics.Vector2(0.68f, 0.53f);
                break;
            case "QuadEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.25f, 0.46f);
                controlPoint2 = new System.Numerics.Vector2(0.45f, 0.94f);
                break;
            case "QuadEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.445f, 0.03f);
                controlPoint2 = new System.Numerics.Vector2(0.515f, 0.955f);
                break;
            case "CubicEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.55f, 0.055f);
                controlPoint2 = new System.Numerics.Vector2(0.675f, 0.19f);
                break;
            case "CubicEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.215f, 0.61f);
                controlPoint2 = new System.Numerics.Vector2(0.355f, 1.0f);
                break;
            case "CubicEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.645f, 0.045f);
                controlPoint2 = new System.Numerics.Vector2(0.355f, 1.0f);
                break;
            case "QuarticEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.895f, 0.03f);
                controlPoint2 = new System.Numerics.Vector2(0.685f, 0.22f);
                break;
            case "QuarticEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.165f, 0.84f);
                controlPoint2 = new System.Numerics.Vector2(0.44f, 1.0f);
                break;
            case "QuarticEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.77f, 0.0f);
                controlPoint2 = new System.Numerics.Vector2(0.175f, 1.0f);
                break;
            case "QuinticEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.755f, 0.05f);
                controlPoint2 = new System.Numerics.Vector2(0.855f, 0.06f);
                break;
            case "QuinticEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.23f, 1.0f);
                controlPoint2 = new System.Numerics.Vector2(0.32f, 1.0f);
                break;
            case "QuinticEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.86f, 0.0f);
                controlPoint2 = new System.Numerics.Vector2(0.07f, 1.0f);
                break;
            case "ExponentialEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.95f, 0.05f);
                controlPoint2 = new System.Numerics.Vector2(0.795f, 0.035f);
                break;
            case "ExponentialEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.19f, 1.0f);
                controlPoint2 = new System.Numerics.Vector2(0.22f, 1.0f);
                break;
            case "ExponentialEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(1.0f, 0.0f);
                controlPoint2 = new System.Numerics.Vector2(0.0f, 1.0f);
                break;
            case "CircleEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.6f, 0.04f);
                controlPoint2 = new System.Numerics.Vector2(0.98f, 0.335f);
                break;
            case "CircleEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.075f, 0.82f);
                controlPoint2 = new System.Numerics.Vector2(0.165f, 1.0f);
                break;
            case "CircleEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.785f, 0.135f);
                controlPoint2 = new System.Numerics.Vector2(0.15f, 0.86f);
                break;
            case "BackEaseIn":
                controlPoint1 = new System.Numerics.Vector2(0.6f, -0.28f);
                controlPoint2 = new System.Numerics.Vector2(0.735f, 0.045f);
                break;
            case "BackEaseOut":
                controlPoint1 = new System.Numerics.Vector2(0.175f, 0.885f);
                controlPoint2 = new System.Numerics.Vector2(0.32f, 1.275f);
                break;
            case "BackEaseInOut":
                controlPoint1 = new System.Numerics.Vector2(0.68f, -0.55f);
                controlPoint2 = new System.Numerics.Vector2(0.265f, 1.55f);
                break;
            default:
                controlPoint1 = new System.Numerics.Vector2(0.0f);
                controlPoint2 = new System.Numerics.Vector2(0.0f);
                break;
        }
        Microsoft.UI.Composition.CompositionEasingFunction pennerEquation = compositor.CreateCubicBezierEasingFunction(controlPoint1, controlPoint2);
        return pennerEquation;
    }

}

