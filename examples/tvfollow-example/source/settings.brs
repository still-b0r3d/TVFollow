' Settings (the options key) and the screens it opens: the channel list, clearing the high
' scores (stuckplay.brs), the TV check, the Screensaver check and About. Every change is saved
' on the TV at once.

function SettingsRows(st as object) as object
    rows = []
    rows.Push({ id: "look", label: "Look", value: LookTitle(st.look), kind: "choice" })
    names = LookColourNames(st.look)
    if names.Count() > 0 then rows.Push({ id: "colour", label: "Colour", value: names[st.colour], kind: "choice" })
    if st.look = "phosphor" then rows.Push({ id: "scan", label: "Scanlines", value: OnOffWord(st.scan), kind: "choice" })
    ' the games' channels are always there, so only the apps and inputs are counted
    shown = 0
    for each c in st.all
        if c.shown then shown = shown + 1
    end for
    hidden = st.all.Count() - shown
    rows.Push({ id: "channels", label: "Channels", value: shown.ToStr() + " shown, " + hidden.ToStr() + " hidden", kind: "open" })
    clock = "12-hour"
    if st.clock24 then clock = "24-hour"
    rows.Push({ id: "clock", label: "Clock", value: clock, kind: "choice" })
    rows.Push({ id: "sound", label: "Sound", value: OnOffWord(st.sound), kind: "choice" })
    rows.Push({ id: "saver", label: "Screensaver", value: SaverPickWord(st.saverPick), kind: "choice" })
    rows.Push(TvWaitRow(st))
    size = st.w.ToStr() + " x " + st.h.ToStr()
    if st.fhdRefused then size = size + " (the TV said no to 1920 x 1080)"
    rows.Push({ id: "screen", label: "Screen size", value: size, kind: "choice" })
    rows.Push({ id: "scores", label: "High scores", value: GameScoresWord(), kind: "open" })
    rows.Push({ id: "check", label: "TV check", value: "What this TV allows", kind: "open" })
    rows.Push({ id: "savercheck", label: "Screensaver check", value: "What the tries did", kind: "open" })
    rows.Push({ id: "about", label: "About", value: st.version, kind: "open" })
    return rows
end function

function OnOffWord(on as boolean) as string
    if on then return "On"
    return "Off"
end function

sub DrawSettings(st as object)
    DrawPanel(st, { title: "Settings", rows: SettingsRows(st), sel: st.sel, info: false, hint: "Left and right change it · OK opens · Back goes back" })
end sub

' Up and down move, Left and Right change a setting, OK opens a page, Back (or the options key)
' goes back to the guide.
sub SettingsKey(st as object, code as integer)
    rows = SettingsRows(st)
    if code = 0 or code = 10 then
        st.mode = "guide"
    else if code = 2 then
        if st.sel > 0 then st.sel = st.sel - 1
    else if code = 3 then
        if st.sel < rows.Count() - 1 then st.sel = st.sel + 1
    else if code = 4 or code = 5 or code = 6 then
        row = rows[st.sel]
        if row.kind = "choice" then
            delta = 1
            if code = 4 then delta = -1
            ChangeSetting(st, row.id, delta)
        else if code <> 4 then
            OpenSetting(st, row.id)
        end if
    end if
    rows = SettingsRows(st)
    if st.sel > rows.Count() - 1 then st.sel = rows.Count() - 1
    Redraw(st)
end sub

