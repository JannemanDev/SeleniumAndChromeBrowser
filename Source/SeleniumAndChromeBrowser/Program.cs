using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Serilog.Core;
using Serilog;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.DevTools;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System;
using OpenQA.Selenium.Remote;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SeleniumAndChromeBrowser
{
    //TODO: - all tabs need to be loaded before opening a new tab by ChromeDriver succeeds?!
    // If Chrome crashed previously then ChromeDriver will not be able to connect to it anymore! Workaround: use new profile or start with GUI one-time and then use headless

    internal class Program
    {
        static Logger logger;
        static IConfigurationRoot config;
        static string executingDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        static async Task Main(string[] args)
        {
            var compileTime = new DateTime(Builtin.CompileTime, DateTimeKind.Utc).ToLocalTime();
            string version = $"Selenium and Chrome example v1.1 - BuildDate {compileTime}";

            string settingsFilename = "appsettings.json";
            if (args.Length > 0) settingsFilename = args[0];

            if (!File.Exists(settingsFilename))
            {
                Console.WriteLine($"{version}\n");
                Console.WriteLine($"Error: settings file \"{settingsFilename}\" not found!");
                return;
            }

            config = InitConfiguration(executingDir, settingsFilename);
            logger = InitLogging(config);

            logger.Information("");
            logger.Information("----------------------------------------------------------------------------------------------------------");
            logger.Information($"{version}\n");
            logger.Information($"Starting, executing directory is \"{executingDir}\"");

            int debuggingPort = config.GetRequiredSection("ChromeDriver").GetValue<int>("DebuggingPort");
            bool runHeadless = config.GetRequiredSection("ChromeDriver").GetValue<bool>("RunHeadless");

            ChromeDriver? driver;
            List<string> originalWindowHandles;
            ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode chromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode = config.GetRequiredSection("ChromeDriver").GetValue<ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode>("ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode");
            do
            {
                ChromeSearchResult chromeSearchResult = ExistingChromeAvailableListeningOnCorrectPort(debuggingPort);
                ChromeInstancesNotListeningOnSpecifiedDebuggingPort chromeInstancesNotListeningOnSpecifiedDebuggingPort = config.GetRequiredSection("ChromeDriver").GetValue<ChromeInstancesNotListeningOnSpecifiedDebuggingPort>("ChromeInstancesNotListeningOnSpecifiedDebuggingPort");

                if (!runHeadless)
                {
                    if (chromeSearchResult != ChromeSearchResult.FoundAndListeningOnCorrectPort)
                    {
                        if (chromeSearchResult != ChromeSearchResult.NotFound) CloseAllChromeInstances(chromeInstancesNotListeningOnSpecifiedDebuggingPort);

                        string profileDir = DetermineProfilePath(debuggingPort);

                        //always use a profile, else ChromeDriver will not connect to it when using multiple chrome instances because then default profile will be used!
                        string argumentsChrome = $"--remote-debugging-port={debuggingPort} --user-data-dir=\"{profileDir}\"";

                        string pathChrome = config.GetRequiredSection("ChromeDriver").GetValue<string>("ChromePath");
                        OpenChromeBrowser(pathChrome, argumentsChrome);
                    }
                }

                driver = StartChromeDriver(debuggingPort, runHeadless, chromeSearchResult);

                originalWindowHandles = GetWindowsHandles(driver);

                if (!runHeadless)
                {
                    //preserve already opened tabs if any, make a new tab active
                    logger.Information("Opening new tab");
                    driver.SwitchTo().NewWindow(WindowType.Tab);
                }
                else
                {
                    //in headless mode a "data;" tab is opened
                    driver.SwitchTo().Window(driver.WindowHandles[0]); //important! do not know why but else it will not work!
                    originalWindowHandles.Remove(driver.WindowHandles[0]);
                }

                string chromeVersion = ChromeUserAgentVersion(driver);
                logger.Information($"Chrome Browser User Agent string: {chromeVersion}");

                bool chromeConnectedIsHeadless = ChromeIsHeadless(driver);

                if (chromeConnectedIsHeadless) logger.Information("ChromeDriver is connected to a Chrome instance that is headless!");
                else logger.Information("ChromeDriver is connected to a Chrome instance that is non-headless (with GUI)!");

                if (chromeConnectedIsHeadless != runHeadless)
                {
                    logger.Error($"RunHeadless is {runHeadless} but chromeConnectedIsHeadless={chromeConnectedIsHeadless}!");
                    if (chromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode == ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode.AutoCloseAndRetry)
                    {
                        logger.Information("Closing Chrome instance and retrying...");

                        KillProcessByPort(debuggingPort);
                    }
                    else if (chromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode == ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode.Exit)
                    {
                        ShutdownChromeDriver(driver, originalWindowHandles);
                        return;
                    }
                    else if (chromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode == ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode.DontClose)
                    {
                        logger.Warning($"ChromeDriver will connect to a Chrome instance which is {HeadlessDescription(chromeConnectedIsHeadless)} instead of the preferred setting {HeadlessDescription(runHeadless)}");
                        break;
                    }
                    else throw new InvalidEnumArgumentException($"Invalid value for ChromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode: {chromeInstanceAlreadyOnSpecifiedDebuggingPortButInDifferentMode}");
                }
                else break;
            } while (true);

            if (driver == null)
            {
                logger.Information("Quiting...");
                ShutdownChromeDriver(driver, originalWindowHandles);
                return;
            }

            Uri website = new Uri(config.GetRequiredSection("Application").GetValue<string>("WebsiteToOpen"));
            logger.Information($"Navigating to {website}");
            driver.Navigate().GoToUrl(website);

            int extraWaitingTimeAfterWebsiteLoadedInSeconds = config.GetRequiredSection("Application").GetValue<int>("ExtraWaitingTimeAfterWebsiteLoadedInSeconds");
            await Task.Delay(TimeSpan.FromSeconds(extraWaitingTimeAfterWebsiteLoadedInSeconds));

            bool dumpContents = config.GetRequiredSection("Application").GetValue<bool>("DumpContents");
            if (dumpContents)
            {
                string filenameWebsiteContents = Path.Combine(executingDir, $"{website.Host}.html");
                File.WriteAllText(filenameWebsiteContents, driver.PageSource);
                logger.Information($"Contents of website {website} saved to \"{filenameWebsiteContents}\"");
            }

            bool takeScreenshot = config.GetRequiredSection("Application").GetValue<bool>("TakeScreenshot");

            if (takeScreenshot)
            {
                // Capture screenshot
                Screenshot screenshot = ((ITakesScreenshot)driver).GetScreenshot();

                // Save the screenshot to a file
                string screenshotPath = Path.Combine(executingDir, $"{website.Host}.png");
                screenshot.SaveAsFile(screenshotPath, ScreenshotImageFormat.Png);
                logger.Information($"Screenshot  of website {website} saved to \"{screenshotPath}\"");
            }

            //logger.Information("Press any key to close the chromedriver and quit...");
            //Console.ReadKey(true);

            ShutdownChromeDriver(driver, originalWindowHandles);
            logger.Information("Ended");
        }

        private static bool IsProfilePathOverriding()
        {
            string? overrideProfilePath = config.GetRequiredSection("ChromeDriver").GetValue<string?>("OverrideProfilePath");
            return !string.IsNullOrEmpty(overrideProfilePath);
        }

        private static string DetermineProfilePath(int debuggingPort)
        {
            string profileDir = executingDir;
            string? overrideProfilePath = config.GetRequiredSection("ChromeDriver").GetValue<string?>("OverrideProfilePath");
            if (string.IsNullOrEmpty(overrideProfilePath))
            {
                bool useTempPathForProfile = config.GetRequiredSection("ChromeDriver").GetValue<bool>("UseTempPathForProfile");
                if (useTempPathForProfile)
                {
                    string tempDir = Environment.ExpandEnvironmentVariables("%TEMP%");
                    profileDir = tempDir;
                }
                profileDir = Path.Combine(profileDir, $"chrome-debug-profile-{debuggingPort}");
                logger.Information($"Using generated profile path based on debuggingPort, using \"{profileDir}\"");
            }
            else
            {
                logger.Information($"Overriding profile path, using \"{overrideProfilePath}\"");
                if (!Directory.Exists(overrideProfilePath))
                {
                    logger.Information($"Profile directory \"{overrideProfilePath}\" does *NOT* exist!");
                    logger.Information($"Creating profile directory \"{overrideProfilePath}\"");
                    Directory.CreateDirectory(overrideProfilePath);
                }
                profileDir = overrideProfilePath;
            }

            return profileDir;
        }

        static IConfigurationRoot InitConfiguration(string loadFromPath, string settingsFilename)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(loadFromPath)
                .AddJsonFile(settingsFilename, optional: false)
                .Build();

            return configuration;
        }

        static Logger InitLogging(IConfigurationRoot configuration)
        {
            Logger logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            return logger;
        }

        private static List<string> GetWindowsHandles(ChromeDriver? driver)
        {
            int windowsCount = driver.WindowHandles.Count;
            logger.Information($"Chrome has {windowsCount} window(s) open:");

            List<string> windowHandles = new List<string>();
            foreach (string windowHandle in driver.WindowHandles)
            {
                logger.Information($" window {windowHandle}");
                windowHandles.Add(windowHandle);
            }

            return windowHandles;
        }

        private static void ShutdownChromeDriver(ChromeDriver? driver, List<string> originalWindowHandles)
        {
            try
            {
                logger.Information($"Closing ChromeDriver...");

                if (driver != null)
                {
                    List<string> windowHandlesToClose = GetWindowsHandles(driver).Except(originalWindowHandles).ToList();

                    // Close all windows
                    foreach (string windowHandle in windowHandlesToClose)
                    {
                        driver.SwitchTo().Window(windowHandle);
                        logger.Information($"Closing window {windowHandle} - {driver.Title}");
                        driver.Close();
                    }
                }

                logger.Information($"Closed ChromeDriver...");
            }
            catch (Exception ex)
            {
                logger.Information($"Error while closing ChromeDriver: {ex.Message}");
            }
            finally
            {
                logger.Information($"Quitting ChromeDriver...");
                driver?.Quit();
                logger.Information($"Quitted ChromeDriver...");
            }
        }

        private static ChromeSearchResult ExistingChromeAvailableListeningOnCorrectPort(int portNumber)
        {
            Dictionary<OSPlatform, NetstatOutputDefinition> platformConfigs = new Dictionary<OSPlatform, NetstatOutputDefinition>
            {
                {
                    OSPlatform.Windows,
                    new NetstatOutputDefinition
                    {
                        Command = @"c:\Windows\System32\netstat.exe",
                        Arguments = "-ano -p TCP",
                        NumColumns = 5,
                        ProtoColumnIndex = 0,
                        LocalAddressColumnIndex = 1,
                        ForeignAddressColumnIndex = 2,
                        StateColumnIndex = 3,
                        PidColumnIndex = 4,
                        State = "LISTENING"
                    }
                },
                {
                    OSPlatform.Linux,
                    new NetstatOutputDefinition
                    {
                        Command = "netstat",
                        Arguments = "-tlnp",
                        NumColumns = 7,
                        ProtoColumnIndex = 0,
                        LocalAddressColumnIndex = 3,
                        ForeignAddressColumnIndex = 4,
                        StateColumnIndex = 5,
                        PidColumnIndex = 6,
                        State = "LISTEN"
                    }
                }
            };

            OSPlatform currentPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows : OSPlatform.Linux;
            NetstatOutputDefinition config = platformConfigs[currentPlatform];

            Process process = new Process();

            process.StartInfo.FileName = config.Command;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.Arguments = config.Arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            logger.Information($"Command run to find all processes using a TCP port: {config.Command} {config.Arguments}");
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            logger.Debug($"Output:\n{output}");
            process.WaitForExit();
            process.Dispose();


            // Parse the output to get the process IDs from the last column
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool chromeRunning = false;
            foreach (string line in lines)
            {
                string[] columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length != config.NumColumns) continue;

                string protoColumn = columns[config.ProtoColumnIndex];
                string localAddressColumn = columns[config.LocalAddressColumnIndex];
                string foreignAddressColumn = columns[config.ForeignAddressColumnIndex];
                string stateColumn = columns[config.StateColumnIndex];
                int pid = int.Parse(columns[config.PidColumnIndex].Split('/')[0]); //on Linux PID is followed by /processName

                try
                {
                    string processName = Process.GetProcessById(pid).ProcessName;

                    if (processName.Equals("chrome", StringComparison.InvariantCultureIgnoreCase))
                    {
                        chromeRunning = true;

                        if (localAddressColumn.EndsWith($":{portNumber}") && stateColumn.Equals(config.State, StringComparison.InvariantCultureIgnoreCase))
                        {
                            logger.Information($"A Chrome process was found {config.State} on port {portNumber}: Process ID: {pid}");
                            return ChromeSearchResult.FoundAndListeningOnCorrectPort;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Information($"Error retrieving process name for PID {pid}: {ex.Message}");
                }
            }

            if (chromeRunning)
            {
                logger.Information($"Chrome process(es) were found, but *none* was in state {config.State} on port {portNumber}");
                return ChromeSearchResult.Found;
            }
            else logger.Information($"Chrome process(es) were *NOT* found!");

            return ChromeSearchResult.NotFound;
        }

        private static ChromeDriver? StartChromeDriver(int debuggingPort, bool headLess, ChromeSearchResult chromeSearchResult)
        {
            bool attach = (chromeSearchResult == ChromeSearchResult.FoundAndListeningOnCorrectPort);

            ChromeOptions chromeOptions = new ChromeOptions();
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Normal;

            string chromeType;
            if (headLess)
            {
                chromeType = "headless instance";

                string profileDir = DetermineProfilePath(debuggingPort);

                //chromeOptions.AddArgument("--verbose");
                //chromeOptions.AddArgument($"--log-path=c:\\temp\\chromedriver.log"); // Specify your desired log file path
                //chromeOptions.AddArgument($"--log-level=ALL");
                //chromeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
                //chromeOptions.SetLoggingPreference(LogType.Client, LogLevel.All);
                //chromeOptions.SetLoggingPreference(LogType.Driver, LogLevel.All);
                //chromeOptions.SetLoggingPreference(LogType.Profiler, LogLevel.All);
                //chromeOptions.SetLoggingPreference(LogType.Server, LogLevel.All);

                chromeOptions.AddArgument($"--user-data-dir={profileDir}");
                chromeOptions.AddArgument("--headless=new");
                if (!attach) chromeOptions.AddArgument($"--remote-debugging-port={debuggingPort}");
                else chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}";

                string[] extraHeadlessArguments = config.GetRequiredSection("ChromeDriver").GetSection("ExtraHeadlessArguments").Get<string[]>();
                extraHeadlessArguments = extraHeadlessArguments.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                chromeOptions.AddArguments(extraHeadlessArguments);

                logger.Information($"Chrome Driver will start in headless mode and with:");
                logger.Information($" remote-debugging-port={debuggingPort}");
                logger.Information($" user-data-dir=\"{profileDir}\"");
                if (extraHeadlessArguments.Any()) logger.Information($" extra arguments: {string.Join(" ", extraHeadlessArguments)}");
            }
            else
            {
                chromeType = "browser";
                chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}"; //this line is not needed when using headless mode
            }

            if (IsProfilePathOverriding() && chromeSearchResult == ChromeSearchResult.Found)
                logger.Warning("If one of the other running Chrome instance(s) is using the same profile, ChromeDriver will fail to connect!");

            ChromeDriver? driver = null;
            try
            {
                string chromeDriverPath = config.GetRequiredSection("ChromeDriver").GetValue<string>("ChromeDriverPath");
                bool usingChromeDriverFromConfig = chromeDriverPath != "";
                string chromeDriverFileName = "";

                if (usingChromeDriverFromConfig)
                {
                    if (Directory.Exists(chromeDriverPath) == false)
                    {
                        logger.Warning($"ChromeDriver directory does not exist: \"{chromeDriverPath}\"");
                        usingChromeDriverFromConfig = false;
                    }

                    chromeDriverFileName = Path.Combine(chromeDriverPath, "chromedriver.exe");
                    if (File.Exists(chromeDriverFileName) == false)
                    {
                        logger.Warning($"ChromeDriver \"chromedriver.exe\" not found in path \"{chromeDriverPath}\"");
                        usingChromeDriverFromConfig = false;
                    }
                }

                ChromeDriverService chromeDriverService;
                if (usingChromeDriverFromConfig)
                {
                    logger.Information($"Using ChromeDriver located at: \"{chromeDriverFileName}\"");
                    chromeDriverService = ChromeDriverService.CreateDefaultService(chromeDriverPath);
                }
                else
                {
                    logger.Information("Trying to use builtin or in path found ChromeDriver");
                    chromeDriverService = ChromeDriverService.CreateDefaultService();
                }

                chromeDriverService.LogPath = Path.Combine(executingDir, "chromedriver.log");
                chromeDriverService.EnableVerboseLogging = false;

                logger.Information($"Starting ChromeDriver and connecting to Chrome {chromeType} on 127.0.0.1:{debuggingPort}\n");
                int connectionTimeOut = config.GetRequiredSection("ChromeDriver").GetValue<int>("connectionTimeOut");
                driver = new ChromeDriver(chromeDriverService, chromeOptions, TimeSpan.FromSeconds(connectionTimeOut));

                ShowChromeDriverVersions(driver);
            }
            catch (Exception ex)
            {
                logger.Information($"Error attaching ChromeDriver: {ex.Message}");
            }

            return driver;
        }

        private static void ShowChromeDriverVersions(ChromeDriver? driver)
        {
            ICapabilities capabilities = ((WebDriver)driver).Capabilities;

            bool isHeadless = capabilities.HasCapability("goog:chromeOptions");

            var seleniumWebDriverVersion = (capabilities.GetCapability("chrome") as Dictionary<string, object>)["chromedriverVersion"];
            logger.Information("ChromeDriver version: " + seleniumWebDriverVersion);

            string? browserVersion = capabilities.GetCapability("browserVersion") as string;
            logger.Information("Chrome Browser version: " + browserVersion);
        }

        private static void OpenChromeBrowser(string pathChrome, string argumentsChrome)
        {
            Process process = new Process();
            process.StartInfo.FileName = pathChrome;
            process.StartInfo.Arguments = argumentsChrome;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            logger.Information("Waiting for Chrome to start");
            process.WaitForInputIdle();
            logger.Information($"Chrome started with arguments: \"{pathChrome}\" {argumentsChrome}");

            //logger.Information("Press any key to continue...");
            //Console.ReadKey(true);
        }

        static bool CloseAllChromeInstances(ChromeInstancesNotListeningOnSpecifiedDebuggingPort chromeInstancesNotListeningOnSpecifiedDebuggingPort)
        {
            if (chromeInstancesNotListeningOnSpecifiedDebuggingPort == ChromeInstancesNotListeningOnSpecifiedDebuggingPort.DontClose) return false;

            Process[] processes = Process.GetProcessesByName("chrome");

            if (processes.Length > 0)
            {
                string json = JsonConvert.SerializeObject(processes.Select(p => new { p.Id, p.ProcessName, p.SessionId, p.HasExited, p.StartTime }),
                    Formatting.Indented,
                    new JsonSerializerSettings //only needed when you want to serialize all process properties
                    {
                        Error = (sender, args) =>
                        {
                            var currentError = args.ErrorContext.Error.Message;
                            args.ErrorContext.Handled = true;
                        },
                    });
                logger.Information($"{processes.Count()} Chrome processes found!");
                logger.Debug($"Process details:\n{json}");

                if (chromeInstancesNotListeningOnSpecifiedDebuggingPort == ChromeInstancesNotListeningOnSpecifiedDebuggingPort.Ask)
                {
                    logger.Information("Press any key to close all Chrome processes or <ESC> to skip...");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) return false;
                }

                logger.Information("Closing processes...");
                processes.ToList().ForEach(p => p.CloseMainWindow());
                logger.Information("Waiting for processes to exit...");
                processes.ToList().ForEach(p => p.WaitForExit());
                logger.Information("Processes all exited...");

                return true;
            }
            else
            {
                logger.Information($"No processes found!");

                return true;
            }
        }

        static string ChromeUserAgentVersion(ChromeDriver? driver)
        {
            if (driver == null) return "n/a";

            //No need to open a new tab
            // Execute JavaScript to get the browser version
            string browserVersion = driver.ExecuteScript("return navigator.userAgent").ToString();

            return browserVersion;
        }

        static bool ChromeIsHeadless(ChromeDriver? driver)
        {
            if (driver == null) throw new ArgumentException("Driver is null!");

            //No need to open a new tab
            //See https://antoinevastel.com/bot%20detection/2018/01/17/detect-chrome-headless-v2.html#:%7E:text=In%20order%20to%20automate%20Chrome,possible%20to%20detect%20Chrome%20headless
            string result = driver.ExecuteScript("return navigator.webdriver").ToString();
            bool chromeIsHeadless = result.Equals("True", StringComparison.InvariantCultureIgnoreCase);

            return chromeIsHeadless;
        }

        static void KillProcessByPort(int port)
        {
            Process process = new Process();
            string filename = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) filename = Path.Combine(executingDir, "killTaskUsingPort.bat");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) filename = Path.Combine(executingDir, "killTaskUsingPort.sh");

            process.StartInfo.FileName = filename;
            process.StartInfo.Arguments = port.ToString();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            process.Dispose();

            logger.Information(output);
        }

        static string HeadlessDescription(bool isHeadless)
        {
            if (isHeadless) return "headless (no GUI)";
            else return "not headless (with GUI)";
        }
    }
}
