' Test Pattern on screen, in the current look: the play screen (the field as a TV showing colour
' bars, the numbers over it, the remote's keys along the bottom) and the demo in the guide's
' preview box. Drawn the way Stuck Pixels is (stuckdraw.brs): what stays put goes once into a
' picture of the whole screen (the chrome), the wall into a picture of its own (a brick that's
' knocked out is painted over), each number into a small picture when it changes; a frame copies
' those and draws the paddle, the balls, the capsules and the bricks of static. Pictures are only
' changed before a frame starts drawing (Roku OS 16.3 refuses a change between drawing a picture
' and showing the frame). The field's units are scaled by the look's field size and st.k.

' The eight colour bars, left to right: white, yellow, cyan, green, magenta, red, blue, black at
' three quarters, as on a TV test card. On the phosphor look they're eight evenly spaced shades
' of the phosphor, from full down to a quarter, the way a one-colour screen shows them (the bars
' get darker left to right, as their colours do).
function TpBars(st as object) as object
    if st.look = "phosphor" then
        c = st.kit
        out = []
        for i = 0 to 7
            out.Push(TpShade(c.bg, c.full, 1 - i * 0.75 / 7))
        end for
        return out
    end if
    return [&hBFBFBFFF, &hBFBF00FF, &h00BFBFFF, &h00BF00FF, &hBF00BFFF, &hBF0000FF, &h0000BFFF, &h1C1C1CFF]
end function

' The colour f of the way from dark to light (0 dark, 1 light), opaque.
function TpShade(dark as integer, light as integer, f as float) as integer
    out = &hFF
    for each sh in [24, 16, 8]
        d = (dark >> sh) and &hFF
        l = (light >> sh) and &hFF
        out = out or (Int(d + (l - d) * f + 0.5) << sh)
    end for
    return out
end function

' b0r3d 95's raised edges for each bar: the light and the dark.
function TpBarEdges() as object
    return {
        light: [&hFFFFFFFF, &hFFFF80FF, &h80FFFFFF, &h80FF80FF, &hFF80FFFF, &hFF8080FF, &h8080FFFF, &h606060FF],
        dark: [&h808080FF, &h606000FF, &h006060FF, &h006000FF, &h600060FF, &h600000FF, &h000060FF, &h000000FF]
    }
end function

function TpKit(st as object) as object
    c = st.kit
    if st.look = "phosphor" then
        return { field: c.bg, paddle: c.full, paddleEdge: c.dim, ball: c.full, capBody: c.soft, capScreen: c.bg, capMark: c.full, bannerBg: c.bg, bannerFg: c.full, noise: c.noise, outline: c.dim, label: c.dim, value: c.full, flash: c.full, sticky: c.nose }
    end if
    if st.look = "vcr" then
        wells = [&h0B1A7AFF, &h0E0E0EFF, &h06401FFF]
        return { field: wells[st.colour], paddle: c.white, paddleEdge: c.shadow, ball: c.yellow, capBody: c.white, capScreen: &h101010FF, capMark: c.white, bannerBg: c.shadow, bannerFg: c.yellow, noise: c.noise, outline: c.grey, label: c.yellow, value: c.white, flash: c.white, sticky: c.yellow }
    end if
    if st.look = "b95" then
        return { field: &h000000FF, paddle: c.face, paddleEdge: c.dark, ball: c.white, capBody: c.face, capScreen: &h101010FF, capMark: c.white, bannerBg: c.navy, bannerFg: c.white, noise: c.noise, outline: &h606060FF, label: c.black, value: c.black, flash: c.white, sticky: &hFF6FD8FF }
    end if
    if st.look = "rgb" then
        return { field: c.well, paddle: c.green, paddleEdge: c.dim, ball: c.white, capBody: c.blue, capScreen: c.bg, capMark: c.white, bannerBg: c.card, bannerFg: c.accent, noise: c.noise, outline: c.rule, label: c.soft, value: c.white, flash: c.white, sticky: c.accent }
    end if
    return { field: &h050B22FF, paddle: c.accent, paddleEdge: &hB8962DFF, ball: c.white, capBody: &hC9D6FFFF, capScreen: &h0A1640FF, capMark: c.white, bannerBg: c.ink, bannerFg: c.accent, noise: c.noise, outline: &h5A5A5AFF, label: c.soft, value: c.white, flash: c.white, sticky: &hFF6FD8FF }
end function

function TpFonts(st as object) as object
    k = st.k
    if st.look = "phosphor" then
        return { title: Vt323(st, Z(40, k)), label: Vt323(st, Z(26, k)), value: Vt323(st, Z(30, k)), small: Vt323(st, Z(24, k)), banner: Vt323(st, Z(48, k)), bar: Vt323(st, Z(26, k)) }
    end if
    if st.look = "vcr" then
        return { title: Silk(st, Z(32, k), true), label: Silk(st, Z(16, k), false), value: Silk(st, Z(24, k), true), small: Silk(st, Z(16, k), false), banner: Silk(st, Z(32, k), true), bar: Silk(st, Z(16, k), false) }
    end if
    if st.look = "b95" then
        return { title: W95(st, Z(26, k)), label: W95(st, Z(19, k)), value: W95(st, Z(21, k)), small: W95(st, Z(18, k)), banner: W95(st, Z(30, k)), bar: W95(st, Z(18, k)) }
    end if
    if st.look = "rgb" then
        return { title: Barlow(st, "extrabold", Z(34, k)), label: Barlow(st, "semibold", Z(19, k)), value: Barlow(st, "bold", Z(30, k)), small: Barlow(st, "semibold", Z(19, k)), banner: Barlow(st, "extrabold", Z(40, k)), bar: Barlow(st, "semibold", Z(18, k)) }
    end if
    return { title: Barlow(st, "extrabold", Z(44, k)), label: Barlow(st, "bold", Z(20, k)), value: Vt323(st, Z(40, k)), small: Vt323(st, Z(26, k)), banner: Barlow(st, "extrabold", Z(40, k)), bar: Barlow(st, "bold", Z(21, k)) }