sub ChangeSetting(st as object, id as string, delta as integer)
    if id = "look" then
        list = LookList()
        i = 0
        for j = 0 to list.Count() - 1
            if list[j] = st.look then i = j
        end for
        i = (i + delta + list.Count()) mod list.Count()
        st.look = list[i]
        RegWrite("look", st.look)
        LoadLook(st)
    else if id = "colour" then
        n = LookColourNames(st.look).Count()
        i = (st.colour + delta + n) mod n
        RegWrite("colour." + st.look, i.ToStr())
        LoadLook(st)
    else if id = "scan" then
        st.scan = not st.scan
        RegWrite("scan", OnOff(st.scan))
    else if id = "clock" then
        st.clock24 = not st.clock24
        clock = "12"
        if st.clock24 then clock = "24"
        RegWrite("clock", clock)
    else if id = "sound" then
        st.sound = not st.sound
        RegWrite("sound", OnOff(st.sound))
        if st.sound then PlayTuneSound(st)
    else if id = "saver" then
        st.saverPick = NextIn(SaverPicks(), st.saverPick, delta)
        RegWrite("saver", st.saverPick)
    else if id = "tvwait" then
        st.tvWaitPref = NextIn(TvWaitChoices(), st.tvWaitPref, delta)
        RegWrite("tvwait", st.tvWaitPref.ToStr())
    else if id = "screen" then
        want = "fhd"
        if st.res = "fhd" then want = "hd"
        SetScreenSize(st, want)
    end if
end sub

sub OpenSetting(st as object, id as string)
    if id = "channels" then
        st.mode = "channels"
        st.csel = 0
    else if id = "scores" then
        ' asks first; "Keep them" is highlighted
        st.mode = "clearscores"
        st.confirmSel = 0
    else if id = "check" then
        st.mode = "check"
        PrintAnswers(st)
    else if id = "savercheck" then
        st.mode = "savercheck"
    else if id = "about" then
        st.mode = "about"
    end if
end sub

' The channel list: every app the guide could show, in order. OK shows or hides one, Rewind and
' Fast-forward move it up or down, Back goes back to Settings.
sub DrawChannelList(st as object)
    rows = []
    for each c in st.all
        state = "Shown"
        if not c.shown then state = "Hidden"
        rows.Push({ label: c.n + "   " + c.sign + "   " + c.name, value: state, kind: "toggle", dim: not c.shown })
    end for
    if rows.Count() = 0 then rows.Push({ label: "No apps found on this TV", value: "", kind: "info", dim: true })
    DrawPanel(st, { title: "Channels", rows: rows, sel: st.csel, info: false, hint: "OK shows or hides · Rewind and Fast-forward move it · Back is done" })
end sub

sub ChannelsKey(st as object, code as integer)
    n = st.all.Count()
    ' the list can change under the highlight (TVFollow's hello adds the inputs)
    if st.csel > n - 1 then st.csel = n - 1
    if st.csel < 0 then st.csel = 0
    if code = 0 or code = 10 then
        st.mode = "settings"
    else if code = 2 then
        if st.csel > 0 then st.csel = st.csel - 1
    else if code = 3 then
        if st.csel < n - 1 then st.csel = st.csel + 1
    else if code = 6 and n > 0 then
        st.all[st.csel].shown = not st.all[st.csel].shown
        SaveChannelPrefs(st)
        ApplyChannelPrefs(st)
    else if (code = 8 or code = 9) and n > 1 then
        j = st.csel - 1
        if code = 9 then j = st.csel + 1
        if j >= 0 and j < n then
            moving = st.all[st.csel]
            st.all[st.csel] = st.all[j]
            st.all[j] = moving
            st.csel = j
            SaveChannelPrefs(st)
            ApplyChannelPrefs(st)
        end if
    end if
    Redraw(st)
end sub

sub DrawTvCheck(st as object)
    DrawPanel(st, { title: "TV check", rows: AllAnswers(st), sel: -1, info: true, hint: "Back goes back to Settings" })
end sub

' What the screensaver tries did, redrawn every second (its idle counts tick along).
sub DrawSaverCheck(st as object)
    DrawPanel(st, { title: "Screensaver check", rows: SaverCheckRows(st), sel: -1, info: true, hint: "Back goes back to Settings" })
end sub

sub DrawAbout(st as object)
    rows = [
        { label: "b0r3d TV", value: st.version },
        { label: "Made by", value: "b0r3d.org" },
        { label: "Code", value: "MIT License with the Commons Clause" },
        { label: "Rukus and the art", value: "All rights reserved, with the sound and the writing" },
        { label: "Fonts", value: "Barlow Condensed, VT323, Silkscreen and W95FA, under the SIL Open Font License 1.1" }
    ]
    DrawPanel(st, { title: "About", rows: rows, sel: -1, info: true, hint: "Back goes back to Settings" })
end sub
