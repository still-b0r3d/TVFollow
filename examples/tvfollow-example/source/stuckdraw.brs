' Stuck Pixels on screen, in the current look: the play screen (the well as a small TV in the
' middle, the numbers on the left, the next piece and the one on hold on small screens on the
' right, the remote's keys along the bottom) and the demo in the guide's preview box.
'
' Text is slow to draw on a Roku, so what stays put is drawn once into a picture of the whole
' screen (the chrome), the landed pixels into a picture of the well, and each number into a
' small picture of its own when it changes. A frame copies those and draws the moving pixels.
' Pictures are only changed before a frame starts drawing (Roku OS 16.3 refuses a change
' between drawing a picture and showing the frame). Laid out on the 1280 x 720 grid, scaled
' by st.k; the well's pixels sit 28 grid units apart.

' The game's colours in the look: one colour for every pixel, falling or landed; the stuck
' pixel's two flicker colours; the well's screen and its empty cells.
function StuckKit(st as object) as object
    c = st.kit
    if st.look = "phosphor" then
        return { wellBg: c.bg, empty: c.faint, block: c.soft, stuckOn: c.full, stuckOff: c.lead, mini: c.bg, flash: c.full, bannerBg: c.bg, bannerFg: c.full, noise: c.noise, light: 0, dark: 0, stuckLight: 0, stuckDark: 0 }
    end if
    if st.look = "vcr" then
        wells = [&h0B1A7AFF, &h0E0E0EFF, &h06401FFF]
        empties = [&h1A2A9AFF, &h1C1C1CFF, &h0D5530FF]
        offs = [&h535A86FF, &h554F28FF, &h497A37FF]
        i = st.colour
        return { wellBg: wells[i], empty: empties[i], block: c.white, stuckOn: c.yellow, stuckOff: offs[i], mini: wells[i], flash: c.white, bannerBg: c.shadow, bannerFg: c.yellow, noise: c.noise, light: 0, dark: 0, stuckLight: 0, stuckDark: 0 }
    end if
    if st.look = "b95" then
        return { wellBg: &h808080FF, empty: &h000000FF, block: &h000080FF, stuckOn: &hFFFF00FF, stuckOff: &h404000FF, mini: &h000000FF, flash: &hFFFFFFFF, bannerBg: c.navy, bannerFg: c.white, noise: c.noise, light: &h5A5AE0FF, dark: &h00004AFF, stuckLight: &hFFFFC0FF, stuckDark: &h8A8A00FF }
    end if
    ' each look has its own branch: a kit only has the colour names its own look uses
    if st.look = "rgb" then
        return { wellBg: c.well, empty: c.track, block: c.blue, stuckOn: c.accent, stuckOff: c.dim, mini: c.well, flash: c.white, bannerBg: c.card, bannerFg: c.accent, noise: c.noise, light: 0, dark: 0, stuckLight: 0, stuckDark: 0 }
    end if
    return { wellBg: &h050B22FF, empty: &h0B1740FF, block: &h8FB4FFFF, stuckOn: c.accent, stuckOff: &h484640FF, mini: &h050B22FF, flash: c.white, bannerBg: c.ink, bannerFg: c.accent, noise: c.noise, light: 0, dark: 0, stuckLight: 0, stuckDark: 0, cabinet: &h1F3170FF, bezel: &h03081AFF }
end function

' The play screen's lettering in the look, loaded once a run.
function StuckFonts(st as object) as object
    k = st.k
    if st.look = "phosphor" then
        return { title: Vt323(st, Z(40, k)), line: Vt323(st, Z(28, k)), label: Vt323(st, Z(30, k)), value: Vt323(st, Z(30, k)), box: Vt323(st, Z(26, k)), banner: Vt323(st, Z(60, k)), bar: Vt323(st, Z(26, k)), small: Vt323(st, Z(24, k)) }
    end if
    if st.look = "vcr" then
        return { title: Silk(st, Z(32, k), true), line: Silk(st, Z(16, k), false), label: Silk(st, Z(16, k), false), value: Silk(st, Z(32, k), true), box: Silk(st, Z(16, k), false), banner: Silk(st, Z(40, k), true), bar: Silk(st, Z(16, k), false), small: Silk(st, Z(16, k), false) }
    end if
    if st.look = "b95" then
        return { title: W95(st, Z(26, k)), line: W95(st, Z(19, k)), label: W95(st, Z(21, k)), value: W95(st, Z(21, k)), box: W95(st, Z(19, k)), banner: W95(st, Z(36, k)), bar: W95(st, Z(18, k)), small: W95(st, Z(18, k)) }
    end if
    if st.look = "rgb" then
        return { title: Barlow(st, "extrabold", Z(34, k)), line: Barlow(st, "semibold", Z(22, k)), label: Barlow(st, "semibold", Z(19, k)), value: Barlow(st, "bold", Z(32, k)), box: Barlow(st, "semibold", Z(19, k)), banner: Barlow(st, "extrabold", Z(46, k)), bar: Barlow(st, "semibold", Z(18, k)), small: Barlow(st, "semibold", Z(19, k)) }
    end if
    return { title: Barlow(st, "extrabold", Z(44, k)), line: Barlow(st, "semibold", Z(22, k)), label: Barlow(st, "bold", Z(20, k)), value: Vt323(st, Z(44, k)), box: Barlow(st, "bold", Z(20, k)), banner: Barlow(st, "extrabold", Z(48, k)), bar: Barlow(st, "bold", Z(21, k)), small: Vt323(st, Z(26, k)), chin: Barlow(st, "extrabold", Z(22, k)) }
end function

' --- Text in the look's way: glowing on the phosphor look (its full colour only), with a hard
' shadow on the VCR look, W95FA's way on b0r3d 95. ---

sub GText(st as object, s as object, text as string, x as integer, y as integer, font as object, color as dynamic, bold as boolean)
    k = st.k
    if st.look = "phosphor" then
        if color = st.kit.full then
            DrawGlow(s, text, x, y, color, st.kit.glow, font, Z(1, k))
        else
            s.DrawText(text, x, y, color, font)
        end if
    else if st.look = "vcr" then
        DrawShadowed(s, text, x, y, color, st.kit.shadow, font, Z(3, k))
    else if st.look = "b95" then
        B95Text(s, text, x, y, color, font, k, bold)
    else
        s.DrawText(text, x, y, color, font)
    end if
end sub

function GTextWidth(st as object, text as string, font as object, bold as boolean) as integer
    if st.look = "b95" then return B95Width(text, font, st.k, bold)
    return font.GetOneLineWidth(text, 99999)
end function

' One line of text as a picture filled with the colour it will sit on, so the lettering's soft
' edges are worked out once, against that colour (as the screensaver's clock is). It has room
' around the text for a glow or a shadow. Invalid when there's no memory for it (the number then
' just isn't shown).
function TextPicture(st as object, text as string, font as object, color as dynamic, bg as dynamic, bold as boolean) as dynamic
    k = st.k
    pad = Z(2, k)
    ' the VCR look's shadow reaches 3 to the right and down, the phosphor glow 1 to the right
    extra = 0
    down = 0
    if st.look = "vcr" then
        extra = Z(3, k)
        down = extra
    else if st.look = "phosphor" then
        extra = Z(1, k)
    end if
    w = GTextWidth(st, text, font, bold) + 2 * pad + extra
    h = font.GetOneLineHeight() + 2 * pad + down
    if w < 1 then w = 1
    bmp = NewPicture(w, h, true)
    if bmp = invalid then return invalid
    bmp.Clear(bg)
    GText(st, bmp, text, pad, pad, font, color, bold)
    return { bmp: bmp, w: w, h: h, pad: pad, extra: extra, text: text }
