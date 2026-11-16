using System;
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
        public const string LastStartedSettingKey = "LastBackgroundTileTaskStartedAt";
        private const uint IntervalMinutes = 15;
        private const string RegistrationRevision = "2";
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

            if (refreshRegistration)
            {
                settings.Values[RegisteredVersionSettingKey] = currentVersion;
            }

            if (accessStatus == BackgroundAccessStatus.Unspecified ||
                accessStatus == BackgroundAccessStatus.DeniedByUser ||
                accessStatus == BackgroundAccessStatus.DeniedBySystemPolicy)
            {
                return false;
            }

            if (!refreshRegistration && registrations.Length > 0)
            {
                return true;
            }

            var builder = new BackgroundTaskBuilder
            {
                Name = TaskName,
                IsNetworkRequested = true
            };
            builder.SetTrigger(new TimeTrigger(IntervalMinutes, false));
            builder.Register();
            settings.Values[RegisteredVersionSettingKey] = currentVersion;

            return true;
        }
    }
}
