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
        private readonly bool WriteFunctionRuntimeStartupLogsToConsole;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTestHost"/> class.
        /// </summary>
        public WorkflowTestHost(
            WorkflowTestInput[] inputs = null,
            string localSettings = null, string parameters = null, string connectionDetails = null, string host = null,
            CsxTestInput[] csxTestInputs = null, DirectoryInfo artifactsDirectory = null, DirectoryInfo customLibraryDirectory = null,
            bool writeFunctionRuntimeStartupLogsToConsole = false)
        {
            this.OutputData = new List<string>();
            this.ErrorData = new List<string>();
            this.WriteFunctionRuntimeStartupLogsToConsole = writeFunctionRuntimeStartupLogsToConsole;

            this.WorkingDirectory = CreateWorkingFolder();
            CreateWorkingFilesRequiredForTest(inputs, localSettings, parameters, connectionDetails, host, csxTestInputs, artifactsDirectory, customLibraryDirectory);
            StartFunctionRuntime();
        }

        /// <summary>
        /// Create all of the working files and folders required to run the test.
        /// </summary>
        protected void CreateWorkingFilesRequiredForTest(WorkflowTestInput[] inputs,
            string localSettings, string parameters, string connectionDetails, string host,
            CsxTestInput[] csxTestInputs, DirectoryInfo artifactsDirectory, DirectoryInfo customLibraryDirectory)
        {
            if (inputs != null && inputs.Length > 0)
            {
                foreach (var input in inputs)
                {
                    Directory.CreateDirectory(Path.Combine(this.WorkingDirectory, input.WorkflowName));
                    CreateWorkingFile(input.WorkflowDefinition, Path.Combine(this.WorkingDirectory, input.WorkflowName, input.WorkflowFilename), true);
                }
            }

            CreateWorkingFile(localSettings, Constants.LOCAL_SETTINGS, true);
            CreateWorkingFile(host, Constants.HOST, true);
            CreateWorkingFile(parameters, Constants.PARAMETERS);
            CreateWorkingFile(connectionDetails, Constants.CONNECTIONS);

            WriteCsxFilesToWorkingFolder(csxTestInputs);
            CopySourceFolderToWorkingFolder(artifactsDirectory, Constants.ARTIFACTS_FOLDER);
            CopySourceFolderToWorkingFolder(customLibraryDirectory, Constants.CUSTOM_LIB_FOLDER);
        }

        /// <summary>
        /// Start the Function runtime.
        /// </summary>
        /// <exception cref="TestException">Thrown when the Function runtime could not be started.</exception>
        protected void StartFunctionRuntime()
        {
            try
            {
                // Kill any remaining function host processes that might interfere with the tests
                KillFunctionHostProcesses();

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

                    if (WriteFunctionRuntimeStartupLogsToConsole || processStarted.Task.IsCompleted)
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
                    throw new TestException("Functions runtime did not start properly. Please make sure you have the latest Azure Functions Core Tools installed and available on your PATH environment variable, and that Azurite is running.");
                }

                if (this.Process.HasExited)
                {
                    throw new TestException($"Functions runtime did not start properly. The error is '{errorData}'. Please make sure you have the latest Azure Functions Core Tools installed and available on your PATH environment variable, and that Azurite is running.");
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
        /// <exception cref="TestException">Thrown when the location of the 'func' executable could not be found.</exception>
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
                throw new TestException($"The enviroment variable PATH does not include the path for the '{FunctionsExecutableName}' executable. Searched:{Environment.NewLine}{string.Join(Environment.NewLine, exePaths.OrderBy(s => s))}");
            }
        }

        /// <summary>
        /// Create a unique working folder that is used by the test.
        /// </summary>
        private string CreateWorkingFolder()
        {
            string workingDirectory = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(workingDirectory);

            if (this.WriteFunctionRuntimeStartupLogsToConsole)
                Console.WriteLine($"Test Working Directory: {workingDirectory}");

            return workingDirectory;
        }

        /// <summary>
        /// Create a working file that is used by the test.
        /// </summary>
        /// <param name="filecontent">The file content.</param>
        /// <param name="fileName">The name of the file to be created.</param>
        /// <param name="isMandatory"><c>true</c> if the file is mandatory (i.e. there must be content), otherwise <c>false</c>.</param>
        /// <exception cref="TestException">A file is mandatory but has no content.</exception>
        private void CreateWorkingFile(string filecontent, string fileName, bool isMandatory = false)
        {
            if (string.IsNullOrEmpty(filecontent))
            {
                if (isMandatory)
                    throw new TestException($"The {fileName} file is not provided or its path not found. This file is needed for the unit testing.");
            }
            else
            {
                File.WriteAllText(Path.Combine(this.WorkingDirectory, fileName), filecontent);
            }
        }

        /// <summary>
        /// Copy the contents of a source folder to the working folder that is used by the test.
        /// </summary>
        /// <param name="directoryToCopy">The source directory to be copied.</param>
        /// <param name="targetDirectoryPath">The name of the target directory.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory to be copied does not exist.</exception>
        private void CopySourceFolderToWorkingFolder(DirectoryInfo directoryToCopy, string targetDirectoryPath)
        {
            if (directoryToCopy != null)
            {
                if (!directoryToCopy.Exists)
                {
                    throw new DirectoryNotFoundException(directoryToCopy.FullName);
                }

                var copyDestinationDirectory = Path.Combine(this.WorkingDirectory, targetDirectoryPath);
                DeepCopyDirectory(source: directoryToCopy, destination: new DirectoryInfo(copyDestinationDirectory));
            }
        }

        /// <summary>
        /// Copies the directory and all sub-directories (deep copy).
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        private static void DeepCopyDirectory(DirectoryInfo source, DirectoryInfo destination)
        {
            if (!destination.Exists)
            {
                destination.Create();
            }

            // Copy files
            foreach (var file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(destination.FullName, file.Name));
            }

            // Copy sub-directories
            foreach (var dir in source.GetDirectories())
            {
                var destinationDir = Path.Combine(destination.FullName, dir.Name);
                DeepCopyDirectory(dir, new DirectoryInfo(destinationDir));
            }
        }

        /// <summary>
        /// Writes the C# script files to the working folder
        /// </summary>
        /// <param name="csxTestInputs"></param>
        private void WriteCsxFilesToWorkingFolder(CsxTestInput[] csxTestInputs)
        {
            foreach (var csxTestInput in csxTestInputs)
            {
                var directory = Path.Combine(this.WorkingDirectory, csxTestInput.RelativePath);
                Directory.CreateDirectory(directory);
                CreateWorkingFile(csxTestInput.Script, Path.Combine(directory, csxTestInput.Filename), true);
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
