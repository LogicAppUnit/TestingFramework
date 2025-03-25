using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Collections.Generic;
using System;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class for the Azurite storage emulator.
    /// </summary>
    internal static class AzuriteHelper
    {
        /// <summary>
        /// Determine if Azurite is running. Without Azurite running we can't run any workflows.
        /// </summary>
        /// <returns>True if Azurite is running, otherwise false.</returns>
        /// <remarks>
        /// Testing if Azurite is running is tricky because there are so many ways that Azurite can be installed and run. For example, you can run it within Visual Studio, or within
        /// Visual Studio Code, or as a stand-alone node application. The most robust way to determine if Azurite is running is to see if anything is listening on the Azurite ports.
        /// </remarks>
        internal static bool IsRunning(TestConfigurationAzurite config)
        {
            ArgumentNullException.ThrowIfNull(config);

            // If Azurite is running, it will run on localhost (127.0.0.1)
            IPAddress expectedIp = new IPAddress(new byte[] { 127, 0, 0, 1 });
            var expectedPorts = new[]
            {
                config.BlobServicePort,
                config.QueueServicePort,
                config.TableServicePort
            };

            // Get the active TCP listeners and filter for the Azurite ports
            IPEndPoint[] activeTcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            List<IPEndPoint> relevantListeners = activeTcpListeners.Where(t => expectedPorts.Contains(t.Port) && t.Address.Equals(expectedIp)).ToList();

            if (relevantListeners.Count == expectedPorts.Length)
            {
                Console.WriteLine($"Azurite is listening on ports {config.BlobServicePort} (Blob service), {config.QueueServicePort} (Queue service) and {config.TableServicePort} (Table service).");
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
