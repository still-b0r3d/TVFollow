' b0r3d TV: a home screen for Roku TVs that looks like an old cable box.
' Everything is drawn on one 2D screen: 1920 x 1080 where the TV allows it, else 1280 x 720.
' Screens (st.mode): the guide, Settings, the channel list, the TV check, the Screensaver check,
' About and clearing the high scores; "stuck" while Stuck Pixels (channel 20) is open
' (stuckplay.brs), "testpat" while Test Pattern (channel 21) is open (tpplay.brs), "fm" for
' b0r3d FM (22, fm.brs), "snake" for Snake (23, snakeplay.brs), "promo" for Commercial Breaks
' (24, promos.brs); "hijack" while Agent Ø's signal is on (hijack.brs); "saver" while Rukus is
' on, "aside" while b0r3d TV steps aside for the TV's own screensaver (saver.brs).

sub Main(args as dynamic)
    st = {
        port: CreateObject("roMessagePort"),
        mode: "guide",
        note: "",
        noteName: "",
        noteTimer: invalid,
        noteInput: false,
        tvf: invalid,
        tvfAsked: 0,
        tvfTimer: CreateObject("roTimespan"),
        moved: false,
        version: AppVersionLine(),
        sel: 0,
        csel: 0,
        confirmSel: 0,
        guideMs: -1,
        sp: invalid,
        tp: invalid,
        demo: invalid,
        demoTimer: invalid,
        demoClock: invalid,
        tpDemo: invalid,
        tpDemoTimer: invalid,
        snake: invalid,
        snakeDemo: invalid,
        snakeDemoTimer: invalid,
        boardReqs: {},
        boardAsk: {},
        runTokens: {},
        tickMs: 0
    }
    ' TVFollow's hello comes as an input event (see tvfollow.brs). Listening starts first, since
    ' the hello can come while the rest is still starting.
    st.input = CreateObject("roInput")
    st.input.SetMessagePort(st.port)
    LoadPrefs(st)
    SaverProbes(st)
    st.clock = ClockNow()
    st.fontReg = RegisterFonts()
    st.families = FontFamilies(st.fontReg)
    st.built = BuildChannels()
    st.apps = st.built.list
    ApplyChannelPrefs(st)
    st.focus = StartFocus(st.channels)
    st.answers = TvAnswers(st)
    st.tuneSound = LoadTuneSound()
    OpenScreen(st, RegRead("res", "fhd"))
    print "b0r3d TV "; st.version; " started at "; st.w; " x "; st.h; " with "; st.channels.Count(); " channels"
    PrintAnswers(st)
    GreetTvFollow(st)
    Redraw(st)

    while true
        msg = wait(LoopWaitMs(st), st.port)
        if msg = invalid then
            if st.mode <> "saver" and st.mode <> "stuck" and st.mode <> "testpat" and st.mode <> "snake" and st.mode <> "fm" and st.mode <> "promo" and st.mode <> "hijack" then Tick(st)
        else if type(msg) = "roUniversalControlEvent" then
            code = msg.GetInt()
            if code < 100 then
                st.idle.Mark()
                st.handTried = false
                if st.mode = "aside" then
                    ' a key the 2D screen passed on just as b0r3d TV stepped aside
                    ComeBack(st)
                else if st.mode = "saver" then
                    ' a key while Rukus is on only ends Rukus
                    StopSaver(st)
                else if not HandleKey(st, code) then
                    exit while
                end if
            else if st.mode = "stuck" and st.screen <> invalid then
                ' a key let go: the game stops sliding or dropping faster
                StuckKey(st, code)
            else if st.mode = "testpat" and st.screen <> invalid then
                ' a key let go: the paddle stops
                TpKey(st, code)
            else if st.mode = "snake" and st.screen <> invalid then
                SnakeKey(st, code)
            end if
        else if type(msg) = "roInputEvent" then
            if TakeTvFollowHello(st, msg.GetInfo()) then
                AddInputChannels(st)
                Redraw(st)
            end if
        else if type(msg) = "roDeviceInfoEvent" then
            ScreensaverEvent(st, msg.GetInfo())
        else if type(msg) = "roAudioPlayerEvent" then
            if st.fm <> invalid then FmAudioEvent(st, msg)
        else if type(msg) = "roUrlEvent" then
            BoardUrlEvent(st, msg)
        else if type(msg) = "roSGNodeEvent" or type(msg) = "roSGScreenEvent" then
            ' a key on the stand-in screen, or that screen closing: back from stepping aside
            ' (the event lets go of the stand-in first, so nothing holds it open)
            msg = invalid
            ComeBack(st)
        end if
        if st.mode = "saver" then SaverTurn(st)
        if st.mode = "stuck" then StuckTurn(st)
        if st.mode = "testpat" then TpTurn(st)
        if st.mode = "snake" then SnakeTurn(st)
        if st.mode = "fm" then FmTurn(st)
        if st.mode = "promo" then PromoTurn(st)
        if st.mode = "hijack" then HijackTurn(st)
        if st.quit then exit while
    end while
