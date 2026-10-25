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
| Screens dimmed, then back to full brightness | Nothing (the screens never went off) |
| Sleep or shutdown | Turns the TV off, if it's showing your PC (quickly, without retries) |
| Paused | Nothing, until you resume (also across restarts) |

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
| `POST /keypress/Up`, `Down`, `Left`, `Right`, `Select`, `Back`, `Home`, `Info`, `InstantReplay`, `Search`, `Rev`, `Play`, `Fwd`, `VolumeUp`, `VolumeDown`, `VolumeMute` | The remote's buttons |
| `POST /keypress/Lit_a` (one per letter), `Backspace`, `Enter` | The remote's typing box |
| `POST /launch/tvinput.hdmi2` (and the other inputs) | The remote's input buttons |

The remote sends its presses one at a time, in order, so typed letters arrive the way
they were typed.

TVFollow treats every reply as untrusted, since anything on the network could answer
on port 8060: it refuses replies over 256 KB, gives every request a hard deadline, never
follows redirects, and never follows references inside a reply (no DTDs, no outside files).

To find the TV, setup tries port 8060 on every address in your PC's own networks (the
`192.168.1.1` to `.254` around the PC, for example; on bigger networks, up to 1,022
addresses). These are ordinary outgoing connections, so Windows Firewall doesn't ask
about them. Setup also tries the address you used last time, so a TV you had to type in
is found again.

If the TV later stops answering and the PC is on the TV's network, TVFollow searches that
network only (never other networks a laptop joins), at most once every ten minutes, and
picks the TV out by its serial number, so it never grabs a different Roku in the house.

If the TV doesn't answer when it's time to turn it on, TVFollow sends a
**Wake-on-LAN packet** for the TV's network card (Wi-Fi or cable, whichever the TV uses;
TVFollow notices if that changes) to the broadcast address of each network the PC is on,
again every 3 seconds, and gives the TV 20 seconds to wake up. If the PC isn't on the
TV's network, it sends nothing.

## Checking for new versions

If **Tell me when there's a new version** is on (it is unless you untick it), TVFollow
asks GitHub once a week for its latest release:

`GET https://api.github.com/repos/still-b0r3d/TVFollow/releases/latest`

It first asks a couple of minutes after starting, then checks every hour whether a week
has passed. If GitHub can't be reached, it tries again six hours later. A newer version
gets one notification and a **Get TVFollow …** item in the tray menu, which opens the
release page on GitHub (it only ever opens TVFollow's own release pages). TVFollow never
downloads or installs an update by itself. When it last checked lives in `updates.ini`;
delete that file to make it check again at the next start.

## Files and settings

| What | Where |
|---|---|
| The program | `%LOCALAPPDATA%\Programs\TVFollow\TVFollow.exe` (plus its icons) |
| Settings | `%APPDATA%\TVFollow\settings.ini` |
| Log | `%APPDATA%\TVFollow\TVFollow.log` (kept under 256 KB, one older copy) |
| Update check | `%APPDATA%\TVFollow\updates.ini` (when it last asked, the newest version seen) |
| Start with Windows | `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, value `TVFollow` |
| Apps list entry | `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\TVFollow` |
| Start menu | `TVFollow` |

`settings.ini` is plain text, and setup writes it:

```ini
ip=192.168.1.50
serial=X000000000
input=hdmi1
wake_mac=00:11:22:33:44:55
off_with_monitors=yes
on_with_monitors=yes
start_with_windows=yes
desktop_shortcuts=no
check_updates=yes
remote_layout=classic
```

| Key | Means |
|---|---|
| `ip` | The TV's address (updated by itself if it changes) |
| `serial` | The TV's serial number, to tell it apart from other Rokus |
| `input` | The input your PC is plugged into |
| `wake_mac` | The TV's network card, for the wake signal (updated by itself) |
| `off_with_monitors`, `on_with_monitors` | The two "follow" options |
| `start_with_windows`, `desktop_shortcuts` | Install options |
| `check_updates` | Ask GitHub once a week whether there's a newer TVFollow |
| `remote_layout` | `classic` (the default), `compact` or `strip` |
| `paused` | Only there while paused from the tray (`paused=yes`) |

Everything is per user, so TVFollow never needs admin rights. Uninstalling removes all
of it.

**Portable copies** keep `settings.ini`, the log and `updates.ini` in a `TVFollow data`
folder next to TVFollow.exe, and write nothing anywhere else: no registry, no Start menu,
no Apps entry. That folder is how TVFollow knows it's portable. Installed or portable,
only one TVFollow follows the monitors at a time: a second one started alongside steps
back.
