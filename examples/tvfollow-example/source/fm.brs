' b0r3d FM, channel 22: the owner's songs streamed from b0r3d.download with Roku's own audio
' player (roAudioPlayer), with a bar visualizer and Ruckus, drawn in every look.
'
' The player keeps the whole song list and moves on by itself at the end of a song (and back to
' the first after the last). Skipping stops it, sets the song and plays again (Play starts at
' the current song; SetNext only picks what comes after it). Pause and Resume keep the place
' in the song. With no network every song fails: after three failures in a row the station
' stops and says so, and OK tries again.

' The songs, with the titles and lengths b0r3d 95's player and b0r3d.download use.
function FmTracks() as object
    return [
        { file: "Three_AM_Zoomies_Boom_Bap.mp3", title: "Three A.M. Zoomies (Boom Bap)", sec: 150 },
        { file: "Locked_and_Loafed.mp3", title: "Locked and Loafed", sec: 180 },
        { file: "Nine_Lives_Zero_Ping.mp3", title: "Nine Lives Zero Ping", sec: 131 },
        { file: "Chub_in_the_Machine.mp3", title: "Chub in the Machine", sec: 181 },
        { file: "Teal_Screen_Paradise.mp3", title: "Teal Screen Paradise", sec: 181 },
        { file: "Ruckus_Domain.mp3", title: "Ruckus Domain", sec: 110 },
        { file: "Ruckus_is_the_Sofa_King.mp3", title: "Ruckus is the Sofa King", sec: 156 },
        { file: "Spray_Bottle_Warfare.mp3", title: "Spray Bottle Warfare", sec: 183 },
        { file: "Breaded_and_Loaded.mp3", title: "Breaded and Loaded", sec: 152 },
        { file: "Dunk_and_Disorderly.mp3", title: "Dunk and Disorderly", sec: 149 },
        { file: "Bored_Dot_Org_synthwave.mp3", title: "Bored Dot Org synthwave", sec: 183 },
        { file: "Agent_Zero.mp3", title: "Agent Zero", sec: 182 },
        { file: "Midnight_On_The_Wire.mp3", title: "Midnight On The Wire", sec: 180 },
        { file: "PIXEL_SAW.mp3", title: "PIXEL SAW", sec: 108 },
        { file: "Chainsaw_Squirrel.mp3", title: "Chainsaw Squirrel", sec: 168 },
        { file: "Zombie_Crystal.mp3", title: "Zombie Crystal", sec: 182 }
    ]
end function

function NewFmState() as object
    return {
        tracks: FmTracks(),
        index: 0,
        player: invalid,
        playing: false,
        paused: false,
        buffering: false,
        fails: 0,
        failed: false,
        elapsedMs: 0,
        frameTimer: CreateObject("roTimespan"),
        bars: [12, 18, 25, 30, 22, 16, 28, 35, 40, 32, 24, 19, 27, 33, 38, 29, 21, 15, 26, 31, 36, 28, 20, 14],
        peaks: [12, 18, 25, 30, 22, 16, 28, 35, 40, 32, 24, 19, 27, 33, 38, 29, 21, 15, 26, 31, 36, 28, 20, 14],
        beat: 0
    }
end function

' Tuning in: the player gets the song list and plays the song the station was on.
sub OpenB0r3dFm(st as object)
    if st.fm = invalid then st.fm = NewFmState()
    fm = st.fm
    st.mode = "fm"
    fm.fails = 0
    if fm.player = invalid then
        fm.player = CreateObject("roAudioPlayer")
        if fm.player <> invalid then
            fm.player.SetMessagePort(st.port)
            content = []
            baseUrl = "https://b0r3d.download/media/b0r3d95/"
            for each t in fm.tracks
                content.Push({
                    url: baseUrl + t.file,
                    StreamFormat: "mp3",
                    Title: t.title,
                    Artist: "b0r3d Soundworks"
                })
            end for
            fm.player.SetContentList(content)
            fm.player.SetLoop(true)
        end if
    end if
    FmPlayTrack(st, fm.index)
end sub

sub CloseB0r3dFm(st as object)
    fm = st.fm
    if fm <> invalid then
        if fm.player <> invalid then fm.player.Stop()
        fm.player = invalid
        fm.playing = false
        fm.paused = false
        fm.buffering = false
    end if
    st.mode = "guide"
    Redraw(st)
end sub

