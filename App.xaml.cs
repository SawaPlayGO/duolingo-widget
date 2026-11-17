using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using Windows.System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using DuolingoWidget.Services;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.Core;

namespace DuolingoWidget
{
    public sealed partial class App : Application
    {
        private static readonly Size CompactWindowSize = new Size(500, 420);
        private static readonly Size PresetWindowSize = new Size(920, 600);
        private static bool presetPageActive;
        private static bool resizingCompactWindow;
        private bool windowSizeHandlerAttached;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;

            ApplicationView.PreferredLaunchViewSize = CompactWindowSize;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await ActivateLaunchAsync(e);
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.Launch && args is LaunchActivatedEventArgs launchArgs)
            {
                await ActivateLaunchAsync(launchArgs);
                return;
            }

            base.OnActivated(args);
        }

        private async Task ActivateLaunchAsync(LaunchActivatedEventArgs e)
        {
            if (Window.Current.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
                AttachWindowSizeHandler();
                bool isPresetPage = rootFrame.Content is MainPage page &&
                    page.IsPresetPageSelected;
                TryResizeForPresetPage(isPresetPage);

                if (string.Equals(e.Arguments?.Trim(), "openDuolingo", StringComparison.OrdinalIgnoreCase))
                {
                    bool launched = await Launcher.LaunchUriAsync(new Uri("https://www.duolingo.com/"));
                    if (!launched)
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "Не удалось открыть Duolingo",
                            Content = "Проверьте, что в Windows настроен браузер для ссылок HTTPS.",
                            CloseButtonText = "OK"
                        };
                        await dialog.ShowAsync();
                    }
                }
            }
        }

        private void AttachWindowSizeHandler()
        {
            if (windowSizeHandlerAttached)
            {
                return;
            }

            Window.Current.SizeChanged += CurrentWindow_SizeChanged;
            windowSizeHandlerAttached = true;
        }

        private void CurrentWindow_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            if (presetPageActive || resizingCompactWindow ||
                (e.Size.Width <= CompactWindowSize.Width + 1 &&
                 e.Size.Height <= CompactWindowSize.Height + 1))
            {
                return;
            }

            resizingCompactWindow = true;
            bool resized = ApplicationView.GetForCurrentView().TryResizeView(CompactWindowSize);
            resizingCompactWindow = false;
            if (!resized)
            {
                Debug.WriteLine("Windows не удалось ограничить размер окна на вкладке настроек.");
            }
        }

        internal static bool TryResizeForPresetPage(bool isPresetPage)
        {
            presetPageActive = isPresetPage;
            Size requestedSize = isPresetPage ? PresetWindowSize : CompactWindowSize;
            return ApplicationView.GetForCurrentView().TryResizeView(requestedSize);
        }

        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            if (!string.Equals(
                args.TaskInstance.Task.Name,
                BackgroundTileTaskRegistration.TaskName,
                StringComparison.Ordinal))
            {
                base.OnBackgroundActivated(args);
                return;
            }

            BackgroundTaskDeferral? deferral = null;
            Window? window = null;
            CoreDispatcher? dispatcher = null;
            UIElement? originalContent = null;
            ApplicationDataContainer? settings = null;
            bool temporaryPageCreated = false;
            string stage = "GET_DEFERRAL";

            try
            {
                deferral = args.TaskInstance.GetDeferral();
                stage = "OPEN_SETTINGS";
                settings = ApplicationData.Current.LocalSettings;
                stage = "ENTRY";
                settings.Values.Remove(BackgroundTileTaskRegistration.LastBackgroundTaskErrorSettingKey);
                settings.Values.Remove(
                    BackgroundTileTaskRegistration.LastBackgroundTaskCancellationSettingKey);
                await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);

                args.TaskInstance.Canceled += (sender, reason) =>
                {
                    try
                    {
                        settings.Values[
                            BackgroundTileTaskRegistration.LastBackgroundTaskCancellationSettingKey] =
                            reason.ToString();
                        settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] =
                            "CANCELLED " + stage;
                        _ = BackgroundTaskDiagnostics.WriteAsync(
                            "cancelled stage=" + stage + " reason=" + reason);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Не удалось записать отмену фоновой задачи: " + ex);
                    }
                };

                stage = "GET_WINDOW";
                await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);
                window = Window.Current;

                stage = "GET_DISPATCHER";
                await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);
                dispatcher = window?.Dispatcher;
                if (dispatcher == null)
                {
                    stage = "HEADLESS_REFRESH";
                    await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);
                    await BackgroundTaskDiagnostics.WriteAsync(
                        "no XAML dispatcher; using background-safe tile refresh");
                    bool refreshed = await BackgroundTileRefreshService.RefreshAsync();
                    stage = settings.Values[
                        BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] as string ??
                        stage;
                    await BackgroundTaskDiagnostics.WriteAsync(
                        "refresh completed success=" + refreshed +
                        " instance=" + args.TaskInstance.InstanceId.ToString("D", CultureInfo.InvariantCulture));
                    return;
                }
                await BackgroundTaskDiagnostics.WriteAsync(
                    "dispatcher acquired hasThreadAccess=" + dispatcher.HasThreadAccess);

                stage = "READ_CONTENT_DISPATCH";
                await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);
                await RunOnDispatcherAsync(dispatcher, () =>
                {
                    stage = "READ_CONTENT_CALLBACK";
                    originalContent = window?.Content;
                    return Task.CompletedTask;
                });
                stage = "READ_CONTENT_COMPLETE";
                await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);
                await BackgroundTaskDiagnostics.WriteAsync(
                    "window content read isNull=" + (originalContent == null) +
                    " type=" + (originalContent == null ? "null" : originalContent.GetType().FullName));

                await RunOnDispatcherAsync(dispatcher, async () =>
                {
                    Window targetWindow = window ?? throw new InvalidOperationException(
                        "Window.Current is unavailable while preparing the foreground page.");
                    MainPage page;
                    if (originalContent is Frame frame && frame.Content is MainPage currentPage)
                    {
                        page = currentPage;
                    }
                    else if (originalContent is MainPage directPage)
                    {
                        page = directPage;
                    }
                    else
                    {
                        stage = "CREATE_PAGE";
                        page = new MainPage(true);
                        targetWindow.Content = page;
                        temporaryPageCreated = true;
                    }

                    stage = "REFRESH";
                    await SetBackgroundTaskStageAsync(settings, stage, args.TaskInstance.InstanceId);
                    bool refreshed = await page.RefreshProfileAndTileAsync(isBackgroundTask: true);
                    await BackgroundTaskDiagnostics.WriteAsync(
                        "refresh completed success=" + refreshed +
                        " instance=" + args.TaskInstance.InstanceId.ToString("D", CultureInfo.InvariantCulture));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Не удалось обновить плитку в фоне: " + ex);
                string errorCode = unchecked((uint)ex.HResult)
                    .ToString("X8", CultureInfo.InvariantCulture);
                if (settings != null)
                {
                    settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] = stage;
                    settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskErrorSettingKey] =
                        stage + " " + errorCode;
                }
                await BackgroundTaskDiagnostics.WriteExceptionAsync(stage, ex);
                try
                {
                    TileUpdaterHelper.SetDiagnosticError(stage, ex);
                }
                catch (Exception diagnosticException)
                {
                    Debug.WriteLine("Не удалось записать HRESULT фоновой ошибки: " + diagnosticException);
                }
            }
            finally
            {
                try
                {
                    if (temporaryPageCreated && window != null && dispatcher != null)
                    {
                        if (settings != null)
                        {
                            stage = "RESTORE_CONTENT";
                            await SetBackgroundTaskStageAsync(
                                settings,
                                stage,
                                args.TaskInstance.InstanceId);
                        }

                        await RunOnDispatcherAsync(dispatcher, () =>
                        {
                            window.Content = originalContent;
                            return Task.CompletedTask;
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Не удалось восстановить содержимое окна после фонового обновления: " + ex);
                }
                finally
                {
                    if (settings != null)
                    {
                        await BackgroundTaskDiagnostics.WriteAsync(
                            "handler exit stage=" + stage +
                            " instance=" + args.TaskInstance.InstanceId.ToString(
                                "D",
                                CultureInfo.InvariantCulture));
                    }

                    if (deferral != null)
                    {
                        deferral.Complete();
                    }
                }
            }
        }

        private static async Task SetBackgroundTaskStageAsync(
            ApplicationDataContainer settings,
            string stage,
            Guid instanceId)
        {
            settings.Values[BackgroundTileTaskRegistration.LastBackgroundTaskStageSettingKey] = stage;
            await BackgroundTaskDiagnostics.WriteAsync(
                "stage=" + stage +
                " instance=" + instanceId.ToString("D", CultureInfo.InvariantCulture));
        }

        private static async Task RunOnDispatcherAsync(CoreDispatcher dispatcher, Func<Task> action)
        {
            var completion = new TaskCompletionSource<bool>();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            await completion.Task;
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}