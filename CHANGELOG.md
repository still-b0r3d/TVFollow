# Changelog

## 1.1 (2026-10-25)

Fixes from a second review of 1.0.

- A reply from "the TV" that tries to send TVFollow somewhere else (a redirect) is no longer
  followed, so nothing answering in the TV's place can send the PC on to another address.
- "The TV's network" now means the network behind your home router, not just the same
  kind of addresses. A laptop on a hotel or office network that happens to use the same
  numbers no longer sends wake packets or looks around for the TV there on its own. TVFollow
  learns the router the first time the TV answers (new `gateway_mac` setting). After a
  router swap, with the TV on, press TV to PC (or TV on or off) in the tray or on the remote
  once: a button you press yourself looks for the TV by its serial number, even at a new
  address, and TVFollow learns the new router. That also means a button pressed on a hotel
  network that uses your home's addresses looks for your TV there (it only ever acts on your
  TV's serial number). If the new router uses other numbers altogether, go through
  Settings… → Change TV… once.
- Typing an emoji into the remote's search box no longer trips an error dialog (which could
  end TVFollow). It goes to the TV as typed; whether the TV shows it is up to the TV.
- A dot, slash or backslash typed into the remote's search box now reaches the TV as typed
  (before, the dot was lost and both slashes arrived as /). TVFollow.exe now says which
  .NET it's built for, so .NET no longer runs it in its old 4.0 compatibility mode.
- Text pasted into the remote's search box now reaches the TV too (the first line of it,
  letter by letter). Before, the box showed it but the TV never got it.
- A settings file that can't be read at sign-in (held open by another program, say) no
  longer makes TVFollow silently fail to start. It waits a few seconds, then says what's
  wrong, instead of running with the wrong settings or offering setup as if there were none.
- An `ip=` line in settings.ini that isn't an IPv4 address (a hand edit gone wrong) is
  ignored and logged, so TVFollow asks to be set up again instead of talking to whatever the
  line named.
- Nothing a TV says about itself can add lines to settings.ini any more. A reply with a line
  break in it (in its network address, say) could have changed the saved TV address or port.
- The automatic following, the tray's TV buttons and the remote's TV to PC button no longer
  talk to the TV at the same time. One pressed while a sequence is running (waking the TV,
  say) waits for it to finish instead of pressing buttons in between. Sleep and shutdown
  don't wait, and the remote's other buttons work straight away as before.
- The remote no longer risks ending TVFollow when the TV's answer arrives just as the remote
  window closes.
- TV to PC (the button, the shortcut and the `pc` command) presses the input straight away
  when the TV is already on, instead of waiting a second and a half first.
- An error inside one of TVFollow's windows is written to the log, and a short box says
  where the log is, instead of the long .NET error dialog. A crash from a background thread
  leaves a line in the log too.

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
