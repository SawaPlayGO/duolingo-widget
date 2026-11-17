using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace DuolingoWidget.Services
{
    public static class BackgroundTileTaskRegistration
    {
        public const string TaskName = "DuolingoTileRefresh";
        public const string LastErrorSettingKey = "LastTileRefreshError";
        public const string LastRegistrationStatusSettingKey = "BackgroundTileTaskRegistrationStatus";
        public const string LastRegistrationDetailsSettingKey = "BackgroundTileTaskRegistrationDetails";
        public const string LastBackgroundTaskStageSettingKey = "LastBackgroundTaskStage";
        public const string LastBackgroundTaskErrorSettingKey = "LastBackgroundTaskError";
        public const string LastBackgroundTaskCancellationSettingKey = "LastBackgroundTaskCancellation";
        private const uint IntervalMinutes = 15;
        private const string RegistrationRevision = "3";
        private const string RegisteredVersionSettingKey = "BackgroundTileTaskRegisteredVersion";

        public static async Task<bool> EnsureRegisteredAsync()
        {
            var settings = ApplicationData.Current.LocalSettings;
            var registrations = BackgroundTaskRegistration.AllTasks.Values
                .Where(registration => string.Equals(
                    registration.Name,
                    TaskName,
                    StringComparison.Ordinal))
                .ToArray();

            var packageVersion = Package.Current.Id.Version;
            string currentVersion = string.Format(
                "{0}.{1}.{2}.{3}",
                packageVersion.Major,
                packageVersion.Minor,
                packageVersion.Build,
                packageVersion.Revision) + "|" + RegistrationRevision;
            string registeredVersion = settings.Values[RegisteredVersionSettingKey] as string ?? string.Empty;
            bool hasRegisteredVersion = !string.IsNullOrEmpty(registeredVersion);
            bool refreshRegistration = hasRegisteredVersion
                ? !string.Equals(registeredVersion, currentVersion, StringComparison.Ordinal)
                : registrations.Length > 0;

            if (refreshRegistration)
            {
                foreach (BackgroundTaskRegistration registration in registrations)
                {
                    registration.Unregister(true);
                }

                BackgroundExecutionManager.RemoveAccess();
            }

            BackgroundAccessStatus accessStatus = BackgroundExecutionManager.GetAccessStatus();
            if (accessStatus == BackgroundAccessStatus.Unspecified || refreshRegistration)
            {
                accessStatus = await BackgroundExecutionManager.RequestAccessAsync();
            }

            if (accessStatus == BackgroundAccessStatus.Unspecified ||
                accessStatus == BackgroundAccessStatus.DeniedByUser ||
                accessStatus == BackgroundAccessStatus.DeniedBySystemPolicy)
            {
                int remainingCount = BackgroundTaskRegistration.AllTasks.Values.Count(registration =>
                    string.Equals(registration.Name, TaskName, StringComparison.Ordinal));
                settings.Values[LastRegistrationStatusSettingKey] = "NO ACCESS";
                settings.Values[LastRegistrationDetailsSettingKey] =
                    "not registered; access=" + accessStatus +
                    "; count=" + remainingCount +
                    "; version=" + currentVersion +
                    "; checked=" + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
                return false;
            }

            if (!refreshRegistration && registrations.Length > 0)
            {
                settings.Values[LastRegistrationStatusSettingKey] = "OK " + registrations.Length;
                settings.Values[LastRegistrationDetailsSettingKey] =
                    "registered; access=" + accessStatus +
                    "; count=" + registrations.Length +
                    "; version=" + currentVersion +
                    "; checked=" + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
                return true;
            }

            var builder = new BackgroundTaskBuilder
            {
                Name = TaskName,
                IsNetworkRequested = true
            };
            builder.SetTrigger(new TimeTrigger(IntervalMinutes, false));
            builder.Register();

            int registeredCount = BackgroundTaskRegistration.AllTasks.Values.Count(registration =>
                string.Equals(registration.Name, TaskName, StringComparison.Ordinal));
            settings.Values[LastRegistrationStatusSettingKey] = registeredCount > 0
                ? "OK " + registeredCount
                : "NO TASK";
            settings.Values[LastRegistrationDetailsSettingKey] =
                "register returned; access=" + accessStatus +
                "; count=" + registeredCount +
                "; version=" + currentVersion +
                "; checked=" + DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            if (registeredCount > 0)
            {
                settings.Values[RegisteredVersionSettingKey] = currentVersion;
            }

            return registeredCount > 0;
        }
    }
}
