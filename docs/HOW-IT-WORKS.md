# How TVFollow works

## Hearing the monitors

TVFollow keeps a hidden window that Windows tells about three things:

- **the screens turning off and on** (the same signal that switches your monitors off
  after the screen timeout),
- **the PC going to sleep**,
- **the PC shutting down or restarting**.

When the screens come on, it also asks Windows when the mouse or keyboard was last used.
That's only a time, not what you typed. While it's waiting for you to come back, it asks
about twice a second.

## The rules

| What happened | What TVFollow does |
|---|---|
| Screens off | Turns the TV off, if it's showing your PC |
| Screens on, mouse or keyboard used in the last 5 seconds | Turns the TV on and switches it to your PC, unless it's on and showing something else |
| Screens on, nobody there | Waits for the mouse or keyboard, then as above |
| Sleep or shutdown | Turns the TV off, if it's showing your PC (quickly, without retries) |
| Paused | Nothing |

TVFollow waits a second after the screens change before acting, so a quick flicker
doesn't switch the TV off and on.

## Talking to the TV

TVs with Roku built in answer simple web requests on your home network, on port 8060.
The phone app uses the same thing. Roku calls it the
[External Control Protocol](https://developer.roku.com/docs/developer-program/dev-tools/external-control-api.md).
TVFollow uses only these:

| Request | Used for |
|---|---|
| `GET /query/device-info` | Is it on? Is it the right TV (serial number)? Is control allowed? |
| `GET /query/active-app` | What is it showing? (`tvinput.hdmi1` means HDMI 1) |
| `GET /query/apps` | The HDMI inputs and their names (setup only) |
| `POST /keypress/PowerOff`, `PowerOn` | Turning it off and on |
| `POST /keypress/InputHDMI1` (or `/launch/tvinput.hdmi1`) | Switching to your PC |

To find the TV, setup tries port 8060 on every address in your PC's own network (the
`192.168.1.1` to `.254` around the PC, for example). These are ordinary outgoing
connections, so Windows Firewall doesn't ask about them. If the TV later gets a new
address, TVFollow searches the same way and picks it out by its serial number, so it
never grabs a different Roku in the house.

If the TV doesn't answer when it's time to turn it on, TVFollow sends a
**Wake-on-LAN packet** to the TV's network address (Wi-Fi or cable, whichever the TV
uses) and gives it 20 seconds to wake up.

## Files and settings

| What | Where |
|---|---|
| The program | `%LOCALAPPDATA%\Programs\TVFollow\TVFollow.exe` (plus its icons) |
| Settings | `%APPDATA%\TVFollow\settings.ini` |
| Log | `%APPDATA%\TVFollow\TVFollow.log` (kept under 256 KB, one older copy) |
| Start with Windows | `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, value `TVFollow` |
| Apps list entry | `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\TVFollow` |
| Start menu | `TVFollow` |

`settings.ini` is plain text, and setup writes it:

```ini
ip=192.168.1.50                 # the TV's address (updated by itself if it changes)
serial=X000000000               # the TV's serial number, to tell it apart from other Rokus
input=hdmi1                     # the input your PC is plugged into
wake_mac=00:11:22:33:44:55      # the TV's network card, for the wake signal
off_with_monitors=yes
on_with_monitors=yes
start_with_windows=yes
desktop_shortcuts=no
```

Everything is per user, so TVFollow never needs admin rights. Uninstalling removes all
of it.