end function

' --- The pictures a run is drawn from ---

function TpArt(st as object) as object
    tp = st.tp
    key = st.look + "|" + st.colour.ToStr() + "|" + st.w.ToStr() + "|" + OnOff(st.scan)
    if st.look = "b95" then key = key + "|" + st.channels.Count().ToStr()
    if tp.art <> invalid then
        if tp.art.key = key then
            if st.look = "b95" and tp.art.chrome <> invalid and tp.art.clockText <> ClockText(st, false) then
                keep = st.screen
                st.screen = tp.art.chrome
                DrawB95Taskbar(st, "Test Pattern")
                st.screen = keep
                tp.art.clockText = ClockText(st, false)
            end if
            return tp.art
        end if
    end if
    ' the old pictures go first, so the TV never holds two whole-screen pictures at once
    tp.art = invalid
    k = st.k
    a = { key: key, kit: TpKit(st), fonts: TpFonts(st), bars: TpBars(st), vals: {}, banners: {}, wallBanner: invalid, wall: invalid, wallKey: "", fpsPic: invalid }
    chrome = NewPicture(st.w, st.h, true)
    if chrome <> invalid then
        keep = st.screen
        st.screen = chrome
        layout = TpChromeFor(st, chrome, a)
        st.screen = keep
    else
        layout = TpChromeFor(st, st.screen, a)
    end if
    a.chrome = chrome
    a.clockText = ClockText(st, false)
    a.fs = layout.fs
    a.sx = layout.fs * k
    a.ox = Z(layout.fieldX, k)
    a.oy = Z(layout.fieldY, k)
    a.slots = layout.slots
    a.fpsSlot = layout.fps
    a.caps = {}
    for each kind in ["multi", "wide", "sticky", "slow", "life"]
        a.caps[kind] = TpCapsulePicture(st, a, kind)
    end for
    a.ballPx = Int(14 * a.sx + 0.5)
    a.scanTile = invalid
    if st.look = "phosphor" and st.scan then a.scanTile = ScanTile(st, Int(1120 * a.sx), Int(548 * a.sx))
    tp.art = a
    tp.wallDirty = true
    return a
end function

function TpChromeFor(st as object, s as object, a as object) as object
    if st.look = "b95" then return TpChromeB95(st, s, a)
    if st.look = "phosphor" then return TpChromePhosphor(st, s, a)
    if st.look = "vcr" then return TpChromeVcr(st, s, a)
    if st.look = "rgb" then return TpChromeRgb(st, s, a)
    return TpChromeCable(st, s, a)
end function

' A capsule as it falls: a little TV, its screen showing what it does (three dots for more balls,
' a long bar for wide, drips for sticky, an hourglass for slow, a plus for a life). Around the
' rabbit ears it's see-through, so it can fall past bricks.
function TpCapsulePicture(st as object, a as object, kind as string) as dynamic
    tk = a.kit
    w = Int(34 * a.sx + 0.5)
    h = Int(26 * a.sx + 0.5)
    bmp = NewPicture(w, h, false)
    if bmp = invalid then return invalid
    bmp.Clear(&h00000000)
    u = w / 17.0
    ' rabbit ears, the cabinet, the screen
    TpGrid(bmp, u, 5, 0, 1, 2, tk.capBody)
    TpGrid(bmp, u, 11, 0, 1, 2, tk.capBody)
    TpGrid(bmp, u, 6, 1, 1, 1, tk.capBody)
    TpGrid(bmp, u, 10, 1, 1, 1, tk.capBody)
    TpGrid(bmp, u, 0, 2, 17, 11, tk.capBody)
    screens = { multi: &hE8E8E8FF, wide: &h20C040FF, sticky: &hD040B0FF, slow: &h20B0D0FF, life: &hD02030FF }
    screen = screens[kind]
    if st.look = "phosphor" then screen = st.kit.dim
    TpGrid(bmp, u, 2, 4, 13, 7, screen)
    mark = &h101010FF
    if kind = "life" then mark = &hFFFFFFFF
    if st.look = "phosphor" then mark = st.kit.bg
    if kind = "multi" then
        for each mx in [4, 8, 12]
            TpGrid(bmp, u, mx, 6, 1, 3, mark)
        end for
    else if kind = "wide" then
        TpGrid(bmp, u, 3, 7, 11, 1, mark)
    else if kind = "sticky" then
        TpGrid(bmp, u, 4, 5, 9, 1, mark)
        TpGrid(bmp, u, 5, 6, 1, 3, mark)
        TpGrid(bmp, u, 11, 6, 1, 2, mark)
    else if kind = "slow" then
        TpGrid(bmp, u, 6, 5, 5, 1, mark)
        TpGrid(bmp, u, 7, 6, 3, 1, mark)
        TpGrid(bmp, u, 8, 7, 1, 1, mark)
        TpGrid(bmp, u, 7, 8, 3, 1, mark)
        TpGrid(bmp, u, 6, 9, 5, 1, mark)
    else if kind = "life" then
        TpGrid(bmp, u, 8, 5, 1, 5, mark)
        TpGrid(bmp, u, 6, 7, 5, 1, mark)
    end if
    return bmp
end function

' A rectangle on a grid of u-pixel cells.
sub TpGrid(s as object, u as float, gx as integer, gy as integer, gw as integer, gh as integer, color as dynamic)
    x = Int(gx * u + 0.5)
    y = Int(gy * u + 0.5)
    w = Int((gx + gw) * u + 0.5) - x
    h = Int((gy + gh) * u + 0.5) - y
    if w < 1 then w = 1
    if h < 1 then h = 1
    s.DrawRect(x, y, w, h, color)
