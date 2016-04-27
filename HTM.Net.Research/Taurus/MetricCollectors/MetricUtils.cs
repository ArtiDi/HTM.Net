using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Util;
using Newtonsoft.Json;

namespace HTM.Net.Research.Taurus.MetricCollectors
{
    /// <summary>
    /// Agent for calling metric operations on the API server.
    /// </summary>
    public static class MetricUtils
    {
        public static MetricsConfiguration GetMetricsConfiguration()
        {
            // Load the metrics.json file
            string metrics = Properties.Resources.metrics;
            return JsonConvert.DeserializeObject<MetricsConfiguration>(metrics);
        }

        /// <summary>
        /// Return all metric names from the given metrics configuration
        /// </summary>
        /// <param name="metricsConfig">metrics configuration as returned by `getMetricsConfiguration()`</param>
        /// <returns>all metric names from the given metricsConfig</returns>
        public static List<string> GetMetricNamesFromConfig(MetricsConfiguration metricsConfig)
        {
            return metricsConfig.Values
                .SelectMany(m => m.Metrics.Keys)
                .ToList();
        }

        /// <summary>
        /// Create a model for a metric
        /// </summary>
        /// <param name="host">API server's hostname or IP address</param>
        /// <param name="apiKey">API server's API Key</param>
        /// <param name="modelParams">model parameters dict per _models POST API</param>
        /// <returns>model info dictionary from the result of the _models POST request on success.</returns>
        public static Map<string, object> CreateHtmModel(string host, string apiKey, CreateModelRequest modelParams)
        {
            // fillsup the data section that will trigger an import (?)
            throw new InvalidOperationException("posts to /_models endpoint webapi");
        }

        /// <summary>
        /// Create a model for a metric
        /// </summary>
        /// <param name="host">API server's hostname or IP address</param>
        /// <param name="apiKey">API server's API Key</param>
        /// <param name="userInfo">A dict containing custom user info to be included in metricSpec</param>
        /// <param name="modelParams">A dict containing custom model params to be included in modelSpec</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="resourceName">Name of the resource with which the metric is associated</param>
        /// <returns>model info dictionary from the result of the _models POST request on success.</returns>
        public static Map<string, object> CreateCustomHtmModel(string host, string apiKey, string metricName, string resourceName, string userInfo, ModelParams modelParams)
        {
            CreateModelRequest mParams = new CreateModelRequest();
            mParams.DataSource = "custom";
            mParams.MetricSpec = new CustomMetricSpec
            {
                Metric = metricName,
                Resource = resourceName,
                UserInfo = userInfo
            };
            mParams.ModelParams = modelParams;
            return CreateHtmModel(host, apiKey, mParams);
        }
    }
}