using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace DuolingoWidget.Services
{
    internal static class BackgroundTaskDiagnostics
    {
        private const ulong MaxLogBytes = 256 * 1024;
        private static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);

        public static async Task WriteAsync(string message)
        {
            await WriteLock.WaitAsync();
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    "diagnostics",
                    CreationCollisionOption.OpenIfExists);
                var file = await folder.CreateFileAsync(
                    "background-task.log",
                    CreationCollisionOption.OpenIfExists);
                var properties = await file.GetBasicPropertiesAsync();
                if (properties.Size > MaxLogBytes)
                {
                    await FileIO.WriteTextAsync(file, string.Empty);
                }

                string line = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                    " " + message.Replace("\r", " ").Replace("\n", " ") +
                    Environment.NewLine;
                await FileIO.AppendTextAsync(file, line);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Не удалось записать диагностику фоновой задачи: " + ex);
            }
            finally
            {
                WriteLock.Release();
            }
        }

        public static Task WriteExceptionAsync(string stage, Exception exception)
        {
            string errorCode = unchecked((uint)exception.HResult)
                .ToString("X8", CultureInfo.InvariantCulture);
            return WriteAsync(
                "error stage=" + stage +
                " type=" + exception.GetType().Name +
                " hresult=0x" + errorCode +
                " message=" + exception.Message +
                " stack=" + exception.StackTrace);
        }
    }
}
