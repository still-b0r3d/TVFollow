# Troubleshooting

Most answers start with the log: right-click the TV icon in the taskbar corner →
**Open log**. Each line says what TVFollow saw or did, with the time. A few lines
explained:

| Log line | Means |
|---|---|
| `monitors off` / `monitors on (last input 0.4s ago)` | Windows switched the screens off or on |
| `off: TV is showing 12, leaving it on` | The TV was showing something other than your PC, so TVFollow left it alone |
| `waiting for the mouse or keyboard before turning the TV on` | The screens came on with nobody there, so it waits for you |
| `power on (was DisplayOff) -> 200` | It pressed the TV's power button, and the TV said OK |
| `-> 403` | The TV refused: its Network access setting is on Limited (see below) |
| `TV not answering, sent a wake packet (3 addresses)` | The TV was asleep too deeply to answer, so TVFollow sent the network wake signal |
| `no network to send a wake packet on` | The PC's own network wasn't up yet (just after sleep, say) |
| `this PC isn't on the TV's network` | A laptop away from home, say: TVFollow leaves the network alone. At home, right after a router swap: turn the TV on once with its remote and TVFollow learns the new router |
| `TV found at 192.168.1.73` | The router gave the TV a new address, and TVFollow found it again |

## Setup can't find my TV

- Make sure the TV is on, not just in standby, and on the same network as the PC. A
  PC on a **guest network** or a **VPN** can't see the TV.
- Some mesh routers and Wi-Fi extenders keep devices from seeing each other ("client
  isolation" or "AP isolation"). Check the router's app.
- Type the address in: click **Enter the TV's address instead**. The TV shows its
  address under **Settings → Network → About**. TVFollow remembers it, so reopening
  Settings finds the TV again.
- If the TV's **Network access** is set to **Disabled**, it doesn't answer at all. On
  the TV: **Settings → System → Advanced system settings → Control by mobile apps →
  Network access → Enabled**.
- A Roku **stick or box** shows up greyed out. It can't turn a TV on or off, so
  TVFollow can't use it.

## Setup says Network access is on Limited

On the TV, with its remote: **Home → Settings → System → Advanced system settings →
Control by mobile apps → Network access → Enabled**. Leave the setup window open; it
notices the change by itself within a few seconds.

## The TV turns off but doesn't come back on

The TV probably switches its network off along with the screen.

1. On the TV: **Settings → System → Power → Fast TV start**, and turn it on.
2. Run the test again: right-click the TV icon → **Settings…** → go to **Test it**.

TVFollow also sends a network wake signal when the TV doesn't answer, again every few
seconds for 20 seconds, which wakes many TVs even from deep standby. If the log shows
`sent a wake packet` and then `TV not found`, Fast TV start is the fix.

## The TV comes on but shows the Roku home screen instead of my PC

Open **Settings…** and check the input on the **Which input is this PC on?** page.
**Show it on the TV** confirms it. TVFollow keeps pressing the input button for about
20 seconds while the TV wakes up; if your TV takes longer, the log says
`gave up waiting for the PC input`.

## The TV doesn't turn off when my monitors do

- TVFollow only turns the TV off while it's **showing your PC**. If it's on another
  input or app, the log says `leaving it on`.
- Check the option **Turn the TV off when this PC's monitors turn off** under
  **Settings…** → **All set**.
- It follows *Windows'* screen timeout (**Settings → System → Power**; on Windows 10,
  **Settings → System → Power & sleep**), not the monitors' own power buttons. Turning a
  monitor off by its button doesn't tell Windows anything.
- Is it paused? The tray icon is grey while paused, and stays paused across restarts
  until you choose **Resume following**.

## The TV turned off while I was watching something on the PC

That happens when Windows switched the screens off, which it does after the screen
timeout with no mouse or keyboard input. Video players normally keep the screens on.
Use **Pause following** while you watch, or lengthen the screen timeout in
**Settings → System → Power** (Windows 10: **Power & sleep**).

## All my screens flicker when the TV turns on

That's Windows noticing a screen came back and laying out the desktop again. It's
normal for any monitor that powers on, and TVFollow can't stop it.

## Windows or my antivirus warns about TVFollow

TVFollow isn't code-signed, so Windows SmartScreen shows **"Windows protected your PC"**
the first time. Click **More info → Run anyway**. Some antivirus tools are wary of small
unsigned programs that look around the local network. TVFollow does that during setup,
to find the TV, and afterwards only if the TV goes missing: then it looks on the TV's own
network (never on other networks a laptop joins), at most once every ten minutes. If
you'd rather check it yourself, [build it from the source](../README.md#build-it-yourself).
The SHA-256 checksum next to each release download is for checking that download.

## The remote's buttons do nothing

The remote's status line says why. **"Network access is on Limited"**: on the TV,
**Settings → System → Advanced system settings → Control by mobile apps → Network
access → Enabled** (Limited still lets the input buttons work, but blocks the others).
**"The TV isn't answering"**: it's off with Fast TV start turned off, or not on this
network; **TV to PC** wakes it if it can.

## The portable choice is greyed out

The note under it says why. Portable needs a folder of your own that TVFollow can write
to. It isn't offered when TVFollow runs from its install folder (that's the installed
copy), or from a temporary folder, which is where Windows puts an exe opened straight from
a zip: unzip it first. It also isn't offered in a folder TVFollow can't write to, such as
Program Files.

## Two PCs, one TV

Both can run TVFollow. Each one only turns the TV off while it's showing *that* PC's
input, so they don't fight over it.

## Start over

Right-click the TV icon → **Settings…** and go through setup again, or uninstall
(**Settings → Apps → Installed apps → TVFollow**; on Windows 10, **Settings → Apps →
Apps & features**) and run TVFollow.exe again.

## Still stuck?

[Open an issue](https://github.com/still-b0r3d/TVFollow/issues/new/choose) and include
your TV's brand and model, its Roku OS version (**Settings → System → About**), your
Windows version and the last few lines of the log.
