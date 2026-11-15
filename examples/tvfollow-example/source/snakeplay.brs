' Snake on the TV, channel 23: its menu, the run, the pause menu, the end of a run,
' initials for the high scores, the leaderboard and how to play. The rules are in
' snake.brs and the play screen in snakedraw.brs; every other screen here is a page
' in the shared layout (panel.brs), so they come in all five looks.
'
' Kept on the TV (registry): snake.initials (the last initials typed) and snake.best
' (top 10 leaderboard), with fallback to sp.best.snake for compatibility. Scores that make the
' table also go to the online board (boards.brs).

sub OpenSnake(st as object)
    if st.snake = invalid then st.snake = NewSnakeGame()
    sn = st.snake
    sn.view = "menu"
    sn.sel = 0
    sn.g = invalid
    sn.art = invalid
    sn.result = invalid
    st.mode = "snake"
    Redraw(st)
end sub

' Back to the guide. The run and its pictures are let go.
sub CloseSnake(st as object)
    sn = st.snake
    sn.g = invalid
    sn.art = invalid
    sn.view = "menu"
    st.mode = "guide"
    Redraw(st)
end sub

function NewSnakeGame() as object
    savedInitials = RegRead("snake.initials", RegRead("sp.initials", "AAA"))
    sn = {
        view: "menu",
        sel: 0,
        initials: CleanInitials(savedInitials),
        letters: ["A", "A", "A"],
        g: invalid,
        art: invalid,
        second: CreateObject("roTimespan"),
        clock: CreateObject("roTimespan"),
        lastMs: 0,
        signal: CreateObject("roTimespan"),
        signalHow: "",
        fps: { frames: 0, timer: CreateObject("roTimespan") },
        result: invalid,
        bestText: "---",
        scoresOnline: false,
        scoresFrom: "menu",
        howtoFrom: "menu",
        sounds: LoadSnakeSounds()
    }
    return sn
end function

' --- Sounds: sound effects played when Sound is on ---

function LoadSnakeSounds() as object
    sounds = {}
    names = { eat: "tp-catch", turn: "sp-turn", over: "sp-over", won: "sp-done" }
    for each key in names
        sound = invalid
        try
            sound = CreateObject("roAudioResource", "pkg:/sounds/" + names[key] + ".wav")
        catch e
            sound = invalid
        end try
        if sound <> invalid then sounds[key] = sound
    end for
    return sounds
end function

sub PlaySnakeSound(st as object, name as string)
    if not st.sound then return
    sn = st.snake
    if sn = invalid or sn.sounds = invalid then return
    sound = sn.sounds[name]
    if sound <> invalid then sound.Trigger(60)
end sub

' --- Time and Loop ---

' How long Main's loop waits: what's left of a frame while a run is on screen, else a second
' (less in the RGB look, whose light flows: RgbMenuWaitMs).
function SnakeWaitMs(st as object) as integer
    sn = st.snake
    if sn = invalid then return 1000
    if sn.view <> "play" and sn.view <> "signal" then return RgbMenuWaitMs(st, 1000)
    left = 16 - (sn.clock.TotalMilliseconds() - sn.lastMs)
    if left < 1 then return 1
    return left
end function

function SnakeBusy(st as object) as boolean
    if st.snake = invalid then return false
    return st.snake.view = "play" or st.snake.view = "signal"
end function

sub SnakeTurn(st as object)
    sn = st.snake
    if sn = invalid then return
    if sn.second.TotalMilliseconds() >= 1000 then
        sn.second.Mark()
        st.clock = ClockNow()
        if SaverChores(st) then return
        if not SnakeBusy(st) then Redraw(st)
    end if
    if not SnakeBusy(st) then
        RgbMenuTurn(st)
        return
    end if

    now = sn.clock.TotalMilliseconds()
    ms = now - sn.lastMs
    if ms < 15 then return
    sn.lastMs = now
    if ms > 50 then ms = 50

    if sn.view = "play" then
        SnakeTick(sn.g, ms)
        SnakeEvents(st)
    else if sn.signal.TotalMilliseconds() >= 2200 then
        FinishSnakeSignal(st)
        return
    end if
    DrawSnake(st)