end function

' A text picture drawn so its text starts at x (align "l"), ends at x ("r") or is centred on x
' ("c"), with its top at y.
sub DrawPic(s as object, pic as dynamic, x as integer, y as integer, align as string)
    if pic = invalid then return
    if align = "r" then
        s.DrawObject(x - pic.w + pic.pad + pic.extra, y - pic.pad, pic.bmp)
    else if align = "c" then
        s.DrawObject(x - (pic.w - pic.extra) \ 2, y - pic.pad, pic.bmp)
    else
        s.DrawObject(x - pic.pad, y - pic.pad, pic.bmp)
    end if
end sub

' --- The pictures a run is drawn from ---

' The look's pictures: the pixel tiles (and the small ones for the next and hold screens) and
' the chrome. Made again when the look, its colour, the screen size, the scanlines or the mode
' change, and on b0r3d 95 when the channels do (its desktop shows the inputs' icons). If the TV
' runs out of picture memory, what couldn't be made is drawn straight onto the screen instead.
function StuckArt(st as object) as object
    sp = st.sp
    key = st.look + "|" + st.colour.ToStr() + "|" + st.w.ToStr() + "|" + OnOff(st.scan) + "|" + sp.g.mode
    if st.look = "b95" then key = key + "|" + st.channels.Count().ToStr()
    if sp.art <> invalid then
        if sp.art.key = key then
            if st.look = "b95" and sp.art.chrome <> invalid and sp.art.clockText <> ClockText(st, false) then RedrawB95Clock(st, sp.art)
            return sp.art
        end if
    end if
    ' the old pictures go first, so the TV never holds two whole-screen pictures at once
    sp.art = invalid
    k = st.k
    a = { key: key, kit: StuckKit(st), fonts: StuckFonts(st), vals: {}, banners: {}, lastBanner: invalid, special: [], stack: invalid, fpsPic: invalid }
    a.P = Z(28, k)
    a.B = Z(26, k)
    a.mP = Z(22, k)
    a.mB = Z(20, k)
    a.tiles = StuckTiles(st, a.kit, a.B, true)
    a.minis = StuckTiles(st, a.kit, a.mB, false)
    chrome = NewPicture(st.w, st.h, true)
    if chrome <> invalid then
        keep = st.screen
        st.screen = chrome
        layout = StuckChromeFor(st, chrome, a)
        st.screen = keep
    else
        ' drawn on the screen every frame instead: slower, but the game goes on
        layout = StuckChromeFor(st, st.screen, a)
    end if
    a.chrome = chrome
    a.clockText = ClockText(st, false)
    a.wellX = Z(layout.wellX, k)
    a.wellY = Z(layout.wellY, k)
    a.slots = layout.slots
    a.nextBox = layout.nextBox
    a.holdBox = layout.holdBox
    a.fpsSlot = layout.fps
    a.scanTile = invalid
    if st.look = "phosphor" and st.scan then a.scanTile = ScanTile(st, sp.g.cols * a.P, sp.g.rows * a.P)
    sp.art = a
    sp.stackDirty = true
    return a
end function

function StuckChromeFor(st as object, s as object, a as object) as object
    if st.look = "b95" then return StuckChromeB95(st, s, a)
    if st.look = "phosphor" then return StuckChromePhosphor(st, s, a)
    if st.look = "vcr" then return StuckChromeVcr(st, s, a)
    if st.look = "rgb" then return StuckChromeRgb(st, s, a)
    return StuckChromeCable(st, s, a)
end function

' A pixel tile of size pixels: a block, the stuck pixel lit and dimmed, an empty cell and four
' frames of static. On b0r3d 95 the pixels have a raised edge. Each keeps its colour too, for
' drawing it as a plain square when there was no memory for the tile.
function StuckTiles(st as object, kit as object, size as integer, withStatic as boolean) as object
    tiles = {
        size: size,
        block: MakeTile(st, size, kit.block, kit.light, kit.dark),
        stuckOn: MakeTile(st, size, kit.stuckOn, kit.stuckLight, kit.stuckDark),
        stuckOff: MakeTile(st, size, kit.stuckOff, 0, 0),
        empty: MakeTile(st, size, kit.empty, 0, 0),
        noise: [],
        colors: { block: kit.block, stuckOn: kit.stuckOn, stuckOff: kit.stuckOff, empty: kit.empty, noise: kit.noise[2] }
    }
    if withStatic then
        for i = 1 to 4
            tiles.noise.Push(NoiseTile(size, kit.noise))
        end for
    end if
    return tiles
end function

' One tile at x, y, or a plain square of its colour when the tile couldn't be made.
sub DrawTile(s as object, tiles as object, name as string, tile as dynamic, x as integer, y as integer)
    if tile <> invalid then
        s.DrawObject(x, y, tile)
    else
        s.DrawRect(x, y, tiles.size, tiles.size, tiles.colors[name])
    end if
end sub

function MakeTile(st as object, size as integer, color as dynamic, light as dynamic, dark as dynamic) as dynamic
    bmp = NewPicture(size, size, false)
    if bmp = invalid then return invalid
    bmp.Clear(color)
    if light <> 0 then
        e = Z(2, st.k)
        bmp.DrawRect(0, 0, size, e, light)
        bmp.DrawRect(0, 0, e, size, light)
        bmp.DrawRect(0, size - e, size, e, dark)
        bmp.DrawRect(size - e, 0, e, size, dark)
    end if
    return bmp
end function

' A square of TV static: grains in the look's four noise shades.
function NoiseTile(size as integer, noise as object) as dynamic
    bmp = NewPicture(size, size, false)
    if bmp = invalid then return invalid
    grain = size \ 8
    if grain < 2 then grain = 2
    for y = 0 to size - 1 step grain
        for x = 0 to size - 1 step grain
            bmp.DrawRect(x, y, grain, grain, noise[Rnd(4) - 1])
        end for
    end for
    return bmp
end function

' The landed pixels: a picture of the well, made again whenever a piece sets or rows clear.
' Stuck pixels and static move, so they are kept in a list and drawn over it every frame. With
' no memory for the picture, the well is drawn straight onto the screen each frame instead.
sub BuildStuckStack(st as object, a as object, g as object)
    if a.stack = invalid then a.stack = NewPicture(g.cols * a.P, g.rows * a.P, false)
    a.special = []
    for r = 0 to g.rows - 1
        for c = 0 to g.cols - 1
            if g.board[r][c] >= 2 then a.special.Push([r, c, g.board[r][c]])
        end for
    end for
    if a.stack <> invalid then DrawStuckWell(st, a, g, a.stack, 0, 0)
end sub

