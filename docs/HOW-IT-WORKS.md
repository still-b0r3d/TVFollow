# How TVFollow works

## Hearing the monitors

TVFollow keeps a hidden window that Windows tells about three things:

- **the screens turning off, dimming and coming back on** (the same signal that switches
  your monitors off after the screen timeout),
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
| Screens dimmed, then back within 30 seconds | Nothing (the screens never went off) |
| Screens still dimmed after 30 seconds | Same as screens off (a screen saver can keep Windows' dim going for the whole screen timeout, and the screens may be dark by then) |
| Screens back from that long dim | Same as screens on |
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

## The developer tools

Shown only once **Turn on the developer tools** is ticked on Settings' Developer tools page;
nothing below runs while it isn't.

- **Developer mode** is read from `query/device-info`'s `developer-enabled`. TVFollow can't
  switch it on: the TV ignored the remote code (Home ×3, Up ×2, Right, Left, Right, Left,
  Right) when TVFollow sent it as ECP key presses, so the page gives the steps for the
  TV's own remote and checks every few seconds while a developer tools page is open.
- **The installer** is the TV's developer web page on port 80, user `rokudev`, behind the
  password chosen on the TV. TVFollow answers its HTTP digest challenge only, never Basic,
  so the password itself never crosses the network. Before anything goes there, TVFollow
  checks over ECP that the device at that address has your TV's serial number and is in
  developer mode, so another Roku that got your TV's address isn't asked. (A serial number
  isn't a secret, so this doesn't stop someone set on faking your TV; a longer password
  makes a guess harder.) **Check** asks for `plugin_install` with HEAD: 200 means the
  password is right, 401 that it's wrong.
- **Pushing** posts the app to `plugin_install` the way the page's own form does
  (`mysubmit=Replace`, the zip as `archive`); if the TV refuses that for a reason other
  than the app's code, TVFollow tries once more with `mysubmit=Install`, as Roku's own
  tools do. TVFollow makes the zip itself: the manifest at the top, forward slashes, the
  files in name order with their own times, junk left out, so the same files always make
  the same zip. Only an answer of 200 counts. The page says how it went in its script
  (`Roku.Message` calls on older Roku OS, `JSON.parse` on newer); when the TV refuses an
  app, its own words go on the App on the TV page (control characters dropped, 300 characters at
  most). "Identical to previous version" means the TV kept the app it had, so TVFollow
  opens it with `launch/dev`. An answer of 577 (or "Failed to check for software update")
  means the TV wants to look for a Roku update first. The app on the TV is read from
  `query/apps` (its id is `dev`).
- **Remove** is `plugin_install` with `mysubmit=Delete`; **Screenshot** is
  `plugin_inspect` with `mysubmit=Screenshot`, then the picture the page links
  (`pkgs/dev.jpg` or `.png`).
- **Putting an app back:** each successful push keeps a copy in `dev\<serial>.zip` in the
  settings folder. When the monitors go off and TVFollow is about to turn off a TV that's
  showing the PC, it first checks the TV still lists its `dev` app. If not, and developer
  mode is on, the copy goes back on, then the TV turns off. A button's **TV off**, sleep
  and shutdown never do this, and a TV on Limited, which hides its list of apps, is
  skipped. If developer mode went off, or the copy couldn't go back (a new password, say),
  a notification says so, once each time. **Remove** deletes the copy.
- **Pushing on save** watches the folder pushed last, sub-folders too, and pushes 1.5
  seconds after the last change. Changes to skipped files (`.git`, editors' temporary
  files) don't count, and a zip that's the same as the one the TV took last isn't sent.
  Only folders on this PC's own drives are watched, not network paths (`\\server\...`).
- **The app log** is the TV's app console on port 8085, plain text over TCP (telnet
  commands are dropped, and keep-alive notices a TV that went away). It needs no password,
  and the window reconnects while it's open. It shows what the app prints while the window
  is open, so open it before pushing or opening the app. TVFollow keeps one connection to
  it for each TV, shared with the app's asks for inputs (below); a window opened while
  that's connected starts with the recent text (the last 32,000 characters at most).
  Everything read also goes into `dev\<serial> app log.txt` in the settings folder, with a
  line marking each new connection (the TV repeats its recent lines to every new reader,
  so some lines come twice). Past 2 MB the file keeps its newer half. Other programs can
  read it while TVFollow writes.