' Plays song i from its start: the player is stopped first, since Play starts at the current
' song and SetNext only says which song comes next.
sub FmPlayTrack(st as object, i as integer)
    fm = st.fm
    if fm = invalid then return
    count = fm.tracks.Count()
    fm.index = ((i mod count) + count) mod count
    fm.elapsedMs = 0
    fm.paused = false
    fm.failed = false
    if fm.player = invalid then
        fm.playing = false
        fm.buffering = false
        fm.failed = true
        Redraw(st)
        return
    end if
    fm.player.Stop()
    fm.player.SetNext(fm.index)
    fm.player.Play()
    fm.playing = true
    fm.buffering = true
    Redraw(st)
end sub

sub FmKey(st as object, code as integer)
    fm = st.fm
    if fm = invalid then return

    ' Back returns to the guide
    if code = 0 then
        CloseB0r3dFm(st)
        return
    end if

    ' OK or Play: pause, carry on from the same spot, or (after the station stopped) try again
    if code = 6 or code = 13 then
        if fm.failed or fm.player = invalid then
            fm.fails = 0
            FmPlayTrack(st, fm.index)
        else if fm.paused then
            fm.player.Resume()
            fm.paused = false
            fm.playing = true
            Redraw(st)
        else if fm.playing then
            fm.player.Pause()
            fm.paused = true
            fm.playing = false
            Redraw(st)
        end if
        return
    end if

    ' Right, Fast-forward or Down: the next song; Left, Rewind or Up: the one before
    if code = 5 or code = 9 or code = 3 then
        fm.fails = 0
        FmPlayTrack(st, fm.index + 1)
    else if code = 4 or code = 8 or code = 2 then
        fm.fails = 0
        FmPlayTrack(st, fm.index - 1)
    end if
end sub

sub FmAudioEvent(st as object, msg as object)
    fm = st.fm
    ' a message left over from a player that's been let go (the station was closed) is ignored
    if fm = invalid then return
    if fm.player = invalid then return
    if msg.isListItemSelected() then
        ' a song starts, the player's own move at the end of the last one included
        fm.index = msg.GetIndex()
        fm.elapsedMs = 0
        Redraw(st)
    else if msg.isStatusMessage() then
        status = msg.GetMessage()
        if status = invalid then status = ""
        print "b0r3d FM status: "; status
        status = LCase(status)
        if status = "start of play" or status = "playing" then
            fm.buffering = false
            fm.fails = 0
        else if status = "buffering" or status = "startup progress" then
            fm.buffering = true
        else if status = "end of stream" or status = "end of playlist" then
            ' the player loops and moves on by itself
            fm.buffering = true
        end if
        Redraw(st)
    else if msg.isRequestFailed() then
        print "b0r3d FM: song "; msg.GetIndex(); " failed: "; msg.GetMessage()
        fm.fails = fm.fails + 1
        if fm.fails >= 3 then
            ' three in a row: most likely no network, so the station stops instead of going
            ' round the list for ever
            fm.player.Stop()
            fm.playing = false
            fm.paused = false
            fm.buffering = false
            fm.failed = true
            Redraw(st)
        else
            FmPlayTrack(st, fm.index + 1)
        end if
    end if
end sub

sub FmTurn(st as object)
    fm = st.fm
    if fm = invalid then return

    ' once a second: the clock, and the chores (the TV kept awake while it plays; paused, Ruckus
    ' may come on as in a paused game)
    if st.fmClock = invalid then st.fmClock = CreateObject("roTimespan")
    if st.fmClock.TotalMilliseconds() >= 1000 then
        st.fmClock.Mark()
        st.clock = ClockNow()
        if MediaChores(st, fm.playing) then return
    end if

    dt = fm.frameTimer.TotalMilliseconds()
    if dt < 33 then return
    fm.frameTimer.Mark()

    ' the song's time only runs while it really plays
    if fm.playing and not fm.buffering then fm.elapsedMs = fm.elapsedMs + dt

    ' the visualizer's bars
    if fm.playing and not fm.buffering then
        fm.beat = (fm.beat + 1) mod 30
        for b = 0 to fm.bars.Count() - 1
            target = Rnd(38) + 4
            if (b + fm.beat \ 4) mod 4 = 0 then target = target + 8
            if target > 48 then target = 48
            fm.bars[b] = Int(fm.bars[b] * 0.7 + target * 0.3)
            ' the peaks hold, then fall
            if fm.bars[b] > fm.peaks[b] then
                fm.peaks[b] = fm.bars[b]
            else if fm.peaks[b] > 2 then
                fm.peaks[b] = fm.peaks[b] - 1
            end if
        end for
    else
        ' paused or waiting: the bars settle
        for b = 0 to fm.bars.Count() - 1
            if fm.bars[b] > 4 then fm.bars[b] = fm.bars[b] - 2
            if fm.peaks[b] > 4 then fm.peaks[b] = fm.peaks[b] - 1
        end for
    end if

    Redraw(st)
