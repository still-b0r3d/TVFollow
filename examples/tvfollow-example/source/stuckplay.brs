' Stuck Pixels on the TV, channel 20: its menu, the run, the pause menu, the end of a run,
' initials for the high scores, the tables and how to play. The rules are in stuck.brs and the
' play screen in stuckdraw.brs; every other screen here is a page in the shared layout
' (panel.brs), so they all come in every look.
'
' Kept on the TV (registry): sp.mode, sp.level and sp.rows (the menu's picks), sp.initials
' (the last initials typed) and sp.best.<mode> (each mode's top 10).

' The games' channels and b0r3d's other own channels, at their own numbers, lowest first (the
' apps' numbers skip 3, 9, 20, 21, 22, 23 and 24).
function GameChannels() as object
    vcr = MakeChannel("video.vcr", "VCR", "VCR", "The VCR on channel 3, made by b0r3d", "vcr")
    vcr.n = "03"
    cam = MakeChannel("cam.rukus", "Rukus Cam", "RUKUS", "Rukus's room, live, made by b0r3d", "cam")
    cam.n = "09"
    stuck = MakeChannel("game.stuck", "Stuck Pixels", "STUCK", "Falling blocks, made by b0r3d", "game")
    stuck.n = "20"
    test = MakeChannel("game.test", "Test Pattern", "TEST", "Brick breaker, made by b0r3d", "game")
    test.n = "21"
    fm = MakeChannel("radio.fm", "b0r3d FM", "B0R3D", "Radio stream, made by b0r3d", "radio")
    fm.n = "22"
    snake = MakeChannel("game.snake", "Snake", "SNAKE", "Retro snake arcade, made by b0r3d", "game")
    snake.n = "23"
    promo = MakeChannel("promo.breaks", "Commercial Breaks", "COMM", "Late-night station promos, made by b0r3d", "promo")
    promo.n = "24"
    return [vcr, cam, stuck, test, fm, snake, promo]
end function

' One of them by its id, so nothing depends on their order in the list.
function GameChannel(id as string) as dynamic
    games = GameChannels()
    i = IndexOfId(games, id)
    if i < 0 then return invalid
    return games[i]
end function

function IsGameNumber(n as integer) as boolean
    return n = 3 or n = 9 or n = 20 or n = 21 or n = 22 or n = 23 or n = 24
end function

' What OK does on a channel, for the guides' hints: "OK opens YouTube", "OK starts Stuck Pixels".
function OkWord(ch as object) as string
    if ch.kind = "game" then return "OK starts " + ch.name
    if ch.kind = "radio" then return "OK tunes to " + ch.name
    if ch.kind = "promo" then return "OK watches " + ch.name
    if ch.kind = "vcr" then return "OK turns on the " + ch.name
    if ch.kind = "cam" then return "OK checks on Rukus"
    return "OK opens " + ch.name
end function

' OK on a game or special channel, after tuning in.
sub OpenGame(st as object, id as string)
    if id = "video.vcr" then OpenDeck(st)
    if id = "cam.rukus" then OpenCam(st)
    if id = "game.stuck" then OpenStuckPixels(st)
    if id = "game.test" then OpenTestPattern(st)
    if id = "game.snake" then OpenSnake(st)
    if id = "radio.fm" then OpenB0r3dFm(st)
    if id = "promo.breaks" then OpenPromo(st)
end sub

sub OpenStuckPixels(st as object)
    if st.sp = invalid then st.sp = NewStuckPixels()
    sp = st.sp
    sp.view = "menu"
    sp.sel = 0
    sp.g = invalid
    sp.art = invalid
    sp.result = invalid
    st.mode = "stuck"
    Redraw(st)
end sub

' Back to the guide. The run and its pictures are let go.
sub CloseStuckPixels(st as object)
    sp = st.sp
    sp.g = invalid
    sp.art = invalid
    sp.fx = []
    sp.view = "menu"
    st.mode = "guide"
    Redraw(st)
end sub