end sub

' The wall's pictures: one brick tile per bar (and one with a little TV on it for capsule
' bricks), the static for bombs and their blasts, and the wall itself, made again for each wall.
sub TpBuildWallArt(st as object, a as object, g as object)
    r = g.rules
    bw = Int(g.brickW * a.sx + 0.5)
    bh = Int(r.brickH * a.sx + 0.5)
    key = bw.ToStr() + "x" + bh.ToStr()
    if a.wallKey <> key then
        a.wallKey = key
        a.bw = bw
        a.bh = bh
        a.brickTiles = []
        a.capTiles = []
        for bar = 0 to 7
            a.brickTiles.Push(TpBrickTile(st, a, bar, false))
            a.capTiles.Push(TpBrickTile(st, a, bar, true))
        end for
        a.noise = []
        for i = 1 to 4
            a.noise.Push(TpNoiseTile(bw, bh, a.kit.noise))
        end for
        a.cellW = Int((g.brickW + r.gap) * a.sx + 0.5)
        a.cellH = Int((r.brickH + r.gap) * a.sx + 0.5)
        a.blastTiles = []
        for i = 1 to 3
            a.blastTiles.Push(TpBlastTile(a.cellW, a.cellH, Z(6, st.k), a.kit.noise))
        end for
    end if
    wallH = Int((r.brickTop + 9 * (r.brickH + r.gap)) * a.sx + 0.5)
    if a.wall = invalid then a.wall = NewPicture(Int(r.w * a.sx + 0.5), wallH, false)
    a.bombs = []
    if a.wall <> invalid then a.wall.Clear(a.kit.field)
    for each b in g.bricks
        if b.alive then
            if b.kind = "bomb" then
                a.bombs.Push(b)
            else if a.wall <> invalid then
                TpDrawBrick(a, a.wall, b, 0, 0)
            end if
        end if
    end for
end sub

' One brick at its place, with the field's top left at ox, oy.
sub TpDrawBrick(a as object, s as object, b as object, ox as integer, oy as integer)
    x = ox + Int(b.x * a.sx + 0.5)
    y = oy + Int(b.y * a.sx + 0.5)
    tile = a.brickTiles[b.bar]
    if b.kind = "capsule" then tile = a.capTiles[b.bar]
    if tile <> invalid then
        s.DrawObject(x, y, tile)
    else
        s.DrawRect(x, y, a.bw, a.bh, a.bars[b.bar])
    end if
end sub

' A brick in its bar's colour: raised on b0r3d 95, outlined when it's the black bar (so it
' shows on the dark field), with a little TV on it when it holds a capsule.
function TpBrickTile(st as object, a as object, bar as integer, capsule as boolean) as dynamic
    w = a.bw
    h = a.bh
    bmp = NewPicture(w, h, false)
    if bmp = invalid then return invalid
    bmp.Clear(a.bars[bar])
    e = Z(2, st.k)
    if st.look = "b95" then
        edges = TpBarEdges()
        bmp.DrawRect(0, 0, w, e, edges.light[bar])
        bmp.DrawRect(0, 0, e, h, edges.light[bar])
        bmp.DrawRect(0, h - e, w, e, edges.dark[bar])
        bmp.DrawRect(w - e, 0, e, h, edges.dark[bar])
    else if bar = 7 then
        DrawFrame(bmp, 0, 0, w, h, e, a.kit.outline)
    end if
    if capsule then
        ' a little TV: a dark rim, a light cabinet, a dark screen (in the phosphor's shades on
        ' that look, where nothing is white)
        rim = &h101010FF
        cabinet = &hE8E8E8FF
        glass = &h303030FF
        if st.look = "phosphor" then
            rim = st.kit.bg
            cabinet = st.kit.full
            glass = st.kit.lead
        end if
        tw = h * 3 \ 2
        tx = (w - tw) \ 2
        ty = h \ 6
        th = h - 2 * ty
        bmp.DrawRect(tx, ty, tw, th, rim)
        bmp.DrawRect(tx + e, ty + e, tw - 2 * e, th - 2 * e, cabinet)
        bmp.DrawRect(tx + 2 * e, ty + 2 * e, tw - 4 * e, th - 4 * e, glass)
    end if
    return bmp
end function

' A brick-sized piece of TV static: grains in the look's four noise shades.
function TpNoiseTile(w as integer, h as integer, noise as object) as dynamic
    bmp = NewPicture(w, h, false)
    if bmp = invalid then return invalid
    grain = h \ 5
    if grain < 2 then grain = 2
    for y = 0 to h - 1 step grain
        for x = 0 to w - 1 step grain
            bmp.DrawRect(x, y, grain, grain, noise[Rnd(4) - 1])
        end for
    end for
    return bmp
end function

' A cell of a bomb's blast (a brick and the gaps around it): grains of static, a third of the
' spots filled, over nothing, so what's under it shows through.
function TpBlastTile(w as integer, h as integer, grain as integer, noise as object) as dynamic
    bmp = NewPicture(w, h, false)
    if bmp = invalid then return invalid
    bmp.Clear(&h00000000)
    for y = 0 to h - 1 step grain
        for x = 0 to w - 1 step grain
            if Rnd(3) = 1 then
                gw = grain
                gh = grain
                if x + gw > w then gw = w - x
                if y + gh > h then gh = h - y
                bmp.DrawRect(x, y, gw, gh, noise[Rnd(4) - 1])
            end if
        end for
    end for
    return bmp
end function

' Bricks knocked out since the last frame are painted over in the wall's picture.
sub TpEraseHits(st as object, a as object, g as object)
    tp = st.tp
    if a.wall = invalid then return
    for each fx in tp.fx
        if fx.kind = "hit" and not fx.DoesExist("erased") then
            fx.erased = true
            b = fx.brick
            a.wall.DrawRect(Int(b.x * a.sx + 0.5), Int(b.y * a.sx + 0.5), a.bw, a.bh, a.kit.field)
        end if
    end for
end sub

' What each number reads now: the score, the lives, the wall, the best, and the capsules that
' are on (with the seconds left).
function TpValueTexts(st as object, g as object) as object
    wall = (g.wall + 1).ToStr()
    if g.mode = "campaign" then wall = wall + "/6"
    effects = ""
    if g.wide then effects = "WIDE"
    if g.stickyMs > 0 then effects = TpJoinWord(effects, "STICKY " + ((g.stickyMs + 999) \ 1000).ToStr())
    if g.slowMs > 0 then effects = TpJoinWord(effects, "SLOW " + ((g.slowMs + 999) \ 1000).ToStr())
    return { score: GroupDigits(g.score), lives: g.lives.ToStr(), wall: wall, best: st.tp.bestText, effects: effects }
end function

function TpJoinWord(a as string, b as string) as string
    if a = "" then return b
    return a + "   " + b
end function

sub UpdateTpValues(st as object, a as object, g as object)
    texts = TpValueTexts(st, g)
    for each name in texts
        slot = a.slots[name]
        if slot <> invalid then
            text = slot.prefix + texts[name]
            if texts[name] = "" then text = ""
            old = a.vals[name]
            fresh = (old = invalid)
            if not fresh then fresh = (old.text <> text)
            if fresh then
                if text = "" then
                    a.vals[name] = { text: "", pic: invalid }
                else
                    bg = slot.bg
                    if st.look = "phosphor" and st.scan then bg = &h00000000
                    a.vals[name] = { text: text, pic: TextPicture(st, text, slot.font, slot.color, bg, false) }
                end if
            end if
        end if
    end for
end sub

sub UpdateTpFps(st as object, a as object)
    if not ShowsFps() or a.fpsSlot = invalid then return
    f = st.tp.fps
    ms = f.timer.TotalMilliseconds()
    if ms < 1000 then return
    shown = Int(f.frames * 1000 / ms + 0.5)
    f.frames = 0
    f.timer.Mark()
    slot = a.fpsSlot
    bg = slot.bg
    if st.look = "phosphor" and st.scan then bg = &h00000000
    a.fpsPic = TextPicture(st, shown.ToStr() + " FPS", slot.font, slot.color, bg, false)
end sub

' A line of words in a box over the field, made once and kept for the run. A cleared wall's
' words carry the wall's number, so they have one place of their own, which the next wall's
' take: a long Endless run doesn't pile up pictures.
function TpBannerPic(st as object, a as object, text as string) as dynamic
    if Left(text, 5) = "WALL " then
        if a.wallBanner <> invalid then
            if a.wallBanner.text = text then return a.wallBanner.pic
        end if
        a.wallBanner = invalid
        pic = TextPicture(st, " " + text + " ", a.fonts.banner, a.kit.bannerFg, a.kit.bannerBg, true)
        if pic <> invalid then a.wallBanner = { text: text, pic: pic }
        return pic
    end if
    pic = a.banners[text]
    if pic = invalid then
        pic = TextPicture(st, " " + text + " ", a.fonts.banner, a.kit.bannerFg, a.kit.bannerBg, true)
        if pic <> invalid then a.banners[text] = pic
    end if
    return pic
end function

' --- A frame ---

sub DrawTpPlay(st as object)
    s = st.screen
    tp = st.tp
    if s = invalid or tp.g = invalid then return
    g = tp.g
    r = g.rules
    a = TpArt(st)
    k = st.k
    ' every picture is brought up to date first...
    if tp.wallDirty then
        TpBuildWallArt(st, a, g)
        tp.wallDirty = false
    end if
    TpEraseHits(st, a, g)
    UpdateTpValues(st, a, g)
    UpdateTpFps(st, a)
    message = TpMessage(st, g)
    banner = invalid
    if message <> "" then banner = TpBannerPic(st, a, message)
    ' ...then the frame is drawn from them: the solid layers without blending
    if a.chrome <> invalid then
        s.SetAlphaEnable(false)
        s.DrawObject(0, 0, a.chrome)
    else
        TpChromeFor(st, s, a)
        s.SetAlphaEnable(false)
    end if
    ox = a.ox
    oy = a.oy
    if a.wall <> invalid then
        s.DrawObject(ox, oy, a.wall)
    else
        for each b in g.bricks
            if b.alive and b.kind <> "bomb" then TpDrawBrick(a, s, b, ox, oy)
        end for
    end if
    now = tp.clock.TotalMilliseconds()
    noiseStep = now \ 90
    for each b in a.bombs
        if b.alive then
            x = ox + Int(b.x * a.sx + 0.5)
            y = oy + Int(b.y * a.sx + 0.5)
            tile = a.noise[(noiseStep + b.col * 3 + b.row) mod 4]
            if tile <> invalid then
                s.DrawObject(x, y, tile)
            else
                s.DrawRect(x, y, a.bw, a.bh, a.kit.noise[2])
            end if
        end if
    end for
    ' the words go under everything that moves, so the ball is never hidden behind them
    s.SetAlphaEnable(true)
    for each name in ["score", "lives", "wall", "best", "effects"]
        slot = a.slots[name]
        v = a.vals[name]
        if slot <> invalid and v <> invalid then DrawPic(s, v.pic, Z(slot.x, k), Z(slot.y, k), slot.align)
    end for
    if a.fpsPic <> invalid then DrawPic(s, a.fpsPic, Z(a.fpsSlot.x, k), Z(a.fpsSlot.y, k), a.fpsSlot.align)
    fieldW = Int(r.w * a.sx + 0.5)
    fieldH = Int(r.h * a.sx + 0.5)
    if banner <> invalid and tp.view <> "signal" then DrawPic(s, banner, ox + fieldW \ 2, oy + Int(330 * a.sx), "c")
    ' the capsules are see-through around their rabbit ears: blended
    if g.capsules.Count() > 0 then
        s.SetAlphaEnable(true)
        for each cap in g.capsules
            pic = a.caps[cap.kind]
            x = ox + Int(cap.x * a.sx + 0.5)
            y = oy + Int(cap.y * a.sx + 0.5)
            if pic <> invalid then
                s.DrawObject(x, y, pic)
            else
                s.DrawRect(x, y, Int(34 * a.sx), Int(26 * a.sx), a.kit.capBody)
            end if
        end for
        s.SetAlphaEnable(false)
    end if
    TpDrawPaddle(st, a, g)
    bp = a.ballPx
    for each b in g.balls
        s.DrawRect(ox + Int(b.x * a.sx + 0.5), oy + Int(b.y * a.sx + 0.5), bp, bp, a.kit.ball)
    end for
    s.SetAlphaEnable(true)
    TpDrawFx(st, a, g)
    if tp.view = "signal" then
        TpDrawSignal(st, a, ox, oy, fieldW, fieldH)
        ' "NO SIGNAL" goes over the static
        if banner <> invalid then DrawPic(s, banner, ox + fieldW \ 2, oy + Int(330 * a.sx), "c")
    end if
    if a.scanTile <> invalid then s.DrawObject(ox, oy, a.scanTile)
    s.SwapBuffers()
    tp.fps.frames = tp.fps.frames + 1
end sub

' The words over the field: how to serve, a cleared wall, a caught capsule, the end.
function TpMessage(st as object, g as object) as string
    tp = st.tp
    if tp.view = "signal" then
        t = tp.signal.TotalMilliseconds()
        if tp.signalHow = "won" and t > 300 then return "ALL SIX WALLS CLEAR"
        if tp.signalHow = "over" and t > 650 then return "NO SIGNAL"
        return ""
    end if
    if g.state = "ready" then return "OK SERVES THE BALL"
    if g.state = "clear" then return "WALL " + (g.wall + 1).ToStr() + " CLEAR, OK FOR THE NEXT"
    for each fx in tp.fx
        if fx.kind = "banner" then
            if fx.t.TotalMilliseconds() < 1200 then return fx.text
        end if
    end for
    return ""
end function

' The paddle: a bar in the look's colour (a raised button on b0r3d 95), with drips while it's
' sticky.
sub TpDrawPaddle(st as object, a as object, g as object)
    s = st.screen
    k = st.k
    p = g.paddle
    r = g.rules
    tk = a.kit
    x = a.ox + Int(p.x * a.sx + 0.5)
    y = a.oy + Int(r.paddleY * a.sx + 0.5)
    w = Int(p.w * a.sx + 0.5)
    h = Int(r.paddleH * a.sx + 0.5)
    color = tk.paddle
    if g.stickyMs > 0 then color = tk.sticky
    if st.look = "b95" then
        B95Button(s, x, y, w, h, Z(2, k), st.kit)
        if g.stickyMs > 0 then s.DrawRect(x + Z(4, k), y + Z(4, k), w - Z(8, k), h - Z(8, k), color)
    else
        if st.look = "vcr" then s.DrawRect(x + Z(3, k), y + Z(3, k), w, h, tk.paddleEdge)
        s.DrawRect(x, y, w, h, color)
        if st.look = "cable" then s.DrawRect(x, y + h - Z(3, k), w, Z(3, k), tk.paddleEdge)
    end if
    if g.stickyMs > 0 then
        for each d in [0.2, 0.5, 0.8]
            s.DrawRect(x + Int(w * d), y + h, Z(3, k), Z(5 + Int(d * 4), k), color)
        end for
    end if
end sub

' A knocked-out brick flashes; a bomb's blast is a burst of static; finished ones are let go.
sub TpDrawFx(st as object, a as object, g as object)
    tp = st.tp
    if tp.fx.Count() = 0 then return
    s = st.screen
    r = g.rules
    kept = []
    for each fx in tp.fx
        t = fx.t.TotalMilliseconds()
        if fx.kind = "hit" and t < 120 then
            kept.Push(fx)
            b = fx.brick
            alpha = Int(200 * (1 - t / 120))
            s.DrawRect(a.ox + Int(b.x * a.sx + 0.5), a.oy + Int(b.y * a.sx + 0.5), a.bw, a.bh, (a.kit.flash and &hFFFFFF00) + alpha)
        else if fx.kind = "blast" and t < 160 and a.blastTiles <> invalid then
            kept.Push(fx)
            ' the bomb's place and the eight around it, a cell of static each (the cells off
            ' the field are left out), changing every 40 ms
            stepX = g.brickW + r.gap
            stepY = r.brickH + r.gap
            fieldW = Int(r.w * a.sx + 0.5)
            fieldH = Int(r.h * a.sx + 0.5)
            n = t \ 40
            for dr = -1 to 1
                for dc = -1 to 1
                    cx = Int((r.margin + (fx.col + dc) * stepX - r.gap / 2) * a.sx + 0.5)
                    cy = Int((r.brickTop + (fx.row + dr) * stepY - r.gap / 2) * a.sx + 0.5)
                    if cx >= 0 and cy >= 0 and cx + a.cellW <= fieldW and cy + a.cellH <= fieldH then
                        tile = a.blastTiles[n mod 3]
                        if tile <> invalid then s.DrawObject(a.ox + cx, a.oy + cy, tile)
                    end if
                    n = n + 1
                end for
            end for
        else if fx.kind = "banner" and t < 1200 then
            kept.Push(fx)
        end if
    end for
    tp.fx = kept
end sub

' The end of a run: the field's screen loses its signal (static rolling down), or, every wall
' cleared, it flashes.
sub TpDrawSignal(st as object, a as object, ox as integer, oy as integer, w as integer, h as integer)
    s = st.screen
    tp = st.tp
    t = tp.signal.TotalMilliseconds()
    if tp.signalHow = "over" then
        p = t / 500
        if p > 1 then p = 1
        bottom = oy + Int(h * p)
        rowH = h \ 40
        if rowH < 1 then rowH = 1
        unit = w \ 32
        if unit < 1 then unit = 1
        y = oy
        while y < bottom
            ' the last row stops where the static has got to, never past the field
            rh = rowH
            if y + rh > bottom then rh = bottom - y
            x = ox
            while x < ox + w
                part = unit * Rnd(6)
                if x + part > ox + w then part = ox + w - x
                s.DrawRect(x, y, part, rh, a.kit.noise[Rnd(4) - 1])
                x = x + part
            end while
            y = y + rowH
        end while
    else if t < 900 and (t \ 150) mod 2 = 0 then
        s.DrawRect(ox, oy, w, h, (a.kit.flash and &hFFFFFF00) + 140)
    end if
end sub

' --- The chrome, look by look. Each draws onto s (st.screen points at it while it draws) and
' returns the field's size and place (fs, fieldX, fieldY on the grid) and the numbers' slots
' (grid units; prefix: words drawn in front of the number). ---

function TpChromeCable(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    s.Clear(c.bg)
    s.DrawText("TEST PATTERN", Z(80, k), Z(24, k), c.white, f.title)
    slots = {}
    for each item in [["score", "SCORE", 470], ["lives", "LIVES", 690], ["wall", "WALL", 810], ["best", "BEST", 960]]
        DrawSpaced(s, item[1], Z(item[2], k), Z(14, k), c.soft, f.label, Z(2, k))
        slots[item[0]] = { x: item[2], y: 38, align: "l", font: f.value, color: c.white, bg: c.bg, prefix: "" }
    end for
    ' the field is a TV's screen: a frame, then the dark screen
    fx = 80
    fy = 92
    DrawFrame(s, Z(fx - 10, k), Z(fy - 10, k), Z(fx + 1120 + 10, k) - Z(fx - 10, k), Z(fy + 548 + 10, k) - Z(fy - 10, k), Z(4, k), c.frame)
    s.DrawRect(Z(fx, k), Z(fy, k), Z(fx + 1120, k) - Z(fx, k), Z(fy + 548, k) - Z(fy, k), a.kit.field)
    slots.effects = { x: fx + 14, y: fy + 12, align: "l", font: f.small, color: c.soft, bg: a.kit.field, prefix: "" }
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
    hx = DrawRun(s, hx + bw + Z(12, k), ty, "MOVE (HOLD FOR FASTER)", c.soft, f.bar, Z(1, k), Z(26, k))
    hx = DrawRun(s, hx, ty, "OK", c.white, f.bar, Z(1, k), Z(10, k))
    hx = DrawRun(s, hx, ty, "SERVE", c.soft, f.bar, Z(1, k), Z(26, k))
    hx = DrawRun(s, hx, ty, "PLAY", c.white, f.bar, Z(1, k), Z(10, k))
    DrawRun(s, hx, ty, "PAUSE", c.soft, f.bar, Z(1, k), 0)
    vty = top + (st.h - top - f.small.GetOneLineHeight()) \ 2
    DrawRight(s, st.version, Z(1240, k), vty, c.soft, f.small)
    fpsX = (Z(1240, k) - f.small.GetOneLineWidth(st.version, 99999) - Z(24, k)) / k
    return { fs: 1.0, fieldX: fx, fieldY: fy, slots: slots, fps: { x: fpsX, y: vty / k, align: "r", font: f.small, color: c.soft, bg: c.bar } }
end function

function TpChromePhosphor(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    glow = Z(1, k)
    s.Clear(c.bg)
    ' the header: the title over a rule, the numbers to its right
    left = Z(48, k)
    right = Z(1232, k)
    title = "B0R3D TV // TEST PATTERN"
    ty = Z(22, k) + (Z(84, k) - Z(22, k) - f.title.GetOneLineHeight()) \ 2
    DrawGlowSpaced(s, title, left, ty, c.full, c.glow, f.title, glow, Z(2, k))
    s.DrawRect(left, Z(84, k), right - left, Z(2, k), c.full)
    vy = ty / k + 6
    slots = {}
    slots.score = { x: 700, y: vy, align: "l", font: f.value, color: c.full, bg: c.bg, prefix: "SCORE " }
    slots.lives = { x: 930, y: vy, align: "l", font: f.value, color: c.full, bg: c.bg, prefix: "LIVES " }
    slots.wall = { x: 1232, y: vy, align: "r", font: f.value, color: c.full, bg: c.bg, prefix: "WALL " }
    ' the field: a glowing frame
    fs = 0.98
    fw = Int(1120 * fs)
    fh = Int(548 * fs)
    fx = (1280 - fw) \ 2
    fy = 96
    DrawFrame(s, Z(fx - 6, k), Z(fy - 6, k), Z(fx + fw + 6, k) - Z(fx - 6, k), Z(fy + fh + 6, k) - Z(fy - 6, k), Z(2, k), c.full)
    slots.best = { x: fx + fw - 12, y: fy + 10, align: "r", font: f.small, color: c.dim, bg: c.bg, prefix: "BEST " }
    slots.effects = { x: fx + 12, y: fy + 10, align: "l", font: f.small, color: c.dim, bg: c.bg, prefix: "" }
    ' the bar: the rule and the keys
    s.DrawRect(left, Z(646, k), right - left, Z(2, k), c.full)
    by = Z(650, k) + (Z(698, k) - Z(650, k) - f.bar.GetOneLineHeight()) \ 2
    DrawGlow(s, "[<>] MOVE (HOLD FOR FASTER)   [OK] SERVE   [PLAY] PAUSE", left, by, c.full, c.glow, f.bar, glow)
    if st.scan then DrawScanlines(st, s)
    return { fs: fs, fieldX: fx, fieldY: fy, slots: slots, fps: { x: 1232, y: by / k, align: "r", font: f.bar, color: c.dim, bg: c.bg } }
end function

function TpChromeRgb(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    s.Clear(c.bg)

    left = Z(48, k)
    right = Z(1232, k)
    ' the score, lives and wall along the field's top are the title line; the field's lit edge is
    ' the look's mark

    fs = 0.98
    fw = Int(1120 * fs)
    fh = Int(548 * fs)
    fx = (1280 - fw) \ 2
    fy = 96

    ' the field's size on screen, worked out the way the game works it out (TpDrawField), so the
    ' lit edge hugs it at every screen size
    fieldW = Int(1120 * fs * k + 0.5)
    fieldH = Int(548 * fs * k + 0.5)
    s.DrawRect(Z(fx, k), Z(fy, k), fieldW, fieldH, c.well)
    DrawRgbEdge(st, s, Z(fx, k), Z(fy, k), fieldW, fieldH)

    slots = {}
    slots.score = { x: fx + 16, y: fy - 40, align: "l", font: f.value, color: c.white, bg: c.bg, prefix: "SCORE " }
    slots.lives = { x: fx + 360, y: fy - 40, align: "l", font: f.value, color: c.green, bg: c.bg, prefix: "LIVES " }
    slots.wall = { x: fx + fw \ 2, y: fy - 40, align: "c", font: f.value, color: c.accent, bg: c.bg, prefix: "WALL " }
    ' these two sit inside the field, so their pictures are filled with the field's colour
    slots.best = { x: fx + fw - 16, y: fy + 10, align: "r", font: f.small, color: c.dim, bg: c.well, prefix: "BEST " }
    slots.effects = { x: fx + 16, y: fy + 10, align: "l", font: f.small, color: c.accent, bg: c.well, prefix: "" }

    s.DrawRect(left, Z(646, k), right - left, Z(2, k), c.rule)
    by = Z(650, k) + (Z(698, k) - Z(650, k) - f.bar.GetOneLineHeight()) \ 2
    hints = "[<>] MOVE (HOLD FOR FASTER)   [OK] SERVE   [PLAY] PAUSE"
    s.DrawText(hints, left, by, c.dim, f.bar)
    return { fs: fs, fieldX: fx, fieldY: fy, slots: slots, fps: { x: 1232, y: by / k, align: "r", font: f.bar, color: c.dim, bg: c.bg } }
end function

function TpChromeVcr(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    sh = Z(3, k)
    s.Clear(c.bg)
    DrawVcrTitle(st, "TEST PATTERN")
    DrawVcrChannel(st, GameChannel("game.test"), Z(26, k))
    slots = {}
    slots.score = { x: 420, y: 40, align: "l", font: f.value, color: c.white, bg: c.bg, prefix: "SCORE " }
    slots.lives = { x: 420, y: 80, align: "l", font: f.value, color: c.white, bg: c.bg, prefix: "LIVES " }
    slots.wall = { x: 700, y: 40, align: "l", font: f.value, color: c.white, bg: c.bg, prefix: "WALL " }
    slots.best = { x: 700, y: 80, align: "l", font: f.value, color: c.yellow, bg: c.bg, prefix: "BEST " }
    ' the field: a white frame with a hard shadow, like the picture-in-picture box
    fs = 0.89
    fw = Int(1120 * fs)
    fh = Int(548 * fs)
    fx = (1280 - fw) \ 2
    fy = 142
    x = Z(fx - 8, k)
    y = Z(fy - 8, k)
    w = Z(fx + fw + 8, k) - x
    h = Z(fy + fh + 8, k) - y
    s.DrawRect(x + Z(5, k), y + Z(5, k), w, h, c.shadow)
    s.DrawRect(x, y, w, h, c.white)
    s.DrawRect(Z(fx, k), Z(fy, k), Z(fx + fw, k) - Z(fx, k), Z(fy + fh, k) - Z(fy, k), a.kit.field)
    slots.effects = { x: fx + 12, y: fy + 10, align: "l", font: f.small, color: c.yellow, bg: a.kit.field, prefix: "" }
    ' the keys and the version
    ty = Z(650, k) + (Z(48, k) - f.bar.GetOneLineHeight()) \ 2
    DrawShadowed(s, "LEFT/RIGHT:MOVE   OK:SERVE   PLAY:PAUSE", Z(56, k), ty, c.white, c.shadow, f.bar, sh)
    vw = f.bar.GetOneLineWidth(st.version, 99999)
    DrawShadowed(s, st.version, Z(1224, k) - vw, ty, c.white, c.shadow, f.bar, sh)
    fpsX = (Z(1224, k) - vw - Z(24, k)) / k
    return { fs: fs, fieldX: fx, fieldY: fy, slots: slots, fps: { x: fpsX, y: ty / k, align: "r", font: f.small, color: c.white, bg: c.bg } }
end function

function TpChromeB95(st as object, s as object, a as object) as object
    c = st.kit
    k = st.k
    f = a.fonts
    t = Z(2, k)
    s.Clear(c.bg)
    DrawB95Desktop(st, invalid, "Test Pattern")
    wx = Z(180, k)
    wy = Z(18, k)
    ww = Z(1260, k) - wx
    wh = Z(662, k) - wy
    B95Raised(s, wx, wy, ww, wh, t, c)
    DrawB95TitleBar(st, wx + Z(4, k), wy + Z(4, k), ww - Z(8, k), "Test Pattern", true)
    mx = wx + Z(14, k)
    for each word in ["Game", "View", "Help"]
        DrawB95Underlined(st, word, mx, Z(64, k) + (Z(30, k) - st.fonts.menu.GetOneLineHeight()) \ 2, st.fonts.menu)
        mx = mx + st.fonts.menu.GetOneLineWidth(word, 99999) + Z(22, k)
    end for
    slots = {}
    vy = 68
    slots.score = { x: 470, y: vy, align: "l", font: f.value, color: c.black, bg: c.face, prefix: "Score: " }
    slots.lives = { x: 690, y: vy, align: "l", font: f.value, color: c.black, bg: c.face, prefix: "Lives: " }
    slots.wall = { x: 820, y: vy, align: "l", font: f.value, color: c.black, bg: c.face, prefix: "Wall: " }
    slots.best = { x: 1244, y: vy, align: "r", font: f.value, color: c.black, bg: c.face, prefix: "Best: " }
    ' the field: a sunken black screen
    fs = 0.93
    fw = Int(1120 * fs)
    fh = Int(548 * fs)
    fx = 720 - fw \ 2
    fy = 102
    B95Sunken(s, Z(fx - 4, k), Z(fy - 4, k), Z(fx + fw + 4, k) - Z(fx - 4, k), Z(fy + fh + 4, k) - Z(fy - 4, k), t, c, a.kit.field)
    slots.effects = { x: fx + 12, y: fy + 10, align: "l", font: f.small, color: c.white, bg: a.kit.field, prefix: "" }
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
    hints = "Hold left or right to move (it speeds up) · OK serves · Play pauses"
    B95Text(s, B95Trim(hints, f.bar, c1w - Z(16, k), k, false), c1x + Z(8, k), textY, c.black, f.bar, k, false)
    B95Text(s, B95Trim(st.version, f.bar, c2w - Z(16, k), k, false), c2x + Z(8, k), textY, c.black, f.bar, k, false)
    return { fs: fs, fieldX: fx, fieldY: fy, slots: slots, fps: { x: fx + fw - 12, y: fy + 10, align: "r", font: f.small, color: c.white, bg: a.kit.field } }
end function

' --- The demo in the guide's preview box: Test Pattern playing itself, a step every 100 ms. ---

function TpDemo(st as object) as object
    if st.tpDemo = invalid then
        st.tpDemo = TpDemoNew(GameSeed())
        st.tpDemoTimer = CreateObject("roTimespan")
    end if
    ms = st.tpDemoTimer.TotalMilliseconds()
    if ms >= 95 then
        n = (ms + 5) \ 100
        if ms > 2000 then n = 1
        for i = 1 to n
            TpDemoStep(st.tpDemo, 100)
        end for
        st.tpDemoTimer.Mark()
    end if
    return st.tpDemo
end function

' The demo, fitted in the preview box (x, y, w, h in pixels): the wall's colour bars, the
' paddle, the balls, and "TEST PATTERN · WALL 02" under it.
sub DrawTpPreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    d = TpDemo(st)
    g = d.g
    r = g.rules
    k = st.k
    kit = TpKit(st)
    bars = TpBars(st)
    f = st.fonts
    font = f.pageInfo
    if st.look = "cable" then font = f.barNum
    if st.look = "phosphor" then font = f.head
    textH = font.GetOneLineHeight() + Z(6, k)
    inset = Z(10, k)
    sx = (w - 2 * inset) / r.w
    sy = (h - 2 * inset - textH) / r.h
    if sy < sx then sx = sy
    fw = Int(r.w * sx)
    fh = Int(r.h * sx)
    ox = x + (w - fw) \ 2
    oy = y + inset
    s.DrawRect(ox, oy, fw, fh, kit.field)
    bw = Int(g.brickW * sx + 0.5)
    bh = Int(r.brickH * sx + 0.5)
    if bw < 1 then bw = 1
    if bh < 1 then bh = 1
    for each b in g.bricks
        if b.alive then
            color = bars[b.bar]
            if b.kind = "bomb" then color = kit.noise[Rnd(4) - 1]
            s.DrawRect(ox + Int(b.x * sx), oy + Int(b.y * sx), bw, bh, color)
        end if
    end for
    for each cap in g.capsules
        s.DrawRect(ox + Int(cap.x * sx), oy + Int(cap.y * sx), Int(34 * sx) + 1, Int(26 * sx) + 1, kit.capBody)
    end for
    p = g.paddle
    s.DrawRect(ox + Int(p.x * sx), oy + Int(r.paddleY * sx), Int(p.w * sx), Int(r.paddleH * sx) + 1, kit.paddle)
    bp = Int(r.ball * sx) + 1
    for each b in g.balls
        s.DrawRect(ox + Int(b.x * sx), oy + Int(b.y * sx), bp, bp, kit.ball)
    end for
    if g.state = "over" then
        for i = 0 to 23
            s.DrawRect(ox, oy + i * fh \ 24, fw, fh \ 24 + 1, kit.noise[Rnd(4) - 1])
        end for
    end if
    label = "TEST PATTERN, WALL " + TwoDigits(g.wall + 1)
    if st.look = "cable" then label = "TEST PATTERN · WALL " + TwoDigits(g.wall + 1)
    if st.look = "phosphor" then label = "TEST PATTERN :: WALL " + TwoDigits(g.wall + 1)
    if st.look = "b95" then label = "Test Pattern, wall " + (g.wall + 1).ToStr()
    color = StuckPreviewSoft(st)
    if st.look = "b95" then color = st.kit.white
    lw = GTextWidth(st, label, font, false)
    GText(st, s, label, ox + fw - lw, oy + fh + Z(4, k), font, color, false)
end sub
