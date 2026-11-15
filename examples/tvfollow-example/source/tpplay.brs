' Test Pattern on the TV, channel 21: its menu, the run, the pause menu, the end of a run,
' initials for the high scores, the tables and how to play. The rules are in testpattern.brs
' and the play screen in tpdraw.brs; every other screen here is a page in the shared layout
' (panel.brs), so they all come in every look.
'
' Kept on the TV (registry): tp.mode and tp.diff (the menu's picks), tp.initials (the last
' initials typed) and tp.best.<mode> (each mode's top 10).

sub OpenTestPattern(st as object)
    if st.tp = invalid then st.tp = NewTestPattern()
    tp = st.tp
    tp.view = "menu"
    tp.sel = 0
    tp.g = invalid
    tp.art = invalid
    tp.result = invalid
    st.mode = "testpat"
    Redraw(st)
end sub

' Back to the guide. The run and its pictures are let go.
sub CloseTestPattern(st as object)
    tp = st.tp
    tp.g = invalid
    tp.art = invalid
    tp.fx = []
    tp.view = "menu"
    st.mode = "guide"
    Redraw(st)
end sub

function NewTestPattern() as object
    tp = {
        view: "menu",
        sel: 0,
        mode: RegRead("tp.mode", "campaign"),
        diff: RegRead("tp.diff", "normal"),
        initials: CleanInitials(RegRead("tp.initials", RegRead("sp.initials", "AAA"))),
        letters: ["A", "A", "A"],
        g: invalid,
        art: invalid,
        fx: [],
        wallDirty: true,
        second: CreateObject("roTimespan"),
        clock: CreateObject("roTimespan"),
        lastMs: 0,
        signal: CreateObject("roTimespan"),
        signalHow: "",
        fps: { frames: 0, timer: CreateObject("roTimespan") },
        result: invalid,
        bestText: "---",
        scoresMode: "campaign",
        scoresOnline: false,
        scoresFrom: "menu",
        howtoFrom: "menu",
        sounds: LoadTpSounds()
    }
    if not InList(TpModes(), tp.mode) then tp.mode = "campaign"
    if not InList(TpDifficulties(), tp.diff) then tp.diff = "normal"
    return tp
end function

' --- The sounds: short effects made by tools/make-sounds.js, played when Sound is on. The end
' of a run sounds like Stuck Pixels'. ---

function LoadTpSounds() as object
    sounds = {}
    names = { paddle: "tp-paddle", brick: "tp-brick", bomb: "tp-bomb", caught: "tp-catch", lost: "tp-lost", clear: "tp-clear", over: "sp-over", won: "sp-done" }
    for each name in names
        sound = invalid
        try
            sound = CreateObject("roAudioResource", "pkg:/sounds/" + names[name] + ".wav")
        catch e
            sound = invalid
        end try
        if sound <> invalid then sounds[name] = sound
    end for
    return sounds
end function

sub PlayTpSound(st as object, name as string)
    if not st.sound then return
    sound = st.tp.sounds[name]
    if sound <> invalid then sound.Trigger(60)
end sub

' --- Time ---

' How long Main's loop waits: what's left of a frame (about 60 a second) while a run is on
' screen, else a second (less in the RGB look, whose light flows: RgbMenuWaitMs).
function TpWaitMs(st as object) as integer
    tp = st.tp
    if tp = invalid then return 1000
    if not TpBusy(st) then return RgbMenuWaitMs(st, 1000)
    left = 16 - (tp.clock.TotalMilliseconds() - tp.lastMs)
    if left < 1 then return 1
    return left
end function

' Whether a run is being played (or is just ending), when Ruckus must not come on.
function TpBusy(st as object) as boolean
    if st.tp = invalid then return false
    return st.tp.view = "play" or st.tp.view = "signal"
end function

