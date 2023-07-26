using LogicAppUnit.InternalHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LogicAppUnit.Hosting
{
    /// <summary>
    /// The workflow test host.
    /// </summary>
    internal class WorkflowTestHost : IDisposable
    {
        private const string FunctionsExecutableName = "func";

        /// <summary>
        /// Get or sets the output data.
        /// </summary>
        public List<string> OutputData { get; private set; }

        /// <summary>
        /// Gets or sets the error data.
        /// </summary>
        public List<string> ErrorData { get; private set; }

        /// <summary>
        /// Gets or sets the Working directory.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// The Function runtime process.
        private Process Process;

        /// <c>true</c> if the Functions runtime start-up logs are to be written to the console, otherwise <c>false</c>.
        /// The start-up logs can be rather verbose so we don't always went to include this information in the test execution logs.
        private readonly bool WriteFunctionRuntineStartupLogsToConsole;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTestHost"/> class.
        /// </summary>
        public WorkflowTestHost(
            WorkflowTestInput[] inputs = null,
            string localSettings = null, string parameters = null, string connectionDetails = null, string host = null, DirectoryInfo artifactsDirectory = null,
            bool writeFunctionRuntineStartupLogsToConsole = false)
        {
            this.WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
            this.OutputData = new List<string>();
            this.ErrorData = new List<string>();
            this.WriteFunctionRuntineStartupLogsToConsole = writeFunctionRuntineStartupLogsToConsole;

            this.StartFunctionRuntime(inputs, localSettings, parameters, connectionDetails, host, artifactsDirectory);
        }

        /// <summary>
        /// Starts the function runtime.
        /// </summary>
        protected void StartFunctionRuntime(WorkflowTestInput[] inputs, string localSettings, string parameters, string connectionDetails, string host, DirectoryInfo artifactsDirectory)
        {
            try
            {
                // Kill any remaining function host processes that might interfere with the tests
                KillFunctionHostProcesses();

                Directory.CreateDirectory(this.WorkingDirectory);

                if (inputs != null && inputs.Length > 0)
                {
                    foreach (var input in inputs)
                    {
                        if (!string.IsNullOrEmpty(input.WorkflowName))
                        {
                            Directory.CreateDirectory(Path.Combine(this.WorkingDirectory, input.WorkflowName));
                            File.WriteAllText(Path.Combine(this.WorkingDirectory, input.WorkflowName, input.WorkflowFilename), input.WorkflowDefinition);
                        }
                    }
                }

                if (artifactsDirectory != null)
                {
                    if (!artifactsDirectory.Exists)
                    {
                        throw new DirectoryNotFoundException(artifactsDirectory.FullName);
                    }

                    var artifactsWorkingDirectory = Path.Combine(this.WorkingDirectory, "Artifacts");
                    Directory.CreateDirectory(artifactsWorkingDirectory);
                    CopyDirectory(source: artifactsDirectory, destination: new DirectoryInfo(artifactsWorkingDirectory));
                }

                if (!string.IsNullOrEmpty(parameters))
                {
                    File.WriteAllText(Path.Combine(this.WorkingDirectory, "parameters.json"), parameters);
                }

                if (!string.IsNullOrEmpty(connectionDetails))
                {
                    File.WriteAllText(Path.Combine(this.WorkingDirectory, "connections.json"), connectionDetails);
                }

                if (!string.IsNullOrEmpty(localSettings))
                {
                    File.WriteAllText(Path.Combine(this.WorkingDirectory, "local.settings.json"), localSettings);
                }
                else
                {
                    throw new InvalidOperationException("The local.settings.json file is not provided or its path not found. This file is needed for the unit testing.");
                }

                if (!string.IsNullOrEmpty(host))
                {
                    File.WriteAllText(Path.Combine(this.WorkingDirectory, "host.json"), host);
                }
                else
                {
                    throw new InvalidOperationException("The host.json file is not provided or its path not found. This file is needed for the unit testing.");
                }

                this.Process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WorkingDirectory = this.WorkingDirectory,
                        FileName = GetEnvPathForFunctionTools(),
                        Arguments = "start --verbose",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                // Hook up an event handler for the Standard Output stream
                var processStarted = new TaskCompletionSource<bool>();
                this.Process.OutputDataReceived += (sender, args) =>
                {
                    var outputData = args.Data;

                    if (WriteFunctionRuntineStartupLogsToConsole || processStarted.Task.IsCompleted)
                    {
                        Console.WriteLine(outputData);
                    }

                    if (outputData != null && outputData.Contains("Host started") && !processStarted.Task.IsCompleted)
                    {
                        processStarted.SetResult(true);
                    }

                    lock (this)
                    {
                        this.OutputData.Add(args.Data);
                    }
                };

                // Hook up an event handler for the Standard Error stream
                var errorData = string.Empty;
                this.Process.ErrorDataReceived += (sender, args) =>
                {
                    errorData = args.Data;
                    Console.Write(errorData);

                    lock (this)
                    {
                        this.ErrorData.Add(args.Data);
                    }
                };

                // Start the Function host process
                this.Process.Start();
                this.Process.BeginOutputReadLine();
                this.Process.BeginErrorReadLine();

                // Wait for the Function host process to start, or timeout after 2 minutes
                var result = Task.WhenAny(processStarted.Task, Task.Delay(TimeSpan.FromMinutes(2))).Result;

                if (result != processStarted.Task)
                {
                    throw new InvalidOperationException("Functions runtime did not start properly. Please make sure you have the latest Azure Functions Core Tools installed and available on your PATH environment variable, and that Azurite is up and running.");
                }

                if (this.Process.HasExited)
                {
                    throw new InvalidOperationException($"Functions runtime did not start properly. The error is '{errorData}'. Please make sure you have the latest Azure Functions Core Tools installed and available on your PATH environment variable, and that Azurite is up and running.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                // Kill any remaining function host processes so that we can then delete the working directory
                KillFunctionHostProcesses();

                Directory.Delete(this.WorkingDirectory, recursive: true);

                throw;
            }
        }

        /// <summary>
        /// Kill all instances of the Function host process.
        /// </summary>
        private static void KillFunctionHostProcesses()
        {
            Process[] processes = Process.GetProcessesByName(FunctionsExecutableName);
            foreach (var process in processes)
            {
                process.Kill(true);
            }
        }

        /// <summary>
        /// Retrieve the path of the 'func' executable (Azure Functions Core tools). 
        /// </summary>
        /// <returns>The path to the 'func' executable.</returns>
        /// <exception cref="Exception">Thrown when the location of the 'func' executable could not be found.</exception>
        private static string GetEnvPathForFunctionTools()
        {
            string environmentPath;
            string exeName;

            // Handle the differences between platforms
            if (OperatingSystem.IsWindows())
            {
                // The path to the 'func' executable can be in any of the environment variable scopes, depending on how the Functions Core Tools were installed.
                // If a DevOps build pipeline has updated the PATH environment variable for the 'Machine' or 'User' scopes, the 'Process' scope is not automatically updated to reflect the change.
                // So merge all three scopes to be sure!
                environmentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) + Path.PathSeparator + 
                                    Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) + Path.PathSeparator +
                                    Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                exeName = $"{FunctionsExecutableName}.exe";
            }
            else
            {
                environmentPath = Environment.GetEnvironmentVariable("PATH");
                exeName = FunctionsExecutableName;
            }

            var exePaths = environmentPath.Split(Path.PathSeparator).Distinct().Select(x => Path.Combine(x, exeName));
            string exePathMatch = exePaths.Where(x => File.Exists(x)).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(exePathMatch))
            {
                Console.WriteLine($"Path for Azure Function Core tools: {exePathMatch}");
                return exePathMatch;
            }
            else
            {
                throw new TestException($"The enviroment variable PATH does not include the path for the '{FunctionsExecutableName}' executable. Searched: {string.Join(Path.PathSeparator, exePaths)}");
            }
        }

        /// <summary>
        /// Copies the directory.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        protected static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
        {
            if (!destination.Exists)
            {
                destination.Create();
            }

            // Copy all files
            var files = source.GetFiles();
            foreach (var file in files)
            {
                file.CopyTo(Path.Combine(destination.FullName, file.Name));
            }

            // Process subdirectories
            var dirs = source.GetDirectories();
            foreach (var dir in dirs)
            {
                // Get destination directory
                var destinationDir = Path.Combine(destination.FullName, dir.Name);

                // Call CopyDirectory() recursively
                CopyDirectory(dir, new DirectoryInfo(destinationDir));
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            LoggingHelper.LogBanner("Test completed");

            // Log the version number
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"LogicAppUnit v{version.Major}.{version.Minor}.{version.Build}");

            try
            {
                // Kill any remaining function host processes so that we can then delete the working directory
                this.Process?.Close();
                KillFunctionHostProcesses();
            }
            finally
            {
                var i = 0;
                while (i < 5)
                {
                    try
                    {
                        Directory.Delete(this.WorkingDirectory, recursive: true);
                        break;
                    }
                    catch
                    {
                        i++;
                        Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                    }
                }
            }
        }
    }
}
