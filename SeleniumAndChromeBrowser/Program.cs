using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Text.RegularExpressions;

namespace SeleniumAndChromeBrowser
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string pathChrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            int debuggingPort = 1559;
            string argumentsChrome = $"--remote-debugging-port={debuggingPort}";
            string website = "https://www.google.nl"; //include protocol!

            bool correctChromeAvailable = ExistingChromeAvailableListeningOnCorrectPort(debuggingPort);
            
            bool closed = false;
            if (!correctChromeAvailable) closed = CloseAnyChromeInstances();
            
            if (closed) OpenChromeBrowser(pathChrome, argumentsChrome);

            Console.WriteLine($"Starting Chrome Driver and connect to Chrome browser on 127.0.0.1:{debuggingPort}");
            ChromeDriver driver = StartChromeDriver(debuggingPort);

            Console.WriteLine("\nOpening new tab");
            //open new tab and navigate to correct website
            driver.SwitchTo().NewWindow(WindowType.Tab);
            Console.WriteLine($"Navigating to {website}");
            driver.Navigate().GoToUrl(website);

            Console.WriteLine("\nPress any key to close the chromedriver...");
            Console.ReadKey(true);
            driver.Quit();
        }

        private static bool ExistingChromeAvailableListeningOnCorrectPort(int portNumber)
        {
            try
            {
                string command = $"netstat -ano | grep {portNumber}";
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
                string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    string[] columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length >= 2 && int.TryParse(columns[columns.Length - 1], out int processId))
                    {
                        Process proc = Process.GetProcessById(processId);
                        if (proc.ProcessName.Equals("chrome", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Console.WriteLine($"Chrome process found listening on port {portNumber}: Process ID: {proc.Id}, Process Name: {proc.ProcessName}\n");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving process list: {ex.Message}");
            }

            return false;
        }

        private static ChromeDriver StartChromeDriver(int debuggingPort)
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            //chromeOptions.AddArgument("--headless");
            //chromeOptions.AddArgument("--disable-gpu");
            //chromeOptions.AddArgument("--no-sandbox");
            chromeOptions.DebuggerAddress = $"127.0.0.1:{debuggingPort}";

            //be sure your regular Chrome browser version (see Help > About) matches this chromedriver package version
            //timeout lowered for easier/faster testing if chromedriver is connected to just created Chrome browser instance
            var driver = new ChromeDriver(ChromeDriverService.CreateDefaultService(), chromeOptions, TimeSpan.FromSeconds(20));
            return driver;
        }

        private static void OpenChromeBrowser(string pathChrome, string argumentsChrome)
        {
            var process = new Process();
            process.StartInfo.FileName = pathChrome;
            process.StartInfo.Arguments = argumentsChrome;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            Console.Write("Waiting for Chrome to start");
            while (string.IsNullOrEmpty(process.MainWindowTitle))
            {
                Console.Write(".");
                Thread.Sleep(100);
                process.Refresh();
            }
            Console.WriteLine($"\nChrome started with arguments: \"{pathChrome}\" {argumentsChrome}");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
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
                Console.WriteLine($"{processes.Count()} processes found:\n{json}");

                Console.WriteLine("\nPress any key to close the processes or <ESC> to skip...");
                if (Console.ReadKey(true).Key == ConsoleKey.Escape) return false;

                Console.WriteLine("Closing processes...");
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