' Main's loop calls this on every turn in the game: a frame when one is due, and once a second
' the clock and the screensaver's chores (Ruckus comes on only while the game is paused or on a
' menu).
sub TpTurn(st as object)
    tp = st.tp
    if tp = invalid then return
    if tp.second.TotalMilliseconds() >= 1000 then
        tp.second.Mark()
        st.clock = ClockNow()
        if SaverChores(st) then return
        if not TpBusy(st) then Redraw(st)
    end if
    if not TpBusy(st) then
        RgbMenuTurn(st)
        return
    end if
    ' the run's own clock: each frame takes what's passed since the last one (50 ms at most)
    now = tp.clock.TotalMilliseconds()
    ms = now - tp.lastMs
    if ms < 15 then return
    tp.lastMs = now
    if ms > 50 then ms = 50
    if tp.view = "play" then
        TpTick(tp.g, ms)
        TpPlayEvents(st)
    else if tp.signal.TotalMilliseconds() >= 2600 then
        FinishTpSignal(st)
        return
    end if
    DrawTestPattern(st)
end sub

' What the rules just did: sounds, flashes and blasts, the wall's end and the run's. One sound a
' frame, the one that matters most.
sub TpPlayEvents(st as object)
    tp = st.tp
    g = tp.g
    if g.events.Count() = 0 then return
    best = ""
    bestRank = 0
    for each e in g.events
        kind = e.kind
        name = ""
        rank = 0
        if kind = "paddle" then
            name = "paddle"
            rank = 1
        else if kind = "brick" then
            name = "brick"
            rank = 2
            tp.fx.Push({ kind: "hit", brick: e.data, t: CreateObject("roTimespan") })
        else if kind = "bomb" then
            name = "bomb"
            rank = 4
            tp.fx.Push({ kind: "blast", row: e.data.row, col: e.data.col, t: CreateObject("roTimespan") })
        else if kind = "catch" then
            name = "caught"
            rank = 5
            tp.fx.Push({ kind: "banner", text: TpCapsuleWord(e.data), t: CreateObject("roTimespan") })
        else if kind = "lost" then
            name = "lost"
            rank = 3
        else if kind = "life" then
            name = "lost"
            rank = 6
        else if kind = "clear" then
            name = "clear"
            rank = 7
        else if kind = "over" or kind = "won" then
            name = kind
            rank = 9
            tp.view = "signal"
            tp.signalHow = kind
            tp.signal.Mark()
        end if
        if kind = "next" then tp.wallDirty = true
        if rank > bestRank then
            bestRank = rank
            best = name
        end if
    end for
    g.events.Clear()
    if bestRank > 0 then PlayTpSound(st, best)
end sub

' A caught capsule's name, shown for a moment over the field.
function TpCapsuleWord(kind as string) as string
    if kind = "multi" then return "MORE BALLS"
    if kind = "wide" then return "WIDE PADDLE"
    if kind = "sticky" then return "STICKY PADDLE"
    if kind = "slow" then return "SLOW BALL"
    if kind = "life" then return "+1 LIFE"
    return ""
end function

' --- A run ---

sub StartTpRun(st as object)
    tp = st.tp
    RegWrite("tp.mode", tp.mode)
    RegWrite("tp.diff", tp.diff)
    tp.g = TpNew(tp.mode, tp.diff, GameSeed())
    tp.g.events.Clear()
    tp.bestText = TpBestShort(tp.mode)
    tp.fx = []
    tp.result = invalid
    tp.art = invalid
    tp.wallDirty = true
    tp.view = "play"
    tp.clock.Mark()
    tp.lastMs = 0
    RequestRunToken(st, BoardKeyFor("testpat", tp.mode))
    Redraw(st)
end sub

sub PauseTp(st as object)
    tp = st.tp
    TpLetGo(tp.g)
    tp.view = "pause"
    tp.sel = 0
    Redraw(st)
end sub

' Back to the run; the time it was paused doesn't count.
sub ResumeTp(st as object)
    tp = st.tp
    tp.view = "play"
    tp.lastMs = tp.clock.TotalMilliseconds()
    Redraw(st)
end sub

' The run's over (out of lives, or every campaign wall cleared): its result, and its place on
' its mode's table, which ranks by score.
sub FinishTpSignal(st as object)
    tp = st.tp
    g = tp.g
    r = { how: g.state, mode: g.mode, diff: g.difficulty, score: g.score, wall: g.wall + 1, time: g.elapsed, place: -1, saved: false }
    r.place = ScoreRank(LoadTpScores(g.mode), g.score, false)
    if g.score <= 0 then r.place = -1
    tp.result = r
    tp.fx = []
    tp.sel = 0
    if r.place >= 0 then
        tp.letters = [Mid(tp.initials, 1, 1), Mid(tp.initials, 2, 1), Mid(tp.initials, 3, 1)]
        tp.view = "initials"
    else
        tp.view = "results"
    end if
    Redraw(st)
end sub

sub SaveTpInitials(st as object)
    tp = st.tp
    r = tp.result
    initials = tp.letters[0] + tp.letters[1] + tp.letters[2]
    list = LoadTpScores(r.mode)
    entry = { i: initials, v: r.score, w: r.wall, d: r.diff, t: r.time }
    r.place = ScoreInsert(list, entry, false)
    r.saved = (r.place >= 0)
    SaveTpScores(r.mode, list)
    SubmitOnlineScore(st, BoardKeyFor("testpat", r.mode), initials, r.score)
    tp.initials = initials
    RegWrite("tp.initials", initials)
    tp.view = "results"
    tp.sel = 0
end sub

function LoadTpScores(mode as string) as object
    return CleanScoreTable(ParseJson(RegRead("tp.best." + mode, "[]")))
end function

sub SaveTpScores(mode as string, list as object)
    RegWrite("tp.best." + mode, FormatJson(list))
end sub

function TpScoresCount() as integer
    n = 0
    for each mode in TpModes()
        n = n + LoadTpScores(mode).Count()
    end for
    return n
end function

sub ClearTpScores()
    for each mode in TpModes()
        SaveTpScores(mode, [])
    end for
end sub

' The best on the play screen: "ABC 12,345", or dashes before there is one.
function TpBestShort(mode as string) as string
    list = LoadTpScores(mode)
    if list.Count() = 0 then return "---"
    return list[0].i + " " + GroupDigits(list[0].v)
end function

' --- The remote ---

' A key in the game: presses and, while a run is played, the releases too (the paddle moves
' while a direction is held).
sub TpKey(st as object, code as integer)
    tp = st.tp
    v = tp.view
    if v = "play" then
        TpPlayKey(st, code)
        return
    end if
    if code >= 100 then return
    if v = "signal" then
        if (code = 6 or code = 0) and tp.signal.TotalMilliseconds() > 700 then FinishTpSignal(st)
        return
    end if
    if v = "menu" then
        TpMenuKey(st, code)
    else if v = "pause" then
        TpPauseKey(st, code)
    else if v = "results" then
        TpResultsKey(st, code)
    else if v = "initials" then
        TpInitialsKey(st, code)
    else if v = "scores" then
        if code = 4 or code = 5 then
            delta = 1
            if code = 4 then delta = -1
            tp.scoresMode = NextIn(TpModes(), tp.scoresMode, delta)
            FetchOnlineScores(st, BoardKeyFor("testpat", tp.scoresMode))
        else if code = 2 or code = 3 then
            ' up and down: the TV's own table or the online one
            tp.scoresOnline = not tp.scoresOnline
        else if code = 0 or code = 6 then
            tp.view = tp.scoresFrom
        end if
    else if v = "howto" then
        if code = 0 or code = 6 then tp.view = tp.howtoFrom
    end if
    if st.mode = "testpat" then Redraw(st)
end sub

' Left and right move the paddle while held (faster and faster), OK serves the ball, lets go of
' a stuck one or goes on to the next wall, and Play, Back or the options key pause.
sub TpPlayKey(st as object, code as integer)
    g = st.tp.g
    if code = 4 then
        TpPress(g, "left")
    else if code = 104 then
        TpRelease(g, "left")
    else if code = 5 then
        TpPress(g, "right")
    else if code = 105 then
        TpRelease(g, "right")
    else if code = 6 then
        TpPress(g, "launch")
    else if code = 13 or code = 0 or code = 10 then
        PauseTp(st)
    end if
end sub

sub TpMenuKey(st as object, code as integer)
    tp = st.tp
    rows = TpMenuRows(st)
    if tp.sel > rows.Count() - 1 then tp.sel = 0
    id = rows[tp.sel].id
    if code = 0 then
        CloseTestPattern(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        tp.sel = PanelStep(rows, tp.sel, delta)
    else if code = 4 or code = 5 then
        delta = 1
        if code = 4 then delta = -1
        if id = "mode" then
            tp.mode = NextIn(TpModes(), tp.mode, delta)
        else if id = "diff" then
            tp.diff = NextIn(TpDifficulties(), tp.diff, delta)
        end if
    else if code = 6 then
        if id = "play" then
            StartTpRun(st)
        else if id = "scores" then
            ShowTpScores(st, tp.mode, "menu")
        else if id = "howto" then
            tp.howtoFrom = "menu"
            tp.view = "howto"
        else if id = "guide" then
            CloseTestPattern(st)
        end if
    end if
end sub

sub TpPauseKey(st as object, code as integer)
    tp = st.tp
    rows = TpPauseRows(st)
    if tp.sel > rows.Count() - 1 then tp.sel = 0
    id = rows[tp.sel].id
    if code = 0 or code = 13 then
        ResumeTp(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        tp.sel = PanelStep(rows, tp.sel, delta)
    else if code = 6 then
        if id = "resume" then
            ResumeTp(st)
        else if id = "restart" then
            if TpScoreRanks(tp.g) then
                FinishTpSignal(st)
            else
                StartTpRun(st)
            end if
        else if id = "howto" then
            tp.howtoFrom = "pause"
            tp.view = "howto"
        else if id = "guide" then
            if TpScoreRanks(tp.g) then
                FinishTpSignal(st)
            else
                CloseTestPattern(st)
            end if
        end if
    end if
end sub

function TpScoreRanks(g as object) as boolean
    if g = invalid or g.score <= 0 then return false
    return ScoreRank(LoadTpScores(g.mode), g.score, false) >= 0
end function

sub TpResultsKey(st as object, code as integer)
    tp = st.tp
    rows = TpResultsRows(st)
    if tp.sel > rows.Count() - 1 then tp.sel = 0
    id = rows[tp.sel].id
    if code = 0 then
        tp.view = "menu"
        tp.sel = 0
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        tp.sel = PanelStep(rows, tp.sel, delta)
    else if code = 6 then
        if id = "again" then
            StartTpRun(st)
        else if id = "scores" then
            ShowTpScores(st, tp.result.mode, "results")
        else if id = "menu" then
            tp.view = "menu"
            tp.sel = 0
        else if id = "guide" then
            CloseTestPattern(st)
        end if
    end if
end sub

' Three letters: left and right change the highlighted one, OK goes on to the next (and on Save
' keeps them), Back keeps them too, so a high score is never lost.
sub TpInitialsKey(st as object, code as integer)
    tp = st.tp
    rows = TpInitialsRows(st)
    if tp.sel > rows.Count() - 1 then tp.sel = 0
    id = rows[tp.sel].id
    if code = 0 then
        SaveTpInitials(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        tp.sel = PanelStep(rows, tp.sel, delta)
    else if code = 4 or code = 5 then
        delta = 1
        if code = 4 then delta = -1
        if Left(id, 1) = "l" then
            i = id.Mid(1).ToInt()
            tp.letters[i] = NextLetter(tp.letters[i], delta)
        end if
    else if code = 6 then
        if id = "save" then
            SaveTpInitials(st)
        else
            tp.sel = PanelStep(rows, tp.sel, 1)
        end if
    end if
end sub

' --- The pages ---

sub DrawTestPattern(st as object)
    tp = st.tp
    v = tp.view
    if v = "play" or v = "signal" then
        DrawTpPlay(st)
    else if v = "menu" then
        DrawPanel(st, { title: "Test Pattern", rows: TpMenuRows(st), sel: tp.sel, info: false, hint: "Left and right change it · OK picks · Back goes to the guide" })
    else if v = "pause" then
        DrawPanel(st, { title: "Paused", rows: TpPauseRows(st), sel: tp.sel, info: false, hint: "OK picks · Play or Back carries on" })
    else if v = "results" then
        DrawPanel(st, { title: TpResultTitle(tp.result), rows: TpResultsRows(st), sel: tp.sel, info: false, hint: "OK picks · Back goes to the menu" })
    else if v = "initials" then
        DrawPanel(st, { title: "New high score", rows: TpInitialsRows(st), sel: tp.sel, info: false, hint: "Left and right pick a letter · OK goes on · Back saves" })
    else if v = "scores" then
        title = "High scores: " + TpModeTitle(tp.scoresMode)
        if tp.scoresOnline then title = "Online board: " + TpModeTitle(tp.scoresMode)
        DrawPanel(st, { title: title, rows: TpScoreRows(st), sel: -1, info: true, hint: "Left and right switch the mode · Up and down: this TV or online · Back goes back" })
    else if v = "howto" then
        DrawPanel(st, { title: "How to play", rows: TpHowToRows(), sel: -1, info: true, hint: "Back goes back" })
    end if
end sub

function TpModeTitle(mode as string) as string
    if mode = "endless" then return "Endless"
    return "Campaign"
end function

function TpDiffTitle(diff as string) as string
    if diff = "easy" then return "Easy"
    if diff = "hard" then return "Hard"
    return "Normal"
end function

function TpModeGoal(mode as string) as string
    if mode = "endless" then return "New walls that never stop, each a little faster"
    return "Six walls, each a little faster than the last"
end function

' The difficulty as the menu shows it: its name and what it means.
function TpDiffWord(diff as string) as string
    if diff = "easy" then return "Easy: 5 lives, wide paddle, half points"
    if diff = "hard" then return "Hard: 2 lives, narrow paddle, 1.5x points"
    return "Normal: 3 lives"
end function

function TpMenuRows(st as object) as object
    tp = st.tp
    best = TpBestShort(tp.mode)
    if best = "---" then best = "None yet"
    return [
        { id: "play", label: "Play", value: TpModeTitle(tp.mode) + ", " + LCase(TpDiffTitle(tp.diff)), kind: "open" },
        { id: "mode", label: "Mode", value: TpModeTitle(tp.mode), kind: "choice" },
        { id: "diff", label: "Difficulty", value: TpDiffWord(tp.diff), kind: "choice" },
        { id: "goal", label: "Goal", value: TpModeGoal(tp.mode), kind: "info", dim: true },
        { id: "best", label: "Best", value: best, kind: "info", dim: true },
        { id: "scores", label: "High scores", value: "The top 10 of each mode", kind: "open" },
        { id: "howto", label: "How to play", value: "The keys and the capsules", kind: "open" },
        { id: "guide", label: "Back to the guide", value: "", kind: "open" }
    ]
end function

function TpPauseRows(st as object) as object
    g = st.tp.g
    again = TpModeTitle(g.mode)
    leave = "This run ends"
    ' a run whose score makes the table goes to the initials first (TpPauseKey)
    if TpScoreRanks(g) then
        again = "Your score goes on the table first"
        leave = again
    end if
    rows = [
        { id: "resume", label: "Carry on", value: "", kind: "open" },
        { id: "restart", label: "Start again", value: again, kind: "open" },
        { id: "howto", label: "How to play", value: "", kind: "open" },
        { id: "guide", label: "Back to the guide", value: leave, kind: "open" }
    ]
    rows.Append(TpRunRows(g.mode, g.difficulty, g.score, g.wall + 1, g.lives, g.elapsed))
    return rows
end function

' A run's numbers, as rows that only show them.
function TpRunRows(mode as string, diff as string, score as integer, wall as integer, lives as integer, time as integer) as object
    wallWord = wall.ToStr()
    if mode = "campaign" then wallWord = wall.ToStr() + " of 6"
    rows = [
        { label: "Score", value: GroupDigits(score), kind: "info", dim: true },
        { label: "Wall", value: wallWord, kind: "info", dim: true }
    ]
    if lives >= 0 then rows.Push({ label: "Lives", value: lives.ToStr(), kind: "info", dim: true })
    rows.Push({ label: "Difficulty", value: TpDiffTitle(diff), kind: "info", dim: true })
    rows.Push({ label: "Time", value: TimeWord(time), kind: "info", dim: true })
    return rows
end function

function TpResultTitle(r as object) as string
    if r.how = "won" then return "All six walls clear"
    return "No signal"
end function

function TpResultsRows(st as object) as object
    r = st.tp.result
    table = "The top 10"
    if r.saved then table = PlaceWord(r.place) + " place, as " + st.tp.initials
    rows = [
        { id: "again", label: "Play again", value: TpModeTitle(r.mode), kind: "open" },
        { id: "scores", label: "High scores", value: table, kind: "open" },
        { id: "menu", label: "Menu", value: "Pick the mode and the difficulty", kind: "open" },
        { id: "guide", label: "Back to the guide", value: "", kind: "open" }
    ]
    rows.Append(TpRunRows(r.mode, r.diff, r.score, r.wall, -1, r.time))
    rows.Push(OnlinePostRow(st, BoardKeyFor("testpat", r.mode), r))
    return rows
end function

function TpInitialsRows(st as object) as object
    tp = st.tp
    r = tp.result
    l = tp.letters
    return [
        { id: "l0", label: "First letter", value: l[0], kind: "choice" },
        { id: "l1", label: "Second letter", value: l[1], kind: "choice" },
        { id: "l2", label: "Third letter", value: l[2], kind: "choice" },
        { id: "save", label: "Save", value: l[0] + l[1] + l[2], kind: "open" },
        { label: TpModeTitle(r.mode), value: GroupDigits(r.score) + ", " + PlaceWord(r.place) + " place", kind: "info", dim: true }
    ]
end function

' The scores page opens on the TV's own table for the mode and asks still.b0r3d.org for the
' mode's online one.
sub ShowTpScores(st as object, mode as string, from as string)
    tp = st.tp
    tp.scoresMode = mode
    tp.scoresFrom = from
    tp.scoresOnline = false
    tp.view = "scores"
    FetchOnlineScores(st, BoardKeyFor("testpat", mode))
end sub

' A mode's table: place and initials, then the score, the wall reached and the difficulty. With
' up or down, the mode's online board instead.
function TpScoreRows(st as object) as object
    tp = st.tp
    mode = tp.scoresMode
    if tp.scoresOnline then return OnlineScoreRows(st, BoardKeyFor("testpat", mode))
    list = LoadTpScores(mode)
    rows = []
    newPlace = -1
    if tp.result <> invalid then
        if tp.result.saved and tp.result.mode = mode then newPlace = tp.result.place
    end if
    for i = 0 to list.Count() - 1
        e = list[i]
        diff = "normal"
        if GetInterface(e.d, "ifString") <> invalid then diff = e.d
        value = GroupDigits(e.v) + ", wall " + AsInt(e.w).ToStr() + ", " + LCase(TpDiffTitle(diff))
        if i = newPlace then value = value + " (new)"
        rows.Push({ label: PlaceWord(i) + "  " + e.i, value: value })
    end for
    if rows.Count() = 0 then rows.Push({ label: "None yet", value: "Play a run to get on the table" })
    return rows
end function

function TpHowToRows() as object
    return [
        { label: "Move", value: "Hold left or right; the paddle speeds up the longer you hold" },
        { label: "Serve", value: "OK sends the ball off the paddle, and lets go of a stuck one" },
        { label: "Pause", value: "Play, Back or the options key" },
        { label: "Walls", value: "Knock out every brick for the next wall. Campaign has six; Endless never stops." },
        { label: "Bombs", value: "Bricks of static blow up the bricks around them" },
        { label: "Capsules", value: "Bricks with a little TV drop one: more balls, a wide paddle, a sticky paddle (12 s), a slow ball (8 s), a life, or a surprise" },
        { label: "Lives", value: "Miss every ball and a life goes; the capsules wear off" }
    ]
end function
