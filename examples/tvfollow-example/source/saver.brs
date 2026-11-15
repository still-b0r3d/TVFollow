' The Ruckus screensaver. Ruckus walks on, sits, grooms, loafs, naps, stretches and wanders off,
' and now and then he chases a stuck pixel, while a clock in the look's font drifts slowly so
' nothing stays in one spot. One scene, two places:
'   - The whole TV: RunScreenSaver() runs when "b0r3d TV" is picked in the TV's Settings,
'     Theme, Screensavers. Roku starts it on its own, so it reads b0r3d TV's saved settings
'     for the look, its colour and the clock.
'   - Inside b0r3d TV: Roku shows no screensaver over a 2D app like this one (at the TV's
'     screensaver time the screen just goes black), so b0r3d TV brings Ruckus on itself, 30
'     seconds before that time, and any key brings back the screen you were on. What happens
'     at the TV's time is the Screensaver setting's pick (see SaverChores).
' Laid out on the 1280 x 720 grid (scaled by st.k), like the looks. Ruckus is 16 x 16 of his
' pixels, each 8 grid units wide; his feet stand on r.floor.

sub RunScreenSaver()
    st = { port: CreateObject("roMessagePort"), mode: "saver", note: "" }
    LoadPrefs(st)
    st.clock = ClockNow()
    st.fontReg = RegisterFonts()
    st.families = FontFamilies(st.fontReg)
    OpenScreen(st, RegRead("res", "fhd"))
    if st.screen = invalid then return
    ' noted for Settings' Screensaver check: proof that the TV ran it, and at what size
    RegWrite("tvsaver", ClockText(st, false) + " " + st.clock.date + ", at " + st.w.ToStr() + " x " + st.h.ToStr())
    RegWrite("tvsavers", (RegRead("tvsavers", "0").ToInt() + 1).ToStr())
    st.saver = NewSaver(st)
    while true
        msg = wait(LoopWaitMs(st), st.port)
        if msg <> invalid then
            ' any key on the remote ends the TV-wide screensaver
            if type(msg) = "roUniversalControlEvent" then
                if msg.GetInt() < 100 then return
            end if
        end if
        if st.saver.frame.TotalMilliseconds() >= 30 then SaverFrame(st)
    end while
end sub

' A fresh scene: Ruckus just off screen, about to walk on, and the clock at a random spot.
function NewSaver(st as object) as object
    k = st.k
    sv = {
        frame: CreateObject("roTimespan"),
        onTimer: CreateObject("roTimespan"),
        hijackTimer: CreateObject("roTimespan"),
        hijacked: false,
        tick: 0,
        u: Z(8, k),
        bg: &h000000FF,
        colors: SaverColors(st),
        fonts: SaverFonts(st),
        runs: RukusRunsFor(st),
        r: { state: "away", timer: 0.8 + Rnd(0), x: -136.0, floor: 600.0, faceLeft: false, t: 0.0, acts: 0, lift: 0.0, goal: 0.0, jump: invalid, zTimer: 0.0 },
        px: invalid,
        pxTimer: 20 + Rnd(0) * 25,
        pxCount: 0,
        pxColors: [&hFF3B30FF, &h34FF5CFF, &h3D7BFFFF],
        pop: invalid,
        zs: [],
        cx: 0.0,
        cy: 0.0,
        cvx: 9.0,
        cvy: 6.0,
        clockText: "",
        clockBmp: invalid,
        clockW: 0,
        clockH: 0
    }
    if st.look = "phosphor" then
        sv.bg = st.kit.bg
        sv.pxColors = [st.kit.full]
    end if
    if Rnd(2) = 1 then sv.cvx = -sv.cvx
    if Rnd(2) = 1 then sv.cvy = -sv.cvy
    MakeSaverClock(st, sv)
    sv.cx = 32 + Rnd(0) * (1280 - 64 - sv.clockW)
    sv.cy = 32 + Rnd(0) * (720 - 64 - sv.clockH)
    return sv
end function

' The clock's fonts: the look's own lettering, big.
function SaverFonts(st as object) as object
    k = st.k
    if st.look = "b95" then return { time: W95(st, Z(72, k)), date: W95(st, Z(26, k)) }
    if st.look = "phosphor" then return { time: Vt323(st, Z(112, k)), date: Vt323(st, Z(36, k)) }
    if st.look = "vcr" then return { time: Silk(st, Z(64, k), true), date: Silk(st, Z(24, k), false) }
    if st.look = "rgb" then return { time: Barlow(st, "semibold", Z(96, k)), date: Barlow(st, "semibold", Z(26, k)) }
    return { time: Vt323(st, Z(104, k)), date: Barlow(st, "semibold", Z(28, k)) }
end function

