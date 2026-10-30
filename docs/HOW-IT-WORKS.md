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

With several TVs, each TV goes by these rules on its own, all at the same time: each is
turned off only while it shows this PC, and a TV that takes a while to wake doesn't hold
up the others. At sleep and shutdown every TV is told at once.

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
| `POST /keypress/Lit_a` (one per letter, typed or pasted), `Backspace`, `Enter` | The remote's typing box |
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
network only, at most once every ten minutes, and picks the TV out by its serial number, so
it never grabs a different Roku in the house. On its own it never searches a network behind
another router; a TV button you press yourself can (see the router note below).

If the TV doesn't answer when it's time to turn it on, TVFollow sends a
**Wake-on-LAN packet** for the TV's network card (Wi-Fi or cable, whichever the TV uses;
TVFollow notices if that changes) to the broadcast address of each network the PC is on,
again every 3 seconds, and gives the TV 20 seconds to wake up. If the PC isn't on the
TV's network, it sends no wake packet: on its own it only asks the TV's last address
whether it's there.

"The TV's network" means the network behind your home router, not just the same kind of
addresses: a laptop that joins a hotel or office network which also uses `192.168.1.x` finds
a different router there, so the automatic following leaves that network alone. TVFollow
remembers the home router's hardware address the first time the TV answers.

**After a router swap**, TVFollow still finds the TV at its old address if the new router
gave it the same one, and learns the new router from that. If the TV got a new address, the
automatic following can't find it and goes quiet. Then press **TV to PC** (or **TV on** /
**TV off**) in the tray, or **TV to PC** on the remote, once. A button you press yourself, on
a network with the TV's old addresses, searches for the TV by its serial number (for up to
20 seconds, each time you press), and once it finds the TV, TVFollow learns the new router and
follows again. It sends no wake packet there, so if your TV drops off the network when it's
off (Fast TV start off), turn it on with its own remote first. Use the tray or the remote,
not a desktop shortcut: a shortcut runs a TVFollow of its own, which doesn't update the copy
in the taskbar corner. If the new router uses other numbers altogether (`192.168.0.x`
instead of `192.168.1.x`, say), the button can't search there; go through
**Settings… → Add or change TVs…** once.

The flip side: pressing one of those buttons on a hotel network that happens to use your
home's addresses makes TVFollow look for your TV there. It only ever acts on a TV with your
TV's serial number.

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

`settings.ini` is plain text. Setup writes it, and TVFollow fills in `gateway_mac` the first
time the TV answers:

```ini
ip=192.168.1.50
serial=X000000000
input=hdmi1
wake_mac=00:11:22:33:44:55
gateway_mac=AA:BB:CC:DD:EE:01
name=Living Room TV
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
| `gateway_mac` | Your home router, so the automatic following leaves another network with the same addresses alone (learned by itself when the TV answers) |
| `name` | What the TV calls itself, for the menus and the log (updated when the Settings window sees a new one) |
| `off_with_monitors`, `on_with_monitors` | The two "follow" options |
| `start_with_windows`, `desktop_shortcuts` | Install options |
| `check_updates` | Ask GitHub once a week whether there's a newer TVFollow |
| `remote_layout` | `classic` (the default), `compact` or `strip` |
| `paused` | Only there while paused from the tray (`paused=yes`) |

**Several TVs:** the first TV uses the keys above, the way TVFollow 1.1 wrote them, so an
older TVFollow reading the file still finds that one. Each other TV has the same six keys
with its number in front, listed after the rest:

```ini
tv2.ip=192.168.1.51
tv2.serial=X000000001
tv2.input=hdmi2
tv2.wake_mac=00:11:22:33:44:66
tv2.gateway_mac=AA:BB:CC:DD:EE:01
tv2.name=Office TV
```

The numbers are the order the Settings window lists the TVs in, and the ones the `on`,
`off` and `pc` commands take.

Everything is per user, so TVFollow never needs admin rights. Uninstalling removes all
of it.

**Portable copies** keep `settings.ini`, the log and `updates.ini` in a `TVFollow data`
folder next to TVFollow.exe, and write nothing anywhere else: no registry, no Start menu,
no Apps entry. That folder is how TVFollow knows it's portable. Installed or portable,
only one TVFollow follows the monitors at a time: a second one started alongside steps
back.