function NewStuckPixels() as object
    sp = {
        view: "menu",
        sel: 0,
        mode: RegRead("sp.mode", "marathon"),
        level: RegRead("sp.level", "0").ToInt(),
        rowsPick: RegRead("sp.rows", "2").ToInt(),
        initials: CleanInitials(RegRead("sp.initials", "AAA")),
        letters: ["A", "A", "A"],
        g: invalid,
        art: invalid,
        stackDirty: true,
        fx: [],
        second: CreateObject("roTimespan"),
        clock: CreateObject("roTimespan"),
        lastMs: 0,
        signal: CreateObject("roTimespan"),
        signalHow: "",
        fps: { frames: 0, shown: -1, timer: CreateObject("roTimespan") },
        result: invalid,
        scoresMode: "marathon",
        scoresOnline: false,
        scoresFrom: "menu",
        howtoFrom: "menu",
        sounds: LoadStuckSounds()
    }
    if not InList(SpModes(), sp.mode) then sp.mode = "marathon"
    if sp.level < 0 or sp.level > 9 then sp.level = 0
    if sp.rowsPick < 0 or sp.rowsPick > SpStaticChoices().Count() - 1 then sp.rowsPick = 2
    return sp
end function

' --- The sounds: short effects made by tools/make-sounds.js, played when Sound is on. ---

function LoadStuckSounds() as object
    sounds = {}
    for each name in ["turn", "hold", "lock", "clear", "big", "fix", "level", "over", "done"]
        sound = invalid
        try
            sound = CreateObject("roAudioResource", "pkg:/sounds/sp-" + name + ".wav")
        catch e
            sound = invalid
        end try
        if sound <> invalid then sounds[name] = sound
    end for
    return sounds
end function

sub PlayStuckSound(st as object, name as string)
    if not st.sound then return
    sound = st.sp.sounds[name]
    if sound <> invalid then sound.Trigger(60)
end sub

' --- Time ---

' How long Main's loop waits: what's left of a frame (about 60 a second) while a run is on
' screen, else a second (less in the RGB look, whose light flows: RgbMenuWaitMs).
function StuckWaitMs(st as object) as integer
    sp = st.sp
    if sp = invalid then return 1000
    if sp.view <> "play" and sp.view <> "signal" then return RgbMenuWaitMs(st, 1000)
    left = 16 - (sp.clock.TotalMilliseconds() - sp.lastMs)
    if left < 1 then return 1
    return left
end function

' Whether a run is being played (or is just ending), when Rukus must not come on.
function StuckBusy(st as object) as boolean
    if st.sp = invalid then return false
    return st.sp.view = "play" or st.sp.view = "signal"
end function

