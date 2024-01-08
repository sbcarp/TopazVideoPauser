# Topaz Video Pauser

Enables one-click pausing and resuming of Topaz Video tasks, also features functionality to schedule OS shutdown or sleep once the tasks are completed.

## Screenshots
![App Introduction](app_intro.png?raw=true "App Introduction")

## How to install?
1. Download the app file from [release page](https://github.com/sbcarp/TopazVideoPauser/releases/)
2. Unzip the file to a folder
3. Run `TapazVideoPauser.exe` inside the folder

## How to use?
1. Once the app is running, it will show up in system tray
2. Double click the tray icon to pause or resume Topaz Video tasks, note: it will not work if there's no running Topaz Video tasks
3. Right click the tray icon for more options

## Find a problem?
Welcome to file bug report in [issue page](https://github.com/sbcarp/TopazVideoPauser/issues)

## FAQ
### Why it doesn't shutdown my computer immediately after tasks are completed?
After tasks are compeleted, there is a 60 seconds delay before it shuts down. This delay is designed to avoid unexpected shutdowns in cases where additional tasks might be pending after the completion of the initial task. Moreover, this delay provides users with an opportunity to cancel the shutdown if needed.

## Limitations
1. This is a Windows only app, and there's no plan for macOS support
2. It doesn't have fine control for individual task or window, if you have multiple Topaz Video Ai windows opened, it will pause all Topaz Video Ai windows.
3. The feature `Shutdown or sleep when tasks finished` doesn't work well for very short tasks (the tasks can be completed within few seconds)
