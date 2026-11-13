' The VCR on channel 3, where old VCRs played. It has a picture of its own, the same in every
' look: the VCR's blue screen with white letters and hard black shadows (VcrKit(0)'s colours,
' in Silkscreen), laid out on the 1280 x 720 grid (scaled by st.k). vcr.brs is the VCR menu
' look, a different thing, so everything here is called "deck".
'
' It comes with no tape. A tape is a video's address, put in with a deep link, Roku's usual way
' in: tape=<address> when b0r3d TV is launched (Main's args), or in an input event while it runs
' (the way TVFollow's hello comes). Roku's own video player (roVideoPlayer) plays it on the video
' plane behind the 2D screen, so while it plays the screen is cleared see-through. The tape isn't
' saved: it's gone when b0r3d TV closes, and without one the VCR says NO TAPE.
'
' The deck's states (st.deck.state): "none" (no tape), "load" (a tape starting), "play",
' "pause", "fail" (the tape didn't play) and "end" (the end of the tape).

function NewDeckState() as object
    return {
        tape: "",
        player: invalid,
        state: "none",
        sec: 0,
        osd: true,
        osdAuto: false,
        osdTimer: CreateObject("roTimespan"),
        blink: CreateObject("roTimespan"),
        lit: true,
        clockTimer: CreateObject("roTimespan")
    }
end function

' Turning the VCR on: its blue screen, and the tape plays if one is in.
sub OpenDeck(st as object)
    if st.deck = invalid then st.deck = NewDeckState()
    d = st.deck
    st.mode = "deck"
    d.clockTimer.Mark()
    d.blink.Mark()
    d.lit = true
    if d.tape = "" then
        print "b0r3d TV VCR: on, no tape"
        d.state = "none"
        Redraw(st)
        return
    end if
    print "b0r3d TV VCR: on, with a tape in"
    DeckPlay(st)
end sub

' Back to the guide: the tape stops and the player is let go. The tape stays in until b0r3d TV
' closes, so channel 3 plays it again when it's opened.
sub CloseDeck(st as object)
    d = st.deck
    if d <> invalid then
        if d.player <> invalid then d.player.Stop()
        d.player = invalid
        print "b0r3d TV VCR: off"
    end if
    st.mode = "guide"
    Redraw(st)
end sub

' Plays the tape from its start. The player is made the first time (one for the VCR, let go when
' it's turned off); for a new tape, or the same one again, it's stopped first.
sub DeckPlay(st as object)
    d = st.deck
    if d = invalid then return
    if d.tape = "" then return
    if d.player = invalid then
        try
            d.player = CreateObject("roVideoPlayer")
        catch e
            d.player = invalid
        end try
        if d.player <> invalid then
            d.player.SetMessagePort(st.port)
            d.player.SetDestinationRect(0, 0, st.w, st.h)
            d.player.SetPositionNotificationPeriod(1)
        end if
    end if
    d.sec = 0
    if d.player = invalid then
        print "b0r3d TV VCR: the TV gave no video player, so the tape failed"
        DeckFailed(st)
        return
    end if
    fmt = DeckFormat(d.tape)
    d.player.Stop()
    d.player.SetContentList([{ Stream: { Url: d.tape }, StreamFormat: fmt }])
    d.player.Play()
    d.state = "load"
    print "b0r3d TV VCR: play "; DeckLogUrl(d.tape); " as "; fmt
    DeckRedraw(st)
end sub

' Back stops the tape and goes back to the guide. OK or Play pauses and carries on, or plays the
' tape again after its end (or after it failed). The options key shows or hides the display over
' the picture. No other key does anything here.
sub DeckKey(st as object, code as integer)
    d = st.deck
    if d = invalid then return
    if code = 0 then
        CloseDeck(st)
    else if code = 6 or code = 13 then
        if d.state = "play" and d.player <> invalid then
            d.player.Pause()
            DeckPaused(st)
        else if d.state = "pause" and d.player <> invalid then
            d.player.Resume()
            DeckStarted(st)
        else if d.state = "end" or d.state = "fail" then
            DeckPlay(st)
        end if
    else if code = 10 then
        if d.state = "play" or d.state = "pause" then
            d.osd = not d.osd
            d.osdAuto = false
            Redraw(st)
        end if
    end if
end sub

