using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace DuolingoWidget.Services
{
    public static class BackgroundTileTaskRegistration
    {
        public const string TaskName = "DuolingoTileRefresh";
        public const string LastErrorSettingKey = "LastTileRefreshError";
        private const uint IntervalMinutes = 15;

        public static async Task<bool> EnsureRegisteredAsync()
        {
            BackgroundAccessStatus accessStatus = BackgroundExecutionManager.GetAccessStatus();
            if (accessStatus == BackgroundAccessStatus.Unspecified)
            {
                accessStatus = await BackgroundExecutionManager.RequestAccessAsync();
            }

            if (accessStatus == BackgroundAccessStatus.Unspecified ||
                accessStatus == BackgroundAccessStatus.DeniedByUser ||
                accessStatus == BackgroundAccessStatus.DeniedBySystemPolicy)
            {
                return false;
            }

            foreach (BackgroundTaskRegistration registration in BackgroundTaskRegistration.AllTasks.Values)
            {
                if (string.Equals(registration.Name, TaskName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var builder = new BackgroundTaskBuilder
            {
                Name = TaskName,
                IsNetworkRequested = true
            };
            builder.SetTrigger(new TimeTrigger(IntervalMinutes, false));
            builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
            builder.Register();

            return true;
        }
    }
}
