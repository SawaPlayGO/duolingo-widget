using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DuolingoWidget.Models;
using DuolingoWidget.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace DuolingoWidget
{
    public sealed partial class MainPage : Page
    {
        private const string PresetSettingKeyPrefix = "ManualPreset.";
        private const string StartPinPromptedSettingKey = "StartPinPrompted";
        private const string TileNotificationVersion = "3";
        private readonly ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private readonly ResourceLoader strings = ResourceLoader.GetForCurrentView();
        private readonly List<PresetChoice> presetChoices = new List<PresetChoice>();
        private readonly DispatcherTimer refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(2)
        };
        private readonly SemaphoreSlim refreshLock = new SemaphoreSlim(1, 1);
        private readonly bool backgroundExecution;
        private bool backgroundTaskAvailable;
        private bool presetPeriodSetFromProfile;
        private bool initializingPresetChoices;
        private bool presetChoicesInitialized;
        private bool initialized;
        private string currentPresetPeriod = ThemeSelector.MorningPeriod;
        private DuolingoProfile? currentProfile;
        private Storyboard? previewAnimation;

        public MainPage() : this(false)
        {
        }

        internal bool IsPresetPageSelected => MainPivot.SelectedIndex == 1;

        internal MainPage(bool backgroundExecution)
        {
            this.backgroundExecution = backgroundExecution;
            InitializeComponent();
            LoadSettings();
            PresetPeriodComboBox.SelectedIndex = 0;
            PopulatePresetChoices();
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void LoadSettings()
        {
            UsernameTextBox.Text = localSettings.Values["DuolingoUsername"] as string ?? string.Empty;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (initialized || backgroundExecution)
            {
                return;
            }

            initialized = true;
            PopulatePresetChoices();
            try
            {
                backgroundTaskAvailable = await BackgroundTileTaskRegistration.EnsureRegisteredAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Не удалось зарегистрировать фоновое обновление: " + ex);
                backgroundTaskAvailable = false;
            }

            string lastRefreshError = localSettings.Values[
                BackgroundTileTaskRegistration.LastErrorSettingKey] as string ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(lastRefreshError))
            {
                StatusText.Text = strings.GetString("TileUpdateFailed");
            }

            refreshTimer.Start();
            await RefreshProfileAndTileAsync();
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Stop();
        }

        private async void RefreshTimer_Tick(object? sender, object e)
        {
            await RefreshProfileAndTileAsync();
        }

        private async void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!initialized)
            {
                return;
            }

            bool isPresetPage = MainPivot.SelectedIndex == 1;
            if (!App.TryResizeForPresetPage(isPresetPage))
            {
                Debug.WriteLine("Windows не удалось изменить размер окна для выбранной вкладки.");
            }

            if (!isPresetPage)
            {
                return;
            }

            await EnsurePresetProfileAsync();
            if (currentProfile != null)
            {
                UpdatePresetPreview(GetSelectedPresetPreviewTheme());
            }
        }

        private async Task EnsurePresetProfileAsync()
        {
            string username = UsernameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                currentProfile = null;
                presetPeriodSetFromProfile = false;
                UpdatePresetAvailability();
                PresetAvailabilityText.Text = strings.GetString("PresetNeedsProfile");
                PresetAvailabilityText.Visibility = Visibility.Visible;
                return;
            }

            bool usernameChanged = localSettings.Values["DuolingoUsername"] as string != username;
            if (usernameChanged)
            {
                SaveSettings();
            }

            if (usernameChanged || currentProfile == null)
            {
                PresetAvailabilityText.Text = strings.GetString("LoadingPresetProfile");
                PresetAvailabilityText.Visibility = Visibility.Visible;
                bool loaded = await RefreshProfileAndTileAsync(waitForCurrentRefresh: true);
                if (!loaded)
                {
                    PresetAvailabilityText.Text = strings.GetString("TileUpdateFailed");
                    PresetAvailabilityText.Visibility = Visibility.Visible;
                    return;
                }

                if (usernameChanged)
                {
                    await RequestStartPinAsync();
                }
            }

            UpdatePresetAvailability();
        }

        private void PresetPeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetPeriodComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string period &&
                period != currentPresetPeriod)
            {
                currentPresetPeriod = period;
                PopulatePresetChoices();
            }
        }

        private void PresetThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!initializingPresetChoices)
            {
                UpdatePresetPreview(GetSelectedPresetPreviewTheme());
            }
        }

        private async void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentProfile == null)
            {
                PresetStatusText.Text = strings.GetString("PresetNeedsProfile");
                return;
            }

            if (currentProfile.Streak <= 0)
            {
                PresetStatusText.Text = strings.GetString("PresetNoStreak");
                return;
            }

            PresetChoice? choice = GetSelectedPresetChoice();
            if (choice == null)
            {
                return;
            }

            string settingKey = GetPresetSettingKey(currentPresetPeriod);
            if (choice.ThemeId == "Random")
            {
                localSettings.Values.Remove(settingKey);
            }
            else
            {
                localSettings.Values[settingKey] = choice.ThemeId;
            }

            PresetStatusText.Text = strings.GetString("Saving");
            bool updated = await RefreshProfileAndTileAsync(waitForCurrentRefresh: true);
            PresetStatusText.Text = strings.GetString(
                updated ? "PresetSaved" : "TileUpdateFailed");
        }

        private void PopulatePresetChoices()
        {
            if (PresetThemeList == null || PresetPeriodComboBox == null)
            {
                return;
            }

            initializingPresetChoices = true;
            presetChoices.Clear();
            PresetThemeList.Items.Clear();
            var randomChoice = new PresetChoice("Random", strings.GetString("RandomPreset"));
            presetChoices.Add(randomChoice);
            PresetThemeList.Items.Add(randomChoice.Title);

            foreach (TileTheme theme in ThemeSelector.GetPresetsForPeriod(currentPresetPeriod))
            {
                var choice = new PresetChoice(
                    theme.Id,
                    strings.GetString("Theme_" + theme.Id));
                presetChoices.Add(choice);
                PresetThemeList.Items.Add(choice.Title);
            }

            string savedThemeId = currentProfile == null
                ? "Random"
                : localSettings.Values[GetPresetSettingKey(currentPresetPeriod)] as string ?? "Random";
            int selectedIndex = presetChoices.FindIndex(choice => choice.ThemeId == savedThemeId);
            PresetThemeList.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            initializingPresetChoices = false;
            presetChoicesInitialized = true;

            UpdatePresetAvailability();
            UpdatePresetPreview(GetSelectedPresetPreviewTheme());
        }

        private void UpdatePresetProfile(DuolingoProfile profile, DateTimeOffset currentTime)
        {
            bool firstProfile = currentProfile == null;
            currentProfile = profile;
            if (backgroundExecution)
            {
                return;
            }

            if (!presetPeriodSetFromProfile)
            {
                string period = ThemeSelector.GetCurrentPeriod(profile, currentTime);
                if (period == ThemeSelector.NoStreakPeriod)
                {
                    period = ThemeSelector.MorningPeriod;
                }

                for (int index = 0; index < PresetPeriodComboBox.Items.Count; index++)
                {
                    if (PresetPeriodComboBox.Items[index] is ComboBoxItem item &&
                        item.Tag as string == period)
                    {
                        PresetPeriodComboBox.SelectedIndex = index;
                        break;
                    }
                }

                presetPeriodSetFromProfile = true;
            }

            if (firstProfile || !presetChoicesInitialized)
            {
                PopulatePresetChoices();
            }
            else
            {
                UpdatePresetAvailability();
            }
        }

        private void UpdatePresetAvailability()
        {
            bool hasProfile = currentProfile != null;
            bool hasStreak = hasProfile && currentProfile!.Streak > 0;
            PresetThemeList.IsEnabled = hasStreak;
            ApplyPresetButton.IsEnabled = hasStreak;
            PresetPeriodComboBox.IsEnabled = hasProfile;
            PresetAvailabilityText.Text = hasProfile
                ? strings.GetString("PresetNoStreak")
                : strings.GetString("PresetNeedsProfile");
            PresetAvailabilityText.Visibility = hasStreak
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private TileTheme GetSelectedPresetPreviewTheme()
        {
            if (currentProfile == null)
            {
                return ThemeCatalog.GetById("1_1");
            }

            PresetChoice? choice = GetSelectedPresetChoice();
            string? selectedThemeId = choice == null || choice.ThemeId == "Random"
                ? null
                : choice.ThemeId;
            return ThemeSelector.SelectForPeriod(
                currentProfile,
                DateTimeOffset.UtcNow,
                currentPresetPeriod,
                selectedThemeId);
        }

        private PresetChoice? GetSelectedPresetChoice()
        {
            int selectedIndex = PresetThemeList.SelectedIndex;
            return selectedIndex >= 0 && selectedIndex < presetChoices.Count
                ? presetChoices[selectedIndex]
                : null;
        }

        private void UpdatePresetPreview(TileTheme theme)
        {
            string streak = currentProfile?.Streak.ToString(CultureInfo.InvariantCulture) ?? "123";
            PresetLargePreview.Theme = theme;
            PresetLargePreview.Streak = streak;
            PresetWidePreview.Theme = theme;
            PresetWidePreview.Streak = streak;
            PresetMediumPreview.Theme = theme;
            PresetMediumPreview.Streak = streak;
            PresetSmallFirePreview.Theme = theme;
            PresetSmallFirePreview.Streak = streak;
            PresetSmallDigitalPreview.Theme = theme;
            PresetSmallDigitalPreview.Streak = streak;
            PresetPreviewTitle.Text = strings.GetString("PresetPreviewTitle") + " · " +
                strings.GetString("Theme_" + theme.Id);
            if (initialized && MainPivot.SelectedIndex == 1)
            {
                AnimatePresetPreview();
            }
        }

        private void PresetPreviewArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            double scale = Math.Min(e.NewSize.Width / 634, e.NewSize.Height / 310);
            PresetPreviewViewbox.Width = 634 * scale;
            PresetPreviewViewbox.Height = 310 * scale;
        }

        private void AnimatePresetPreview()
        {
            if (previewAnimation != null)
            {
                previewAnimation.Stop();
            }

            PresetPreviewRoot.Opacity = 0.25;
            PresetPreviewScale.ScaleX = 0.9;
            PresetPreviewScale.ScaleY = 0.9;

            var storyboard = new Storyboard();
            var fade = new DoubleAnimation
            {
                From = 0.25,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(240))
            };
            Storyboard.SetTarget(fade, PresetPreviewRoot);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);

            var scaleX = new DoubleAnimation
            {
                From = 0.9,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(240)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(scaleX, PresetPreviewScale);
            Storyboard.SetTargetProperty(scaleX, "ScaleX");
            storyboard.Children.Add(scaleX);

            var scaleY = new DoubleAnimation
            {
                From = 0.9,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(240)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(scaleY, PresetPreviewScale);
            Storyboard.SetTargetProperty(scaleY, "ScaleY");
            storyboard.Children.Add(scaleY);

            previewAnimation = storyboard;
            storyboard.Begin();
        }

        private static string GetPresetSettingKey(string period) =>
            PresetSettingKeyPrefix + period;

        private void SaveSettings()
        {
            string username = UsernameTextBox.Text.Trim();
            if (localSettings.Values["DuolingoUsername"] as string != username)
            {
                currentProfile = null;
                presetPeriodSetFromProfile = false;
                UpdatePresetAvailability();
            }

            localSettings.Values["DuolingoUsername"] = username;
        }

        internal async Task<bool> RefreshProfileAndTileAsync(bool waitForCurrentRefresh = false)
        {
            bool lockAcquired = waitForCurrentRefresh
                ? await refreshLock.WaitAsync(Timeout.Infinite)
                : await refreshLock.WaitAsync(0);
            if (!lockAcquired)
            {
                return false;
            }

            try
            {
                string username = UsernameTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(username))
                {
                    StatusText.Text = strings.GetString("EnterUsername");
                    return false;
                }

                DuolingoProfile profile = await DuolingoApiClient.GetProfileAsync(username);
                DateTimeOffset currentTime = DateTimeOffset.UtcNow;
                UpdatePresetProfile(profile, currentTime);
                string period = ThemeSelector.GetCurrentPeriod(profile, currentTime);
                string selectedThemeId = period == ThemeSelector.NoStreakPeriod
                    ? string.Empty
                    : localSettings.Values[GetPresetSettingKey(period)] as string ?? string.Empty;
                TileTheme theme = ThemeSelector.Select(profile, currentTime, selectedThemeId);
                string streak = profile.Streak.ToString(CultureInfo.InvariantCulture);
                string smallVariant = ThemeSelector.GetDailySmallTileVariant(profile, currentTime);
                string renderKey = theme.Id + "|" + streak + "|" + smallVariant;
                bool alreadyRendered = localSettings.Values["RenderedTileKey"] as string == renderKey &&
                    !string.IsNullOrEmpty(localSettings.Values["TileImageVersion"] as string);

                if (!alreadyRendered)
                {
                    await TileRenderer.RenderThemeAsync(
                        theme,
                        streak,
                        smallVariant,
                        LargeRender,
                        WideRender,
                        MediumRender,
                        SmallFireRender,
                        SmallDigitalRender);

                    localSettings.Values["ThemeId"] = theme.Id;
                    localSettings.Values["RenderedTileKey"] = renderKey;
                }

                bool needsTileUpdate = TileUpdaterHelper.TestModeEnabled ||
                    !alreadyRendered ||
                    localSettings.Values["TileNotificationVersion"] as string != TileNotificationVersion;
                if (needsTileUpdate)
                {
                    await TileUpdaterHelper.UpdateTileAsync();
                    localSettings.Values["TileNotificationVersion"] = TileNotificationVersion;
                }

                StatusText.Text = strings.GetString(
                    backgroundTaskAvailable ? "TileUpdated" : "TileUpdatedBackgroundLimited");
                localSettings.Values.Remove(BackgroundTileTaskRegistration.LastErrorSettingKey);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Не удалось обновить данные плитки: " + ex);
                localSettings.Values[BackgroundTileTaskRegistration.LastErrorSettingKey] = ex.Message;
                StatusText.Text = strings.GetString("TileUpdateFailed");
                return false;
            }
            finally
            {
                refreshLock.Release();
            }
        }

        private async Task RequestStartPinAsync()
        {
            if (localSettings.Values[StartPinPromptedSettingKey] is bool alreadyPrompted &&
                alreadyPrompted)
            {
                return;
            }

            try
            {
                var appEntries = await Package.Current.GetAppListEntriesAsync();
                var appEntry = appEntries.FirstOrDefault();
                if (appEntry == null)
                {
                    throw new InvalidOperationException("The package has no Start menu app entry.");
                }

                StartScreenManager startScreenManager = StartScreenManager.GetDefault();
                if (await startScreenManager.ContainsAppListEntryAsync(appEntry))
                {
                    localSettings.Values[StartPinPromptedSettingKey] = true;
                    return;
                }

                bool pinned = await startScreenManager.RequestAddAppListEntryAsync(appEntry);
                localSettings.Values[StartPinPromptedSettingKey] = true;
                Debug.WriteLine("Start menu pin request result: " + pinned);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Не удалось запросить закрепление в меню Пуск: " + ex);
                StatusText.Text = strings.GetString("StartPinRequestFailed");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            try
            {
                StatusText.Text = strings.GetString("Saving");
                SaveSettings();
                bool updated = await RefreshProfileAndTileAsync(waitForCurrentRefresh: true);
                if (updated)
                {
                    await RequestStartPinAsync();
                }
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        private sealed class PresetChoice
        {
            public PresetChoice(string themeId, string title)
            {
                ThemeId = themeId;
                Title = title;
            }

            public string ThemeId { get; }
            public string Title { get; }
        }
    }
}