end sub

sub SnakeEvents(st as object)
    sn = st.snake
    g = sn.g
    if g = invalid or g.events.Count() = 0 then return
    for each e in g.events
        if e.kind = "eat" then
            PlaySnakeSound(st, "eat")
        else if e.kind = "over" or e.kind = "won" then
            PlaySnakeSound(st, e.kind)
            sn.view = "signal"
            sn.signalHow = e.kind
            sn.signal.Mark()
        end if
    end for
    g.events.Clear()
end sub

' --- A Run ---

sub StartSnakeRun(st as object)
    sn = st.snake
    sn.g = SnakeNew(GameSeed())
    sn.g.events.Clear()
    sn.bestText = SnakeBestShort()
    sn.result = invalid
    sn.art = invalid
    sn.view = "play"
    sn.clock.Mark()
    sn.lastMs = 0
    RequestRunToken(st, BoardKeyFor("snake", ""))
    Redraw(st)
end sub

sub PauseSnake(st as object)
    sn = st.snake
    sn.view = "pause"
    sn.sel = 0
    Redraw(st)
end sub

sub ResumeSnake(st as object)
    sn = st.snake
    sn.view = "play"
    sn.lastMs = sn.clock.TotalMilliseconds()
    Redraw(st)
end sub

sub FinishSnakeSignal(st as object)
    sn = st.snake
    g = sn.g
    if g = invalid then
        sn.view = "menu"
        sn.sel = 0
        Redraw(st)
        return
    end if

    r = {
        how: g.state,
        score: g.score,
        length: g.snake.Count(),
        speed: g.speed,
        time: g.elapsed,
        place: -1,
        value: g.score,
        saved: false
    }
    ' a run with no points doesn't go on the table
    if g.score > 0 then r.place = ScoreRank(LoadSnakeScores(), r.value, false)
    sn.result = r
    sn.sel = 0

    if r.place >= 0 then
        sn.letters = [Mid(sn.initials, 1, 1), Mid(sn.initials, 2, 1), Mid(sn.initials, 3, 1)]
        sn.view = "initials"
    else
        sn.view = "results"
    end if
    Redraw(st)
end sub

sub SaveSnakeInitials(st as object)
    sn = st.snake
    r = sn.result
    initials = sn.letters[0] + sn.letters[1] + sn.letters[2]
    list = LoadSnakeScores()
    entry = { i: initials, v: r.value, s: r.score, l: r.length, t: r.time }
    r.place = ScoreInsert(list, entry, false)
    r.saved = (r.place >= 0)
    SaveSnakeScores(list)
    SubmitOnlineScore(st, BoardKeyFor("snake", ""), initials, r.score)
    sn.initials = initials
    RegWrite("snake.initials", initials)
    RegWrite("sp.initials", initials)
    sn.view = "results"
    sn.sel = 0
end sub

function LoadSnakeScores() as object
    raw = RegRead("snake.best", RegRead("sp.best.snake", "[]"))
    return CleanScoreTable(ParseJson(raw))
end function

sub SaveSnakeScores(list as object)
    json = FormatJson(list)
    RegWrite("snake.best", json)
    RegWrite("sp.best.snake", json)
end sub

function SnakeScoresCount() as integer
    return LoadSnakeScores().Count()
end function

sub ClearSnakeScores()
    SaveSnakeScores([])
end sub

function SnakeBestShort() as string
    list = LoadSnakeScores()
    if list.Count() = 0 then return "---"
    e = list[0]
    return e.i + " " + e.v.ToStr()
end function

function SnakeBestWord() as string
    list = LoadSnakeScores()
    if list.Count() = 0 then return "None yet"
    e = list[0]
    return e.i + ", " + e.v.ToStr() + " points"
end function

