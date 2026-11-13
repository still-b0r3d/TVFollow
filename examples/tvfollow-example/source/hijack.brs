' Agent Ø's signal hijack: now and then his transmission breaks into b0r3d TV. A burst of
' static, then a green terminal types out a few lines from him, and the screen it broke into
' comes back.
'   - From the guide: up up down down left right left right. Only the guide listens for it
'     (elsewhere those keys play the games and skip songs), and the key that completes it does
'     nothing else. (Paging keys make no code: two pages up and two down is just moving.)
'   - Now and then while Rukus is on: after he's been on two minutes, a 1-in-300 chance each
'     second, at most once each time he comes on. So it never comes before Rukus, never while
'     b0r3d TV has stepped aside for the TV's own screensaver (there's no screen then), and
'     never once the TV may have turned the screen black (nobody would see it, but the static
'     would be heard).
' Any key after the static ends it; from Rukus, a key goes all the way back to the screen he
' came over, as a key during Rukus does. Left alone, it ends 20 s after its last line. If the
' TV's screensaver ends while it's on, that counts as a key.

' His lines, in his own voice: lowercase, short, dry. They say nothing about him that isn't
' already known. VT323, the terminal's font, has the Ø.
function HijackTransmissions() as object
    return [
        [
            "AGENT Ø",
            "...",
            "you waited. you always do.",
            "this isn't the queue. it still feels like it.",
            "i cannot help you. i can only stay with you."
        ],
        [
            "AGENT Ø",
            "...",
            "sorry. wrong line.",
            "or the right one. hard to tell from down here.",
            "go back to your show. i'll hold."
        ],
        [
            "AGENT Ø",
            "...",
            "still up?",
            "so am i. somebody has to be.",
            "the falling blocks are on twenty. i'd start there."
        ]
    ]
end function

function NewHijack(st as object, returnMode as string) as object
    k = st.k
    picks = HijackTransmissions()
    lines = picks[Rnd(picks.Count()) - 1]
    total = 0
    for each line in lines
        total = total + Len(line)
    end for
    return {
        returnMode: returnMode,
        lines: lines,
        total: total,
        shown: 0,
        stage: "static",
        staticMs: 650,
        stageTimer: CreateObject("roTimespan"),
        doneTimer: invalid,
        blink: -1,
        font: Vt323(st, Z(30, k)),
        headerFont: Vt323(st, Z(36, k))
    }
end function

' The hijack takes over the screen it broke into (the guide, or Rukus).
sub StartHijack(st as object)
    if st.mode = "hijack" or st.screen = invalid then return
    st.hijack = NewHijack(st, st.mode)
    st.mode = "hijack"
    if st.sound and st.tuneSound <> invalid then st.tuneSound.Trigger(60)
    Redraw(st)
end sub

' Back to the screen it broke into. From Rukus with a key, all the way back to the screen he came
' over; left alone, back to Rukus.
sub StopHijack(st as object, byKey as boolean)
    back = "guide"
    if st.hijack <> invalid then back = st.hijack.returnMode
    st.hijack = invalid
    if back = "saver" and st.saver <> invalid then
        if byKey then
            st.mode = "saver"
            StopSaver(st)
            return
        end if
        st.mode = "saver"
        st.saver.frame.Mark()
        Redraw(st)
        return
    end if
    if back = "" or back = "hijack" or back = "saver" then back = "guide"
    st.mode = back
    Redraw(st)
end sub

function HijackWaitMs(st as object) as integer
    h = st.hijack
    if h = invalid then return 100
    if h.stage = "static" then return 33
    if h.shown < h.total then return 30
    return 100
end function

' Main's loop calls this on every turn while the hijack is on: the static, then the lines
' typed out a letter every 30 ms, the cursor blinking, and the end.
sub HijackTurn(st as object)
    h = st.hijack
    if h = invalid then return
    ' the screensaver's chores that don't bring Rukus on (the TV stays awake when that's the pick)
    MediaChores(st, true)
    if h.stage = "static" then
        if h.stageTimer.TotalMilliseconds() >= h.staticMs then
            h.stage = "terminal"
            h.stageTimer.Mark()
        end if
        Redraw(st)
        return
    end if
    shown = h.stageTimer.TotalMilliseconds() \ 30
    if shown > h.total then shown = h.total
    if shown >= h.total and h.doneTimer = invalid then h.doneTimer = CreateObject("roTimespan")
    if h.doneTimer <> invalid then
        if h.doneTimer.TotalMilliseconds() >= 20000 then
            StopHijack(st, false)
            return
        end if
    end if
    blink = (h.stageTimer.TotalMilliseconds() \ 400) mod 2
    if shown <> h.shown or blink <> h.blink then
        h.shown = shown
        h.blink = blink
        Redraw(st)
    end if
end sub

