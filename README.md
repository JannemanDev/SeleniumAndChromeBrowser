# Using Selenium and Chrome in C#

This is a C# example project that shows how to attach to a current running Chrome browser (or start a new one) or run headless and then open a new tab and load a website.
It also works when there are multiple Chrome instances running (or not) on different remote debugging ports.

This project has an `appsettings.json` where you can find all settings for the example.
It also writes all output to a logfile. Logging is done by `Serilog` and can be controlled by changing the settings.

Important to know is that Chrome uses a __profile__ located at some directory which contains all Chrome settings and user preferences.  
Chrome can also listen on a specific __port__ so you can attach to it from for example C# using the Selenium NuGet package and control what is happening in the browser.
This way you can automate webtasks and/or test if webpages are loaded correctly and are functioning as expected.  
Profile location and port can be set by starting Chrome using arguments like `--remote-debugging-port` and `--user-data-dir`.
See `startChromeWithRemoteDebugging.bat` for an example how to use.  

In non-headless mode the example first checks if there is a Chrome running and listening on the specified `DebuggingPort`, if not a new instance on that port will be started which automatically reuse the correct profile.  
If it's the first time a new profile will be created by Chrome. If this is the case Chrome may ask some _one-time_ questions. The answers will be persisted in the profile.  
If there are any Chrome processes not running on the specified port you can optionally have them automatically closed.
You can control this with `ChromeInstancesNotListeningOnSpecifiedDebuggingPort`:
- AutoClose
- DontClose
- Ask

The profile is either located under the application direction or found in the Windows temp path. This can be controlled by using `UseTempPathForProfile`.  
In this example project the profile location is dependent on the `DebuggingPort`, so it supports running multiple Chrome instances on different ports.  

In headless mode it just starts a non-visible Chrome instance (a remote connection is not needed so therefore `DebuggingPort` is ignored/not used).  

There are also some helper batch files:
- `checkIfChromeIsListeningOnRemoteDebuggingPort.bat`: checks if Chrome is running and if it's listening on a specific port.
- `startChrome.bat`: start Chrome normally
- `startChromeWithRemoteDebugging.bat`: starts Chrome on a default (or given) remote debugging port by using a specific profile (using the Windows temporary path)

## Know limitations / pending issues

- If your Chrome instance saves the tab session and contains multiple tabs pointing to some websites, the example sometimes fails to attach to Chrome and won't continue opening a tab.
It will fail with an connection timeout. Some workarounds:
  - before the connection timeout occurs click one or more of the tabs to force load it.
  - do not save the tab session in Chrome by switching from `Continue where you left off` to `Open the New Tab page` in `Settings > On startup`.
  - run in headless mode. See `appsettings.json`
  - use a dedicated Chrome profile which is only used for a specific purpose (like this example project or testing a website)
    