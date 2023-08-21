# SeleniumAndChromeBrowser

Example project that shows how to attach to a current running Chrome browser (or start a new one) or run headless and then open a new tab and load a website.
This also works when there are running multiple Chrome instances on different remote debugging ports.

Important to know is that Chrome uses a profile located at some directory which contains all settings and user preferences.  
Chrome can also listen on a specific port so you can attach to it from for example C# using Selenium NuGet package and control what is happening in the browser.
Both can be set by starting Chrome using arguments like `--remote-debugging-port` and `--user-data-dir`.
See `startChromeWithRemoteDebugging.bat` for an example.

This project has an `appsettings.json` where you can find all settings for the example.

There are also some helper batch files:
- `checkIfChromeIsListeningOnRemoteDebuggingPort.bat`: checks if Chrome is running and if it's listening on a specific port.
- `startChrome.bat`: start Chrome normally
- `startChromeWithRemoteDebugging.bat`: starts Chrome on a default (or given) remote debugging port by using a specific profile (using the Windows temporary path)

## Know limitations / pending issues

- If your Chrome instance saves the tab session and contains multiple tabs pointing to some websites, the example sometimes fails to attach to Chrome and won't continue opening a tab.
It will fail with an connection timeout. The workaround is to click one or more tabs to force load it before the timeout occurs. 
Another workaround is to not save the tab session in Chrome by switching from `Continue where you left off` to `Open the New Tab page` in `Settings > On startup`.
Or just run in headless mode.