' The clock's colours on the dark screen, and the soft one for his z's. Each look has its own
' branch: a kit only has the colour names its own look uses.
function SaverColors(st as object) as object
    c = st.kit
    if st.look = "b95" then return { time: &hFFFFFFFF, date: &hC0C0C0FF, soft: &hC0C0C0FF, glow: 0 }
    if st.look = "phosphor" then return { time: c.full, date: c.dim, soft: c.dim, glow: c.glow }
    if st.look = "vcr" then return { time: c.white, date: c.yellow, soft: c.white, glow: 0 }
    if st.look = "rgb" then return { time: c.white, date: c.accent, soft: c.soft, glow: 0 }
    return { time: c.text, date: c.soft, soft: c.dimText, glow: 0 }
end function

' Every pose, facing left and facing right, in the look's colours, as runs of one colour
' ([x, y, width, colour] in his pixels), so a frame draws a few dozen rectangles.
function RukusRunsFor(st as object) as object
    colors = RukusColorsFor(st)
    poses = RukusPoses()
    runs = {}
    for each name in poses
        runs[name + "L"] = SpriteRuns(poses[name], colors, false)
        runs[name + "R"] = SpriteRuns(poses[name], colors, true)
    end for
    return runs
end function

function SpriteRuns(rows as object, colors as object, flip as boolean) as object
    runs = []
    for r = 0 to rows.Count() - 1
        line = rows[r]
        n = Len(line)
        c = 0
        while c < n
            code = Mid(line, c + 1, 1)
            if colors.DoesExist(code) then
                color = colors[code]
                e = c + 1
                while e < n
                    nextCode = Mid(line, e + 1, 1)
                    if not colors.DoesExist(nextCode) then exit while
                    if colors[nextCode] <> color then exit while
                    e = e + 1
                end while
                x = c
                if flip then x = n - e
                runs.Push([x, r, e - c, color])
                c = e
            else
                c = c + 1
            end if
        end while
    end for
    return runs
end function

' One frame: everything moves on by the time since the last one, then the screen is drawn.
sub SaverFrame(st as object)
    sv = st.saver
    if sv = invalid then return
    dt = sv.frame.TotalMilliseconds() / 1000
    sv.frame.Mark()
    if dt > 0.1 then dt = 0.1
    sv.tick = sv.tick + dt
    if sv.tick >= 1 then
        sv.tick = 0
        st.clock = ClockNow()
        MakeSaverClock(st, sv)
    end if
    StepRukus(sv, dt)
    StepPixel(sv, dt)
    StepZs(sv, dt)
    StepClock(sv, dt)
    DrawSaver(st)
end sub

' --- Ruckus ---

sub StepRukus(sv as object, dt as dynamic)
    r = sv.r
    r.t = r.t + dt
    r.timer = r.timer - dt
    if r.state = "away" then
        if r.timer <= 0 then RukusEnter(sv)
        return
    end if
    if r.state = "pounce" then
        StepPounce(sv, dt)
        return
    end if
    if WantsPixel(sv) then
        ChasePixel(sv, dt)
        return
    end if
    if r.state = "chase" then RukusNext(sv)
    if r.state = "enter" or r.state = "leave" then
        WalkTo(r, r.goal, 42, dt)
        if Abs(r.x - r.goal) < 1 then
            if r.state = "leave" then
                r.state = "away"
                r.timer = 4 + Rnd(0) * 6
            else
                RukusNext(sv)
            end if
        end if
        return
    end if
    if r.state = "walk" then
        dir = 1
        if r.faceLeft then dir = -1
        r.x = r.x + dir * 42 * dt
        if r.x < 24 then
            r.x = 24
            r.faceLeft = false
        else if r.x > 1280 - 128 - 24 then
            r.x = 1280 - 128 - 24
            r.faceLeft = true
        end if
    end if
    if r.timer <= 0 then RukusNext(sv)
end sub

' He walks on from one side, his feet somewhere in the lower half, and does a few things.
sub RukusEnter(sv as object)
    r = sv.r
    r.floor = 440 + Rnd(0) * 240
    r.t = 0
    r.lift = 0
    r.acts = 6 + Rnd(7)
    if Rnd(2) = 1 then
        r.x = -136
        r.goal = 80 + Rnd(0) * 520
    else
        r.x = 1288
        r.goal = 1280 - 128 - 80 - Rnd(0) * 520
    end if
    r.faceLeft = (r.goal < r.x)
    r.state = "enter"
end sub

' What he does next: a stroll, a sit, a groom, a loaf, a nap or a stretch, and after a few of
' those he wanders off at the nearer side.
sub RukusNext(sv as object)
    r = sv.r
    r.t = 0
    r.acts = r.acts - 1
    if r.acts < 0 then
        r.state = "leave"
        r.goal = 1288
        if r.x + 64 < 640 then r.goal = -136
        sv.px = invalid
        return
    end if
    roll = Rnd(0)
    if roll < 0.34 then
        r.state = "walk"
        r.faceLeft = (Rnd(2) = 1)
        r.timer = 3 + Rnd(0) * 5
    else if roll < 0.54 then
        r.state = "sit"
        r.timer = 3.5 + Rnd(0) * 5
    else if roll < 0.66 then
        r.state = "groom"
        r.timer = 2.5 + Rnd(0) * 3
    else if roll < 0.80 then
        r.state = "loaf"
        r.timer = 5 + Rnd(0) * 7
    else if roll < 0.92 then
        r.state = "nap"
        r.timer = 18 + Rnd(0) * 24
        r.zTimer = 0.8
    else
        r.state = "stretch"
        r.timer = 1.6 + Rnd(0) * 1.4
    end if