' The player's messages: the tape starting, where it's got to (once a second), pausing and
' carrying on, its end and its failure. A message from a player that's been let go (the VCR was
' turned off) is ignored, and so is "playback was interrupted", which only follows a Stop.
sub DeckEvent(st as object, msg as object)
    d = st.deck
    if d = invalid then return
    if d.player = invalid then return
    if msg.isPlaybackPosition() then
        d.sec = msg.GetIndex()
        if d.state = "load" then
            DeckStarted(st)
        else if d.state = "play" and d.osd then
            ' the tape counter moves on
            DeckRedraw(st)
        end if
    else if msg.isStatusMessage() then
        if LCase(AsText(msg.GetMessage())) = "start of play" and d.state = "load" then DeckStarted(st)
    else if msg.isFullResult() then
        if d.state = "play" or d.state = "pause" or d.state = "load" then
            print "b0r3d TV VCR: end of tape at "; DeckCounterText(d.sec)
            d.state = "end"
            DeckStopped(st)
        end if
    else if msg.isRequestFailed() then
        print "b0r3d TV VCR: the tape failed ("; msg.GetIndex(); "): "; AsText(msg.GetMessage())
        DeckFailed(st)
    else if msg.isPaused() then
        if d.state = "play" then DeckPaused(st)
    else if msg.isResumed() then
        if d.state = "pause" then DeckStarted(st)
    end if
end sub

' Play starts, or carries on after a pause: the picture, with PLAY and the counter for 5 s.
sub DeckStarted(st as object)
    d = st.deck
    if d.state = "load" then print "b0r3d TV VCR: playing"
    d.state = "play"
    d.osd = true
    d.osdAuto = true
    d.osdTimer.Mark()
    DeckRedraw(st)
end sub

' Paused: PAUSE stays up over the picture until play carries on (or the options key hides it).
sub DeckPaused(st as object)
    d = st.deck
    d.state = "pause"
    d.osd = true
    d.osdAuto = false
    DeckRedraw(st)
end sub

sub DeckFailed(st as object)
    st.deck.state = "fail"
    DeckStopped(st)
end sub

' Back on the blue screen, its 12:00 starting a blink.
sub DeckStopped(st as object)
    d = st.deck
    d.lit = true
    d.blink.Mark()
    DeckRedraw(st)
end sub

' The player's messages can come while Rukus or Agent Ø's signal is over the VCR: the deck's
' screen is drawn only while it's the one up.
sub DeckRedraw(st as object)
    if st.mode = "deck" then Redraw(st)
end sub

' Main's loop calls this on every turn on channel 3: once a second the clock and the chores (the
' TV kept awake while a tape plays or starts; on the blue screen or paused, Rukus may come on, as
' in a paused game), the display going 5 s after play starts, and the 12:00 blinking.
sub DeckTurn(st as object)
    d = st.deck
    if d = invalid then return
    if d.clockTimer.TotalMilliseconds() >= 1000 then
        d.clockTimer.Mark()
        st.clock = ClockNow()
        if MediaChores(st, d.state = "play" or d.state = "load") then return
    end if
    if d.state = "play" and d.osdAuto and d.osdTimer.TotalMilliseconds() >= 5000 then
        d.osd = false
        d.osdAuto = false
        Redraw(st)
    else if DeckShowsClock(d) and d.blink.TotalMilliseconds() >= 500 then
        d.blink.Mark()
        d.lit = not d.lit
        Redraw(st)
    end if
end sub

' How long Main's loop waits on channel 3: until the 12:00's next blink on the blue screen, else
' a quarter of a second (for the display's 5 s and the chores; the player's messages wake the
' loop too).
function DeckWaitMs(st as object) as integer
    d = st.deck
    if d = invalid then return 500
    if not DeckShowsClock(d) then return 250
    due = 500 - d.blink.TotalMilliseconds()
    if due < 10 then return 10
    if due > 500 then return 500
    return due
end function

' Whether the deck is stopped, with the clock up: no tape, a tape that failed, or its end.
function DeckShowsClock(d as object) as boolean
    return d.state = "none" or d.state = "fail" or d.state = "end"
end function

function DeckFonts(st as object) as object
    key = st.look + "|" + Str(st.k)
    if st.deckFonts = invalid or st.deckFontsKey <> key then
        st.deckFonts = BuildDeckFonts(st)
        st.deckFontsKey = key
    end if
    return st.deckFonts
end function

' Silkscreen in every look, since the lettering is the VCR's own, not the look's. The sizes are
' multiples of 16 on the grid where they can be, so they land on Silkscreen's 8-pixel steps at
' both screen sizes.
function BuildDeckFonts(st as object) as object
    k = st.k
    return {
        osd: Silk(st, Z(32, k), true),
        big: Silk(st, Z(64, k), true),
        bar: Silk(st, Z(22, k), false),
        little: Silk(st, Z(16, k), true)
    }
end function

' 125 seconds into the tape as "SP 0:02:05", as a VCR's tape counter shows it.
function DeckCounterText(sec as integer) as string
    if sec < 0 then sec = 0
    hours = sec \ 3600
    mins = (sec mod 3600) \ 60
    secs = sec mod 60
    return "SP " + hours.ToStr() + ":" + TwoDigits(mins) + ":" + TwoDigits(secs)
end function

' What the middle of the blue screen says.
function DeckMiddleWord(d as object) as string
    if d.state = "load" then return "LOADING"
    if d.state = "fail" then return "NO SIGNAL"
    if d.state = "end" then return "END OF TAPE"
    return "NO TAPE"
end function

' The full screen on channel 3. While a tape plays or is paused, a see-through screen over the
' picture, with the display (PLAY or PAUSE and the counter) while it's up; otherwise the blue
' screen: the mode and the 12:00 (or the counter while a tape starts) along the top, a word in
' the middle, and a bar with the channel and the keys. No scanlines: the picture is the VCR's.
sub DrawDeckScreen(st as object)
    s = st.screen
    d = st.deck
    if s = invalid or d = invalid then return
    k = st.k
    kit = VcrKit(0)
    f = DeckFonts(st)
    sh = Z(3, k)
    lx = Z(56, k)
    rx = Z(1224, k)
    ty = Z(40, k)
    shape = Z(24, k)
    if d.state = "play" or d.state = "pause" then
        s.Clear(&h00000000)
        if d.osd then
            word = "PLAY"
            if d.state = "pause" then word = "PAUSE"
            DrawDeckMode(s, word, lx, ty, shape, f.osd, kit, sh)
            DrawShadowedRight(s, DeckCounterText(d.sec), rx, ty, kit.white, kit.shadow, f.osd, sh)
        end if
        s.SwapBuffers()
        return
    end if

    s.Clear(kit.bg)
    if d.state = "load" then
        DrawDeckMode(s, "PLAY", lx, ty, shape, f.osd, kit, sh)
        DrawShadowedRight(s, DeckCounterText(d.sec), rx, ty, kit.white, kit.shadow, f.osd, sh)
    else
        DrawDeckMode(s, "STOP", lx, ty, shape, f.osd, kit, sh)
        ' the VCR's clock, blinking 12:00 because nobody ever set it (not the real time)
        if d.lit then DrawShadowedRight(s, "12:00", rx, ty, kit.white, kit.shadow, f.osd, sh)
    end if
    middle = DeckMiddleWord(d)
    mw = f.big.GetOneLineWidth(middle, 99999)
    DrawShadowed(s, middle, (st.w - mw) \ 2, (st.h - f.big.GetOneLineHeight()) \ 2, kit.white, kit.shadow, f.big, Z(4, k))

    ruleY = Z(632, k)
    s.DrawRect(lx + sh, ruleY + sh, rx - lx, Z(3, k), kit.shadow)
    s.DrawRect(lx, ruleY, rx - lx, Z(3, k), kit.white)
    by = Z(644, k) + (Z(48, k) - f.bar.GetOneLineHeight()) \ 2
    ch = GameChannel("video.vcr")
    DrawShadowed(s, "CH " + ch.n + "  " + ch.sign, lx, by, kit.white, kit.shadow, f.bar, sh)
    keys = "[BACK] GUIDE"
    if d.state = "end" or d.state = "fail" then keys = "[OK] PLAY   [BACK] GUIDE"
    DrawShadowedRight(s, keys, rx, by, kit.white, kit.shadow, f.bar, sh)
    s.SwapBuffers()
end sub

' The mode, as a VCR shows it top left: the word, then its sign, each with the same hard shadow.
' size is the sign's height.
sub DrawDeckMode(s as object, word as string, x as integer, y as integer, size as integer, font as object, kit as object, sh as integer)
    DrawShadowed(s, word, x, y, kit.white, kit.shadow, font, sh)
    sx = x + font.GetOneLineWidth(word, 99999) + size \ 2
    cy = y + font.GetOneLineHeight() \ 2
    DrawDeckSign(s, word, sx + sh, cy + sh, size, kit.shadow)
    DrawDeckSign(s, word, sx, cy, size, kit.white)
end sub

' A mode's sign, drawn as shapes since the fonts may not have them: a triangle for PLAY, two bars
' for PAUSE and a square for STOP. Its left edge is at x, centred on cy.
sub DrawDeckSign(s as object, word as string, x as integer, cy as integer, size as integer, color as dynamic)
    if word = "PLAY" then
        DrawPlay(s, x, cy, size, color)
    else if word = "PAUSE" then
        barW = Int(size * 0.32 + 0.5)
        s.DrawRect(x, cy - size \ 2, barW, size, color)
        s.DrawRect(x + size - barW, cy - size \ 2, barW, size, color)
    else
        side = Int(size * 0.85 + 0.5)
        s.DrawRect(x, cy - side \ 2, side, side, color)
    end if
end sub

' The guide's preview box: a small copy of the blue screen, STOP with the blinking 12:00 and
' NO TAPE, or PLAY and TAPE IN when a tape is in (OK plays it). DemoWaitMs waits on
' st.deckDemoTimer, which is marked at each blink, so the box is drawn again every 500 ms.
sub DrawDeckPreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    k = st.k
    kit = VcrKit(0)
    f = DeckFonts(st)
    if st.deckDemoTimer = invalid then
        st.deckDemoTimer = CreateObject("roTimespan")
        st.deckDemoLit = true
    else if st.deckDemoTimer.TotalMilliseconds() >= 480 then
        st.deckDemoTimer.Mark()
        st.deckDemoLit = not st.deckDemoLit
    end if
    tapeIn = false
    if st.deck <> invalid then tapeIn = (st.deck.tape <> "")

    sh = Z(2, k)
    pad = Z(18, k)
    ty = y + Z(16, k)
    shape = Z(12, k)
    s.DrawRect(x, y, w, h, kit.bg)
    middle = "NO TAPE"
    if tapeIn then
        DrawDeckMode(s, "PLAY", x + pad, ty, shape, f.little, kit, sh)
        middle = "TAPE IN"
    else
        DrawDeckMode(s, "STOP", x + pad, ty, shape, f.little, kit, sh)
        if st.deckDemoLit then DrawShadowedRight(s, "12:00", x + w - pad, ty, kit.white, kit.shadow, f.little, sh)
    end if
    mw = f.osd.GetOneLineWidth(middle, 99999)
    DrawShadowed(s, middle, x + (w - mw) \ 2, y + (h - f.osd.GetOneLineHeight()) \ 2, kit.white, kit.shadow, f.osd, Z(3, k))
end sub

' --- Putting a tape in ---

' Whether a launch's or an input event's parameters carry a tape.
function DeckHasTape(params as dynamic) as boolean
    if type(params) <> "roAssociativeArray" then return false
    return params.DoesExist("tape")
end function

' A tape from a deep link, at launch or while b0r3d TV runs: the VCR comes on and plays it,
' whatever was on screen. Only an http:// or https:// address of up to 2000 characters goes in;
' anything else is left out, and the log says so.
sub DeckTakeTape(st as object, tape as dynamic)
    url = AsText(tape).Trim()
    if not DeckTapeOk(url) then
        print "b0r3d TV VCR: left out a tape that isn't an http:// or https:// address of up to 2000 characters"
        return
    end if
    ' what's over the screen goes first: the stand-in screen, Agent Ø's signal, Rukus
    if st.mode = "aside" then ComeBack(st)
    if st.screen = invalid then return
    if st.mode = "hijack" then StopHijack(st, true)
    if st.mode = "saver" then StopSaver(st)
    if st.deck = invalid then st.deck = NewDeckState()
    st.deck.tape = url
    if st.mode = "deck" then
        ' a new tape in the VCR that's on
        DeckPlay(st)
        return
    end if
    ' the channel that's open closes, as Back would close it
    if st.mode = "stuck" then CloseStuckPixels(st)
    if st.mode = "testpat" then CloseTestPattern(st)
    if st.mode = "snake" then CloseSnake(st)
    if st.mode = "fm" then CloseB0r3dFm(st)
    if st.mode = "promo" then ClosePromo(st)
    if st.mode = "cam" then CloseCam(st)
    ' then channel 3 comes on, as when OK opens it in the guide
    ch = GameChannel("video.vcr")
    i = IndexOfId(st.channels, ch.id)
    if i >= 0 then st.focus = i
    st.note = ""
    st.noteTimer = invalid
    RememberOpened(ch.id)
    TuneIn(st, ch)
    OpenDeck(st)
end sub

' An http:// or https:// address with something after it, of up to 2000 characters.
function DeckTapeOk(url as string) as boolean
    if Len(url) > 2000 then return false
    low = LCase(url)
    if Left(low, 7) = "http://" and Len(low) > 7 then return true
    if Left(low, 8) = "https://" and Len(low) > 8 then return true
    return false
end function

' "hls" when the address's path ends in .m3u8 (a stream's playlist, as from a PC), else "mp4".
function DeckFormat(url as string) as string
    path = url
    for each cut in ["?", "#"]
        p = Instr(1, path, cut)
        if p > 0 then path = Left(path, p - 1)
    end for
    if LCase(Right(path, 5)) = ".m3u8" then return "hls"
    return "mp4"
end function

' The address for the log, cut to 120 characters (it can be 2000).
function DeckLogUrl(url as string) as string
    if Len(url) <= 120 then return url
    return Left(url, 120) + "..."
end function
