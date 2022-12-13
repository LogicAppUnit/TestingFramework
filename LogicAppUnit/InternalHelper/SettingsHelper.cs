using Newtonsoft.Json.Linq;
using LogicAppUnit.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class to manage the <i>local.settings.json</i> file.
    /// </summary>
    internal class SettingsHelper
    {
        private readonly JObject _jObjectSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsHelper"/> class.
        /// </summary>
        /// <param name="settingsContent">The contents of the settings file.</param>
        public SettingsHelper(string settingsContent)
        {
            if (string.IsNullOrEmpty(settingsContent))
                throw new ArgumentNullException(nameof(settingsContent));

            _jObjectSettings = JObject.Parse(settingsContent);
        }

        /// <summary>
        /// Returns the settings content.
        /// </summary>
        /// <returns>The settings content.</returns>
        public override string ToString()
        {
            return _jObjectSettings.ToString();
        }

        /// <summary>
        /// Update the <i>local settings</i> by replacing all URL references to external systems with the URL reference for the mock test server.
        /// </summary>
        /// <param name="externalApiUrls">List of external API host names to be replaced.</param>
        public void ReplaceExternalUrlsWithMockServer(List<string> externalApiUrls)
        {
            // It is acceptable for a test project not to define any external API URLs if there are no external API dependencies in the workflows
            if (externalApiUrls.Count == 0)
                return;

            foreach (string apiUrl in externalApiUrls)
            {
                // Get all of the settings that start with the external API URL
                var settings = _jObjectSettings.SelectToken("Values").Children<JProperty>().Where(x => x.Value.ToString().StartsWith(apiUrl)).ToList();
                if (settings.Count > 0)
                {
                    Console.WriteLine($"Updating local settings file for '{apiUrl}':");

                    settings.ForEach((setting) => {
                        // Get the original URL that points to the external endpoint
                        Uri externalUrl = new Uri(setting.Value.ToString());

                        // Replace the host with the mock URL
                        Uri newExternalUrl = new Uri(new Uri(TestEnvironment.FlowV2MockTestHostUri), externalUrl.AbsolutePath);
                        setting.Value = newExternalUrl;

                        Console.WriteLine($"    {setting.Name}:");
                        Console.WriteLine($"      {externalUrl} ->");
                        Console.WriteLine($"        {newExternalUrl}");
                    });
                }
            }
        }

        /// <summary>
        /// Update the <i>local settings</i> by replacing values as defined in the dictionary.
        /// </summary>
        /// <param name="settingsToUpdate">The settings to be updated.</param>
        public void ReplaceSettingOverrides(Dictionary<string, string> settingsToUpdate)
        {
            if (settingsToUpdate == null)
                throw new ArgumentNullException(nameof(settingsToUpdate));

            Console.WriteLine($"Updating local settings file with test overrides:");

            foreach (KeyValuePair<string, string> setting in settingsToUpdate)
            {
                var settingToUpdate = _jObjectSettings.SelectToken("Values").Children<JProperty>().Where(x => x.Name == setting.Key).FirstOrDefault();
                Console.WriteLine($"    {setting.Key}");

                if (settingToUpdate != null)
                {
                    Console.WriteLine($"      Updated value to: {setting.Value}");
                    settingToUpdate.Value = setting.Value;
                }
                else
                {
                    Console.WriteLine($"      WARNING: Setting does not exist");
                }
            }
        }

        /// <summary>
        /// Get the value for a setting.
        /// </summary>
        /// <param name="settingName">The name of the setting.</param>
        /// <returns>The value of the setting, or <c>null</c> if the setting does not exist.</returns>
        public string GetSettingValue(string settingName)
        {
            if (settingName == null)
                throw new ArgumentNullException(nameof(settingName));

            var setting = _jObjectSettings.SelectToken("Values").Children<JProperty>().Where(x => x.Name == settingName).FirstOrDefault();

            return setting?.Value.ToString();
        }

        /// <summary>
        /// Get the value of the <i>OperationOptions</i> setting for a workflow.
        /// </summary>
        /// <param name="workflowName">The name of the workflow.</param>
        /// <returns>The value of the setting, or <c>null</c> if the setting does not exist.</returns>
        public string GetWorkflowOperationOptionsValue(string workflowName)
        {
            if (workflowName == null)
                throw new ArgumentNullException(nameof(workflowName));

            return GetSettingValue($"Workflows.{workflowName}.OperationOptions");
        }

        /// <summary>
        /// Set the value of the <i>OperationOptions</i> setting for a workflow.
        /// </summary>
        /// <param name="workflowName">The name of the workflow.</param>
        /// <param name="value">The value to be set.</param>
        /// <returns>The setting that has been created.</returns>
        public string SetWorkflowOperationOptionsValue(string workflowName, string value)
        {
            if (workflowName == null)
                throw new ArgumentNullException(nameof(workflowName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            string settingName = $"Workflows.{workflowName}.OperationOptions";
            _jObjectSettings["Values"][settingName] = value;

            return $"{settingName} = {value}";
        }
    }
}