' The well's screen, its empty cells and the landed pixels, with the well's top left at x, y.
sub DrawStuckWell(st as object, a as object, g as object, s as object, x as integer, y as integer)
    t = a.tiles
    s.DrawRect(x, y, g.cols * a.P, g.rows * a.P, a.kit.wellBg)
    for r = 0 to g.rows - 1
        row = g.board[r]
        ry = y + r * a.P
        for c = 0 to g.cols - 1
            if row[c] = 1 then
                DrawTile(s, t, "block", t.block, x + c * a.P, ry)
            else
                DrawTile(s, t, "empty", t.empty, x + c * a.P, ry)
            end if
        end for
    end for
end sub

function StuckSlotNames() as object
    return ["score", "level", "lines", "time", "stuck", "fixed", "best"]
end function

function StuckSlotLabels(mode as string) as object
    lines = "LINES"
    if mode = "sprint" then lines = "LEFT"
    return ["SCORE", "LEVEL", lines, "TIME", "STUCK", "FIXED", "BEST"]
end function

' What each number reads now.
function StuckValueTexts(st as object, g as object) as object
    lines = g.lines
    if g.mode = "sprint" then
        lines = g.rules.sprintLines - g.lines
        if lines < 0 then lines = 0
    end if
    linesText = lines.ToStr()
    if lines < 100 then linesText = Right("00" + linesText, 3)
    return {
        score: GroupDigits(g.score),
        level: TwoDigits(g.level),
        lines: linesText,
        time: TimeWord(g.elapsed),
        stuck: TwoDigits(g.stuckNow),
        fixed: TwoDigits(g.fixed),
        best: st.sp.bestText
    }
end function

sub UpdateStuckValues(st as object, a as object, g as object)
    texts = StuckValueTexts(st, g)
    for each name in texts
        text = texts[name]
        slot = a.slots[name]
        if slot <> invalid then
            old = a.vals[name]
            fresh = (old = invalid)
            if not fresh then fresh = (old.text <> text)
            if fresh then
                bg = slot.bg
                if st.look = "phosphor" and st.scan then bg = &h00000000
                a.vals[name] = TextPicture(st, text, slot.font, slot.color, bg, false)
            end if
        end if
    end for
end sub

' The frames-per-second counter test builds carry, so speed can be read on the TV.
sub UpdateStuckFps(st as object, a as object)
    if not ShowsFps() or a.fpsSlot = invalid then return
    f = st.sp.fps
    ms = f.timer.TotalMilliseconds()
    if ms < 1000 then return
    shown = 0
    if ms > 0 then shown = Int(f.frames * 1000 / ms + 0.5)
    f.frames = 0
    f.timer.Mark()
    slot = a.fpsSlot
    bg = slot.bg
    if st.look = "phosphor" and st.scan then bg = &h00000000
    a.fpsPic = TextPicture(st, shown.ToStr() + " FPS", slot.font, slot.color, bg, false)
end sub

' --- A frame ---

sub DrawStuckPlay(st as object)
    s = st.screen
    sp = st.sp
    if s = invalid or sp.g = invalid then return
    g = sp.g
    a = StuckArt(st)
    k = st.k
    ' every picture is brought up to date first...
    if sp.stackDirty then
        BuildStuckStack(st, a, g)
        sp.stackDirty = false
    end if
    UpdateStuckValues(st, a, g)
    UpdateStuckFps(st, a)
    banner = StuckBannerPic(st, a)
    ' ...then the frame is drawn from them. Everything up to the numbers is solid, so it's
    ' copied without blending, which is quicker on a Roku; blending is back on for the flashes,
    ' the scanlines and anything drawn as text.
    if a.chrome <> invalid then
        s.SetAlphaEnable(false)
        s.DrawObject(0, 0, a.chrome)
    else
        StuckChromeFor(st, s, a)
        s.SetAlphaEnable(false)
    end if
    wx = a.wellX
    wy = a.wellY
    if a.stack <> invalid then
        s.DrawObject(wx, wy, a.stack)
    else
        DrawStuckWell(st, a, g, s, wx, wy)
    end if
    t = a.tiles
    now = sp.clock.TotalMilliseconds()
    lit = (now \ 450) mod 2 = 0
    stuckName = "stuckOff"
    stuckTile = t.stuckOff
    if lit then
        stuckName = "stuckOn"
        stuckTile = t.stuckOn
    end if
    noiseStep = now \ 80
    for each cell in a.special
        x = wx + cell[1] * a.P
        y = wy + cell[0] * a.P
        if cell[2] = 2 then
            DrawTile(s, t, stuckName, stuckTile, x, y)
        else
            DrawTile(s, t, "noise", t.noise[(noiseStep + cell[0] * 3 + cell[1] * 5) mod 4], x, y)
        end if
    end for
    ' a new level's banner goes under the falling piece, so the piece never goes out of sight
    bannerX = wx + (g.cols * a.P) \ 2
    bannerY = 0
    if banner <> invalid then
        bannerY = wy + (g.rows * a.P - banner.h) \ 2 + banner.pad
        if sp.view <> "signal" then DrawPic(s, banner, bannerX, bannerY, "c")
    end if
    if g.state = "play" then
        cells = SpCells(g)
        for i = 0 to cells.Count() - 1
            r = cells[i][0]
            if r >= 0 then
                if i = g.cur.stuck then
                    DrawTile(s, t, stuckName, stuckTile, wx + cells[i][1] * a.P, wy + r * a.P)
                else
                    DrawTile(s, t, "block", t.block, wx + cells[i][1] * a.P, wy + r * a.P)
                end if
            end if
        end for
    end if
    DrawStuckMini(st, a, g.upcoming, a.nextBox, lit)
    DrawStuckMini(st, a, g.held, a.holdBox, lit)
    s.SetAlphaEnable(true)
    for each name in StuckSlotNames()
        slot = a.slots[name]
        if slot <> invalid then DrawPic(s, a.vals[name], Z(slot.x, k), Z(slot.y, k), slot.align)
    end for
    DrawStuckFx(st, a, wx, wy)
    if sp.view = "signal" then
        DrawStuckSignal(st, a, wx, wy, g)
        ' "NO SIGNAL" goes over the static
        if banner <> invalid then DrawPic(s, banner, bannerX, bannerY, "c")
    end if
    if a.scanTile <> invalid then s.DrawObject(wx, wy, a.scanTile)
    if a.fpsPic <> invalid then DrawPic(s, a.fpsPic, Z(a.fpsSlot.x, k), Z(a.fpsSlot.y, k), a.fpsSlot.align)
    s.SwapBuffers()
    sp.fps.frames = sp.fps.frames + 1
end sub

' The next piece or the one on hold, centred on its small screen ([x, y, w, h] in pixels).
sub DrawStuckMini(st as object, a as object, piece as dynamic, area as object, lit as boolean)
    if piece = invalid then return
    s = st.screen
    shape = st.sp.g.shapes[piece.p]
    cells = shape.cells[0]
    minR = 99
    maxR = -1
    minC = 99
    maxC = -1
    for each cell in cells
        if cell[0] < minR then minR = cell[0]
        if cell[0] > maxR then maxR = cell[0]
        if cell[1] < minC then minC = cell[1]
        if cell[1] > maxC then maxC = cell[1]
    end for
    pw = (maxC - minC + 1) * a.mP - (a.mP - a.mB)
    ph = (maxR - minR + 1) * a.mP - (a.mP - a.mB)
    ox = area[0] + (area[2] - pw) \ 2 - minC * a.mP
    oy = area[1] + (area[3] - ph) \ 2 - minR * a.mP
    t = a.minis
    for i = 0 to cells.Count() - 1
        x = ox + cells[i][1] * a.mP
        y = oy + cells[i][0] * a.mP
        if i = piece.stuck and lit then
            DrawTile(s, t, "stuckOn", t.stuckOn, x, y)
        else if i = piece.stuck then
            DrawTile(s, t, "stuckOff", t.stuckOff, x, y)
        else
            DrawTile(s, t, "block", t.block, x, y)
        end if
    end for
