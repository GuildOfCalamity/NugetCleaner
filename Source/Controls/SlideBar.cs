using System;
using System.Diagnostics;
using System.Timers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace NugetCleaner;

/// <summary>
/// Similar to <see cref="AutoCloseInfoBar"/> but done with <see cref="Storyboard"/>s
/// instead of <see cref="Microsoft.UI.Composition.ScalarKeyFrameAnimation"/>s and
/// <see cref="Microsoft.UI.Composition.Vector3KeyFrameAnimation"/>s.
/// </summary>
public partial class SlideBar : InfoBar
{
    long? _mpToken;
    Storyboard? _currentStoryboard;
    
    // For some unknown reason this timer event would never fire,
    // so I have switched this to a "System.Timers.Timer" class.
    //DispatcherTimer? _closeTimer;
    static System.Timers.Timer _tmrClose = new System.Timers.Timer();

    public SlideBar() // : base()
    {
        //DefaultStyleKey = typeof(InfoBar);

        // Add TranslateTransform for sliding animation.
        RenderTransform = new TranslateTransform();

        // Setup basic control events.
        this.Loaded += SlideInfoBarOnLoaded;
        this.Unloaded += SlideInfoBarOnUnloaded;
    }

    void SlideInfoBarOnLoaded(object sender, RoutedEventArgs e)
    {
        _mpToken = this.RegisterPropertyChangedCallback(MessageProperty, MessageChanged);
        _tmrClose.Interval = AutoCloseInterval;
        _tmrClose.Enabled = true;
        _tmrClose.Elapsed += OnTick;
    }

