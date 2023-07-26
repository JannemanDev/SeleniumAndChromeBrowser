using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace SeleniumAndChromeBrowser
{
    //TODO: - Serilog
    //TODO: - Starting a new Chrome instance when already one present (at other port) does not work
    //TODO: - all tabs need to be loaded before opening a new tab by ChromeDriver succeeds?!

    internal class Program
    {
        static void Main(string[] args)
        {
            string pathChrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            int debuggingPort = 1559;
            string argumentsChrome = $"--remote-debugging-port={debuggingPort}";
            string website = "https://www.google.nl"; //include protocol!

            ChromeSearchResult chromeSearchResult = ExistingChromeAvailableListeningOnCorrectPort(debuggingPort);

            if (chromeSearchResult != ChromeSearchResult.FoundAndListeningOnCorrectPort) CloseAnyChromeInstances(); //mandatory!!

            if (chromeSearchResult != ChromeSearchResult.FoundAndListeningOnCorrectPort) OpenChromeBrowser(pathChrome, argumentsChrome);

            Console.WriteLine($"\nStarting Chrome Driver and connect to Chrome browser on 127.0.0.1:{debuggingPort}");
            ChromeDriver? driver = StartChromeDriver(debuggingPort);

            if (driver == null)
            {
                Console.WriteLine("Quiting...");
                return;
            }

            Console.WriteLine("\nOpening new tab");
            //open new tab and navigate to correct website
            driver.SwitchTo().NewWindow(WindowType.Tab);
            Console.WriteLine($"Navigating to {website}");
            driver.Navigate().GoToUrl(website);

            Console.WriteLine("\nPress any key to close the chromedriver and quit...");
            Console.ReadKey(true);

            try
            {
                Console.WriteLine($"Closing ChromeDriver...");
                driver.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while closing ChromeDriver: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"Quitting ChromeDriver...");
                driver.Quit();
            }
        }

        private static ChromeSearchResult ExistingChromeAvailableListeningOnCorrectPort(int portNumber)
        {
            string command = $"netstat -ano";
            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = new Process() { StartInfo = startInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            Console.WriteLine($"Command: {command}");
            Console.WriteLine($"Output:\n{output}");
            process.WaitForExit();
            process.Dispose();

            // Parse the output to get the process IDs from the last column
            string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            bool chromeRunning = false;
            bool foundChromeOnCorrectPort = false;
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
                            foundChromeOnCorrectPort = true;
                            Console.WriteLine($"Chrome process found LISTENING on port {portNumber}: Process ID: {pid}\n");
                            return ChromeSearchResult.FoundAndListeningOnCorrectPort;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving process name for PID {pid}: {ex.Message}\n");
                }
            }

            if (chromeRunning)
            {
                Console.WriteLine($"Chrome process was found, but it was not LISTENING on port {portNumber}\n");
                return ChromeSearchResult.Found;
            }
            else Console.WriteLine($"Chrome process was NOT found!\n");

            return ChromeSearchResult.NotFound;
        }


        private static ChromeDriver? StartChromeDriver(int debuggingPort)
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            //chromeOptions.AddArgument("--headless");
            //chromeOptions.AddArgument("--disable-gpu");
            //chromeOptions.AddArgument("--no-sandbox");
            chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}";

            //be sure your regular Chrome browser version (see Help > About) matches this chromedriver package version
            //timeout lowered for easier/faster testing if chromedriver is connected to just created Chrome browser instance

            ChromeDriver driver = null;
            try
            {
                //driver = new ChromeDriver(chromeOptions);
                driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), chromeOptions, TimeSpan.FromSeconds(60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error attaching ChromeDriver: {ex.Message}");
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

            Console.Write("Waiting for Chrome to start");
            process.WaitForInputIdle();
            Console.WriteLine($"\nChrome started with arguments: \"{pathChrome}\" {argumentsChrome}");

            //Console.WriteLine("Press any key to continue...");
            //Console.ReadKey(true);
        }

        static bool CloseAnyChromeInstances()
        {
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
                Console.WriteLine($"{processes.Count()} Chrome processes found:\n{json}");

                Console.WriteLine("\nPress any key to close the processes or <ESC> to skip...");
                if (Console.ReadKey(true).Key == ConsoleKey.Escape) return false;

                Console.WriteLine("\nClosing processes...");
                processes.ToList().ForEach(p => p.CloseMainWindow());
                Console.WriteLine("Waiting for processes to exit...");
                processes.ToList().ForEach(p => p.WaitForExit());
                Console.WriteLine("Processes all exited...");
                return true;
            }
            else
            {
                Console.WriteLine($"No processes found!");
                return true;
            }
        }
    }
}