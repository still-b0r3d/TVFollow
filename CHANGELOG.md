# Changelog

## 1.3 (not released yet)

A new Settings window, and developer tools for putting your own Roku apps on the TV.

- **The Settings window has a page for each part**, picked from a list down its left side,
  like Windows' own Settings: TV, Following, Remote, This PC, Updates and Developer tools.
  It keeps one size; a page with more than fits scrolls. **Tab** goes from the list into
  the open page, then to the footer.
- In Windows' high contrast themes, the Settings window's page list and the grey footers
  of the Settings and setup windows take the theme's colours. Before, the footers' version
  line was in the theme's text colour on TVFollow's own light grey: white on near-white in
  the dark themes.
- **Settings… → Developer tools → Turn on the developer tools** (off to start with) adds
  two pages, **App on the TV** and **App options**, and **Developer tools…** to the tray
  menu (right under **Settings…**), which opens Settings on them. There's no separate
  developer tools window.
- The Developer tools page says whether the TV's developer mode is on and, while it's
  off, gives the steps to turn it on with the TV's own remote. The TV ignores the code
  when TVFollow sends it, so TVFollow's remote can't do it. The page keeps checking and
  says when it's on.
- The password chosen on the TV is typed in once and kept encrypted for your Windows
  account (`dev_password=` in settings.ini). **Check** tries it on the TV.
- **Push a .zip…**, **Push a folder…** (TVFollow zips it, leaving out `.git`, `Thumbs.db`
  and editors' temporary files) and **Push the example app**. An app one folder down is
  found. When the TV refuses an app, its own words say why.
- The example app is b0r3d TV 0.71 (Build: 87e560a), b0r3d's channel guide for Roku TVs
  that looks like an old cable box, with a VCR, a camera on Rukus's room, three games, a
  radio station and late-night commercials. With **Let the app switch the TV's inputs**
  ticked, the TV's inputs are channels in its guide. Its source is in
  `examples/tvfollow-example`, under its own license. Once it's on the TV, it uses b0r3d's
  online high-score boards and streams b0r3d FM, and its VCR plays videos sent to it; the
  README's Privacy section says what goes where.
- **Open on the TV**, **Remove**, **Screenshot…** (saves a picture of the app's screen)
  and **App log…** (the app's console: what it prints, and where it stopped if it
  crashed). Whatever TVFollow reads of the app log also goes into
  `%APPDATA%\TVFollow\dev\<serial> app log.txt`, the newest 2 MB or so.
- **Open the app whenever the TV goes to Roku's home screen** (off to start with): the app
  becomes the TV's home screen while this PC is on. Turning the TV on or backing out of
  another app lands in it. Switching from an app to one of the TV's inputs passes Roku's
  home screen, which doesn't count when TVFollow switched (for the app, or from its remote
  or Settings) or when it's gone within 4 seconds, so the TV stays on the input. Pressing
  **Home** twice in a row stays on Roku's home screen. Not while following is paused, and
  never on a TV that's off.
- `TVFollow.exe push <folder or .zip>` and `TVFollow.exe screenshot <picture or folder>`,
  with the developer tools on: **Push a folder…** and **Screenshot…** for test scripts,
  with the saved password, which never leaves TVFollow. They print how it went and end
  with 0, 1 or 2. Nothing they have to say comes in a message box, so a script never waits
  on a click. A picture named `.png` or `.jpg` is saved as that kind, whichever of the two
  the TV sent.
- **Put the app back when it disappears from the TV** (on to start with): TVFollow keeps
  a copy of the app pushed last and puts it back, if the TV lost it, just before it turns
  the TV off with your monitors. If developer mode itself went off, or the app couldn't
  go back, a notification says so, once each time. The copy moves with the settings when
  you switch between installed and portable.
- **Push the app's folder again whenever a file in it is saved**, for making apps. When
  the TV can't take it (it's off, say), a notification says so once, not at every save.
  It works on the folder pushed last, until a .zip or the example goes onto the TV instead;
  one that didn't get there changes nothing.
- **Let the app switch the TV's inputs** (off to start with): a Roku TV won't let an app
  switch its inputs, so the pushed app can ask TVFollow, which switches the TV while this
  PC is on. The README says how an app asks.
- **Settings…** in the tray menu, double-clicking the icon and opening TVFollow again all
  bring a minimized Settings window back up. Before, it stayed down in the taskbar.
- A TV woken from deep standby is switched to your PC even when it comes up on Roku's home
  screen or on another input. Before, TVFollow took it for a TV that was on already and
  left it there. A TV that was on all along, and only slow to answer, is still left alone.
- The version and the build are shown apart: `TVFollow v1.3 · Build: abc1234` at the bottom
  of the setup and Settings windows (on two lines there now), in the log and in
  `TVFollow.exe --version`. The exe's **Product version** says `1.3 build abc1234`.

## 1.2.1 (2026-10-31)

A fix for the TV staying off when you come back.

- Coming back to the PC while Windows still had the screens dimmed left the TV off. With a
  screen saver on, Windows can keep the screens dimmed for the whole screen timeout (half an
  hour with a 30-minute timeout), and on some PCs they're dark all that time. A dim that lasts
  30 seconds now counts as the screens going off, so the TV turns off, and coming back from it
  counts as them coming on, so the TV turns on with the mouse. A shorter dim still changes
  nothing. The log says when the screens dim.
- The version in the setup and Settings windows, the log and `TVFollow.exe --version`
  carries the commit the copy was built from, like `1.2.1+abc1234`, and so does the exe's
  **Product version** (right-click TVFollow.exe → Properties → Details). GitHub's build
  fills it in; a copy built with build.cmd says `1.2.1+dev`.
- TVFollow.exe.sha256 ends its line the way `sha256sum -c` expects, so
  `sha256sum -c TVFollow.exe.sha256` can check a download.

## 1.2 (2026-10-30)

More than one TV.

- TVFollow can follow the monitors with several TVs, each with its own input. Setup's
  first step lists the TVs on the network with tick boxes: tick every TV the PC is plugged
  into, and each new one goes through the next steps (control, input, test) in turn.
- TVs already set up keep their settings when setup opens again, so adding a second TV
  later is quick: **Settings… → Add or change TVs…**, tick it, and only the new TV goes
  through the steps. Untick a TV to stop using it. A TV set up before that doesn't answer
  at the time stays listed and keeps its settings. To check a TV already set up again
  (its control setting, input or the test), tick **Go through the next steps again for
  TVs set up already**.
- Updating from a new download is quicker for the same reason: the TVs already set up
  stay as they are, so it's step 1, Next, then Finish.
- Each TV follows on its own and at the same time: a TV that's slow to wake doesn't hold
  up the others, and sleep and shutdown tell every TV at once.
- With two or more TVs the tray menu has **All TVs on / off / to PC** and a menu for each
  TV with its own buttons and remote. The remote gets a drop-down at the top to pick the
  TV it works, the Settings window shows each TV with its own input, and the log says
  which TV each line is about.
- The `on`, `off` and `pc` commands (and the desktop shortcuts) do every TV. Add a TV's
  number from the Settings window, or its name, to do one: `TVFollow.exe on 2`.
- settings.ini keeps the first TV in the keys 1.1 uses; the others are `tv2.ip=`,
  `tv2.input=` and so on. A new `name=` keeps each TV's name for the menus.
- The setup and Settings windows say "made by b0r3d.org" again, linking to b0r3d.org.
- A TV's name with an & in it shows as it is in the menus and windows.

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