end sub

' Flashes where rows cleared, sparks where stuck pixels popped. Finished ones are let go.
sub DrawStuckFx(st as object, a as object, wx as integer, wy as integer)
    sp = st.sp
    if sp.fx.Count() = 0 then return
    s = st.screen
    k = st.k
    g = sp.g
    kept = []
    for each fx in sp.fx
        t = fx.t.TotalMilliseconds()
        if fx.kind = "flash" and t < 180 then
            kept.Push(fx)
            alpha = Int(170 * (1 - t / 180))
            color = (a.kit.flash and &hFFFFFF00) + alpha
            for each r in fx.rows
                s.DrawRect(wx, wy + r * a.P, g.cols * a.P - (a.P - a.B), a.B, color)
            end for
        else if fx.kind = "pop" and t < 500 then
            kept.Push(fx)
            p = t / 500
            spread = Z(4 + 60 * p, k)
            size = Z(8 * (1 - p) + 2, k)
            for each cell in fx.cells
                cx = wx + cell[1] * a.P + a.B \ 2
                cy = wy + cell[0] * a.P + a.B \ 2
                for each d in [[-1, -0.6], [1, -0.6], [-0.4, -1.2], [0.4, -1.2]]
                    s.DrawRect(cx + Int(d[0] * spread) - size \ 2, cy + Int(d[1] * spread) - size \ 2, size, size, a.kit.stuckOn)
                end for
            end for
        else if fx.kind = "banner" and t < 1600 then
            kept.Push(fx)
        end if
    end for
    sp.fx = kept
end sub

