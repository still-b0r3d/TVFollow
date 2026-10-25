# Changelog

## 1.1 (not released yet)

Fixes from a second review of 1.0.

- A reply from "the TV" that tries to send TVFollow somewhere else (a redirect) is ignored.
  TVFollow never follows redirects, so nothing on the network can make the PC request
  another address.
- "The TV's network" now means the network behind your home router, not just the same
  kind of addresses. A laptop on a hotel or office network that happens to use the same
  numbers no longer sends wake packets or looks around for the TV there. TVFollow learns
  the router the first time the TV answers (new `gateway_mac` setting); after a router
  swap, turn the TV on once with its remote and it learns the new one.
- Typing an emoji into the remote's search box no longer trips an error dialog (which could
  end TVFollow). It goes to the TV as one character; whether the TV shows it is up to the TV.
- Text pasted into the remote's search box now reaches the TV too (the first line of it,
  letter by letter). Before, the box showed it but the TV never got it.
- A settings file that can't be read at sign-in (held open by another program, say) no
  longer makes TVFollow silently fail to start. It waits a few seconds, then says what's
  wrong, instead of running with the wrong settings or offering setup as if there were none.
- An `ip=` line in settings.ini that isn't an IPv4 address (a hand edit gone wrong) is
  ignored and logged, so TVFollow asks to be set up again instead of talking to whatever the
  line named.

## 1.0 (2026-10-24)

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
