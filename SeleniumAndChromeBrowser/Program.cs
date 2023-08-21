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
            config = InitConfiguration(executingDir);
            logger = InitLogging(config);

            logger.Information("");
            logger.Information("----------------------------------------------------------------------------------------------------------");
            logger.Information($"Starting, executing directory is \"{executingDir}\"");

            int debuggingPort = config.GetRequiredSection("ChromeDriver").GetValue<int>("DebuggingPort");
            bool runHeadless = config.GetRequiredSection("ChromeDriver").GetValue<bool>("RunHeadless");

            if (!runHeadless)
            {
                ChromeSearchResult chromeSearchResult = ExistingChromeAvailableListeningOnCorrectPort(debuggingPort);

                if (chromeSearchResult != ChromeSearchResult.FoundAndListeningOnCorrectPort)
                {
                    ChromeInstancesNotListeningOnSpecifiedDebuggingPort chromeInstancesNotListeningOnSpecifiedDebuggingPort = config.GetRequiredSection("ChromeDriver").GetValue<ChromeInstancesNotListeningOnSpecifiedDebuggingPort>("ChromeInstancesNotListeningOnSpecifiedDebuggingPort");
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

            ChromeDriver? driver = StartChromeDriver(debuggingPort, runHeadless);

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

        static IConfigurationRoot InitConfiguration(string loadFromPath)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(loadFromPath)
                .AddJsonFile("appsettings.json", optional: false)
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
            string command = $"netstat -ano -p TCP";
            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = new Process() { StartInfo = startInfo };
            logger.Information($"Command run to find all processes using a TCP port: {command}");
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
                if (columns.Length != 5) continue;

                string protoColumn = columns[0];
                string localAddressColumn = columns[1];
                string foreignAddressColumn = columns[2];
                string stateColumn = columns[3];
                int pid = int.Parse(columns[4]);

                try
                {
                    string processName = Process.GetProcessById(pid).ProcessName;

                    if (processName.Equals("chrome", StringComparison.InvariantCultureIgnoreCase))
                    {
                        chromeRunning = true;

                        if (localAddressColumn.EndsWith($":{portNumber}") && stateColumn.Equals("LISTENING", StringComparison.InvariantCultureIgnoreCase))
                        {
                            logger.Information($"A Chrome process was found LISTENING on port {portNumber}: Process ID: {pid}");
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
                logger.Information($"Chrome process(es) were found, but *none* was LISTENING on port {portNumber}");
                return ChromeSearchResult.Found;
            }
            else logger.Information($"No Chrome process was NOT found!");

            return ChromeSearchResult.NotFound;
        }

        private static ChromeDriver? StartChromeDriver(int debuggingPort, bool headLess = false)
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            if (headLess)
            {
                chromeOptions.AddArgument("--headless=new");
                logger.Information($"Starting Chrome Driver in headless mode, ignoring debuggingPort");
            }
            else chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}";

            /*
                PageLoadStrategy:
                -normal: This is the default behavior where WebDriver waits for the full page to load, 
                         including sub-resources such as images, scripts, and stylesheets.
                -eager: WebDriver waits for the DOMContentLoaded event, which signifies that the initial HTML document 
                        has been completely loaded and parsed.
                -none: WebDriver does not wait for the page to load at all. This can be useful for scenarios where 
                       you want to take control of waiting for elements explicitly using explicit waits.
            */
            chromeOptions.PageLoadStrategy = PageLoadStrategy.Normal;

            //be sure your regular Chrome browser version (see Help > About) matches this chromedriver package version
            //timeout lowered for easier/faster testing if chromedriver is connected to just created Chrome browser instance

            ChromeDriver? driver = null;
            try
            {
                logger.Information($"Starting Chrome Driver and connect to Chrome browser on 127.0.0.1:{debuggingPort}\n");
                driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), chromeOptions, TimeSpan.FromSeconds(20));
            }
            catch (Exception ex)
            {
                logger.Information($"Error attaching ChromeDriver: {ex.Message}");
            }

            return driver;
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
    }
}