' The level-up banner, as a picture (made once per text), or invalid.
function StuckBannerPic(st as object, a as object) as dynamic
    sp = st.sp
    text = ""
    for each fx in sp.fx
        if fx.kind = "banner" then
            t = fx.t.TotalMilliseconds()
            if t < 1600 and (t > 600 or (t \ 150) mod 2 = 0) then text = fx.text
        end if
    end for
    if sp.view = "signal" then
        t = sp.signal.TotalMilliseconds()
        text = ""
        if sp.signalHow = "over" and t > 650 then text = "NO SIGNAL"
        if sp.signalHow = "done" and t > 300 then
            text = "ALL CLEAR"
            if sp.g.mode = "sprint" then text = TimeWord(sp.g.elapsed)
        end if
    end if
    if text = "" then return invalid
    ' a level's words (and a sprint's time) are new each time: they have one place, which the
    ' next one takes, so a long run doesn't pile up pictures
    if text <> "NO SIGNAL" and text <> "ALL CLEAR" then
        if a.lastBanner <> invalid then
            if a.lastBanner.text = text then return a.lastBanner.pic
        end if
        a.lastBanner = invalid
        pic = TextPicture(st, " " + text + " ", a.fonts.banner, a.kit.bannerFg, a.kit.bannerBg, true)
        if pic <> invalid then a.lastBanner = { text: text, pic: pic }
        return pic
    end if
    pic = a.banners[text]
    if pic = invalid then
        pic = TextPicture(st, " " + text + " ", a.fonts.banner, a.kit.bannerFg, a.kit.bannerBg, true)
        a.banners[text] = pic
    end if
    return pic
end function

' The end of a run. Topped out: the well's screen loses its signal, static rolling down over it.
' Finished: the screen flashes.
sub DrawStuckSignal(st as object, a as object, wx as integer, wy as integer, g as object)
    s = st.screen
    sp = st.sp
    t = sp.signal.TotalMilliseconds()
    w = g.cols * a.P - (a.P - a.B)
    h = g.rows * a.P - (a.P - a.B)
    if sp.signalHow = "over" then
        p = t / 400
        if p > 1 then p = 1
        bottom = wy + Int(h * p)
        rows = 36
        rowH = h \ rows
        if rowH < 1 then rowH = 1
        unit = w \ 24
        if unit < 1 then unit = 1
        levels = a.kit.noise
        y = wy
        while y < bottom
            x = wx
            while x < wx + w
                part = unit * Rnd(6)
                if x + part > wx + w then part = wx + w - x
                s.DrawRect(x, y, part, rowH, levels[Rnd(4) - 1])
                x = x + part
            end while
            y = y + rowH
        end while
    else if t < 900 and (t \ 150) mod 2 = 0 then
        s.DrawRect(wx, wy, w, h, (a.kit.flash and &hFFFFFF00) + 150)
    end if
end sub

' --- The chrome, look by look. Each draws onto s (st.screen points at it while it draws) and
' returns where the moving parts go: wellX, wellY (grid units), the next and hold screens
' ([x, y, w, h] in pixels), and slots for the numbers (grid units). ---

function StuckModeLine(st as object) as string
    sp = st.sp
    mode = sp.g.mode
    dot = " · "
    if st.look = "phosphor" then dot = " :: "
    if mode = "sprint" then return "CH 20" + dot + "SPRINT 40 AT LEVEL " + sp.g.startLevel.ToStr()
    if mode = "cleanup" then return "CH 20" + dot + "CLEANUP, " + sp.g.staticRows.ToStr() + " ROWS OF STATIC"
    return "CH 20" + dot + "MARATHON FROM LEVEL " + sp.g.startLevel.ToStr()
end function

function StuckChromeCable(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    sk = a.kit
    s.Clear(c.bg)
    ' the TV: a cabinet, a dark bezel round the screen, a b0r3d badge and a power light on its
    ' chin, and two feet
    cx = Z(444, k)
    cy = Z(32, k)
    cw = Z(836, k) - cx
    chH = Z(616, k) - cy
    s.DrawRect(cx, cy, cw, chH, sk.cabinet)
    DrawFrame(s, cx, cy, cw, chH, Z(4, k), c.frame)
    s.DrawRect(Z(462, k), Z(50, k), Z(818, k) - Z(462, k), Z(574, k) - Z(50, k), sk.bezel)
    DrawCenteredSpaced(s, "b0r3d", cx, Z(574, k), cw, Z(612, k) - Z(574, k), c.soft, f.chin, Z(2, k))
    s.DrawRect(Z(788, k), Z(589, k), Z(14, k), Z(8, k), &hFF4A3AFF)
    s.DrawRect(Z(492, k), Z(616, k), Z(44, k), Z(14, k), c.frame)
    s.DrawRect(Z(744, k), Z(616, k), Z(44, k), Z(14, k), c.frame)

    ' left: the name, the mode and the numbers' labels
    x = Z(56, k)
    s.DrawText("STUCK PIXELS", x, Z(42, k), c.white, f.title)
    s.DrawText(TrimToWidth(StuckModeLine(st), f.line, Z(412, k) - x), x, Z(98, k), c.accent, f.line)
    names = StuckSlotNames()
    labels = StuckSlotLabels(st.sp.g.mode)
    slots = {}
    for i = 0 to 5
        ly = 140 + i * 76
        DrawSpaced(s, labels[i], x, Z(ly, k), c.soft, f.label, Z(2, k))
        slots[names[i]] = { x: 56, y: ly + 28, align: "l", font: f.value, color: c.white, bg: c.bg }
    end for

    ' right: the next channel's screen, the one on hold, and the best
    nextBox = StuckBoxCable(st, s, a, "NEXT CH", 868, 64, 236, 150)
    holdBox = StuckBoxCable(st, s, a, "HOLD", 868, 272, 236, 150)
    DrawSpaced(s, labels[6], Z(868, k), Z(476, k), c.soft, f.label, Z(2, k))
    slots.best = { x: 868, y: 504, align: "l", font: f.value, color: c.white, bg: c.bg }

    ' the bar of keys, and the version
    top = Z(668, k)
    s.DrawRect(0, top, st.w, st.h - top, c.bar)
    bw = Z(48, k)
    bh = Z(44, k)
    bt = top + (st.h - top - bh) \ 2
    ty = top + (st.h - top - f.bar.GetOneLineHeight()) \ 2
    mid = bt + bh \ 2
    hx = Z(40, k)
    s.DrawRect(hx, bt, bw, bh, c.cell1)
    DrawArrowShape(s, hx + Z(9, k), mid - Z(7, k), Z(10, k), Z(14, k), false, c.white)
    DrawArrowShape(s, hx + bw - Z(19, k), mid - Z(7, k), Z(10, k), Z(14, k), true, c.white)
    hx = DrawRun(s, hx + bw + Z(12, k), ty, "MOVE", c.soft, f.bar, Z(1, k), Z(26, k))
    hx = DrawRun(s, hx, ty, "OK", c.white, f.bar, Z(1, k), Z(10, k))
    hx = DrawRun(s, hx, ty, "TURN", c.soft, f.bar, Z(1, k), Z(26, k))
    s.DrawRect(hx, bt, bw, bh, c.cell1)
    DrawChevron(s, hx + bw \ 2, mid, Z(6, k), Z(3, k), true, c.white)
    hx = DrawRun(s, hx + bw + Z(12, k), ty, "DROP", c.soft, f.bar, Z(1, k), Z(26, k))
    s.DrawRect(hx, bt, bw, bh, c.cell1)
    DrawChevron(s, hx + bw \ 2, mid, Z(6, k), Z(3, k), false, c.white)
    hx = DrawRun(s, hx + bw + Z(12, k), ty, "FASTER", c.soft, f.bar, Z(1, k), Z(26, k))
    hx = DrawRun(s, hx, ty, "REPLAY", c.white, f.bar, Z(1, k), Z(10, k))
    hx = DrawRun(s, hx, ty, "HOLD", c.soft, f.bar, Z(1, k), Z(26, k))
    hx = DrawRun(s, hx, ty, "PLAY", c.white, f.bar, Z(1, k), Z(10, k))
    DrawRun(s, hx, ty, "PAUSE", c.soft, f.bar, Z(1, k), 0)
    vty = top + (st.h - top - f.small.GetOneLineHeight()) \ 2
    DrawRight(s, st.version, Z(1240, k), vty, c.soft, f.small)
    fpsX = (Z(1240, k) - f.small.GetOneLineWidth(st.version, 99999) - Z(24, k)) / k
    return { wellX: 472, wellY: 60, slots: slots, nextBox: nextBox, holdBox: holdBox, fps: { x: fpsX, y: vty / k, align: "r", font: f.small, color: c.soft, bg: c.bar } }
end function

' A small screen with its label over it. Returns the inside, [x, y, w, h] in pixels.
function StuckBoxCable(st as object, s as object, a as object, label as string, x as integer, y as integer, w as integer, h as integer) as object
    c = st.kit
    k = st.k
    DrawSpaced(s, label, Z(x, k), Z(y, k), c.soft, a.fonts.label, Z(2, k))
    bx = Z(x, k)
    by = Z(y + 28, k)
    bw = Z(x + w, k) - bx
    bh = Z(y + 28 + h, k) - by
    s.DrawRect(bx, by, bw, bh, a.kit.cabinet)
    DrawFrame(s, bx, by, bw, bh, Z(3, k), c.frame)
    inset = Z(12, k)
    s.DrawRect(bx + inset, by + inset, bw - 2 * inset, bh - 2 * inset, a.kit.mini)
    return [bx + inset, by + inset, bw - 2 * inset, bh - 2 * inset]
end function

function StuckChromePhosphor(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    glow = Z(1, k)
    s.Clear(c.bg)
    DrawPhosphorHeader(st, "B0R3D TV // STUCK PIXELS", false)

    ' left: the mode, then each number on a dotted line
    x = Z(48, k)
    s.DrawText(TrimToWidth("> " + UCase(StuckModeLine(st)), f.line, Z(430, k) - x), x, Z(100, k), c.dim, f.line)
    names = StuckSlotNames()
    labels = StuckSlotLabels(st.sp.g.mode)
    slots = {}
    for i = 0 to 5
        ry = 150 + i * 50
        DrawGlow(s, labels[i], x, Z(ry, k), c.full, c.glow, f.label, glow)
        lw = f.label.GetOneLineWidth(labels[i], 99999)
        DrawDots(s, x + lw + Z(12, k), Z(300, k), Z(ry, k) + f.label.GetOneLineHeight() \ 2 + Z(4, k), Z(2, k), Z(5, k), c.lead)
        slots[names[i]] = { x: 420, y: ry, align: "r", font: f.value, color: c.full, bg: c.bg }
    end for

    ' the well's tube: a glowing frame
    DrawFrame(s, Z(462, k), Z(102, k), Z(818, k) - Z(462, k), Z(626, k) - Z(102, k), Z(2, k), c.full)

    ' right: the next channel and the one on hold, and the best
    nextBox = StuckBoxPhosphor(st, s, a, "NEXT CH ::", 860, 104, 260, 160)
    holdBox = StuckBoxPhosphor(st, s, a, "HOLD ::", 860, 316, 260, 160)
    by = 528
    DrawGlow(s, labels[6], Z(860, k), Z(by, k), c.full, c.glow, f.label, glow)
    lw = f.label.GetOneLineWidth(labels[6], 99999)
    DrawDots(s, Z(860, k) + lw + Z(12, k), Z(1070, k), Z(by, k) + f.label.GetOneLineHeight() \ 2 + Z(4, k), Z(2, k), Z(5, k), c.lead)
    slots.best = { x: 1232, y: by, align: "r", font: f.value, color: c.full, bg: c.bg }

    ' the bar: the rule and the keys (the version is up in the header)
    left = Z(48, k)
    right = Z(1232, k)
    s.DrawRect(left, Z(646, k), right - left, Z(2, k), c.full)
    ty = Z(650, k) + (Z(698, k) - Z(650, k) - f.bar.GetOneLineHeight()) \ 2
    hints = "[<>] MOVE   [OK] TURN   [^] DROP   [V] FASTER   [REPLAY] HOLD   [PLAY] PAUSE"
    DrawGlow(s, hints, left, ty, c.full, c.glow, f.bar, glow)
    fpsX = right / k
    if st.scan then DrawScanlines(st, s)
    return { wellX: 472, wellY: 112, slots: slots, nextBox: nextBox, holdBox: holdBox, fps: { x: fpsX, y: ty / k, align: "r", font: f.bar, color: c.dim, bg: c.bg } }
end function

function StuckBoxPhosphor(st as object, s as object, a as object, label as string, x as integer, y as integer, w as integer, h as integer) as object
    c = st.kit
    k = st.k
    DrawGlow(s, label, Z(x, k), Z(y, k), c.full, c.glow, a.fonts.box, Z(1, k))
    bx = Z(x, k)
    by = Z(y + 34, k)
    bw = Z(x + w, k) - bx
    bh = Z(y + 34 + h, k) - by
    DrawFrame(s, bx, by, bw, bh, Z(2, k), c.full)
    inset = Z(8, k)
    return [bx + inset, by + inset, bw - 2 * inset, bh - 2 * inset]
end function

function StuckChromeVcr(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    sh = Z(3, k)
    s.Clear(c.bg)
    DrawVcrTitle(st, "STUCK PIXELS")
    DrawVcrChannel(st, GameChannel("game.stuck"), Z(26, k))

    ' left: each number's label, the number under it
    x = Z(56, k)
    names = StuckSlotNames()
    labels = StuckSlotLabels(st.sp.g.mode)
    slots = {}
    for i = 0 to 5
        ly = 132 + i * 76
        DrawShadowed(s, labels[i], x, Z(ly, k), c.yellow, c.shadow, f.label, sh)
        slots[names[i]] = { x: 56, y: ly + 24, align: "l", font: f.value, color: c.white, bg: c.bg }
    end for

    ' the well: a white frame with a hard shadow, like the picture-in-picture box
    StuckFrameVcr(st, s, a, 460, 60, 820, 588)

    ' right: next and hold, the best
    nextBox = StuckBoxVcr(st, s, a, "NEXT CH", 868, 168, 236, 140)
    holdBox = StuckBoxVcr(st, s, a, "HOLD", 868, 362, 236, 140)
    DrawShadowed(s, labels[6], Z(868, k), Z(556, k), c.yellow, c.shadow, f.label, sh)
    slots.best = { x: 868, y: 580, align: "l", font: f.value, color: c.white, bg: c.bg }

    DrawVcrBar(st, ":DROP", "OK:TURN  REPLAY:HOLD", ":PAUSE", false)
    vw = st.fonts.bar.GetOneLineWidth(st.version, 99999)
    ty = Z(646, k) + (Z(48, k) - st.fonts.bar.GetOneLineHeight()) \ 2
    fpsX = (Z(1224, k) - vw - Z(24, k)) / k
    return { wellX: 472, wellY: 72, slots: slots, nextBox: nextBox, holdBox: holdBox, fps: { x: fpsX, y: ty / k, align: "r", font: f.small, color: c.white, bg: c.bg } }
end function

sub StuckFrameVcr(st as object, s as object, a as object, x1 as integer, y1 as integer, x2 as integer, y2 as integer)
    c = st.kit
    k = st.k
    x = Z(x1, k)
    y = Z(y1, k)
    w = Z(x2, k) - x
    h = Z(y2, k) - y
    s.DrawRect(x + Z(5, k), y + Z(5, k), w, h, c.shadow)
    s.DrawRect(x, y, w, h, c.white)
    b = Z(6, k)
    s.DrawRect(x + b, y + b, w - 2 * b, h - 2 * b, a.kit.wellBg)
end sub

function StuckBoxVcr(st as object, s as object, a as object, label as string, x as integer, y as integer, w as integer, h as integer) as object
    c = st.kit
    k = st.k
    DrawShadowed(s, label, Z(x, k), Z(y, k), c.yellow, c.shadow, a.fonts.box, Z(3, k))
    StuckFrameVcr(st, s, a, x, y + 28, x + w, y + 28 + h)
    inset = Z(12, k)
    bx = Z(x, k)
    by = Z(y + 28, k)
    return [bx + inset, by + inset, Z(x + w, k) - bx - 2 * inset, Z(y + 28 + h, k) - by - 2 * inset]
end function

function StuckChromeRgb(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    s.Clear(c.bg)

    headY = Z(38, k)
    left = Z(48, k)
    right = Z(1232, k)
    ' the look's lit LED bar, then the title and the mode
    barW = Z(18, k)
    DrawRgbLedBar(st, s, left, headY + Z(10, k), Z(48, k))
    s.DrawText("STUCK PIXELS", left + barW, headY, c.white, f.title)
    s.DrawText(TrimToWidth(StuckModeLine(st), f.line, Z(430, k) - left - barW), left + barW, headY + Z(36, k), c.soft, f.line)

    wx = Z(462, k)
    wy = Z(102, k)
    ww = Z(818, k) - wx
    wh = Z(626, k) - wy
    s.DrawRect(wx, wy, ww, wh, c.well)
    DrawRgbEdge(st, s, wx, wy, ww, wh)

    ' the numbers' labels and places: the same names as the other looks', so each gets its number
    names = StuckSlotNames()
    labels = StuckSlotLabels(st.sp.g.mode)
    slots = {}
    ry = 112
    for i = 0 to 5
        s.DrawText(labels[i], left + Z(12, k), Z(ry, k), c.soft, f.label)
        slots[names[i]] = { x: 420, y: ry + 22, align: "r", font: f.value, color: c.white, bg: c.bg }
        ry = ry + 78
    end for

    nextBox = StuckBoxRgb(st, s, a, "NEXT PIECE", 860, 104, 260, 160)
    holdBox = StuckBoxRgb(st, s, a, "HOLD PIECE", 860, 316, 260, 160)
    s.DrawText(labels[6], Z(872, k), Z(520, k), c.soft, f.label)
    slots.best = { x: 1232, y: 544, align: "r", font: f.value, color: c.accent, bg: c.bg }

    s.DrawRect(left, Z(646, k), right - left, Z(2, k), c.rule)
    ty = Z(650, k) + (Z(698, k) - Z(650, k) - f.bar.GetOneLineHeight()) \ 2
    hints = "[<>] MOVE   [OK] TURN   [^] DROP   [V] DROP FASTER   [REPLAY] HOLD   [PLAY] PAUSE"
    s.DrawText(hints, left, ty, c.dim, f.bar)
    fpsX = right / k
    return { wellX: 472, wellY: 112, slots: slots, nextBox: nextBox, holdBox: holdBox, fps: { x: fpsX, y: ty / k, align: "r", font: f.bar, color: c.dim, bg: c.bg } }
end function

function StuckBoxRgb(st as object, s as object, a as object, label as string, x as integer, y as integer, w as integer, h as integer) as object
    c = st.kit
    k = st.k
    bx = Z(x, k)
    by = Z(y + 26, k)
    bw = Z(x + w, k) - bx
    bh = Z(y + 26 + h, k) - by
    s.DrawText(label, bx + Z(8, k), Z(y, k), c.soft, a.fonts.label)
    s.DrawRect(bx - Z(2, k), by - Z(2, k), bw + Z(4, k), bh + Z(4, k), c.frame)
    s.DrawRect(bx, by, bw, bh, c.well)
    inset = Z(8, k)
    return [bx + inset, by + inset, bw - 2 * inset, bh - 2 * inset]
end function

function StuckChromeB95(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    t = Z(2, k)
    s.Clear(c.bg)
    DrawB95Desktop(st, invalid, "Stuck Pixels")

    ' the window, its title bar and its menu bar
    wx = Z(180, k)
    wy = Z(18, k)
    ww = Z(1260, k) - wx
    wh = Z(662, k) - wy
    B95Raised(s, wx, wy, ww, wh, t, c)
    DrawB95TitleBar(st, wx + Z(4, k), wy + Z(4, k), ww - Z(8, k), "Stuck Pixels", true)
    mx = wx + Z(14, k)
    for each word in ["Game", "View", "Help"]
        DrawB95Underlined(st, word, mx, Z(64, k) + (Z(30, k) - st.fonts.menu.GetOneLineHeight()) \ 2, st.fonts.menu)
        mx = mx + st.fonts.menu.GetOneLineWidth(word, 99999) + Z(22, k)
    end for

    ' the TV: a raised grey set with the well sunk into it
    B95Raised(s, Z(532, k), Z(94, k), Z(908, k) - Z(532, k), Z(626, k) - Z(94, k), t, c)
    B95Sunken(s, Z(546, k), Z(102, k), Z(894, k) - Z(546, k), Z(618, k) - Z(102, k), t, c, a.kit.wellBg)

    ' left: the numbers in a group box
    DrawB95Group(st, Z(196, k), Z(112, k), Z(516, k) - Z(196, k), Z(470, k) - Z(112, k), "Score")
    names = StuckSlotNames()
    labels = ["Score:", "Level:", "Lines:", "Time:", "Stuck:", "Fixed:", "Best:"]
    if st.sp.g.mode = "sprint" then labels[2] = "Left:"
    slots = {}
    for i = 0 to 5
        ry = 138 + i * 52
        B95Text(s, labels[i], Z(214, k), Z(ry, k), c.black, f.label, k, true)
        slots[names[i]] = { x: 498, y: ry, align: "r", font: f.value, color: c.black, bg: c.face }
    end for

    ' right: next and on hold in group boxes, each with a small black screen, and the best
    nextBox = StuckBoxB95(st, s, a, "Next channel", 928, 112, 316, 170)
    holdBox = StuckBoxB95(st, s, a, "On hold", 928, 310, 316, 170)
    B95Text(s, labels[6], Z(940, k), Z(510, k), c.black, f.label, k, true)
    slots.best = { x: 1244, y: 510, align: "r", font: f.value, color: c.black, bg: c.face }

    ' the status bar: the keys and the version
    statusY = Z(630, k)
    statusH = Z(658, k) - statusY
    c1x = wx + Z(6, k)
    c2w = Z(250, k)
    c2x = wx + ww - Z(6, k) - c2w
    c1w = c2x - Z(4, k) - c1x
    B95Edge(s, c1x, statusY, c1w, statusH, t, c.shadow, c.white)
    B95Edge(s, c2x, statusY, c2w, statusH, t, c.shadow, c.white)
    textY = statusY + (statusH - f.bar.GetOneLineHeight()) \ 2
    hints = "Left and right move · OK turns · Up drops · Down drops faster · Replay holds · Play pauses"
    B95Text(s, B95Trim(hints, f.bar, c1w - Z(16, k), k, false), c1x + Z(8, k), textY, c.black, f.bar, k, false)
    B95Text(s, B95Trim(st.version, f.bar, c2w - Z(16, k), k, false), c2x + Z(8, k), textY, c.black, f.bar, k, false)
    ' the counter sits at the right of the window's menu bar
    return { wellX: 552, wellY: 108, slots: slots, nextBox: nextBox, holdBox: holdBox, fps: { x: 1244, y: 70, align: "r", font: f.small, color: c.black, bg: c.face } }
end function

function StuckBoxB95(st as object, s as object, a as object, label as string, x as integer, y as integer, w as integer, h as integer) as object
    c = st.kit
    k = st.k
    t = Z(2, k)
    DrawB95Group(st, Z(x, k), Z(y, k), Z(x + w, k) - Z(x, k), Z(y + h, k) - Z(y, k), label)
    bx = Z(x + 16, k)
    by = Z(y + 22, k)
    bw = Z(x + w - 16, k) - bx
    bh = Z(y + h - 14, k) - by
    B95Sunken(s, bx, by, bw, bh, t, c, a.kit.mini)
    inset = 2 * t
    return [bx + inset, by + inset, bw - 2 * inset, bh - 2 * inset]
end function

' b0r3d 95's taskbar clock moves on: only the taskbar is drawn again, into the chrome, before
' the frame that shows it.
sub RedrawB95Clock(st as object, a as object)
    keep = st.screen
    st.screen = a.chrome
    DrawB95Taskbar(st, "Stuck Pixels")
    st.screen = keep
    a.clockText = ClockText(st, false)
end sub

' --- The demo in the guide's preview box: the game playing itself, a step every 200 ms, with
' its name and numbers beside it. ---

function StuckDemo(st as object) as object
    if st.demo = invalid then
        st.demo = SpDemoNew(GameSeed())
        st.demoTimer = CreateObject("roTimespan")
        st.demoClock = CreateObject("roTimespan")
    end if
    ms = st.demoTimer.TotalMilliseconds()
    if ms >= 195 then
        ' a step every 200 ms; after the preview has been away a while, just one
        n = (ms + 5) \ 200
        if ms > 2000 then n = 1
        for i = 1 to n
            SpDemoStep(st.demo)
        end for
        st.demoTimer.Mark()
    end if
    return st.demo
end function

' How often the guide is drawn again while its preview plays a demo: five times a second for
' Stuck Pixels, ten for Test Pattern (its ball moves), about nine for Snake, ten for b0r3d FM's
' bars, twice for Commercial Breaks (its card turns over every 8 s) and for the VCR (its 12:00
' blinks), ten for Rukus Cam (he walks), or less on a TV that takes long to draw the guide (never
' more than half the time spent drawing).
function DemoRedrawMs(st as object) as integer
    id = PreviewGameId(st)
    ms = 2 * st.guideMs
    base = 200
    if id = "game.test" then base = 100
    if id = "game.snake" then base = 110
    if id = "radio.fm" then base = 100
    if id = "promo.breaks" then base = 500
    if id = "video.vcr" then base = 500
    if id = "cam.rukus" then base = 100
    if ms < base then ms = base
    if ms > 1000 then ms = 1000
    return ms
end function

' How long Main's loop waits while the guide's preview shows a demo: until it's due again. Each
' preview marks its own timer when it moves on (or, for the two that don't step, when drawn).
function DemoWaitMs(st as object) as integer
    id = PreviewGameId(st)
    interval = DemoRedrawMs(st)
    timer = st.demoTimer
    if id = "game.test" then timer = st.tpDemoTimer
    if id = "game.snake" then timer = st.snakeDemoTimer
    if id = "radio.fm" then timer = st.fmDemoTimer
    if id = "promo.breaks" then timer = st.promoDemoTimer
    if id = "video.vcr" then timer = st.deckDemoTimer
    if id = "cam.rukus" then timer = st.camDemoTimer
    if timer = invalid then return interval
    left = interval - timer.TotalMilliseconds()
    if left < 10 then return 10
    if left > interval then return interval
    return left
end function

' The game whose channel the guide's highlight is on, or "".
function PreviewGameId(st as object) as string
    if not GuideShowsGame(st) then return ""
    return st.channels[st.focus].id
end function

' A game or special channel's demo in the guide's preview box.
sub DrawGamePreview(st as object, ch as object, x as integer, y as integer, w as integer, h as integer)
    if ch.id = "game.test" then
        DrawTpPreview(st, x, y, w, h)
    else if ch.id = "radio.fm" then
        DrawFmPreview(st, x, y, w, h)
    else if ch.id = "game.snake" then
        DrawSnakePreview(st, x, y, w, h)
    else if ch.id = "promo.breaks" then
        DrawPromoPreview(st, x, y, w, h)
    else if ch.id = "video.vcr" then
        DrawDeckPreview(st, x, y, w, h)
    else if ch.id = "cam.rukus" then
        DrawCamPreview(st, x, y, w, h)
    else
        DrawStuckPreview(st, x, y, w, h)
    end if
end sub

' Whether a channel's preview box plays a demo of it: the games, b0r3d FM, Commercial Breaks,
' the VCR and Rukus Cam (live), in every look.
function HasPreview(ch as object) as boolean
    return ch.kind = "game" or ch.kind = "radio" or ch.kind = "promo" or ch.kind = "vcr" or ch.kind = "cam"
end function

' Whether the guide's highlight is on a channel with a demo in its preview box.
function GuideShowsGame(st as object) as boolean
    if st.mode <> "guide" then return false
    if st.channels.Count() = 0 then return false
    return HasPreview(st.channels[st.focus])
end function

' The demo, centred in the preview box (x, y, w, h in pixels): a small well, and beside it the
' name, the level, the lines, the stuck pixels and the next piece.
sub DrawStuckPreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    d = StuckDemo(st)
    g = d.g
    k = st.k
    kit = StuckKit(st)
    f = st.fonts
    p = Z(13, k)
    b = p - Z(1, k)
    ww = g.cols * p
    wh = g.rows * p
    titleFont = f.pageTitle
    statFont = f.pageInfo
    if st.look = "cable" then statFont = f.barNum
    if st.look = "phosphor" then statFont = f.row
    title = "STUCK PIXELS"
    if st.look = "b95" then title = "Stuck Pixels"
    ' the column beside the well is as wide as its title (Silkscreen's bold capitals are wide);
    ' if that won't fit the box, the title takes the numbers' smaller lettering
    gap = Z(28, k)
    textW = GTextWidth(st, title, titleFont, true)
    if ww + gap + textW > w - Z(16, k) then
        titleFont = statFont
        textW = GTextWidth(st, title, titleFont, true)
    end if
    if textW < Z(150, k) then textW = Z(150, k)
    ox = x + (w - ww - gap - textW) \ 2
    oy = y + (h - wh) \ 2
    lit = (st.demoClock.TotalMilliseconds() \ 450) mod 2 = 0
    frameColor = kit.block
    if st.look = "cable" then frameColor = &h8FB4FFFF
    if st.look = "vcr" then frameColor = st.kit.white
    if st.look = "phosphor" then frameColor = st.kit.full
    if st.look = "b95" then frameColor = &h808080FF
    inset = Z(3, k)
    s.DrawRect(ox - inset - Z(2, k), oy - inset - Z(2, k), ww + 2 * inset + Z(4, k), wh + 2 * inset + Z(4, k), frameColor)
    s.DrawRect(ox - inset, oy - inset, ww + 2 * inset, wh + 2 * inset, kit.wellBg)
    s.DrawRect(ox, oy, ww - (p - b), wh - (p - b), kit.empty)
    for r = 0 to g.rows - 1
        row = g.board[r]
        for c = 0 to g.cols - 1
            v = row[c]
            if v <> 0 then
                color = kit.block
                if v = 2 then
                    color = kit.stuckOff
                    if lit then color = kit.stuckOn
                else if v = 3 then
                    color = kit.noise[Rnd(4) - 1]
                end if
                s.DrawRect(ox + c * p, oy + r * p, b, b, color)
            end if
        end for
    end for
    ' the empty cells' grid lines
    for c = 1 to g.cols - 1
        s.DrawRect(ox + c * p - (p - b), oy, p - b, wh - (p - b), kit.wellBg)
    end for
    for r = 1 to g.rows - 1
        s.DrawRect(ox, oy + r * p - (p - b), ww - (p - b), p - b, kit.wellBg)
    end for
    if g.state = "play" then
        cells = SpCells(g)
        for i = 0 to cells.Count() - 1
            if cells[i][0] >= 0 then
                color = kit.block
                if i = g.cur.stuck then
                    color = kit.stuckOff
                    if lit then color = kit.stuckOn
                end if
                s.DrawRect(ox + cells[i][1] * p, oy + cells[i][0] * p, b, b, color)
            end if
        end for
    else
        ' topped out: the little screen fills with static
        for r = 0 to g.rows - 1
            s.DrawRect(ox, oy + r * p, ww - (p - b), b, kit.noise[Rnd(4) - 1])
        end for
    end if

    ' beside it, in the look's lettering
    tx = ox + ww + gap
    ty = oy + Z(6, k)
    textColor = StuckPreviewText(st)
    softColor = StuckPreviewSoft(st)
    GText(st, s, title, tx, ty, titleFont, textColor, true)
    ty = ty + titleFont.GetOneLineHeight() + Z(10, k)
    stats = [["LEVEL", TwoDigits(g.level)], ["LINES", Right("00" + g.lines.ToStr(), 3)], ["STUCK", TwoDigits(g.stuckNow)]]
    for each line in stats
        label = line[0]
        if st.look = "b95" then label = Left(label, 1) + LCase(Mid(label, 2)) + ":"
        color = softColor
        if line[0] = "STUCK" and (st.look = "vcr" or st.look = "cable") then color = kit.stuckOn
        GText(st, s, label + " " + line[1], tx, ty, statFont, color, false)
        ty = ty + statFont.GetOneLineHeight() + Z(4, k)
    end for
    ty = ty + Z(6, k)
    nextWord = "NEXT"
    if st.look = "b95" then nextWord = "Next:"
    GText(st, s, nextWord, tx, ty, statFont, softColor, false)
    ty = ty + statFont.GetOneLineHeight() + Z(6, k)
    if g.upcoming <> invalid then
        for each cell in g.shapes[g.upcoming.p].cells[0]
            s.DrawRect(tx + cell[1] * p, ty + cell[0] * p, b, b, kit.block)
        end for
    end if
end sub

function StuckPreviewText(st as object) as dynamic
    if st.look = "phosphor" then return st.kit.full
    if st.look = "b95" then return st.kit.white
    return &hFFFFFFFF
end function

function StuckPreviewSoft(st as object) as dynamic
    if st.look = "phosphor" then return st.kit.dim
    if st.look = "b95" then return st.kit.white
    if st.look = "vcr" then return st.kit.white
    return st.kit.soft
end function
