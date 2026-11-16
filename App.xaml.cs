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

            BackgroundTaskDeferral deferral = args.TaskInstance.GetDeferral();
            var window = Window.Current;
            UIElement originalContent = window.Content;
            bool temporaryPageCreated = false;

            try
            {
                ApplicationData.Current.LocalSettings.Values[
                    BackgroundTileTaskRegistration.LastStartedSettingKey] =
                    DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                await TileUpdaterHelper.UpdateTileAsync(updateTimestamp: false);

                MainPage page = null!;
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
                    page = new MainPage(true);
                    window.Content = page;
                    temporaryPageCreated = true;
                }

                await page.RefreshProfileAndTileAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Не удалось обновить плитку в фоне: " + ex);
                ApplicationData.Current.LocalSettings.Values[
                    BackgroundTileTaskRegistration.LastErrorSettingKey] = ex.Message;
            }
            finally
            {
                if (temporaryPageCreated)
                {
                    window.Content = originalContent;
                }

                deferral.Complete();
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}