end sub

' On screen and not just walking on or off.
function RukusSettled(r as object) as boolean
    return r.state <> "away" and r.state <> "enter" and r.state <> "leave"
end function

sub WalkTo(r as object, goal as dynamic, speed as dynamic, dt as dynamic)
    d = goal - r.x
    r.faceLeft = (d < 0)
    stepX = speed * dt
    if Abs(d) <= stepX then
        r.x = goal
    else if d < 0 then
        r.x = r.x - stepX
    else
        r.x = r.x + stepX
    end if
end sub

' The sprite for what he's doing: two frames for walking (faster when he runs), groom and sit
' in turn for grooming, the two loafs in turn for loafing and napping.
function RukusPose(r as object) as string
    doing = r.state
    if doing = "enter" or doing = "leave" or doing = "walk" then
        if Int(r.t / 0.22) mod 2 = 1 then return "walk"
        return "stand"
    end if
    if doing = "chase" then
        if Int(r.t / 0.11) mod 2 = 1 then return "walk"
        return "stand"
    end if
    if doing = "pounce" or doing = "stretch" then return "stretch"
    if doing = "groom" then
        if Int(r.t / 0.45) mod 2 = 1 then return "sit"
        return "groom"
    end if
    if doing = "loaf" then
        if Int(r.t / 1.3) mod 2 = 1 then return "loaf2"
        return "loaf"
    end if
    if doing = "nap" then
        if Int(r.t / 1.1) mod 2 = 1 then return "loaf2"
        return "loaf"
    end if
    return "sit"
end function

' --- The stuck pixel: one lit dot on his floor. He notices it after a moment (not while he
' naps), runs over and pounces, and it pops. ---

sub StepPixel(sv as object, dt as dynamic)
    if sv.pop <> invalid then
        sv.pop.t = sv.pop.t + dt
        if sv.pop.t > 0.5 then sv.pop = invalid
    end if
    r = sv.r
    settled = RukusSettled(r)
    if sv.px = invalid then
        if not settled then return
        sv.pxTimer = sv.pxTimer - dt
        if sv.pxTimer > 0 then return
        ' somewhere along his floor, not right under him, so there's a chase
        x = 60 + Rnd(0) * 1150
        for tries = 1 to 5
            if Abs(x - (r.x + 64)) > 220 then exit for
            x = 60 + Rnd(0) * 1150
        end for
        sv.px = { x: x, y: r.floor - 8, notice: 0.8 + Rnd(0) * 0.8, color: sv.pxColors[sv.pxCount mod sv.pxColors.Count()] }
        sv.pxCount = sv.pxCount + 1
    else if settled and r.state <> "nap" then
        sv.px.notice = sv.px.notice - dt
    end if
end sub

function WantsPixel(sv as object) as boolean
    if sv.px = invalid then return false
    r = sv.r
    return sv.px.notice <= 0 and RukusSettled(r) and r.state <> "nap"
end function

' He runs to the pixel, and once he's close, pounces on it.
sub ChasePixel(sv as object, dt as dynamic)
    r = sv.r
    if r.state <> "chase" then
        r.state = "chase"
        r.t = 0
    end if
    target = sv.px.x + 4 - 64
    d = target - r.x
    r.faceLeft = (d < 0)
    if Abs(d) <= 72 then
        r.state = "pounce"
        r.t = 0
        r.jump = { x0: r.x, x1: target, t: 0.0, dur: 0.45 + Abs(d) / 400, arc: 34 + Abs(d) * 0.3 }
        return
    end if
    stepX = 230 * dt
    if d < 0 then
        r.x = r.x - stepX
    else
        r.x = r.x + stepX
    end if
end sub

sub StepPounce(sv as object, dt as dynamic)
    r = sv.r
    j = r.jump
    j.t = j.t + dt
    p = j.t / j.dur
    if p > 1 then p = 1
    r.x = j.x0 + (j.x1 - j.x0) * p
    r.lift = j.arc * 4 * p * (1 - p)
    if p < 1 then return
    r.lift = 0
    r.jump = invalid
    if sv.px <> invalid then
        sv.pop = { x: sv.px.x, y: sv.px.y, t: 0.0, color: sv.px.color }
        sv.px = invalid
    end if
    sv.pxTimer = 35 + Rnd(0) * 50
    r.state = "sit"
    r.t = 0
    r.timer = 2.5 + Rnd(0) * 2
end sub

' --- His z's while he naps, floating up and away. ---