end sub

function FmFonts(st as object) as object
    key = st.look + "|" + Str(st.k)
    if st.fmFonts = invalid or st.fmFontsKey <> key then
        st.fmFonts = BuildFmFonts(st)
        st.fmFontsKey = key
    end if
    return st.fmFonts
end function

function BuildFmFonts(st as object) as object
    k = st.k
    if st.look = "phosphor" then
        return {
            brand: Vt323(st, Z(36, k)),
            sub: Vt323(st, Z(24, k)),
            name: Vt323(st, Z(34, k)),
            now: Vt323(st, Z(24, k)),
            clock: Vt323(st, Z(30, k)),
            bar: Vt323(st, Z(24, k))
        }
    end if
    if st.look = "vcr" then
        return {
            brand: Silk(st, Z(28, k), true),
            sub: Silk(st, Z(18, k), false),
            name: Silk(st, Z(28, k), true),
            now: Silk(st, Z(18, k), false),
            clock: Silk(st, Z(26, k), true),
            bar: Silk(st, Z(18, k), false)
        }
    end if
    if st.look = "b95" then
        return {
            brand: W95(st, Z(24, k)),
            sub: W95(st, Z(18, k)),
            name: W95(st, Z(24, k)),
            now: W95(st, Z(18, k)),
            clock: W95(st, Z(20, k)),
            bar: W95(st, Z(18, k))
        }
    end if
    if st.look = "rgb" then
        return {
            brand: Barlow(st, "extrabold", Z(32, k)),
            sub: Barlow(st, "semibold", Z(18, k)),
            name: Barlow(st, "extrabold", Z(32, k)),
            now: Barlow(st, "medium", Z(22, k)),
            clock: Barlow(st, "semibold", Z(26, k)),
            bar: Barlow(st, "semibold", Z(18, k))
        }
    end if
    return {
        brand: Barlow(st, "extrabold", Z(34, k)),
        sub: Barlow(st, "semibold", Z(20, k)),
        name: Barlow(st, "bold", Z(30, k)),
        now: Barlow(st, "medium", Z(22, k)),
        clock: Vt323(st, Z(32, k)),
        bar: Barlow(st, "bold", Z(20, k))
    }
end function

' 125 seconds as "2:05".
function FmTimeText(sec as integer) as string
    if sec < 0 then sec = 0
    mins = sec \ 60
    secs = sec mod 60
    return mins.ToStr() + ":" + TwoDigits(secs)
end function

function FmKit(st as object) as object
    c = st.kit
    if st.look = "phosphor" then
        return {
            bg: c.bg,
            rule: c.dim,
            accent: c.full,
            well: c.bg,
            wellBorder: c.full,
            title: c.full,
            soft: c.dim,
            bar: c.dim,
            barFill: c.full,
            barTrack: c.lead,
            onAir: c.full,
            buffering: c.soft,
            paused: c.dim
        }
    end if
    if st.look = "vcr" then
        return {
            bg: c.bg,
            rule: c.white,
            accent: c.yellow,
            well: c.pip,
            wellBorder: c.white,
            title: c.white,
            soft: c.grey,
            bar: c.grey,
            barFill: c.yellow,
            barTrack: c.shadow,
            onAir: c.green,
            buffering: c.yellow,
            paused: &hFF4444FF
        }
    end if
    if st.look = "b95" then
        return {
            bg: c.bg,
            rule: c.shadow,
            accent: c.navy,
            well: c.black,
            wellBorder: c.shadow,
            title: c.black,
            soft: c.dark,
            bar: c.dark,
            barFill: c.navy,
            barTrack: c.face,
            onAir: c.white,
            buffering: c.white,
            paused: c.red
        }
    end if
    if st.look = "rgb" then
        return {
            bg: c.bg,
            rule: c.rule,
            accent: c.accent,
            well: c.well,
            wellBorder: c.frame,
            title: c.white,
            soft: c.soft,
            bar: c.dim,
            barFill: c.accent,
            barTrack: c.highlight,
            onAir: c.green,
            buffering: c.accent,
            paused: c.red
        }
    end if
    return {
        bg: c.bg,
        rule: c.frame,
        accent: c.accent,
        well: c.cell1,
        wellBorder: c.frame,
        title: c.white,
        soft: c.soft,
        bar: c.dimText,
        barFill: c.accent,
        barTrack: c.bar,
        onAir: &h5CFF7AFF,
        buffering: c.accent,
        paused: &hFF4444FF
    }