' Main's loop calls this on every turn in the game: a frame when one is due, and once a second
' the clock and the screensaver's chores (Rukus comes on only while the game is paused or on a
' menu).
sub StuckTurn(st as object)
    sp = st.sp
    if sp = invalid then return
    if sp.second.TotalMilliseconds() >= 1000 then
        sp.second.Mark()
        st.clock = ClockNow()
        if SaverChores(st) then return
        if not StuckBusy(st) then Redraw(st)
    end if
    if not StuckBusy(st) then
        RgbMenuTurn(st)
        return
    end if
    ' the run's own clock: each frame takes what's passed since the last one, so no fraction
    ' of a millisecond goes missing (a frame of a slow moment counts as 50 ms at most)
    now = sp.clock.TotalMilliseconds()
    ms = now - sp.lastMs
    if ms < 15 then return
    sp.lastMs = now
    if ms > 50 then ms = 50
    if sp.view = "play" then
        SpTick(sp.g, ms)
        StuckEvents(st)
    else if sp.signal.TotalMilliseconds() >= 2600 then
        FinishSignal(st)
        return
    end if
    DrawStuck(st)
end sub

' What the rules just did: sounds, flashes and pops, and the end of the run. One sound a frame,
' the one that matters most.
sub StuckEvents(st as object)
    sp = st.sp
    g = sp.g
    if g.events.Count() = 0 then return
    best = ""
    bestRank = 0
    for each e in g.events
        kind = e.kind
        name = ""
        rank = 0
        if kind = "turn" then
            name = "turn"
            rank = 1
        else if kind = "hold" then
            name = "hold"
            rank = 2
            sp.stackDirty = true
        else if kind = "lock" then
            name = "lock"
            rank = 3
            sp.stackDirty = true
        else if kind = "clear" then
            name = "clear"
            rank = 5
            if e.data.Count() >= 3 then
                name = "big"
                rank = 6
            end if
            sp.fx.Push({ kind: "flash", rows: e.data, t: CreateObject("roTimespan") })
            sp.stackDirty = true
        else if kind = "fix" then
            name = "fix"
            rank = 7
            sp.fx.Push({ kind: "pop", cells: e.data, t: CreateObject("roTimespan") })
            sp.stackDirty = true
        else if kind = "level" then
            name = "level"
            rank = 8
            sp.fx.Push({ kind: "banner", text: "LEVEL " + TwoDigits(e.data), t: CreateObject("roTimespan") })
        else if kind = "over" or kind = "done" then
            name = kind
            rank = 9
            sp.view = "signal"
            sp.signalHow = kind
            sp.signal.Mark()
            sp.stackDirty = true
        end if
        if rank > bestRank then
            bestRank = rank
            best = name
        end if
    end for
    g.events.Clear()
    if bestRank > 0 then PlayStuckSound(st, best)
end sub

' --- A run ---

sub StartStuckRun(st as object)
    sp = st.sp
    RegWrite("sp.mode", sp.mode)
    RegWrite("sp.level", sp.level.ToStr())
    RegWrite("sp.rows", sp.rowsPick.ToStr())
    sp.g = SpNew(sp.mode, sp.level, SpStaticChoices()[sp.rowsPick], GameSeed())
    sp.g.events.Clear()
    sp.bestText = StuckBestShort(sp.mode)
    sp.fx = []
    sp.result = invalid
    sp.stackDirty = true
    ' the chrome shows the mode, so it's drawn again for each run
    sp.art = invalid
    sp.view = "play"
    sp.clock.Mark()
    sp.lastMs = 0
    RequestRunToken(st, BoardKeyFor("stuck", sp.mode))
    Redraw(st)
end sub

sub PauseStuck(st as object)
    sp = st.sp
    SpLetGo(sp.g)
    sp.view = "pause"
    sp.sel = 0
    Redraw(st)
end sub

' Back to the run; the time it was paused doesn't count.
sub ResumeStuck(st as object)
    sp = st.sp
    sp.view = "play"
    sp.lastMs = sp.clock.TotalMilliseconds()
    Redraw(st)
end sub

' The run's over: its result, and its place on the table when it makes it. Marathon always
' counts; Sprint 40 and Cleanup count only when finished. Sprint 40 ranks by time, the others
' by score.
sub FinishSignal(st as object)
    sp = st.sp
    g = sp.g
    r = { how: g.state, mode: g.mode, score: g.score, lines: g.lines, level: g.level, time: g.elapsed, fixed: g.fixed, place: -1, value: g.score, saved: false }
    if g.mode = "sprint" then r.value = g.elapsed
    ranked = (g.mode = "marathon" or g.state = "done")
    if ranked then r.place = ScoreRank(LoadStuckScores(g.mode), r.value, g.mode = "sprint")
    sp.result = r
    sp.fx = []
    sp.sel = 0
    if r.place >= 0 then
        sp.letters = [Mid(sp.initials, 1, 1), Mid(sp.initials, 2, 1), Mid(sp.initials, 3, 1)]
        sp.view = "initials"
    else
        sp.view = "results"
    end if
    Redraw(st)
end sub

sub SaveStuckInitials(st as object)
    sp = st.sp
    r = sp.result
    initials = sp.letters[0] + sp.letters[1] + sp.letters[2]
    list = LoadStuckScores(r.mode)
    entry = { i: initials, v: r.value, s: r.score, n: r.lines, l: r.level, t: r.time, f: r.fixed }
    r.place = ScoreInsert(list, entry, r.mode = "sprint")
    r.saved = (r.place >= 0)
    SaveStuckScores(r.mode, list)
    SubmitOnlineScore(st, BoardKeyFor("stuck", r.mode), initials, r.score)
    sp.initials = initials
    RegWrite("sp.initials", initials)
    sp.view = "results"
    sp.sel = 0
end sub

function LoadStuckScores(mode as string) as object
    return CleanScoreTable(ParseJson(RegRead("sp.best." + mode, "[]")))
end function

sub SaveStuckScores(mode as string, list as object)
    RegWrite("sp.best." + mode, FormatJson(list))
end sub

' Settings' "High scores" line: how many the games keep.
function GameScoresWord() as string
    n = TpScoresCount() + SnakeScoresCount()
    for each mode in SpModes()
        n = n + LoadStuckScores(mode).Count()
    end for
    if n = 0 then return "None saved"
    if n = 1 then return "1 saved"
    return n.ToStr() + " saved"
end function

sub ClearStuckScores()
    for each mode in SpModes()
        SaveStuckScores(mode, [])
    end for
end sub

' --- The remote ---

' A key in the game: presses and, while a run is played, the releases too (sliding and soft
' dropping last while a key is held).
sub StuckKey(st as object, code as integer)
    sp = st.sp
    v = sp.view
    if v = "play" then
        StuckPlayKey(st, code)
        return
    end if
    if code >= 100 then return
    if v = "signal" then
        ' OK or Back skips the end of the run's static after a moment
        if (code = 6 or code = 0) and sp.signal.TotalMilliseconds() > 700 then FinishSignal(st)
        return
    end if
    if v = "menu" then
        StuckMenuKey(st, code)
    else if v = "pause" then
        StuckPauseKey(st, code)
    else if v = "results" then
        StuckResultsKey(st, code)
    else if v = "initials" then
        StuckInitialsKey(st, code)
    else if v = "scores" then
        board = BoardKeyFor("stuck", sp.scoresMode)
        if code = 4 or code = 5 then
            delta = 1
            if code = 4 then delta = -1
            sp.scoresMode = NextIn(SpModes(), sp.scoresMode, delta)
            board = BoardKeyFor("stuck", sp.scoresMode)
            if board = "" then sp.scoresOnline = false
            FetchOnlineScores(st, board)
        else if (code = 2 or code = 3) and board <> "" then
            ' up and down: the TV's own table or the online one, for a mode that has one
            sp.scoresOnline = not sp.scoresOnline
        else if code = 0 or code = 6 then
            sp.view = sp.scoresFrom
        end if
    else if v = "howto" then
        if code = 0 or code = 6 then sp.view = sp.howtoFrom
    end if
    if st.mode = "stuck" then Redraw(st)
end sub

' Left and right move (held, they slide), down drops faster while held, up drops it all the
' way, OK and fast-forward turn right, rewind turns left, Replay holds, and Play, Back or the
' options key pause.
sub StuckPlayKey(st as object, code as integer)
    g = st.sp.g
    if code = 4 then
        SpPress(g, "left")
    else if code = 104 then
        SpRelease(g, "left")
    else if code = 5 then
        SpPress(g, "right")
    else if code = 105 then
        SpRelease(g, "right")
    else if code = 3 then
        SpPress(g, "down")
    else if code = 103 then
        SpRelease(g, "down")
    else if code = 2 then
        SpPress(g, "drop")
    else if code = 6 or code = 9 then
        SpPress(g, "cw")
    else if code = 8 then
        SpPress(g, "ccw")
    else if code = 7 then
        SpPress(g, "hold")
    else if code = 13 or code = 0 or code = 10 then
        PauseStuck(st)
    end if
end sub

' Up and down move between the rows that do something; the rows that only show a number are
' passed over.
function PanelStep(rows as object, sel as integer, delta as integer) as integer
    i = sel
    while true
        i = i + delta
        if i < 0 or i >= rows.Count() then return sel
        if rows[i].kind <> "info" then return i
    end while
    return sel
end function

sub StuckMenuKey(st as object, code as integer)
    sp = st.sp
    rows = StuckMenuRows(st)
    if sp.sel > rows.Count() - 1 then sp.sel = 0
    id = rows[sp.sel].id
    if code = 0 then
        CloseStuckPixels(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sp.sel = PanelStep(rows, sp.sel, delta)
    else if code = 4 or code = 5 then
        delta = 1
        if code = 4 then delta = -1
        if id = "mode" then
            sp.mode = NextIn(SpModes(), sp.mode, delta)
        else if id = "level" then
            sp.level = (sp.level + delta + 10) mod 10
        else if id = "rows" then
            n = SpStaticChoices().Count()
            sp.rowsPick = (sp.rowsPick + delta + n) mod n
        end if
    else if code = 6 then
        if id = "play" then
            StartStuckRun(st)
        else if id = "scores" then
            ShowStuckScores(st, sp.mode, "menu")
        else if id = "howto" then
            sp.howtoFrom = "menu"
            sp.view = "howto"
        else if id = "guide" then
            CloseStuckPixels(st)
        end if
    end if
end sub

sub StuckPauseKey(st as object, code as integer)
    sp = st.sp
    rows = StuckPauseRows(st)
    if sp.sel > rows.Count() - 1 then sp.sel = 0
    id = rows[sp.sel].id
    if code = 0 or code = 13 then
        ResumeStuck(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sp.sel = PanelStep(rows, sp.sel, delta)
    else if code = 6 then
        if id = "resume" then
            ResumeStuck(st)
        else if id = "restart" then
            if StuckScoreRanks(sp.g) then
                FinishSignal(st)
            else
                StartStuckRun(st)
            end if
        else if id = "howto" then
            sp.howtoFrom = "pause"
            sp.view = "howto"
        else if id = "guide" then
            if StuckScoreRanks(sp.g) then
                FinishSignal(st)
            else
                CloseStuckPixels(st)
            end if
        end if
    end if
end sub

' Whether leaving a run from the pause menu would put it on its table, by FinishSignal's rule:
' Marathon always counts, Sprint 40 and Cleanup only once finished.
function StuckScoreRanks(g as object) as boolean
    if g = invalid or g.score <= 0 then return false
    if g.mode <> "marathon" and g.state <> "done" then return false
    val = g.score
    if g.mode = "sprint" then val = g.elapsed
    return ScoreRank(LoadStuckScores(g.mode), val, g.mode = "sprint") >= 0
end function

sub StuckResultsKey(st as object, code as integer)
    sp = st.sp
    rows = StuckResultsRows(st)
    if sp.sel > rows.Count() - 1 then sp.sel = 0
    id = rows[sp.sel].id
    if code = 0 then
        sp.view = "menu"
        sp.sel = 0
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sp.sel = PanelStep(rows, sp.sel, delta)
    else if code = 6 then
        if id = "again" then
            StartStuckRun(st)
        else if id = "scores" then
            ShowStuckScores(st, sp.result.mode, "results")
        else if id = "menu" then
            sp.view = "menu"
            sp.sel = 0
        else if id = "guide" then
            CloseStuckPixels(st)
        end if
    end if
end sub

' Three letters: left and right change the highlighted one, OK goes on to the next (and on Save
' keeps them), Back keeps them too, so a high score is never lost.
sub StuckInitialsKey(st as object, code as integer)
    sp = st.sp
    rows = StuckInitialsRows(st)
    if sp.sel > rows.Count() - 1 then sp.sel = 0
    id = rows[sp.sel].id
    if code = 0 then
        SaveStuckInitials(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sp.sel = PanelStep(rows, sp.sel, delta)
    else if code = 4 or code = 5 then
        delta = 1
        if code = 4 then delta = -1
        if Left(id, 1) = "l" then
            i = id.Mid(1).ToInt()
            sp.letters[i] = NextLetter(sp.letters[i], delta)
        end if
    else if code = 6 then
        if id = "save" then
            SaveStuckInitials(st)
        else
            sp.sel = PanelStep(rows, sp.sel, 1)
        end if
    end if
end sub

' --- The pages ---

sub DrawStuck(st as object)
    sp = st.sp
    v = sp.view
    if v = "play" or v = "signal" then
        DrawStuckPlay(st)
    else if v = "menu" then
        DrawPanel(st, { title: "Stuck Pixels", rows: StuckMenuRows(st), sel: sp.sel, info: false, hint: "Left and right change it · OK picks · Back goes to the guide" })
    else if v = "pause" then
        DrawPanel(st, { title: "Paused", rows: StuckPauseRows(st), sel: sp.sel, info: false, hint: "OK picks · Play or Back carries on" })
    else if v = "results" then
        DrawPanel(st, { title: StuckResultTitle(sp.result), rows: StuckResultsRows(st), sel: sp.sel, info: false, hint: "OK picks · Back goes to the menu" })
    else if v = "initials" then
        DrawPanel(st, { title: "New high score", rows: StuckInitialsRows(st), sel: sp.sel, info: false, hint: "Left and right pick a letter · OK goes on · Back saves" })
    else if v = "scores" then
        title = "High scores: " + StuckModeTitle(sp.scoresMode)
        hint = "Left and right switch the mode · Back goes back"
        if BoardKeyFor("stuck", sp.scoresMode) <> "" then
            if sp.scoresOnline then title = "Online board: " + StuckModeTitle(sp.scoresMode)
            hint = "Left and right switch the mode · Up and down: this TV or online · Back goes back"
        end if
        DrawPanel(st, { title: title, rows: StuckScoreRows(st), sel: -1, info: true, hint: hint })
    else if v = "howto" then
        DrawPanel(st, { title: "How to play", rows: StuckHowToRows(), sel: -1, info: true, hint: "Back goes back" })
    end if
end sub

function StuckModeTitle(mode as string) as string
    if mode = "sprint" then return "Sprint 40"
    if mode = "cleanup" then return "Cleanup"
    return "Marathon"
end function

function StuckGoalWord(mode as string) as string
    if mode = "sprint" then return "Clear 40 lines as fast as you can"
    if mode = "cleanup" then return "Clear 25 lines through the static"
    return "Faster every 10 lines, until the signal goes"
end function

' "3: a row every 0.76 s"
function StuckLevelWord(level as integer) as string
    ms = SpDropMs(level)
    if ms = 1000 then return level.ToStr() + ": a row a second"
    hundredths = ms \ 10
    text = (hundredths \ 100).ToStr() + "." + TwoDigits(hundredths mod 100)
    return level.ToStr() + ": a row every " + text + " s"
end function

' The best on the play screen: "ABC 12,345", "ABC 1:23.4", or dashes before there is one.
function StuckBestShort(mode as string) as string
    list = LoadStuckScores(mode)
    if list.Count() = 0 then return "---"
    e = list[0]
    if mode = "sprint" then return e.i + " " + TimeWord(e.v)
    return e.i + " " + GroupDigits(e.v)
end function

function StuckRowsWord(pick as integer) as string
    n = SpStaticChoices()[pick]
    if n = 0 then return "None"
    return n.ToStr() + " rows"
end function

' The mode's best: "ABC, 12,345", or for Sprint 40 the time.
function StuckBestWord(mode as string) as string
    list = LoadStuckScores(mode)
    if list.Count() = 0 then return "None yet"
    e = list[0]
    if mode = "sprint" then return e.i + ", " + TimeWord(e.v)
    return e.i + ", " + GroupDigits(e.v)
end function

function StuckMenuRows(st as object) as object
    sp = st.sp
    play = StuckModeTitle(sp.mode) + " from level " + sp.level.ToStr()
    rows = [
        { id: "play", label: "Play", value: play, kind: "open" },
        { id: "mode", label: "Mode", value: StuckModeTitle(sp.mode), kind: "choice" },
        { id: "level", label: "Start level", value: StuckLevelWord(sp.level), kind: "choice" }
    ]
    if sp.mode = "cleanup" then rows.Push({ id: "rows", label: "Rows of static", value: StuckRowsWord(sp.rowsPick), kind: "choice" })
    rows.Push({ id: "goal", label: "Goal", value: StuckGoalWord(sp.mode), kind: "info", dim: true })
    rows.Push({ id: "best", label: "Best", value: StuckBestWord(sp.mode), kind: "info", dim: true })
    rows.Push({ id: "scores", label: "High scores", value: "The top 10 of each mode", kind: "open" })
    rows.Push({ id: "howto", label: "How to play", value: "The keys and the stuck pixels", kind: "open" })
    rows.Push({ id: "guide", label: "Back to the guide", value: "", kind: "open" })
    return rows
end function

function StuckPauseRows(st as object) as object
    g = st.sp.g
    again = StuckModeTitle(g.mode)
    leave = "This run ends"
    ' a run whose score makes the table goes to the initials first (StuckPauseKey)
    if StuckScoreRanks(g) then
        again = "Your score goes on the table first"
        leave = again
    end if
    rows = [
        { id: "resume", label: "Carry on", value: "", kind: "open" },
        { id: "restart", label: "Start again", value: again, kind: "open" },
        { id: "howto", label: "How to play", value: "", kind: "open" },
        { id: "guide", label: "Back to the guide", value: leave, kind: "open" }
    ]
    rows.Append(StuckRunRows(g.mode, g.score, g.lines, g.level, g.elapsed, g.fixed))
    return rows
end function

' A run's numbers, as rows that only show them.
function StuckRunRows(mode as string, score as integer, lines as integer, level as integer, time as integer, fixed as integer) as object
    linesWord = lines.ToStr()
    if mode = "sprint" then linesWord = lines.ToStr() + " of 40"
    if mode = "cleanup" then linesWord = lines.ToStr() + " of 25"
    return [
        { label: "Score", value: GroupDigits(score), kind: "info", dim: true },
        { label: "Lines", value: linesWord, kind: "info", dim: true },
        { label: "Level", value: level.ToStr(), kind: "info", dim: true },
        { label: "Time", value: TimeWord(time), kind: "info", dim: true },
        { label: "Stuck pixels fixed", value: fixed.ToStr(), kind: "info", dim: true }
    ]
end function

function StuckResultTitle(r as object) as string
    if r.how = "done" and r.mode = "sprint" then return "40 lines in " + TimeWord(r.time)
    if r.how = "done" then return "All clear"
    return "No signal"
end function

function StuckResultsRows(st as object) as object
    r = st.sp.result
    table = "The top 10"
    if r.saved then table = PlaceWord(r.place) + " place, as " + st.sp.initials
    rows = [
        { id: "again", label: "Play again", value: StuckModeTitle(r.mode), kind: "open" },
        { id: "scores", label: "High scores", value: table, kind: "open" },
        { id: "menu", label: "Menu", value: "Pick the mode and the level", kind: "open" },
        { id: "guide", label: "Back to the guide", value: "", kind: "open" }
    ]
    rows.Append(StuckRunRows(r.mode, r.score, r.lines, r.level, r.time, r.fixed))
    return rows
end function

function StuckInitialsRows(st as object) as object
    sp = st.sp
    r = sp.result
    l = sp.letters
    shown = GroupDigits(r.score)
    if r.mode = "sprint" then shown = TimeWord(r.time)
    return [
        { id: "l0", label: "First letter", value: l[0], kind: "choice" },
        { id: "l1", label: "Second letter", value: l[1], kind: "choice" },
        { id: "l2", label: "Third letter", value: l[2], kind: "choice" },
        { id: "save", label: "Save", value: l[0] + l[1] + l[2], kind: "open" },
        { label: StuckModeTitle(r.mode), value: shown + ", " + PlaceWord(r.place) + " place", kind: "info", dim: true }
    ]
end function

' The scores page opens on the TV's own table for the mode and asks still.b0r3d.org for the
' mode's online one (Marathon only).
sub ShowStuckScores(st as object, mode as string, from as string)
    sp = st.sp
    sp.scoresMode = mode
    sp.scoresFrom = from
    sp.scoresOnline = false
    sp.view = "scores"
    FetchOnlineScores(st, BoardKeyFor("stuck", mode))
end sub

' A mode's table: place and initials, then the score (Sprint 40: the time) and more. With up
' or down, the mode's online board instead.
function StuckScoreRows(st as object) as object
    sp = st.sp
    mode = sp.scoresMode
    board = BoardKeyFor("stuck", mode)
    if sp.scoresOnline and board <> "" then return OnlineScoreRows(st, board)
    list = LoadStuckScores(mode)
    rows = []
    newPlace = -1
    if sp.result <> invalid then
        if sp.result.saved and sp.result.mode = mode then newPlace = sp.result.place
    end if
    for i = 0 to list.Count() - 1
        e = list[i]
        if mode = "sprint" then
            value = TimeWord(e.v) + ", " + GroupDigits(AsInt(e.s)) + " points"
        else
            value = GroupDigits(e.v) + ", level " + AsInt(e.l).ToStr() + ", " + AsInt(e.n).ToStr() + " lines"
        end if
        if i = newPlace then value = value + " (new)"
        rows.Push({ label: PlaceWord(i) + "  " + e.i, value: value })
    end for
    if rows.Count() = 0 then rows.Push({ label: "None yet", value: "Finish a run to get on the table" })
    return rows
end function

' A saved number, or 0 when it's missing.
function AsInt(v as dynamic) as integer
    if GetInterface(v, "ifInt") <> invalid then return v
    return 0
end function

function StuckHowToRows() as object
    return [
        { label: "Move", value: "Left and right; hold one to slide" },
        { label: "Turn", value: "OK or fast-forward turns right, rewind turns left" },
        { label: "Drop", value: "Down drops faster; up drops it all the way" },
        { label: "Hold", value: "Replay puts the piece aside, or swaps it back, once a piece" },
        { label: "Pause", value: "Play, Back or the options key" },
        { label: "Lines", value: "Fill a row from side to side and it clears" },
        { label: "Stuck pixels", value: "Now and then a piece comes with a stuck pixel that flickers. Its row has to be filled twice: the first time, the stuck pixel pops out for extra points and leaves a hole; fill that, and the row clears." },
        { label: "Modes", value: "Marathon gets faster every 10 lines. Sprint 40 is against the clock. Cleanup starts over rows of static." }
    ]
end function

' --- Settings: clearing the high scores, after asking ---

function ClearScoresRows() as object
    return [
        { id: "keep", label: "Keep them", value: "", kind: "open" },
        { id: "clear", label: "Clear them all", value: "Stuck Pixels, Test Pattern and Snake, every mode", kind: "open" }
    ]
end function

sub DrawClearScores(st as object)
    DrawPanel(st, { title: "Clear the high scores?", rows: ClearScoresRows(), sel: st.confirmSel, info: false, hint: "OK picks · Back keeps them" })
end sub

sub ClearScoresKey(st as object, code as integer)
    rows = ClearScoresRows()
    if code = 0 or code = 10 then
        st.mode = "settings"
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        st.confirmSel = PanelStep(rows, st.confirmSel, delta)
    else if code = 6 then
        if rows[st.confirmSel].id = "clear" then
            ClearStuckScores()
            ClearTpScores()
            ClearSnakeScores()
        end if
        st.mode = "settings"
    end if
    Redraw(st)
end sub