sub StepZs(sv as object, dt as dynamic)
    r = sv.r
    if r.state = "nap" then
        r.zTimer = r.zTimer - dt
        if r.zTimer <= 0 then
            r.zTimer = 1.4
            if r.faceLeft then
                sv.zs.Push({ x: r.x + 20, y: r.floor - 100, t: 0.0, dx: -7 })
            else
                sv.zs.Push({ x: r.x + 128 - 32, y: r.floor - 100, t: 0.0, dx: 7 })
            end if
        end if
    end if
    if sv.zs.Count() = 0 then return
    kept = []
    for each zee in sv.zs
        zee.t = zee.t + dt
        zee.y = zee.y - 16 * dt
        zee.x = zee.x + zee.dx * dt
        if zee.t < 2.6 then kept.Push(zee)
    end for
    sv.zs = kept
end sub

' --- The clock: the time and the date as one picture, made again when they change, so a
' frame only copies it (drawing text is slow on a Roku). It drifts and bounces off the edges.
' The picture is filled with the screen's own dark colour, so the lettering's soft edges are
' worked out once, against that colour.
' The old picture is let go before the new one is made, so the two are never held at once. If
' the TV has no picture memory left for the new one, the clock is left out (DrawSaver draws
' Ruckus without it) and made again once PicturesFailing's 5 seconds are up.

sub MakeSaverClock(st as object, sv as object)
    text = ClockText(st, false)
    day = st.clock.date
    if text + "|" + day = sv.clockText then return
    if PicturesFailing(st) then return
    f = sv.fonts
    c = sv.colors
    k = st.k
    pad = Z(8, k)
    gap = Z(6, k)
    tw = f.time.GetOneLineWidth(text, 99999)
    dw = f.date.GetOneLineWidth(day, 99999)
    w = tw
    if dw > w then w = dw
    w = w + 2 * pad
    timeH = f.time.GetOneLineHeight()
    h = timeH + gap + f.date.GetOneLineHeight() + 2 * pad
    sv.clockBmp = invalid
    sv.clockText = ""
    sv.clockW = w / k
    sv.clockH = h / k
    bmp = NewPicture(w, h, true)
    if bmp = invalid then
        PictureFailed(st)
        return
    end if
    bmp.Clear(sv.bg)
    tx = (w - tw) \ 2
    if c.glow <> 0 then
        DrawGlow(bmp, text, tx, pad, c.time, c.glow, f.time, Z(2, k))
    else
        bmp.DrawText(text, tx, pad, c.time, f.time)
    end if
    bmp.DrawText(day, (w - dw) \ 2, pad + timeH + gap, c.date, f.date)
    sv.clockBmp = bmp
    sv.clockText = text + "|" + day
end sub

sub StepClock(sv as object, dt as dynamic)
    sv.cx = sv.cx + sv.cvx * dt
    sv.cy = sv.cy + sv.cvy * dt
    maxX = 1280 - 24 - sv.clockW
    maxY = 720 - 24 - sv.clockH
    if sv.cx < 24 then
        sv.cx = 24
        sv.cvx = Abs(sv.cvx)
    else if sv.cx > maxX then
        sv.cx = maxX
        sv.cvx = -Abs(sv.cvx)
    end if
    if sv.cy < 24 then
        sv.cy = 24
        sv.cvy = Abs(sv.cvy)
    else if sv.cy > maxY then
        sv.cy = maxY
        sv.cvy = -Abs(sv.cvy)
    end if
end sub

' --- Drawing ---

sub DrawSaver(st as object)
    s = st.screen
    sv = st.saver
    if s = invalid or sv = invalid then return
    k = st.k
    s.Clear(sv.bg)
    if sv.clockBmp <> invalid then s.DrawObject(Z(sv.cx, k), Z(sv.cy, k), sv.clockBmp)
    if sv.px <> invalid then s.DrawRect(Z(sv.px.x, k), Z(sv.px.y, k), sv.u, sv.u, sv.px.color)
    if sv.pop <> invalid then DrawPop(st, sv)
    DrawRukus(st, sv)
    DrawZs(st, sv)
    if ScanlinesOn(st) then DrawScanlines(st)
    s.SwapBuffers()
end sub

sub DrawRukus(st as object, sv as object)
    r = sv.r
    if r.state = "away" then return
    s = st.screen
    k = st.k
    u = sv.u
    side = "R"
    if r.faceLeft then side = "L"
    ox = Z(r.x, k)
    oy = Z(r.floor - 128 - r.lift, k)
    for each seg in sv.runs[RukusPose(r) + side]
        s.DrawRect(ox + seg[0] * u, oy + seg[1] * u, seg[2] * u, u, seg[3])
    end for
end sub

' The popped pixel: four sparks flying up and out, shrinking.
sub DrawPop(st as object, sv as object)
    s = st.screen
    k = st.k
    p = sv.pop
    spread = 6 + p.t * 150
    size = Z(7 * (1 - p.t / 0.5) + 1, k)
    for each d in [[-1, -0.6], [1, -0.6], [-0.4, -1.2], [0.4, -1.2]]
        x = Z(p.x + 4 + d[0] * spread, k) - size \ 2
        y = Z(p.y + 4 + d[1] * spread, k) - size \ 2
        s.DrawRect(x, y, size, size, p.color)
    end for