end function

' Main draw dispatcher for b0r3d FM
sub DrawFmScreen(st as object)
    s = st.screen
    k = st.k
    fm = st.fm
    if s = invalid or fm = invalid then return

    kit = FmKit(st)
    s.Clear(kit.bg)

    f = FmFonts(st)

    left = Z(48, k)
    right = Z(1232, k)
    w = right - left

    ' Header bar
    headY = Z(38, k)
    if st.look = "rgb" then
        DrawRgbLedBar(st, s, left, headY + Z(2, k), f.brand.GetOneLineHeight() - Z(6, k))
        s.DrawText("98.5 b0r3d FM  ·  STEREO", left + Z(22, k), headY, kit.title, f.brand)
    else if st.look = "phosphor" then
        DrawGlow(s, "98.5 B0R3D FM // HIGH FIDELITY STEREO", left, headY, kit.accent, st.kit.glow, f.brand, Z(1, k))
    else if st.look = "vcr" then
        DrawShadowed(s, "98.5 B0R3D FM STEREO", left, headY, kit.title, st.kit.shadow, f.brand, Z(3, k))
    else if st.look = "b95" then
        s.Clear(kit.bg)
        DrawB95Taskbar(st, "b0r3d FM")
        B95Text(s, "98.5 b0r3d FM - Stereo Broadcast", left, headY, kit.title, f.brand, k, true)
    else
        s.DrawText("98.5 b0r3d FM", left, headY, kit.title, f.brand)
        s.DrawText("STEREO BROADCAST", left, headY + Z(34, k), kit.soft, f.sub)
    end if

    ' the station's state, top right
    statusWord = "[ON AIR]"
    statusCol = kit.onAir
    if fm.failed then
        statusWord = "[NO SIGNAL]"
        statusCol = kit.paused
    else if fm.paused or not fm.playing then
        statusWord = "[PAUSED]"
        statusCol = kit.paused
    else if fm.buffering then
        statusWord = "[TUNING...]"
        statusCol = kit.buffering
    end if

    clockW = f.clock.GetOneLineWidth(statusWord, 99999)
    statusY = headY + Z(2, k)
    ' the RGB look's title is bigger than the word: the word sits on the title's baseline
    if st.look = "rgb" then statusY = headY + f.brand.GetAscent() - f.clock.GetAscent()
    s.DrawText(statusWord, right - clockW, statusY, statusCol, f.clock)

    s.DrawRect(left, Z(98, k), w, Z(2, k), kit.rule)

    ' Equalizer Well (Spectrum Analyzer)
    eqX = left
    eqY = Z(120, k)
    eqW = w
    eqH = Z(260, k)

    if st.look = "b95" then
        B95Sunken(s, eqX, eqY, eqW, eqH, Z(2, k), st.kit, kit.well)
    else if st.look = "rgb" then
        s.DrawRect(eqX, eqY, eqW, eqH, kit.well)
        DrawRgbEdge(st, s, eqX, eqY, eqW, eqH)
    else if st.look = "phosphor" then
        DrawFrame(s, eqX, eqY, eqW, eqH, Z(2, k), kit.wellBorder)
    else if st.look = "vcr" then
        s.DrawRect(eqX + Z(4, k), eqY + Z(4, k), eqW, eqH, st.kit.shadow)
        s.DrawRect(eqX, eqY, eqW, eqH, kit.wellBorder)
        b = Z(4, k)
        s.DrawRect(eqX + b, eqY + b, eqW - 2 * b, eqH - 2 * b, kit.well)
    else
        s.DrawRect(eqX, eqY, eqW, eqH, kit.well)
        DrawFrame(s, eqX, eqY, eqW, eqH, Z(2, k), kit.wellBorder)
    end if

    ' Draw the animated equalizer bars
    numBars = fm.bars.Count()
    barGap = Z(6, k)
    totalGaps = (numBars - 1) * barGap
    barW = (eqW - Z(64, k) - totalGaps) \ numBars
    barsStartX = eqX + Z(32, k)
    barsBaseY = eqY + eqH - Z(24, k)

    maxBarH = eqH - Z(48, k)

    for i = 0 to numBars - 1
        bx = barsStartX + i * (barW + barGap)
        val = fm.bars[i]
        bh = Int(maxBarH * (val / 48) + 0.5)
        if bh < Z(4, k) then bh = Z(4, k)
        by = barsBaseY - bh

        ' Color gradients across spectrum
        barCol = &h3388FFFF
        if i >= 16 then
            barCol = &hFF2A4DFF
        else if i >= 8 then
            barCol = &h2AFF66FF
        end if
        if st.look = "phosphor" then barCol = kit.accent
        if st.look = "vcr" then barCol = kit.accent

        s.DrawRect(bx, by, barW, bh, barCol)

        ' Peak hold indicator
        pVal = fm.peaks[i]
        ph = Int(maxBarH * (pVal / 48) + 0.5)
        py = barsBaseY - ph - Z(3, k)
        peakCol = &hFFFFFFFF
        if st.look = "phosphor" then peakCol = kit.accent
        s.DrawRect(bx, py, barW, Z(2, k), peakCol)
    end for

    ' Track Info Card
    track = fm.tracks[fm.index]
    infoY = eqY + eqH + Z(30, k)

    titlePrefix = TwoDigits(fm.index + 1) + ". "
    s.DrawText(titlePrefix + track.title, left, infoY, kit.title, f.name)

    elapsedSec = fm.elapsedMs \ 1000
    if elapsedSec > track.sec then elapsedSec = track.sec
    timeStr = FmTimeText(elapsedSec) + " / " + FmTimeText(track.sec)
    infoText = "Artist: b0r3d Soundworks   Track: " + timeStr
    if fm.failed then infoText = "The station can't be reached right now. OK tries again."
    s.DrawText(infoText, left, infoY + Z(44, k), kit.soft, f.now)

    ' Ruckus, sitting by the song, clear of the bars' frame above him
    sc = Z(7, k)
    rukusX = right - 16 * sc - Z(16, k)
    rukusY = infoY - Z(10, k)
    DrawSprite(s, RukusLoaf(), RukusColorsFor(st), rukusX, rukusY, sc)

    ' Track progress bar
    progY = infoY + Z(80, k)
    progW = w - Z(160, k)
    s.DrawRect(left, progY, progW, Z(6, k), kit.barTrack)
    fillW = Int(progW * (elapsedSec / track.sec) + 0.5)
    if fillW > progW then fillW = progW
    s.DrawRect(left, progY, fillW, Z(6, k), kit.barFill)

    ' Remote hints bar
    barY = Z(652, k)
    s.DrawRect(left, barY - Z(8, k), w, Z(2, k), kit.rule)
    hintText = "[OK/PLAY] PAUSE/RESUME   [<>] SKIP TRACK   [BACK] GUIDE"
    s.DrawText(hintText, left, barY, kit.bar, f.bar)
    DrawRight(s, "CH 22 - b0r3d FM", right, barY, kit.bar, f.bar)

    if ScanlinesOn(st) then DrawScanlines(st)
    s.SwapBuffers()
