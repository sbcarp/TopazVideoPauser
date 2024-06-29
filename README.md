# Topaz Video Pauser

Enables one-click pausing and resuming of Topaz Video tasks, also features functionality to schedule OS shutdown or sleep once the tasks are completed.

- Auto detect tasks running status
- Double click tray icon to pause or resume tasks
- Limit CPU usage for tasks
- Support Auto mode (experimental) to control tasks based on user activity (e.g. pause on full screen, half speed when user is active, full speed on idle)
- Schedule OS shutdown or sleep after tasks are finished

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
### Why pause or resume doesn't work?
For any new encoding task, there a 15s delay before allowing pausing. This is to ensure enough time for model loading, as premature pausing could potentially cause the task to hang indefinitely.
Pause or reumse action is limited to be ran once per 3 seconds, to avoid stability issue, if the icon doesn't change when you double click on it, wait for few seconds and try it again. If that's not the case, feel free to file bug report.

### Try to run it but nothing happens?
It may require administrator privileges to run, try right click on it and select "Run as administrator"

### What is automatic mode?
When automatic mode is enabled, it will automatically controls tasks based on current usage pattern, it dynamically adjusts task performance based on three key states of user activity: Fullscreen, User Activity, and Idle.

### What are Fullscreen, User Activity, and Idle in automatic mode?
Fullscreen: This refers to when an application is running in a mode that occupies the entire screen, without showing the desktop or other windows. Common examples include playing a video game or watching a video in fullscreen mode.

User Activity: This is detected when the user is actively engaging with the computer, such as by moving the mouse or typing on the keyboard. It indicates that the user is currently using the computer.

Idle: This state is recognized when there has been no user activity, like mouse movements or keyboard typing, for a duration of 30 seconds. It suggests that the user is not actively using the computer at that moment.

### Why it doesn't shutdown my computer immediately after tasks are completed?
After tasks are compeleted, there is a 60 seconds delay before it shuts down. This delay is designed to avoid unexpected shutdowns in cases where additional tasks might be pending after the completion of the initial task. Moreover, this delay provides users with an opportunity to cancel the shutdown if needed.

### Why the app is so large?
The app has .net runtime bundled, so you don't need to install it separately. The app itself is lightweight, consumes almost 0% CPU and 50mb memory while running.

## Limitations
1. This is a Windows only app, and there's no plan for macOS support
2. It doesn't have fine control for individual task or window, if you have multiple Topaz Video Ai windows opened, it will pause all Topaz Video Ai windows.
3. The feature `Shutdown or sleep when tasks finished` doesn't work well for very short tasks (the tasks can be completed within few seconds)

## Credits
[Icon by Creative Squad](https://www.freepik.com/icon/play_11081629#fromView=search&term=pause&track=ais&page=1&position=47&uuid=6faf0780-3bf4-40ca-9d00-ee690853b7bc)
[Icon by Creative Squad](https://www.freepik.com/icon/lock_11081621#fromView=resource_detail&position=9)
[Icon by Creative Squad](https://www.freepik.com/icon/lines_11081623#fromView=resource_detail&position=8)
[Icon by Creative Squad](https://www.freepik.com/icon/lock_11081619#fromView=resource_detail&position=10)
[Icon by riajulislam](https://www.freepik.com/icon/shutdown_3541892#fromView=search&term=shutdown&track=ais&page=1&position=69&uuid=dfcdb1dd-2377-493a-80c3-68c4465518f2)
[Icon by Pixel Bazaar](https://www.iconfinder.com/icons/11618327/circle_geometry_dot_stroke_icon)