end sub

' Each z is four rows of a little pixel letter.
sub DrawZs(st as object, sv as object)
    if sv.zs.Count() = 0 then return
    s = st.screen
    k = st.k
    u = Z(3, k)
    color = sv.colors.soft
    for each zee in sv.zs
        x = Z(zee.x, k)
        y = Z(zee.y, k)
        s.DrawRect(x, y, 4 * u, u, color)
        s.DrawRect(x + 2 * u, y + u, u, u, color)
        s.DrawRect(x + u, y + 2 * u, u, u, color)
        s.DrawRect(x, y + 3 * u, 4 * u, u, color)
    end for
end sub

' --- Inside b0r3d TV ---

' Settings' picks. "keep": b0r3d TV resets the TV's idle clock so Ruckus stays on (the first
' try); "hand": b0r3d TV steps aside for the TV's own screensaver (the second try); "black": the
' TV's black screen comes at its time, as in any 2D app; "off": no Ruckus inside b0r3d TV.
function SaverPicks() as object
    return ["keep", "hand", "black", "off"]
end function

function SaverPickWord(pick as string) as string
    if pick = "keep" then return "Ruckus, keeps the TV awake"
    if pick = "hand" then return "Ruckus, then the TV's screensaver"
    if pick = "black" then return "Ruckus, then black"
    return "Off"
end function

' The TV's screensaver wait in minutes (0 is off), for a TV that doesn't say its own.
function TvWaitChoices() as object
    return [1, 2, 5, 10, 15, 30, 0]
end function

function MinutesWord(minutes as integer) as string
    if minutes <= 0 then return "Off"
    if minutes = 1 then return "1 minute"
    return minutes.ToStr() + " minutes"
end function

' Settings' row for the TV's wait: the TV's own answer when it gives one, else a choice.
function TvWaitRow(st as object) as object
    row = { id: "tvwait", label: "TV's screensaver wait", value: MinutesWord(st.tvWaitPref), kind: "choice" }
    if st.tvWait >= 0 then
        row.value = MinutesWord(st.tvWait) + ", the TV says"
        row.kind = "info"
    end if
    return row
end function

' What b0r3d TV can learn about the TV's screensaver, asked once at the start, and the timers
' the screensaver runs on.
sub SaverProbes(st as object)
    st.am = CreateObject("roAppManager")
    st.di = CreateObject("roDeviceInfo")
    st.di.SetMessagePort(st.port)
    try
        st.di.EnableScreensaverExitedEvent(true)
    catch e
        print "b0r3d TV: the TV won't say when a screensaver ends: "; e.message
    end try
    st.tvWait = -1
    try
        minutes = st.am.GetScreensaverTimeout()
        if minutes <> invalid then st.tvWait = minutes
    catch e
        print "b0r3d TV: the TV doesn't say its screensaver wait: "; e.message
    end try
    ' the idle-clock reset: does the call exist at all? (b0r3d TV has only just opened, so
    ' resetting the idle clock now changes nothing)
    st.canReset = true
    ' whether this run has seen the reset work (FinishIdleTest)
    st.keepWorks = false
    st.idleResult = RegRead("idlereset", "not tried yet: it runs 20 s into a wait, when Ruckus keeps the TV awake")
    try
        st.am.UpdateLastKeyPressTime()
    catch e
        st.canReset = false
        st.idleResult = "not on this TV (" + e.message + ")"
    end try
    st.idle = CreateObject("roTimespan")
    st.keepTimer = CreateObject("roTimespan")
    st.choreTimer = CreateObject("roTimespan")
    st.asideTimer = CreateObject("roTimespan")
    st.idleTested = false
    st.idlePending = -1
    st.keepCapped = false
    st.handTried = false
    st.asideSaw = false
    st.saver = invalid
    st.saverFrom = "guide"
    st.asideFrom = "guide"
    st.sg = invalid
    st.sgScene = invalid
    st.quit = false
end sub

' How long Main's loop waits for a key: what's left of a frame (about 30 a second) while Ruckus
' is on, the open game's or channel's own frames, the demo's next step while the guide's
' preview plays one, else a second. The RGB look's lights move, so its guide and pages are drawn
' again up to 10 times a second, but, as with the demos, never more than half the time goes to
' drawing (RgbWaitMs). While the TV's own screensaver may have blanked the screen, neither a
' demo nor the light is drawn more than once a second (it would only be drawn onto black).
' Never 0, which would mean "wait for ever".
function LoopWaitMs(st as object) as integer
    if st.mode = "stuck" then return StuckWaitMs(st)
    if st.mode = "testpat" then return TpWaitMs(st)
    if st.mode = "snake" then return SnakeWaitMs(st)
    if st.mode = "fm" then return 33
    if st.mode = "promo" then return PromoWaitMs(st)
    if st.mode = "deck" then return DeckWaitMs(st)
    if st.mode = "cam" then return CamWaitMs(st)
    if st.mode = "hijack" then return HijackWaitMs(st)
    if st.mode = "saver" and st.saver <> invalid then
        left = 33 - st.saver.frame.TotalMilliseconds()
        if left < 1 then return 1
        if left > 33 then return 33
        return left
    end if
    ms = 1000
    if TvMayBeBlank(st, 0) then return ms
    if GuideShowsGame(st) then ms = DemoWaitMs(st)
    if st.look = "rgb" and st.mode <> "saver" and st.mode <> "aside" then
        led = RgbWaitMs(st)
        if ms > led then ms = led
    end if
    return ms
