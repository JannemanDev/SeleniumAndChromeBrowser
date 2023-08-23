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

    internal class Program
    {
        static Logger logger;
        static IConfigurationRoot config;
        static string executingDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

        static void Main(string[] args)
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

                        string profileDir = executingDir;
                        bool useTempPathForProfile = config.GetRequiredSection("ChromeDriver").GetValue<bool>("UseTempPathForProfile");
                        if (useTempPathForProfile)
                        {
                            string tempDir = Environment.ExpandEnvironmentVariables("%TEMP%");
                            profileDir = tempDir;
                        }
                        profileDir = Path.Combine(profileDir, $"chrome-debug-profile-{debuggingPort}");

                        //always use a profile, else ChromeDriver will not connect to it when using multiple chrome instances because then default profile will be used!
                        string argumentsChrome = $"--remote-debugging-port={debuggingPort} --user-data-dir=\"{profileDir}\"";

                        string pathChrome = config.GetRequiredSection("ChromeDriver").GetValue<string>("ChromePath");
                        OpenChromeBrowser(pathChrome, argumentsChrome);
                    }
                }

                driver = StartChromeDriver(debuggingPort, runHeadless, chromeSearchResult == ChromeSearchResult.FoundAndListeningOnCorrectPort);

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
                        ShutdownChromeDriver(driver);
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
                ShutdownChromeDriver(driver);
                return;
            }

            logger.Information("Opening new tab");
            //open new tab and navigate to correct website
            driver.SwitchTo().NewWindow(WindowType.Tab);
            string website = "https://www.google.nl"; //include protocol!
            logger.Information($"Navigating to {website}");
            driver.Navigate().GoToUrl(website);

            logger.Information("Press any key to close the chromedriver and quit...");
            Console.ReadKey(true);

            ShutdownChromeDriver(driver);
            logger.Information("Ended");
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

        private static void ShutdownChromeDriver(ChromeDriver? driver)
        {
            try
            {
                logger.Information($"Closing ChromeDriver...");
                driver?.Close();
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
            //string command = $"netstat -ano -p TCP";
            //ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            //{
            //    RedirectStandardOutput = true,
            //    UseShellExecute = false,
            //    CreateNoWindow = true
            //};

            //Process process = new Process() { StartInfo = startInfo };
            //logger.Information($"Command run to find all processes using a TCP port: {command}");
            //process.Start();

            //string output = process.StandardOutput.ReadToEnd();
            //logger.Debug($"Output:\n{output}");
            //process.WaitForExit();
            //process.Dispose();

            Process process = new Process();
            string command = "";
            string arguments = "";
            int numColumns;
            int protoColumnIndex;
            int localAddressColumnIndex;
            int foreignAddressColumnIndex;
            int stateColumnIndex;
            int pidColumnIndex;
            string state;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command = @"c:\Windows\System32\netstat.exe";
                arguments = "-ano -p TCP";
                numColumns = 5;
                protoColumnIndex = 0;
                localAddressColumnIndex = 1;
                foreignAddressColumnIndex = 2;
                stateColumnIndex = 3;
                pidColumnIndex = 4;
                state = "LISTENING";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                command = "netstat";
                arguments = "-tlnp";
                numColumns = 7;
                protoColumnIndex = 0;
                localAddressColumnIndex = 3;
                foreignAddressColumnIndex = 4;
                stateColumnIndex = 5;
                pidColumnIndex = 6;
                state = "LISTEN";
            }
            else throw new ArgumentException($"Unsupported OS: {RuntimeInformation.OSDescription}");

            process.StartInfo.FileName = command;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            logger.Information($"Command run to find all processes using a TCP port: {command} {arguments}");
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
                if (columns.Length != numColumns) continue;

                string protoColumn = columns[protoColumnIndex];
                string localAddressColumn = columns[localAddressColumnIndex];
                string foreignAddressColumn = columns[foreignAddressColumnIndex];
                string stateColumn = columns[stateColumnIndex];
                int pid = int.Parse(columns[pidColumnIndex].Split('/')[0]); //on Linux PID is followed by /processName

                try
                {
                    string processName = Process.GetProcessById(pid).ProcessName;

                    if (processName.Equals("chrome", StringComparison.InvariantCultureIgnoreCase))
                    {
                        chromeRunning = true;

                        if (localAddressColumn.EndsWith($":{portNumber}") && stateColumn.Equals(state, StringComparison.InvariantCultureIgnoreCase))
                        {
                            logger.Information($"A Chrome process was found {state} on port {portNumber}: Process ID: {pid}");
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
                logger.Information($"Chrome process(es) were found, but *none* was in state {state} on port {portNumber}");
                return ChromeSearchResult.Found;
            }
            else logger.Information($"No Chrome process was NOT found!");

            return ChromeSearchResult.NotFound;
        }

        private static ChromeDriver? StartChromeDriver(int debuggingPort, bool headLess, bool attach)
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            string chromeType;
            if (headLess)
            {
                chromeType = "headless instance";

                chromeOptions.AddArgument("--headless=new");
                if (!attach) chromeOptions.AddArgument($"--remote-debugging-port={debuggingPort}");
                else chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}";

                string[] extraHeadlessArguments = config.GetRequiredSection("ChromeDriver").GetSection("ExtraHeadlessArguments").Get<string[]>();
                extraHeadlessArguments = extraHeadlessArguments.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                chromeOptions.AddArguments(extraHeadlessArguments);

                string extraHeadlessArgumentsString = "";
                if (extraHeadlessArguments.Any()) extraHeadlessArgumentsString = $"and extra arguments: {string.Join(" ", extraHeadlessArguments)}";
                logger.Information($"Chrome Driver will start in headless mode with remote-debugging-port={debuggingPort} {extraHeadlessArgumentsString}");
            }
            else
            {
                chromeType = "browser";
                chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}"; //this line is not needed when using headless mode
            }

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

            driver.Navigate().GoToUrl("about:blank");

            // Execute JavaScript to get the browser version
            string browserVersion = driver.ExecuteScript("return navigator.userAgent").ToString();

            return browserVersion;
        }

        static bool ChromeIsHeadless(ChromeDriver? driver)
        {
            if (driver == null) throw new ArgumentException("Driver is null!");

            driver.Navigate().GoToUrl("about:blank");

            //See https://antoinevastel.com/bot%20detection/2018/01/17/detect-chrome-headless-v2.html#:%7E:text=In%20order%20to%20automate%20Chrome,possible%20to%20detect%20Chrome%20headless
            bool chromeIsHeadless = driver.ExecuteScript("return navigator.webdriver").ToString().Equals("True", StringComparison.InvariantCultureIgnoreCase);

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
