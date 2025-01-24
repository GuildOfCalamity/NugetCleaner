using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace NugetCleaner;

/// <summary>
/// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart?tabs=cs
/// </summary>
internal class NotificationManager
{
    bool m_isRegistered;

    Dictionary<int, Action<AppNotificationActivatedEventArgs>> c_map;

    public NotificationManager()
    {
        m_isRegistered = false;

        // When adding new a scenario, be sure to add its notification handler here.
        c_map = new Dictionary<int, Action<AppNotificationActivatedEventArgs>>();
        c_map.Add(ToastWithAvatar.ScenarioId, ToastWithAvatar.NotificationReceived);
    }

    ~NotificationManager()
    {
        Unregister();
    }

    public void Init()
    {
        // To ensure all Notification handling happens in this process instance, register for
        // NotificationInvoked before calling Register(). Without this a new process will
        // be launched to handle the notification.
        AppNotificationManager notificationManager = AppNotificationManager.Default;
        notificationManager.NotificationInvoked += App.OnNotificationInvoked;
        notificationManager.Register();
        m_isRegistered = true;
    }

    public void Unregister()
    {
        if (m_isRegistered)
        {
            AppNotificationManager.Default.Unregister();
            m_isRegistered = false;
        }
    }

    public void ProcessLaunchActivationArgs(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
    {
        // If the user clicks on the notification body, your app needs to launch the chat thread window
        if (notificationActivatedEventArgs.Argument.Contains("openThread"))
        {
            GenerateChatThreadWindow();
        }
        else // If the user responds to a message by clicking a button in the notification
        if (notificationActivatedEventArgs.Argument.Contains("reply"))
        {
            // Get the user input
            var userInput = notificationActivatedEventArgs.UserInput;
            var replyBoxText = userInput.TryGetValue("replyBox", out var replyText) ? replyText : string.Empty;

            // Process the reply text
            SendReplyToUser(replyBoxText);
        }
    }

    /// <summary>
    /// Placeholder for the GenerateChatThreadWindow method
    /// </summary>
    void GenerateChatThreadWindow()
    {
        // Implementation to generate the chat thread window
    }

    /// <summary>
    /// Placeholder for the SendReplyToUser method
    /// </summary>
    /// <param name="replyText"></param>
    void SendReplyToUser(string replyText)
    {
        // Implementation to send a reply to the user
    }
}

/// <summary>
/// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart?tabs=cs
/// </summary>
public class ToastWithAvatar
{
    public const int ScenarioId = 1;
    public const string ScenarioName = "Local Toast with Avatar Image";

    public static bool SendToast()
    {
        var appNotification = new AppNotificationBuilder()
            .AddArgument("action", "ToastClick")
            .AddArgument("scenarioTag", ScenarioId.ToString())
            .SetAppLogoOverride(new System.Uri("file:///" + $"{Directory.GetCurrentDirectory().Replace("\\", "/")}/Assets/Square150x150Logo.scale-200.png"), AppNotificationImageCrop.Circle)
            .AddText(ScenarioName)
            .AddText("This is an example message using XML")
            .AddButton(new AppNotificationButton("Open App")
                .AddArgument("action", "OpenApp")
                .AddArgument("scenarioTag", ScenarioId.ToString()))
            .BuildNotification();

        appNotification.Expiration = DateTime.Now.AddDays(2);
        appNotification.ExpiresOnReboot = true;
        AppNotificationManager.Default.Show(appNotification);

        return appNotification.Id != 0; // return true (indicating success) if the toast was sent (if it has an Id)
    }

    public static bool SendRegularToast(string header, string body)
    {
        var appNotification = new AppNotificationBuilder()
            .AddText(header)
            .AddText(body)
            .SetAppLogoOverride(new Uri("ms-appx:///Assets/NoticeIcon.png"))
            .SetAttributionText("AboutAppName")
            .SetDuration(AppNotificationDuration.Default)
            .BuildNotification();

        appNotification.Expiration = DateTime.Now.AddDays(2);
        appNotification.ExpiresOnReboot = true;
        AppNotificationManager.Default.Show(appNotification);
        
        return appNotification.Id != 0; // return true (indicating success) if the toast was sent (if it has an Id)
    }

    public static bool SendWarningToast(string header, string body, string buttonNotification)
    {
        var appNotification = new AppNotificationBuilder()
               .AddText(header)
               .AddText(body)
               .SetAppLogoOverride(new Uri("ms-appx:///Assets/WarningIcon.png"))
               .AddButton(new AppNotificationButton(buttonNotification)
                   .SetInvokeUri(new Uri("https://github.com/GuildOfCalamity?tab=repositories")))
               .SetDuration(AppNotificationDuration.Default)
               .BuildNotification();

        appNotification.Expiration = DateTime.Now.AddDays(2);
        appNotification.ExpiresOnReboot = true;
        AppNotificationManager.Default.Show(appNotification);

        return appNotification.Id != 0; // return true (indicating success) if the toast was sent (if it has an Id)
    }

    public static bool CreateActivationToast()
    {
        var toastContent = new AppNotificationBuilder()
            .AddText("Notification Title")
            .AddText("Notification Body")
            .AddArgument("action", "openApp")
            .AddButton(new AppNotificationButton("Open App"))
            //.SetToastActivatesApp()
            .BuildNotification();

        toastContent.Expiration = DateTime.Now.AddDays(2);
        toastContent.ExpiresOnReboot = true;
        AppNotificationManager.Default.Show(toastContent);

        return toastContent.Id != 0; // return true (indicating success) if the toast was sent (if it has an Id)
    }

    public static void NotificationReceived(AppNotificationActivatedEventArgs args)
    {
        Debug.WriteLine($"[INFO] NotificationReceived: {args.Argument}");
        foreach (var a in args.Arguments)
        {
            Debug.WriteLine($" - {a.Key}:{a.Value}");
        }
    }
}