end function

' The RGB look's wait between drawings: 100 ms, or twice as long as the last one took to draw
' (Tick measures it), up to a second.
function RgbWaitMs(st as object) as integer
    ms = 100
    if st.tickMs <> invalid then
        if 2 * st.tickMs > ms then ms = 2 * st.tickMs
    end if
    if ms > 1000 then ms = 1000
    return ms
end function

' Main's loop calls this on every turn while Ruckus is on: a frame when one is due (whether the
' wait ran out or a message came), then the chores, and now and then Agent Ø (hijack.brs).
sub SaverTurn(st as object)
    if st.saver = invalid then return
    if st.saver.frame.TotalMilliseconds() >= 30 then SaverFrame(st)
    if SaverChores(st) then return
    MaybeIdleHijack(st)
end sub

' The TV's screensaver wait in seconds (0: its screensaver is off), from the TV when it says,
' else from Settings.
function TvWaitSeconds(st as object) as integer
    if st.tvWait >= 0 then return st.tvWait * 60
    return st.tvWaitPref * 60
end function

' Ruckus comes on 30 seconds before the TV's time (after 5 minutes when the TV has none).
function SaverStartSeconds(st as object) as integer
    tvTime = TvWaitSeconds(st)
    if tvTime <= 0 then return 300
    if tvTime - 30 < 20 then return 20
    return tvTime - 30
end function

' About once a second while b0r3d TV is open (from Tick, and from Ruckus's frames): keeps the TV
' awake when that's the pick, brings Ruckus on when the wait is up, and steps aside for the TV's
' own screensaver when that's the pick. Returns true when it has just brought Ruckus on.
function SaverChores(st as object) as boolean
    if st.choreTimer.TotalMilliseconds() < 900 then return false
    st.choreTimer.Mark()
    if st.idlePending >= 0 then FinishIdleTest(st)
    if st.mode = "aside" or st.saverPick = "off" then return false
    idle = st.idle.TotalSeconds()
    tvTime = TvWaitSeconds(st)
    if st.saverPick = "keep" and tvTime > 0 then KeepAwake(st, idle)
    if st.mode = "saver" then
        if st.saverPick = "hand" and tvTime > 0 and not st.handTried and idle >= tvTime - 5 then StepAside(st)
        return false
    end if
    ' in a game, Ruckus only comes on while it's paused (or on its menus)
    if st.mode = "stuck" and StuckBusy(st) then return false
    if st.mode = "testpat" and TpBusy(st) then return false
    if st.mode = "snake" and SnakeBusy(st) then return false
    if idle < SaverStartSeconds(st) then return false
    StartSaver(st)
    return true
end function

' The chores for a channel that plays on its own (a radio station, the ads, a tape in the VCR,
' Ruckus Cam), about once a second.
' While it plays, Ruckus doesn't come on over it, but the TV is kept awake as SaverChores does
' for the "keep" pick (without that, the TV turns the screen black at its screensaver time, as
' over any 2D app, even mid-song). Paused, it's like a paused game: the usual chores, Ruckus
' included. Returns true when Ruckus has just come on.
function MediaChores(st as object, playing as boolean) as boolean
    if not playing then return SaverChores(st)
    if st.choreTimer.TotalMilliseconds() < 900 then return false
    st.choreTimer.Mark()
    if st.idlePending >= 0 then FinishIdleTest(st)
    if st.saverPick = "keep" and TvWaitSeconds(st) > 0 then KeepAwake(st, st.idle.TotalSeconds())
    return false
end function

' Ruckus comes on over whichever screen is up; StopSaver brings that screen back.
sub StartSaver(st as object)
    print "b0r3d TV: Ruckus comes on after "; st.idle.TotalSeconds(); " s without a key"
    st.saverFrom = st.mode
    st.mode = "saver"
    st.saver = NewSaver(st)
    DrawSaver(st)
end sub

sub StopSaver(st as object)
    st.mode = st.saverFrom
    st.saver = invalid
    Redraw(st)
end sub