end sub

' Preview in the guide preview box when Channel 22 is highlighted
sub DrawFmPreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    k = st.k
    f = FmFonts(st)
    kit = FmKit(st)
    if st.fmDemoTimer = invalid then st.fmDemoTimer = CreateObject("roTimespan")
    if st.fmDemoTimer.TotalMilliseconds() >= 95 then st.fmDemoTimer.Mark()

    s.DrawRect(x, y, w, h, kit.well)
    DrawFrame(s, x, y, w, h, Z(2, k), kit.wellBorder)

    title = "98.5 b0r3d FM"
    tw = f.name.GetOneLineWidth(title, 99999)
    titleColor = kit.title
    subColor = kit.soft
    if st.look = "b95" then
        titleColor = st.kit.white
        subColor = st.kit.light
    end if
    s.DrawText(title, x + (w - tw) \ 2, y + Z(20, k), titleColor, f.name)

    subtitle = "LIVE RADIO BROADCAST"
    sw = f.sub.GetOneLineWidth(subtitle, 99999)
    s.DrawText(subtitle, x + (w - sw) \ 2, y + Z(60, k), subColor, f.sub)

    ' Mini visualizer bars
    num = 12
    gap = Z(4, k)
    barW = (w - Z(40, k) - (num - 1) * gap) \ num
    baseY = y + h - Z(24, k)
    for b = 0 to num - 1
        bx = x + Z(20, k) + b * (barW + gap)
        bh = Z(Rnd(32) + 6, k)
        col = &h2AFF66FF
        if b >= 8 then col = &hFF2A4DFF
        ' as on the full screen: the phosphor and VCR looks keep to their own colour
        if st.look = "phosphor" or st.look = "vcr" then col = kit.accent
        s.DrawRect(bx, baseY - bh, barW, bh, col)
    end for

    sc = Z(5, k)
    DrawSprite(s, RukusLoaf(), RukusColorsFor(st), x + w \ 2 - 8 * sc, baseY - Z(50, k), sc)
end sub
