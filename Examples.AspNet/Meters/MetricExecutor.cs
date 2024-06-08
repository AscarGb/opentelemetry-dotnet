using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Examples.AspNet.Meters
{
    public static class MetricExecutor
    {

        public static async Task<T> ExecuteTask<T>(
            Func<Task<T>> action,
            string operationName,
            string agentName,
            Action<TimeSpan, string, string, bool> metricAction)
        {
            var hasError = false;
            var sw = Stopwatch.StartNew();

            try
            {
                var result = await action.Invoke();
                return result;
            }
            catch
            {
                hasError = true;

                throw;
            }
            finally
            {
                metricAction(
                    sw.Elapsed,
                    operationName,
                    agentName,
                    hasError);
            }
        }

        public static T Execute<T>(
            Func<T> action,
            string operationName,
            string agentName,
            Action<TimeSpan, string, string, bool> metricAction)
        {
            var hasError = false;
            var sw = Stopwatch.StartNew();

            try
            {
                var result = action.Invoke();
                return result;
            }
            catch
            {
                hasError = true;

                throw;
            }
            finally
            {
                metricAction(
                    sw.Elapsed,
                    operationName,
                    agentName,
                    hasError);
            }
        }

        public static T ExecuteMonad<T>(
            Func<(T result, bool hasError)> action,
            string operationName,
            string agentName,
            Action<TimeSpan, string, string, bool> metricAction)
        {
            var hasError = false;
            var sw = Stopwatch.StartNew();

            try
            {
                var (result, isError) = action.Invoke();

                hasError = isError;

                return result;
            }
            catch
            {
                hasError = true;

                throw;
            }
            finally
            {
                metricAction(
                    sw.Elapsed,
                    operationName,
                    agentName,
                    hasError);
            }
        }
    }
}