    void SlideInfoBarOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_mpToken != null)
            this.UnregisterPropertyChangedCallback(MessageProperty, (long)_mpToken);
            
        if (_tmrClose != null)
        {
            Debug.WriteLine($"[INFO] Disposing timer.");
            _tmrClose.Enabled = false;
            _tmrClose.Elapsed -= OnTick;
        }
    }

    #region [Dependency Properties]
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(SlideBar),
            new PropertyMetadata(false, OnIsOpenChanged));
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }
    static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SlideBar)d;
        if (ctrl != null)
        {
            if ((bool)e.NewValue)
                ctrl.OpenInfoBar();
            else
                ctrl.CloseInfoBar();
        }
    }

    public static readonly DependencyProperty FadeDurationProperty = DependencyProperty.Register(
            nameof(FadeDuration),
            typeof(TimeSpan),
            typeof(SlideBar),
            new PropertyMetadata(TimeSpan.FromMilliseconds(1000)));
    public TimeSpan FadeDuration
    {
        get => (TimeSpan)GetValue(FadeDurationProperty);
        set => SetValue(FadeDurationProperty, value);
    }

    public static readonly DependencyProperty AutoCloseIntervalProperty = DependencyProperty.Register(
            nameof(AutoCloseInterval),
            typeof(double),
            typeof(SlideBar),
            new PropertyMetadata(6000));
    public double AutoCloseInterval
    {
        get => (double)GetValue(AutoCloseIntervalProperty);
        set => SetValue(AutoCloseIntervalProperty, value);
    }

    public static readonly DependencyProperty SlideUpProperty = DependencyProperty.Register(
        nameof(SlideUp),
        typeof(bool),
        typeof(SlideBar),
        new PropertyMetadata(true, OnSlideUpChanged));
    public bool SlideUp
    {
        get => (bool)GetValue(SlideUpProperty);
        set => SetValue(SlideUpProperty, value);
    }
    static void OnSlideUpChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SlideBar)d;
        if (ctrl != null)
        {
            ctrl.ChangeSlideUp((bool)e.NewValue);
        }
    }

    public static readonly DependencyProperty SlideAmountProperty = DependencyProperty.Register(
    nameof(SlideAmount),
    typeof(double),
    typeof(SlideBar),
    new PropertyMetadata(60d, OnSlideAmountChanged));
    public double SlideAmount
    {
        get => (double)GetValue(SlideAmountProperty);
        set => SetValue(SlideAmountProperty, value);
    }
    static void OnSlideAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SlideBar)d;
        if (ctrl != null)
        {
            ctrl.ChangeSlideAmount((double)e.NewValue);
        }
    }

    #endregion

    void OnTick(object? sender, ElapsedEventArgs e)
    {
        _tmrClose.Stop();
        // When using System.Timers.Timer we're probably not on a UI thread when this
        // event fires so we'll need to set the DependencyProperty via the Dispatcher.
        this.DispatcherQueue.TryEnqueue(() => IsOpen = false);
    }

    void OpenInfoBar()
    {
        // Stop any current animation
        StopCurrentAnimation();

        // Stop the close timer (if running)
        if (_tmrClose != null)
            _tmrClose.Stop();

        // Reset opacity and visibility for fade-in animation
        this.Opacity = 0;
        this.Visibility = Visibility.Visible;
        base.IsOpen = true;

        var slideUpAnimation = new DoubleAnimation
        {
            From = SlideUp ? SlideAmount : 0, // Start offset
            To = SlideUp ? 0 : SlideAmount,   // End offset
            Duration = new Duration(FadeDuration),
            EasingFunction = new QuadraticEase()
        };

        var fadeInAnimation = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = new Duration(FadeDuration),
            EasingFunction = new QuadraticEase()
        };

        var storyboard = new Storyboard();

        Storyboard.SetTarget(slideUpAnimation, RenderTransform);
        Storyboard.SetTargetProperty(slideUpAnimation, "Y");

        Storyboard.SetTarget(fadeInAnimation, this);
        Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");

        storyboard.Children.Add(slideUpAnimation);
        storyboard.Children.Add(fadeInAnimation);
        
        _currentStoryboard = storyboard;
        
        storyboard.Begin();

        // Start the auto-close timer
        if (_tmrClose != null)
        {
            Debug.WriteLine($"[INFO] Starting close timer.");
            _tmrClose.Start();
        }
    }

    void CloseInfoBar()
    {
        // Stop any current animation
        StopCurrentAnimation();

        var slideDownAnimation = new DoubleAnimation
        {
            From = SlideUp ? 0 : SlideAmount, // Start offset
            To = SlideUp ? SlideAmount : 0,   // End offset
            Duration = new Duration(Alteration(FadeDuration, 0.75)),
            EasingFunction = new QuadraticEase()
        };

        var fadeOutAnimation = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = new Duration(Alteration(FadeDuration, 0.75)),
            EasingFunction = new QuadraticEase()
        };

        var storyboard = new Storyboard();

        Storyboard.SetTarget(slideDownAnimation, RenderTransform);
        Storyboard.SetTargetProperty(slideDownAnimation, "Y");

        Storyboard.SetTarget(fadeOutAnimation, this);
        Storyboard.SetTargetProperty(fadeOutAnimation, "Opacity");

        storyboard.Children.Add(slideDownAnimation);
        storyboard.Children.Add(fadeOutAnimation);
        
        _currentStoryboard = storyboard;

        // Hide InfoBar after animation is completed.
        storyboard.Completed += (s, e) =>
        {
            Debug.WriteLine($"[INFO] Storyboard.Completed event");
            this.Visibility = Visibility.Collapsed;
            base.IsOpen = false;
        };
        
        storyboard?.Begin();
    }

    void StopCurrentAnimation()
    {
        if (_currentStoryboard != null)
        {
            Debug.WriteLine($"[INFO] Stopping _currentStoryboard");
            _currentStoryboard?.Stop();
            _currentStoryboard = null;
        }
    }

    void ChangeSlideUp(bool value) // RFU
    {
        Debug.WriteLine($"[INFO] SlideUp is now '{value}'.");
    }

    void ChangeSlideAmount(double value) // RFU
    {
        Debug.WriteLine($"[INFO] SlideAmount is now '{value}'.");
    }

    /// <summary>
    /// Callback for our control's property change.
    /// TODO: Rework this to activate the InfoBar, if it's not already open, once the user changes the Message.
    /// </summary>
    void MessageChanged(DependencyObject o, DependencyProperty p)
    {
        var obj = o as SlideBar;
        if (obj == null)
            return;

        if (p != MessageProperty)
            return;

        if (obj.IsOpen)
        {
            // If message was changed, reset the timer.
            if (_tmrClose != null)
            {
                _tmrClose.Stop();
                _tmrClose.Interval = AutoCloseInterval;
                _tmrClose.Start();
            }
        }
        else
        {
            Debug.WriteLine($"[INFO] '{obj.GetType()}' is not open, skipping timer reset.");
        }
    }

    internal TimeSpan Alteration(TimeSpan timeSpan, double scalar) => new TimeSpan((long)(timeSpan.Ticks * scalar));
}