function SnakeScoreRanks(g as object) as boolean
    if g = invalid or g.score <= 0 then return false
    return ScoreRank(LoadSnakeScores(), g.score, false) >= 0
end function

' --- Remote Controls ---

sub SnakeKey(st as object, code as integer)
    sn = st.snake
    v = sn.view
    if v = "play" then
        SnakePlayKey(st, code)
        return
    end if
    if code >= 100 then return
    if v = "signal" then
        if (code = 6 or code = 0) and sn.signal.TotalMilliseconds() > 500 then
            FinishSnakeSignal(st)
        end if
        return
    end if
    if v = "menu" then
        SnakeMenuKey(st, code)
    else if v = "pause" then
        SnakePauseKey(st, code)
    else if v = "results" then
        SnakeResultsKey(st, code)
    else if v = "initials" then
        SnakeInitialsKey(st, code)
    else if v = "scores" then
        if code = 0 or code = 6 then
            sn.view = sn.scoresFrom
            sn.sel = 0
            Redraw(st)
        else if code = 2 or code = 3 then
            ' up and down: the TV's own table or the online one
            sn.scoresOnline = not sn.scoresOnline
            Redraw(st)
        end if
    else if v = "howto" then
        if code = 0 or code = 6 then
            sn.view = sn.howtoFrom
            sn.sel = 0
            Redraw(st)
        end if
    end if
end sub

sub SnakePlayKey(st as object, code as integer)
    g = st.snake.g
    if g = invalid then return
    if code = 2 then
        if SnakeTurnDir(g, 0, -1) then PlaySnakeSound(st, "turn")
    else if code = 3 then
        if SnakeTurnDir(g, 0, 1) then PlaySnakeSound(st, "turn")
    else if code = 4 then
        if SnakeTurnDir(g, -1, 0) then PlaySnakeSound(st, "turn")
    else if code = 5 then
        if SnakeTurnDir(g, 1, 0) then PlaySnakeSound(st, "turn")
    else if code = 13 or code = 0 or code = 10 then
        PauseSnake(st)
    end if
end sub

