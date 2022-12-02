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
    internal static class SettingsHelper
    {
        /// <summary>
        /// Update the <i>local settings</i> by replacing all URL references to external systems with the URL reference for the mock test server.
        /// </summary>
        internal static void ReplaceExternalUrlsToFallBackOnMockServer(ref string localSettings, List<string> externalApiUrls)
        {
            // Not every Logic App has a local settings file
            if (string.IsNullOrEmpty(localSettings))
                return;

            // It is acceptable for a test project not to define any external API URLs if there are no external API dependencies in the workflows
            if (externalApiUrls.Count == 0)
                return;

            var jObjectSettings = JObject.Parse(localSettings);

            // Process each external API URL at a time
            foreach (string apiUrl in externalApiUrls)
            {
                // Get all of the settings that start with the external API URL
                var settings = jObjectSettings.SelectToken("Values").Children<JProperty>().Where(x => x.Value.ToString().StartsWith(apiUrl)).ToList();
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

            localSettings = jObjectSettings.ToString();
        }

        /// <summary>
        /// Update the <i>local settings</i> by replacing values as defined in the dictionary.
        /// </summary>
        /// <param name="localSettings">The local settings to be updated.</param>
        /// <param name="settingsToUpdate">The settings to be updated.</param>
        internal static string ReplaceSettingOverrides(string localSettings, Dictionary<string, string> settingsToUpdate)
        {
            if (string.IsNullOrEmpty(localSettings))
                throw new ArgumentNullException(nameof(localSettings), "Cannot apply local settings test overrides when there are no local settings for the Logic App");
            if (settingsToUpdate == null)
                throw new ArgumentNullException(nameof(settingsToUpdate));

            Console.WriteLine($"Updating local settings file with test overrides:");

            var jObjectSettings = JObject.Parse(localSettings);
            foreach (KeyValuePair<string, string> setting in settingsToUpdate)
            {
                var settingToUpdate = jObjectSettings.SelectToken("Values").Children<JProperty>().Where(x => x.Name == setting.Key).FirstOrDefault();
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

            return jObjectSettings.ToString();
        }
    }
}