end sub

' One key press. Returns false when b0r3d TV should close.
function HandleKey(st as object, code as integer) as boolean
    ' no 2D screen (stepping aside): nothing to act on
    if st.screen = invalid then return true
    if st.mode = "hijack" then
        HijackKey(st, code)
        return true
    end if
    ' Agent Ø's code counts only keys pressed on the guide
    if st.mode <> "guide" then st.keyBuffer = invalid
    if st.mode = "stuck" then
        StuckKey(st, code)
        return true
    else if st.mode = "testpat" then
        TpKey(st, code)
        return true
    else if st.mode = "snake" then
        SnakeKey(st, code)
        return true
    else if st.mode = "fm" then
        FmKey(st, code)
        return true
    else if st.mode = "promo" then
        PromoKey(st, code)
        return true
    else if st.mode = "clearscores" then
        ClearScoresKey(st, code)
        return true
    else if st.mode = "settings" then
        SettingsKey(st, code)
        return true
    else if st.mode = "channels" then
        ChannelsKey(st, code)
        return true
    else if st.mode = "check" or st.mode = "about" or st.mode = "savercheck" then
        if code = 0 or code = 10 then
            st.mode = "settings"
            Redraw(st)
        end if
        return true
    end if
    ' the guide; the key that completes Agent Ø's code does nothing else (hijack.brs)
    if CheckHijackTrigger(st, code) then return true
    if code = 0 then
        return false
    else if code = 2 then
        MoveFocus(st, -1)
    else if code = 3 then
        MoveFocus(st, 1)
    else if code = 8 then
        MoveFocus(st, -GuideRowsFor(st))
    else if code = 9 then
        MoveFocus(st, GuideRowsFor(st))
    else if code = 7 then
        JumpLast(st)
    else if code = 6 then
        OpenChannel(st)
    else if code = 10 then
        st.mode = "settings"
        st.sel = 0
        Redraw(st)
    end if
    return true
end function

' Up and down move the highlight one row; Rewind and Fast-forward a page. It stops at the ends.
sub MoveFocus(st as object, delta as integer)
    count = st.channels.Count()
    if count = 0 then return
    st.moved = true
    n = st.focus + delta
    if n < 0 then n = 0
    if n > count - 1 then n = count - 1
    if n <> st.focus then
        st.focus = n
        st.note = ""
        st.noteTimer = invalid
        Redraw(st)
    end if
end sub

' LAST (Replay) jumps between the two channels opened most recently, like a cable remote's LAST key.
sub JumpLast(st as object)
    st.moved = true
    last = RegRead("last", "")
    prev = RegRead("prev", "")
    target = last
    if st.channels.Count() > 0 then
        if st.channels[st.focus].id = last then target = prev
    end if
    i = IndexOfId(st.channels, target)
    if i < 0 and target = last then i = IndexOfId(st.channels, prev)
    if i < 0 then
        st.note = "LAST jumps between the last two channels you opened."
    else
        st.focus = i
        st.note = ""
    end if
    st.noteTimer = invalid
    Redraw(st)
end sub

' OK tunes in: a burst of static and the channel banner, then the Roku's app manager opens the
' app, TVFollow switches to the input, or the game starts. The channel is remembered first,
' because b0r3d TV closes as the other app opens.
sub OpenChannel(st as object)
    if st.channels.Count() = 0 then return
    ch = st.channels[st.focus]
    RememberOpened(ch.id)
    TuneIn(st, ch)
    if ch.kind = "game" or ch.kind = "radio" or ch.kind = "promo" then
        st.note = ""
        st.noteTimer = invalid
        OpenGame(st, ch.id)
        return
    end if
    st.noteName = ch.name
    st.noteTimer = CreateObject("roTimespan")
    st.noteInput = (ch.kind = "input")
    if st.noteInput then
        st.note = "Asking TVFollow to switch to " + ch.name + "..."
        Redraw(st)
        AskTvFollow(st, ch)
        return
    end if
    st.note = "Opening " + ch.name + "..."
    Redraw(st)
    print "b0r3d TV: opening "; ch.id
    try
        apps = CreateObject("roAppManager")
        apps.LaunchApp(ch.id, "", {})
    catch e
        print "b0r3d TV: the TV refused "; ch.id; ": "; e.message
        st.note = "The TV refused to open " + ch.name + ": " + e.message
        st.noteTimer = invalid
        Redraw(st)
    end try
end sub