' The first try at keeping Ruckus on past the TV's time: Roku's old hidden call that resets the
' TV's idle clock, every 20 s while b0r3d TV sits idle, for 2 hours at most (so the TV can still
' rest and switch itself off). The first time, it reads the TV's own idle count before, and again
' a second later (FinishIdleTest), for the Screensaver check page. The real proof is no black
' screen at the TV's time.
sub KeepAwake(st as object, idle as integer)
    if not st.canReset or idle < 20 then return
    if idle > 7200 then
        if not st.keepCapped then print "b0r3d TV: 2 hours without a key, so the TV's own screensaver may come"
        st.keepCapped = true
        return
    end if
    if st.keepTimer.TotalSeconds() < 20 then return
    st.keepTimer.Mark()
    before = TvIdleSeconds(st)
    try
        st.am.UpdateLastKeyPressTime()
    catch e
        st.canReset = false
        SaveIdleResult(st, "not on this TV (" + e.message + ")")
        return
    end try
    if st.idleTested then return
    if before >= 0 and before < 5 then return
    st.idleTested = true
    if before < 0 then
        SaveIdleResult(st, "the call ran, but the TV doesn't say its idle time, at " + ClockText(st, false))
        return
    end if
    st.idlePending = before
end sub

' The self-test's second look, a second after the first reset: a count that went down means
' the reset took.
sub FinishIdleTest(st as object)
    before = st.idlePending
    st.idlePending = -1
    after = TvIdleSeconds(st)
    stamp = ", at " + ClockText(st, false)
    if after >= 0 and after < before then
        st.keepWorks = true
        SaveIdleResult(st, "works: the TV's idle time went from " + before.ToStr() + " s to " + after.ToStr() + " s" + stamp)
    else
        SaveIdleResult(st, "the call ran, but the TV's idle time went on to " + after.ToStr() + " s" + stamp)
    end if
end sub

' Seconds since the last key, as the TV counts them, or -1 when it doesn't say.
function TvIdleSeconds(st as object) as integer
    seconds = -1
    try
        v = st.di.TimeSinceLastKeypress()
        if v <> invalid then seconds = v
    catch e
        seconds = -1
    end try
    return seconds
end function

sub SaveIdleResult(st as object, text as string)
    st.idleResult = text
    RegWrite("idlereset", text)
    print "b0r3d TV idle-clock reset: "; text
end sub

' The second try: b0r3d TV closes its 2D screen (Roku allows other kinds of screen once the
' last 2D one is gone) and shows its splash picture on a small SceneGraph screen instead, so the
' TV's own screensaver can come on over it: Ruckus, when he's picked for the whole TV. Any key,
' or that screensaver ending, brings the 2D screen back (ComeBack).
sub StepAside(st as object)
    st.handTried = true
    st.asideFrom = st.saverFrom
    st.asideSaw = false
    st.saver = invalid
    st.screen = invalid
    st.fonts = invalid
    ok = false
    try
        st.sg = CreateObject("roSGScreen")
        if st.sg <> invalid then
            st.sg.SetMessagePort(st.port)
            st.sgScene = st.sg.CreateScene("AsideScene")
            if st.sgScene <> invalid then
                st.sg.Show()
                st.sgScene.ObserveField("keyPressed", st.port)
                ok = true
            end if
        end if
    catch e
        print "b0r3d TV: couldn't step aside: "; e.message
    end try
    if not ok then
        CloseStandIn(st)
        if not ReopenScreen(st) then return
        SaveHandOver(st, "didn't work: the TV wouldn't show the stand-in screen, so Ruckus stayed (" + ClockText(st, false) + ")")
        st.mode = st.asideFrom
        StartSaver(st)
        return
    end if
    st.mode = "aside"
    st.asideTimer.Mark()
    print "b0r3d TV: stepped aside for the TV's own screensaver"
end sub

' Back from stepping aside: the SceneGraph screen closes and the 2D screen opens again.
sub ComeBack(st as object)
    if st.mode <> "aside" then return
    away = st.asideTimer.TotalSeconds()
    CloseStandIn(st)
    if not ReopenScreen(st) then return
    st.mode = st.asideFrom
    st.idle.Mark()
    st.handTried = false
    stamp = " (" + ClockText(st, false) + ")"
    if st.asideSaw then
        SaveHandOver(st, "worked: the TV's screensaver came on over b0r3d TV, and b0r3d TV came back after it" + stamp)
    else
        SaveHandOver(st, "a key brought b0r3d TV back after " + SecondsWord(away) + " aside; the TV's screensaver was due 5 s in" + stamp)
    end if
    Redraw(st)
end sub

' Closes the stand-in screen and keeps no hold on it, so the 2D screen can open again. (Not
' waiting for SceneGraph's "closed" message: in the simulator the display is gone by then. If
' the TV needs a moment, ReopenScreen tries again for 2 s.)
sub CloseStandIn(st as object)
    if st.sg = invalid then return
    if st.sgScene <> invalid then
        drift = st.sgScene.findNode("drift")
        if drift <> invalid then drift.control = "stop"
    end if
    st.sg.Close()
    st.sg = invalid
    st.sgScene = invalid
end sub

' Opens the 2D screen again, trying for up to 2 s. If it won't open, nothing is left to show a
' picture or hear a key, so b0r3d TV closes (Main's loop ends).
function ReopenScreen(st as object) as boolean
    for attempt = 1 to 10
        OpenScreen(st, RegRead("res", "fhd"))
        if st.screen <> invalid then return true
        sleep(200)
    end for
    SaveHandOver(st, "didn't work: the 2D screen wouldn't open again, so b0r3d TV closed (" + ClockText(st, false) + ")")
    st.quit = true
    return false
