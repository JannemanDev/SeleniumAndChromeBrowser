# Using Selenium and Chrome in C#

This is a C# example project that shows how to attach to a current running Chrome instance (or start a new one) with GUI/browser or without GUI (headless) and then open a new tab and load a website.
It also works when there are multiple Chrome instances running (or not) on different remote debugging ports. This can be GUI or headless instances.

This project has an `appsettings.json` where you can find all settings for the example.
It also writes all output to a logfile. Logging is done by `Serilog` and can be controlled by changing the settings.

Important to know is that Chrome uses a __profile__ located at some directory which contains all Chrome settings and user preferences.  
Chrome can also listen on a specific __port__ so you can attach to it from for example C# using the Selenium NuGet package and control what is happening in the browser.
This way you can automate webtasks and/or test if webpages are loaded correctly and are functioning as expected.  
Profile location and port can be set by starting Chrome using arguments like `--remote-debugging-port` and `--user-data-dir`.
See `startChromeWithRemoteDebugging.bat` for an example how to use.  

The example first checks if there is a Chrome instance running and listening on the specified `DebuggingPort`, if not a new instance on that port will be started which automatically reuse the correct profile when using the same port as before.  
If it's the first time a new profile will be created by Chrome. If this is the case Chrome may ask some _one-time_ questions. The answers will be persisted in the profile.  
If there are any Chrome processes running on different port than the `DebuggingPort` from the settings you can optionally have them automatically closed.
You can control this with `ChromeInstancesNotListeningOnSpecifiedDebuggingPort`:
- AutoClose
- DontClose
- Ask

If there is already a Chrome instance running on the specified `DebuggingPort` and it is in a different mode (GUI or headless) than specified in settings by `RunHeadless` you can control what to do with this instance by setting `ChromeInstancesNotListeningOnSpecifiedDebuggingPort`:
- AutoCloseAndRetry
- DontClose
- Exit

If you want to run headless you can specify extra arguments by using `ExtraHeadlessArguments`. For example:
```json
	"ExtraHeadlessArguments": [ "no-sandbox", "disable-gpu" ]
```

If you want to use a specific version of the `chromedriver` executable you can specify (only!) the path _containing_ it.
Which browser is used is specified by `ChromePath`. Setting both can help when specific versions are needed. It's important that both version match!!
Luckily ChromeDriver will report if there is a mismatch.

The profile is either located under the application direction or found in the Windows temp path. This can be controlled by using `UseTempPathForProfile`.  
In this example project the profile location is dependent on the `DebuggingPort`, so it supports running multiple Chrome instances on different ports.  

There are also some helper batch files for Windows:
- `checkIfChromeIsListeningOnRemoteDebuggingPort.bat`: checks if Chrome is running and if it's listening on a specific port.
- `startChrome.bat`: start Chrome normally
- `startChromeWithRemoteDebugging.bat`: starts Chrome on a default (or given) remote debugging port by using a specific profile (using the Windows temporary path)
- `killTaskUsingPort.bat`: kills a task which is LISTENING on a given port using a TCP connection. This script is also used by the example to kill a specific Chrome instance.

Be sure your regular Chrome browser version (see Help > About) matches the ChromeDriver version.

## Building yourself

There is a build script `buildAll.bat` which builds the source to several platforms (Windows x86/x64, Linux arm/arm64/x64 and OS x64).
If you are owner of this repository you can use the GitHub helper batch files to also publish these builds (it supports uploading files >25MB).

## Know limitations / pending issues

- Example is cross platform with the only limitation that on non-Windows killing a Chrome instance on a specific port using TCP is not available.
- If your Chrome instance saves the tab session and contains multiple tabs pointing to some websites, the example sometimes fails to attach to Chrome and won't continue opening a tab.
It will fail with an connection timeout. Some workarounds:
  - before the connection timeout occurs click one or more of the tabs to force load it.
  - do not save the tab session in Chrome by switching from `Continue where you left off` to `Open the New Tab page` in `Settings > On startup`.
  - run in headless mode. See `appsettings.json`
  - use a dedicated Chrome profile which is only used for a specific purpose (like this example project or testing a website)
    