' Once a second (more often while the preview box plays a demo, or the RGB look's lights move):
' the clock moves on, a channel that didn't open says so, Rukus comes on when b0r3d TV has sat
' idle long enough, and the screen is drawn again, since Roku's own screensaver can wipe it while
' b0r3d TV is open.
sub Tick(st as object)
    st.clock = ClockNow()
    ' no hello yet: TVFollow may have connected before b0r3d TV was listening, so ask again
    if st.tvf = invalid then
        askAt = [3000, 10000]
        if st.tvfAsked <= askAt.Count() then
            if st.tvfTimer.TotalMilliseconds() > askAt[st.tvfAsked - 1] then GreetTvFollow(st)
        end if
    end if
    if st.noteTimer <> invalid then
        if st.noteTimer.TotalMilliseconds() > 6000 then
            if st.noteInput then
                st.note = "Nothing yet: TVFollow didn't switch to " + st.noteName + "."
                ' a fresh hello, in case TVFollow connected again and its new token didn't arrive
                GreetTvFollow(st)
            else
                st.note = "Nothing yet: the TV didn't open " + st.noteName + "."
            end if
            st.noteTimer = invalid
        end if
    end if
    if SaverChores(st) then return
    ' how long this takes to draw sets how often the RGB look's lights may move (RgbWaitMs)
    timer = CreateObject("roTimespan")
    Redraw(st)
    st.tickMs = timer.TotalMilliseconds()
end sub

sub Redraw(st as object)
    ' no 2D screen while b0r3d TV steps aside
    if st.screen = invalid then return
    if st.mode = "saver" then
        DrawSaver(st)
    else if st.mode = "hijack" then
        DrawHijack(st)
    else if st.mode = "stuck" then
        DrawStuck(st)
    else if st.mode = "testpat" then
        DrawTestPattern(st)
    else if st.mode = "snake" then
        DrawSnake(st)
    else if st.mode = "fm" then
        DrawFmScreen(st)
    else if st.mode = "promo" then
        DrawPromoScreen(st)
    else if st.mode = "clearscores" then
        DrawClearScores(st)
    else if st.mode = "settings" then
        DrawSettings(st)
    else if st.mode = "channels" then
        DrawChannelList(st)
    else if st.mode = "check" then
        DrawTvCheck(st)
    else if st.mode = "about" then
        DrawAbout(st)
    else if st.mode = "savercheck" then
        DrawSaverCheck(st)
    else
        timer = CreateObject("roTimespan")
        DrawGuideFor(st)
        st.guideMs = timer.TotalMilliseconds()
    end if
end sub

' The highlight starts on the channel opened last, so coming back lands where you left.
function StartFocus(channels as object) as integer
    i = IndexOfId(channels, RegRead("last", ""))
    if i < 0 then return 0
    return i
end function

function IndexOfId(channels as object, id as string) as integer
    if id = "" then return -1
    for i = 0 to channels.Count() - 1
        if channels[i].id = id then return i
    end for
    return -1
end function

sub RememberOpened(id as string)
    last = RegRead("last", "")
    if last = id then return
    if last <> "" then RegWrite("prev", last)
    RegWrite("last", id)
end sub

' The test page's answers, in the app log too.
sub PrintAnswers(st as object)
    for each line in AllAnswers(st)
        print "b0r3d TV check: "; line.label; ": "; line.value
    end for
end sub

' "v<version> · Build: <build>": the version from the manifest, the build from BuildStamp().
function AppVersionLine() as string
    info = CreateObject("roAppInfo")
    return "v" + info.GetValue("major_version") + "." + info.GetValue("minor_version") + " · Build: " + BuildStamp()
end function

' The time and date in the TV's own time zone, in the forms the looks use.
function ClockNow() as object
    dt = CreateObject("roDateTime")
    dt.ToLocalTime()
    hours = dt.GetHours()
    mins = dt.GetMinutes()
    secs = dt.GetSeconds()
    half = "AM"
    if hours >= 12 then half = "PM"
    h12 = hours mod 12
    if h12 = 0 then h12 = 12
    days = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"]
    months = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]
    day = days[dt.GetDayOfWeek()]
    return {
        time: h12.ToStr() + ":" + TwoDigits(mins) + " " + half,
        hm12: h12.ToStr() + ":" + TwoDigits(mins),
        half: half,
        time24: TwoDigits(hours) + ":" + TwoDigits(mins),
        ss: TwoDigits(secs),
        date: day + " " + months[dt.GetMonth() - 1] + " " + dt.GetDayOfMonth().ToStr(),
        isoDate: day + " " + dt.GetYear().ToStr() + "-" + TwoDigits(dt.GetMonth()) + "-" + TwoDigits(dt.GetDayOfMonth()),
        minute: hours * 60 + mins,
        second: secs,
        hours: hours,
        mins: mins
    }
end function