end function

' The TV says a screensaver just ended (EnableScreensaverExitedEvent). Someone pressed a key to
' end it, even if the TV kept that key for itself, so it counts as one.
sub ScreensaverEvent(st as object, info as dynamic)
    if not IsTrue(info, "exitedScreensaver") then return
    print "b0r3d TV: a screensaver ended (mode "; st.mode; ")"
    st.idle.Mark()
    st.handTried = false
    if st.mode = "aside" then
        st.asideSaw = true
        ComeBack(st)
        return
    end if
    ' the TV's own black screen came over b0r3d TV, so Ruckus didn't stay on past its time
    during = ""
    if st.mode = "saver" then during = ", while Ruckus was on"
    if st.mode = "hijack" then during = ", while Agent Ø's signal was on"
    RegWrite("blank", "came on over b0r3d TV and went" + during + " (" + ClockText(st, false) + ")")
    if st.mode = "saver" then
        StopSaver(st)
    else if st.mode = "hijack" then
        ' as a key: the signal ends, and from Ruckus so does he
        StopHijack(st, true)
    else
        ' Ruckus Cam goes straight back to its full frames
        if st.mode = "cam" and st.cam <> invalid then st.cam.blank = false
        Redraw(st)
    end if
end sub

function IsTrue(aa as dynamic, key as string) as boolean
    if type(aa) <> "roAssociativeArray" then return false
    v = aa.Lookup(key)
    if type(v) = "Boolean" or type(v) = "roBoolean" then return v
    return false
end function

sub SaveHandOver(st as object, text as string)
    RegWrite("handover", text)
    print "b0r3d TV hand-over: "; text
end sub

' --- The Screensaver check page (Settings) ---

function SaverCheckRows(st as object) as object
    return [
        { label: "Inside b0r3d TV", value: SaverPlan(st) },
        { label: "TV's saver wait", value: TvWaitAnswer(st) },
        { label: "Idle now", value: IdleAnswer(st) },
        { label: "Idle-clock reset", value: st.idleResult },
        { label: "Hand over", value: RegRead("handover", "not tried yet") },
        { label: "TV's black screen", value: RegRead("blank", "not seen yet") },
        { label: "Whole-TV Ruckus", value: TvSaverAnswer() }
    ]
end function

function SaverPlan(st as object) as string
    if st.saverPick = "off" then return "off: at the TV's screensaver time the screen goes black, as in any 2D app"
    tvTime = TvWaitSeconds(st)
    text = "Ruckus comes on after " + SecondsWord(SaverStartSeconds(st)) + " without a key"
    if tvTime <= 0 then return text + "; the TV's screensaver is off, so he stays"
    if st.saverPick = "keep" then return text + ", and b0r3d TV resets the TV's idle clock every 20 s so he stays (for up to 2 hours)"
    if st.saverPick = "hand" then return text + "; at " + SecondsWord(tvTime - 5) + " b0r3d TV steps aside, so the TV's own screensaver comes on at " + SecondsWord(tvTime)
    return text + "; at " + SecondsWord(tvTime) + " the TV turns the screen black"
end function

function TvWaitAnswer(st as object) as string
    if st.tvWait >= 0 then return MinutesWord(st.tvWait) + " (the TV says)"
    return "the TV doesn't say, so b0r3d TV goes by Settings: " + MinutesWord(st.tvWaitPref)
end function

function IdleAnswer(st as object) as string
    text = SecondsWord(st.idle.TotalSeconds()) + " since b0r3d TV's last key"
    tv = TvIdleSeconds(st)
    if tv >= 0 then text = text + "; the TV counts " + SecondsWord(tv)
    return text
end function

function TvSaverAnswer() as string
    last = RegRead("tvsaver", "")
    if last = "" then return "hasn't come on yet (pick b0r3d TV in the TV's Settings, Theme, Screensavers)"
    times = RegRead("tvsavers", "1").ToInt()
    if times = 1 then return "came on once, at " + last
    return "came on " + times.ToStr() + " times, last at " + last
end function

function SecondsWord(seconds as integer) as string
    mins = seconds \ 60
    secs = seconds mod 60
    if mins = 0 then return secs.ToStr() + " s"
    if secs = 0 then return mins.ToStr() + " min"
    return mins.ToStr() + " min " + secs.ToStr() + " s"
end function

' The item delta steps from current in a list, going round; the first when current isn't in it.
function NextIn(list as object, current as dynamic, delta as integer) as dynamic
    n = list.Count()
    i = -1
    for j = 0 to n - 1
        if list[j] = current then i = j
    end for
    if i < 0 then return list[0]
    return list[(i + delta + n) mod n]
end function

function InList(list as object, item as dynamic) as boolean
    for each v in list
        if v = item then return true
    end for
    return false
end function