- **Opening the app from the home screen:** with **Open the app whenever the TV goes to
  Roku's home screen** ticked, the same looks as for inputs (below: every 3 seconds, only
  on the TV's network) notice the home screen: an app of type `home` in
  `query/active-app` (Roku OS 15 calls it "Roku Dynamic Menu"), or one with no id on older
  TVs. Then TVFollow checks the TV is on (`power-mode` `PowerOn`, so a TV in standby isn't
  woken), looks again 1.2 seconds later (a moment's home screen between two apps doesn't
  count), checks `query/apps` still lists a `dev` app, and sends `launch/dev`. Back on the
  home screen within a minute of that, with nothing else in between, means someone pressed
  Home again: the TV stays home until it shows something else or turns off. That also
  stops an app that crashes as it starts from being opened over and over. Nothing happens
  while following is paused or while TVFollow is turning that TV on or off.
- **Letting the app switch inputs:** a Roku TV answers 403 when an app sends its own
  remote-control service a key press or a launch (at `127.0.0.1` or the TV's own address),
  and its app manager won't open `tvinput.*`, so an app can't switch the TV's input itself.
  With **Let the app switch the TV's inputs** ticked, TVFollow asks `query/active-app`
  every 3 seconds, but only while this PC is on the TV's network, and every 15 seconds
  once the TV has missed three looks in a row. While the pushed app (`dev`) is on screen,
  it checks the TV's serial number once, then reads the app's console (port 8085, through
  the same connection as the **App log…** window, since the TV allows one reader). Once the
  connection has lasted half a second, TVFollow sends the hello, after checking `dev` is
  still on screen: `POST /input` with `tvfollow=hello`, `protocol=1`, `token` (8 random
  characters, new with each connection), `pc`, `inputs`, then `input1`, `name1` and so on,
  the inputs read from `query/apps`. A hello that doesn't get through goes again at the next
  look. A console line `TVFollow hello` gets another hello. A line
  `TVFollow ask <token> <input>` gets `launch/<input>`, but only with this connection's
  token (the TV repeats its recent lines to a new reader, so old asks come round again)
  and only for an input `query/apps` lists; one worker carries them out, the latest ask and
  one hello at a time. An ask that isn't carried out goes in the log, as one line of at
  most 60 characters (the same one once in a row). After a switch, or three missed looks,
  TVFollow lets go of the console until the app is back on screen. A console whose first
  line is "Console connection is already in use" (another program reads it) gets no
  hello, and the log says so once. Unticking the option, switching the developer tools off
  or quitting stops all of it at once, even in the middle of a look. Nothing on the PC
  listens: TVFollow connects to the TV, as it does for everything else. (Reading the app's
  registry with `query/registry/dev` would have been simpler, but a TV only answers that
  once it has a developer signing key; otherwise it says "Device not keyed".)
- **Zips and folders** are read with limits: 200 MB and 20,000 files and folders in all,
  and a folder at most 32 levels deep, so a link that loops back up stops quickly. Paths
  in a zip that point outside it (`..`, `C:`, a leading `/`) are left out.

## Files and settings

| What | Where |
|---|---|
| The program | `%LOCALAPPDATA%\Programs\TVFollow\TVFollow.exe` (plus its icons) |
| Settings | `%APPDATA%\TVFollow\settings.ini` |
| Log | `%APPDATA%\TVFollow\TVFollow.log` (kept under 256 KB, one older copy) |
| Update check | `%APPDATA%\TVFollow\updates.ini` (when it last asked, the newest version seen) |
| Pushed apps' copies | `%APPDATA%\TVFollow\dev\` (developer tools only: the app pushed last to each TV, and the app log read from it) |
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
| `developer_tools` | Only there while the developer tools are switched on (`developer_tools=yes`) |
| `dev_password` | The developer mode password, encrypted for your Windows account (`dpapi:` and Windows' own encryption). Copied to another PC or account, it can't be read, and the password box is empty again |
| `dev_folder` | The app's folder pushed last (empty after pushing a zip or the example) |
| `dev_push_back`, `dev_on_save`, `dev_inputs`, `dev_home` | The developer tools' four options (`dev_push_back=no`, `dev_on_save=yes`, `dev_inputs=yes` and `dev_home=yes` are only written when set that way) |

**Several TVs:** the first TV uses the keys above, the way TVFollow 1.1 wrote them, so an
older TVFollow reading the file still finds that one. Each other TV has the same six keys
(and its own `dev_` keys) with its number in front, listed after the rest:

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
