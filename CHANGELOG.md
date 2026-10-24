# Changelog

## 1.0 (not released yet)

The first public release.

- Turns a TV with Roku built in off when the PC's monitors turn off, and back on (and
  over to the PC's input) when you come back. Only touches the TV while it's showing
  the PC.
- Waits for the mouse or keyboard before turning the TV on, so screens waking with
  nobody there don't switch it on.
- Turns the TV off on sleep and shutdown.
- Wakes a TV that's gone into deep standby with a network wake signal, and finds the TV
  again if the router gives it a new address.
- A setup window: finds the TVs on the network, walks through the TV's Network access
  setting, picks the HDMI input by the TV's own names, and tests turning the TV off and
  on.
- Installs per user without admin. Start menu entry, optional start with Windows,
  optional TV on / off / to PC desktop shortcuts, and uninstall from Settings → Apps.
- Or runs portable: settings in a `TVFollow data` folder next to the exe, nothing else on
  the PC touched.
- A tray icon with the remote, TV buttons, Pause following (remembered across restarts),
  Settings, the log and Quit.
- A remote on screen: power, TV to PC, Back, Home, arrows and OK, Options, instant
  replay, Search, playback, volume and mute, your inputs by name, the keyboard as a
  remote, and a box that types into the TV's search. Three layouts: classic (tall),
  compact and wide strip.
- A Settings window: the TV and its input, the follow options and Pause, the remote's
  layout, start with Windows and shortcuts, the update check. Changes apply straight
  away; opening TVFollow again while it runs brings it up.
- Opening a newer TVFollow.exe shows setup, and finishing installs it over the old one.
- Careful on the network: replies from "the TV" are treated as untrusted (no DTDs, size
  and time limits), and a TV that went missing is only searched for on its own network,
  at most every ten minutes.
- Tells you when a new version is out: once a week it asks GitHub for the latest release
  (can be switched off in setup). It never downloads or installs anything by itself.
- Licensed MIT with the Commons Clause: free to use, change and share, not to sell.
- Commands for scripts and hotkeys: `on`, `off`, `pc`, `setup`, `uninstall`, `--version`.
