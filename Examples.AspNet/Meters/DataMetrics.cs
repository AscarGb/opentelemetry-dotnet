using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace Examples.AspNet.Meters
{
    public static class DataMetrics
    {
        public const string ActDataSeconds = "act_data_seconds";
        public static readonly double[] DataOperationSeconds = { 0.3, 0.5, 1, 5, 15, 30 };
        private const string None = "none";
        private static string? _appName;
        private static Counter<long>? _errors;
        private static Histogram<double>? _executeTime;
        private static Meter? _meter;
        private static Counter<long>? _actDataStatusCount;

        public static void Initialize(string appName, string appVersion)
        {
            _appName = appName;
            _meter = new Meter(_appName, appVersion);

            _executeTime =
                    _meter.CreateHistogram<double>(
                        ActDataSeconds,
                        "seconds",
                        "Performanse");

            _errors = _meter.CreateCounter<long>(
                "act_data_error_count",
                "pcs",
                "Error counter");

            _actDataStatusCount = _meter.CreateCounter<long>(
                "act_data_status_count",
                "pcs",
                "Data status counter");
        }

        private static void OnDataOperation(
            TimeSpan elapsedMilliSeconds,
            string operationName,
            string agentName,
            bool hasError)
        {
            var seconds = elapsedMilliSeconds.TotalSeconds;

            _executeTime?.Record(
                seconds,
                new KeyValuePair<string, object?>("op", operationName ?? None),
                new KeyValuePair<string, object?>("agent", agentName ?? None),
                new KeyValuePair<string, object?>("app", _appName ?? None));

            if (hasError)
                _errors?.Add(
                    1,
                    new KeyValuePair<string, object?>("op", operationName ?? None),
                    new KeyValuePair<string, object?>("agent", agentName ?? None),
                    new KeyValuePair<string, object?>("app", _appName ?? None));
        }

        public static void OndataStatusChange(string status) =>
                _actDataStatusCount?.Add(
                    1,
                    new KeyValuePair<string, object?>("status", status ?? None));

        public static async Task<T> ExecuteTask<T>(
            Func<Task<T>> action,
            string operationName,
            string agentName) =>
                await MetricExecutor.ExecuteTask(action, operationName, agentName, OnDataOperation);

        public static T Execute<T>(
            Func<T> action,
            string operationName,
            string agentName) =>
                MetricExecutor.Execute(action, operationName, agentName, OnDataOperation);

        public static T ExecuteMonad<T>(
            Func<(T result, bool hasError)> action,
            string operationName,
            string agentName) =>
                MetricExecutor.ExecuteMonad(action, operationName, agentName, OnDataOperation);
    }
}