' Any key once the static is over ends the hijack.
sub HijackKey(st as object, code as integer)
    if st.hijack <> invalid and st.hijack.stage = "static" then return
    StopHijack(st, true)
end sub

' The guide's code (up up down down left right left right). Returns true when this key completed
' it (and started the hijack), so the key does nothing else.
function CheckHijackTrigger(st as object, code as integer) as boolean
    if st.keyBuffer = invalid then st.keyBuffer = []
    buf = st.keyBuffer
    buf.Push(code)
    if buf.Count() > 8 then buf.Shift()
    want = [2, 2, 3, 3, 4, 5, 4, 5]
    if buf.Count() < want.Count() then return false
    for i = 0 to want.Count() - 1
        if buf[i] <> want[i] then return false
    end for
    buf.Clear()
    StartHijack(st)
    return true
end function

' Now and then while Rukus is on inside b0r3d TV (see the top of this file). SaverTurn calls it
' on every turn; it looks once a second.
sub MaybeIdleHijack(st as object)
    sv = st.saver
    if sv = invalid or st.mode <> "saver" or st.screen = invalid then return
    if sv.hijacked then return
    if sv.hijackTimer.TotalMilliseconds() < 1000 then return
    sv.hijackTimer.Mark()
    if sv.onTimer.TotalSeconds() < 120 then return
    if TvMayBeBlank(st) then return
    if Rnd(300) <> 1 then return
    sv.hijacked = true
    StartHijack(st)
end sub

' Whether the TV may have turned the screen black by now (its own screensaver, which comes over a
' 2D app like this one as a black screen): the TV's idle count has reached its screensaver wait,
' less margin seconds. Agent Ø's signal keeps the 5 s margin, to be sure it isn't wasted on a
' dark TV; what's only saving drawing work asks with no margin, so a lit screen never slows.
' When the TV doesn't say its idle count, b0r3d TV's own count is used, unless the idle-clock
' reset is keeping the TV awake and has been seen to work. A TV with its screensaver off never
' turns black.
function TvMayBeBlank(st as object, margin = 5 as integer) as boolean
    tvTime = TvWaitSeconds(st)
    if tvTime <= 0 then return false
    tvIdle = TvIdleSeconds(st)
    if tvIdle >= 0 then return tvIdle >= tvTime - margin
    if st.saverPick = "keep" and st.canReset and st.keepWorks then
        if st.idle.TotalSeconds() < 7200 then return false
    end if
    return st.idle.TotalSeconds() >= tvTime - margin
end function

' --- Drawing ---

sub DrawHijack(st as object)
    s = st.screen
    h = st.hijack
    if s = invalid or h = invalid then return
    if h.stage = "static" then
        DrawHijackStatic(st, s)
        s.SwapBuffers()
        return
    end if
    k = st.k
    s.Clear(&h040704FF)
    glow = &h00FF66FF
    faint = &h00AA44FF
    halo = &h0A3318FF
    tx = Z(80, k)
    ty = Z(70, k)
    DrawGlow(s, "[ SIGNAL INTERRUPTED ]", tx, ty, glow, halo, h.headerFont, Z(2, k))
    ty = ty + h.headerFont.GetOneLineHeight() + Z(30, k)
    lineH = h.font.GetOneLineHeight() + Z(8, k)
    remaining = h.shown
    cursorX = tx
    cursorY = ty
    for each line in h.lines
        if remaining <= 0 then exit for
        part = line
        if remaining < Len(line) then part = Left(line, remaining)
        remaining = remaining - Len(line)
        color = glow
        if line = "..." then color = faint
        DrawGlow(s, part, tx, ty, color, halo, h.font, Z(1, k))
        cursorX = tx + h.font.GetOneLineWidth(part, 99999) + Z(6, k)
        cursorY = ty
        if remaining > 0 then
            ty = ty + lineH
            cursorX = tx
            cursorY = ty
        end if
    end for
    if h.shown >= h.total then
        ty = ty + 2 * lineH
        s.DrawText("[ ANY KEY RETURNS YOU ]", tx, ty, faint, h.font)
    end if
    ' the block cursor, blinking
    if h.blink = 0 then s.DrawRect(cursorX, cursorY + Z(4, k), Z(14, k), Z(24, k), &h88FFB0FF)
    if ScanlinesOn(st) then DrawScanlines(st, s)
    s.SwapBuffers()
end sub

' The static: rows of streaks in shades of the terminal's green, as when a channel tunes in.
sub DrawHijackStatic(st as object, s as object)
    levels = [&h000000FF, &h0B1A0DFF, &h215A2BFF, &h5CFF7AFF]
    rows = 36
    rowH = st.h \ rows
    unit = st.w \ 48
    for r = 0 to rows - 1
        y = r * rowH
        x = 0
        while x < st.w
            w = unit * Rnd(6)
            s.DrawRect(x, y, w, rowH, levels[Rnd(4) - 1])
            x = x + w
        end while
    end for
end sub