sub SnakeMenuKey(st as object, code as integer)
    sn = st.snake
    rows = SnakeMenuRows(st)
    if sn.sel > rows.Count() - 1 then sn.sel = 0
    id = rows[sn.sel].id
    if code = 0 then
        CloseSnake(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sn.sel = PanelStep(rows, sn.sel, delta)
    else if code = 6 then
        if id = "play" then
            StartSnakeRun(st)
        else if id = "scores" then
            ShowSnakeScores(st, "menu")
        else if id = "howto" then
            sn.howtoFrom = "menu"
            sn.view = "howto"
        else if id = "guide" then
            CloseSnake(st)
        end if
    end if
    Redraw(st)
end sub

sub SnakePauseKey(st as object, code as integer)
    sn = st.snake
    rows = SnakePauseRows(st)
    if sn.sel > rows.Count() - 1 then sn.sel = 0
    id = rows[sn.sel].id
    if code = 0 or code = 13 then
        ResumeSnake(st)
        return
    end if
    if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sn.sel = PanelStep(rows, sn.sel, delta)
        Redraw(st)
    else if code = 6 then
        if id = "resume" then
            ResumeSnake(st)
        else if id = "restart" then
            if SnakeScoreRanks(sn.g) then
                FinishSnakeSignal(st)
            else
                StartSnakeRun(st)
            end if
        else if id = "howto" then
            sn.howtoFrom = "pause"
            sn.view = "howto"
            Redraw(st)
        else if id = "guide" then
            if SnakeScoreRanks(sn.g) then
                FinishSnakeSignal(st)
            else
                CloseSnake(st)
            end if
        end if
    end if
end sub

sub SnakeResultsKey(st as object, code as integer)
    sn = st.snake
    rows = SnakeResultsRows(st)
    if sn.sel > rows.Count() - 1 then sn.sel = 0
    id = rows[sn.sel].id
    if code = 0 then
        sn.view = "menu"
        sn.sel = 0
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sn.sel = PanelStep(rows, sn.sel, delta)
    else if code = 6 then
        if id = "again" then
            StartSnakeRun(st)
        else if id = "scores" then
            ShowSnakeScores(st, "results")
        else if id = "menu" then
            sn.view = "menu"
            sn.sel = 0
        else if id = "guide" then
            CloseSnake(st)
        end if
    end if
    Redraw(st)
end sub

sub SnakeInitialsKey(st as object, code as integer)
    sn = st.snake
    rows = SnakeInitialsRows(st)
    if sn.sel > rows.Count() - 1 then sn.sel = 0
    id = rows[sn.sel].id
    if code = 0 then
        SaveSnakeInitials(st)
    else if code = 2 or code = 3 then
        delta = 1
        if code = 2 then delta = -1
        sn.sel = PanelStep(rows, sn.sel, delta)
    else if code = 4 or code = 5 then
        delta = 1
        if code = 4 then delta = -1
        if Left(id, 1) = "l" then
            i = id.Mid(1).ToInt()
            sn.letters[i] = NextLetter(sn.letters[i], delta)
        end if
    else if code = 6 then
        if id = "save" then
            SaveSnakeInitials(st)
        else
            sn.sel = PanelStep(rows, sn.sel, 1)
        end if
    end if
    Redraw(st)
end sub

' --- Panel Rows ---

function SnakeMenuRows(st as object) as object
    return [
        { id: "play", label: "Play", value: "Retro snake arcade", kind: "open" },
        { id: "best", label: "Best", value: SnakeBestWord(), kind: "info", dim: true },
        { id: "rules", label: "Rules", value: "Wrap arena · Eat to grow and speed up", kind: "info", dim: true },
        { id: "scores", label: "Leaderboard", value: "Top 10 players", kind: "open" },
        { id: "howto", label: "How to play", value: "Controls and scoring", kind: "open" },
        { id: "guide", label: "Back to the guide", value: "", kind: "open" }
    ]
end function

function SnakePauseRows(st as object) as object
    g = st.snake.g
    again = "New run"
    leave = "This run ends"
    ' a run whose score makes the table goes to the initials first (SnakePauseKey)
    if SnakeScoreRanks(g) then
        again = "Your score goes on the table first"
        leave = again
    end if
    rows = [
        { id: "resume", label: "Carry on", value: "", kind: "open" },
        { id: "restart", label: "Start again", value: again, kind: "open" },
        { id: "howto", label: "How to play", value: "", kind: "open" },
        { id: "guide", label: "Back to the guide", value: leave, kind: "open" }
    ]
    if g <> invalid then rows.Append(SnakeRunRows(g.score, g.snake.Count(), g.speed, g.elapsed))
    return rows
end function

function SnakeRunRows(score as integer, length as integer, speed as integer, time as integer) as object
    return [
        { label: "Score", value: score.ToStr() + " points", kind: "info", dim: true },
        { label: "Length", value: length.ToStr() + " segments", kind: "info", dim: true },
        { label: "Speed", value: speed.ToStr() + " ms / tick", kind: "info", dim: true },
        { label: "Time", value: TimeWord(time), kind: "info", dim: true }
    ]
end function

' The results page's title: a win (the whole arena filled) keeps the words of its banner.
function SnakeResultsTitle(sn as object) as string
    if sn.result <> invalid then
        if sn.result.how = "won" then return "Arena Clear"
    end if
    return "Game Over"
end function

function SnakeResultsRows(st as object) as object
    sn = st.snake
    r = sn.result
    table = "Top 10 leaderboard"
    if r.saved then table = PlaceWord(r.place) + " place, as " + sn.initials
    rows = [
        { id: "again", label: "Play again", value: "New run", kind: "open" },
        { id: "scores", label: "Leaderboard", value: table, kind: "open" },
        { id: "menu", label: "Menu", value: "Back to snake menu", kind: "open" },
        { id: "guide", label: "Back to the guide", value: "", kind: "open" }
    ]
    rows.Append(SnakeRunRows(r.score, r.length, r.speed, r.time))
    return rows
end function

function SnakeInitialsRows(st as object) as object
    sn = st.snake
    r = sn.result
    l = sn.letters
    return [
        { id: "l0", label: "First letter", value: l[0], kind: "choice" },
        { id: "l1", label: "Second letter", value: l[1], kind: "choice" },
        { id: "l2", label: "Third letter", value: l[2], kind: "choice" },
        { id: "save", label: "Save", value: l[0] + l[1] + l[2], kind: "open" },
        { label: "Score", value: r.score.ToStr() + " points, " + PlaceWord(r.place) + " place", kind: "info", dim: true }
    ]
end function

' The scores page opens on the TV's own table and asks still.b0r3d.org for the online one.
sub ShowSnakeScores(st as object, from as string)
    sn = st.snake
    sn.scoresFrom = from
    sn.scoresOnline = false
    sn.view = "scores"
    FetchOnlineScores(st, BoardKeyFor("snake", ""))
end sub

' The TV's own top 10 (or, with up or down, the online board's).
function SnakeScoreRows(st as object) as object
    sn = st.snake
    if sn.scoresOnline then return OnlineScoreRows(st, BoardKeyFor("snake", ""))
    list = LoadSnakeScores()
    rows = []
    newPlace = -1
    if sn.result <> invalid and sn.result.saved then newPlace = sn.result.place
    for i = 0 to list.Count() - 1
        e = list[i]
        val = AsInt(e.s).ToStr() + " points, length " + AsInt(e.l).ToStr() + ", " + TimeWord(AsInt(e.t))
        if i = newPlace then val = val + " (new)"
        rows.Push({ label: PlaceWord(i) + "  " + e.i, value: val })
    end for
    if rows.Count() = 0 then rows.Push({ label: "None yet", value: "Finish a run to enter the leaderboard" })
    return rows
end function

function SnakeHowToRows() as object
    return [
        { label: "Steer", value: "Use the D-pad (Up, Down, Left, Right) to change direction" },
        { label: "Food", value: "Each bite of food makes the snake one longer and scores a point" },
        { label: "Speed", value: "Each bite speeds up the snake by 3 ms, down to 70 ms" },
        { label: "Wrap", value: "Moving past any board edge wraps around to the other side" },
        { label: "Danger", value: "Biting your own body ends the run immediately" },
        { label: "Pause", value: "Press Play or Back anytime to pause the run" }
    ]
end function

' --- Page Drawing ---

sub DrawSnake(st as object)
    sn = st.snake
    v = sn.view
    if v = "play" or v = "signal" then
        DrawSnakePlay(st)
    else if v = "menu" then
        DrawPanel(st, { title: "Snake", rows: SnakeMenuRows(st), sel: sn.sel, info: false, hint: "OK picks · Back goes to the guide" })
    else if v = "pause" then
        DrawPanel(st, { title: "Paused", rows: SnakePauseRows(st), sel: sn.sel, info: false, hint: "OK picks · Play or Back carries on" })
    else if v = "results" then
        DrawPanel(st, { title: SnakeResultsTitle(sn), rows: SnakeResultsRows(st), sel: sn.sel, info: false, hint: "OK picks · Back goes to the menu" })
    else if v = "initials" then
        DrawPanel(st, { title: "New high score", rows: SnakeInitialsRows(st), sel: sn.sel, info: false, hint: "Left and right pick a letter · OK goes on · Back saves" })
    else if v = "scores" then
        title = "Leaderboard: Snake"
        if sn.scoresOnline then title = "Online board: Snake"
        DrawPanel(st, { title: title, rows: SnakeScoreRows(st), sel: -1, info: true, hint: "Up and down switch between this TV and online · Back goes back" })
    else if v = "howto" then
        DrawPanel(st, { title: "How to play", rows: SnakeHowToRows(), sel: -1, info: true, hint: "Back goes back" })
    end if
end sub
