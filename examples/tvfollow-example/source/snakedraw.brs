' Snake on screen, in the current look: the play screen (the 20x20 arena in the center,
' the score and best counters on the left, length and speed on the right, remote keys
' along the bottom) and the autoplay demo in the guide's preview box. Drawn the way
' Stuck Pixels and Test Pattern are: what stays put (the arena's grid included) goes once
' into a picture of the whole screen (the chrome), each number into a small picture of its
' own when it changes, and each frame copies those and draws the snake and the food.

function SnakeKit(st as object) as object
    c = st.kit
    if st.look = "phosphor" then
        return {
            field: c.bg,
            grid: c.faint,
            snake: c.soft,
            head: c.full,
            food: c.full,
            border: c.full,
            bannerBg: c.bg,
            bannerFg: c.full,
            noise: c.noise,
            label: c.dim,
            value: c.full,
            flash: c.full
        }
    end if
    if st.look = "vcr" then
        wells = [&h0B1A7AFF, &h0E0E0EFF, &h06401FFF]
        empties = [&h1A2A9AFF, &h1C1C1CFF, &h0D5530FF]
        return {
            field: wells[st.colour],
            grid: empties[st.colour],
            snake: c.white,
            head: c.yellow,
            food: &hFF3B5CFF,
            border: c.white,
            bannerBg: c.shadow,
            bannerFg: c.yellow,
            noise: c.noise,
            label: c.yellow,
            value: c.white,
            flash: c.white
        }
    end if
    if st.look = "b95" then
        return {
            field: &h000000FF,
            grid: &h1A1A1AFF,
            snake: &h00C000FF,
            head: &h39FF88FF,
            food: &hFF2244FF,
            border: c.shadow,
            bannerBg: c.navy,
            bannerFg: c.white,
            noise: c.noise,
            label: c.black,
            value: c.black,
            flash: &hFFFFFFFF
        }
    end if
    if st.look = "rgb" then
        return {
            field: c.well,
            grid: c.rule,
            snake: c.green,
            head: &hA6FFCEFF,
            food: &hFF3B5CFF,
            border: c.dim,
            bannerBg: c.card,
            bannerFg: c.accent,
            noise: c.noise,
            label: c.green,
            value: c.white,
            flash: c.white
        }
    end if
    ' Cable look (default)
    return {
        field: &h050B22FF,
        grid: &h0D183BFF,
        snake: &h39FF88FF,
        head: &hA6FFCEFF,
        food: &hFF3B5CFF,
        border: &h1F3170FF,
        bannerBg: c.ink,
        bannerFg: c.accent,
        noise: c.noise,
        label: c.soft,
        value: c.white,
        flash: c.white
    }
end function

function SnakeFonts(st as object) as object
    k = st.k
    if st.look = "phosphor" then
        return {
            title: Vt323(st, Z(38, k)),
            label: Vt323(st, Z(26, k)),
            value: Vt323(st, Z(36, k)),
            bar: Vt323(st, Z(24, k)),
            banner: Vt323(st, Z(56, k)),
            box: Vt323(st, Z(22, k))
        }
    end if
    if st.look = "vcr" then
        return {
            title: Silk(st, Z(30, k), true),
            label: Silk(st, Z(16, k), false),
            value: Silk(st, Z(30, k), true),
            bar: Silk(st, Z(16, k), false),
            banner: Silk(st, Z(38, k), true),
            box: Silk(st, Z(14, k), false)
        }
    end if
    if st.look = "b95" then
        return {
            title: W95(st, Z(24, k)),
            label: W95(st, Z(18, k)),
            value: W95(st, Z(22, k)),
            bar: W95(st, Z(18, k)),
            banner: W95(st, Z(34, k)),
            box: W95(st, Z(16, k))
        }
    end if
    if st.look = "rgb" then
        return {
            title: Silk(st, Z(30, k), true),
            label: Silk(st, Z(16, k), true),
            value: Silk(st, Z(30, k), true),
            bar: Silk(st, Z(16, k), false),
            banner: Silk(st, Z(40, k), true),
            box: Silk(st, Z(16, k), false)
        }
    end if
    return {
        title: Barlow(st, "extrabold", Z(40, k)),
        label: Barlow(st, "bold", Z(19, k)),
        value: Vt323(st, Z(42, k)),
        bar: Barlow(st, "bold", Z(20, k)),
        banner: Barlow(st, "extrabold", Z(46, k)),
        box: Barlow(st, "bold", Z(18, k))
    }
end function

' --- The pictures a run is drawn from ---

' The chrome, its numbers' places and the arena's scanlines. Made again when the look, its
' colour, the screen size or the scanlines change; on b0r3d 95 only the taskbar is drawn
' again, into the chrome, when its clock moves on. If the TV runs out of picture memory, the
' chrome is drawn straight onto the screen every frame instead.
function SnakeArt(st as object) as object
    sn = st.snake
    key = st.look + "|" + st.colour.ToStr() + "|" + st.w.ToStr() + "|" + OnOff(st.scan)
    if sn.art <> invalid then
        if sn.art.key = key then
            if st.look = "b95" and sn.art.chrome <> invalid and sn.art.clockText <> ClockText(st, false) then
                keep = st.screen
                st.screen = sn.art.chrome
                DrawB95Taskbar(st, "Snake")
                st.screen = keep
                sn.art.clockText = ClockText(st, false)
            end if
            return sn.art
        end if
    end if
    ' the old pictures go first, so the TV never holds two whole-screen pictures at once
    sn.art = invalid
    k = st.k
    rules = SnakeRules()
    a = {
        key: key,
        kit: SnakeKit(st),
        fonts: SnakeFonts(st),
        chrome: invalid,
        vals: {},
        banners: {},
        fpsPic: invalid,
        scanTile: invalid,
        cols: rules.cols,
        rows: rules.rows,
        P: Z(25, k),
        B: Z(23, k)
    }
    a.arenaW = a.cols * a.P
    a.arenaH = a.rows * a.P
    a.arenaX = (st.w - a.arenaW) \ 2
    ' the arena's top sits on a scanline (a multiple of 4 grid units), so the scanlines over it
    ' line up with the chrome's
    a.arenaY = Z(104, k)
    chrome = NewPicture(st.w, st.h, true)
    if chrome <> invalid then
        ' st.screen points at the chrome while it's drawn, as the shared helpers draw there
        keep = st.screen
        st.screen = chrome
        a.slots = SnakeChromeFor(st, chrome, a)
        st.screen = keep
    else
        a.slots = SnakeChromeFor(st, st.screen, a)
    end if
    a.chrome = chrome
    a.clockText = ClockText(st, false)
    if ScanlinesOn(st) then a.scanTile = ScanTile(st, a.arenaW, a.arenaH)
    sn.art = a
    return a
end function

' The arena's screen and its grid.
sub DrawSnakeArena(st as object, s as object, a as object)
    k = st.k
    ax = a.arenaX
    ay = a.arenaY
    s.DrawRect(ax, ay, a.arenaW, a.arenaH, a.kit.field)
    line = Z(1, k)
    for col = 1 to a.cols - 1
        s.DrawRect(ax + col * a.P, ay, line, a.arenaH, a.kit.grid)
    end for
    for row = 1 to a.rows - 1
        s.DrawRect(ax, ay + row * a.P, a.arenaW, line, a.kit.grid)
    end for
end sub

' What stays put, in the look's way. Draws onto s (st.screen points at it too) and returns
' where each number goes: { x, y (pixels), align, font, color, bg }.
function SnakeChromeFor(st as object, s as object, a as object) as object
    k = st.k
    kit = a.kit
    f = a.fonts
    c = st.kit
    ax = a.arenaX
    ay = a.arenaY
    aw = a.arenaW
    ah = a.arenaH
    slots = {}

    if st.look = "b95" then
        s.Clear(c.bg)
        ' the arena in a window, with its title bar
        B95Raised(s, ax - Z(18, k), ay - Z(42, k), aw + Z(36, k), ah + Z(60, k), Z(3, k), c)
        s.DrawRect(ax - Z(15, k), ay - Z(39, k), aw + Z(30, k), Z(24, k), c.navy)
        GText(st, s, "Snake", ax - Z(8, k), ay - Z(35, k), f.box, c.white, true)
        B95Sunken(s, ax - Z(4, k), ay - Z(4, k), aw + Z(8, k), ah + Z(8, k), Z(2, k), c, invalid)
        DrawSnakeArena(st, s, a)
        ' left: the score and the best
        B95Sunken(s, Z(60, k), ay, Z(280, k), Z(150, k), Z(2, k), c, c.face)
        GText(st, s, "SCORE", Z(80, k), ay + Z(14, k), f.label, c.black, true)
        GText(st, s, "BEST", Z(80, k), ay + Z(80, k), f.label, c.black, true)
        ' right: the length, the speed and the keys
        rx = st.w - Z(340, k)
        B95Sunken(s, rx, ay, Z(280, k), Z(220, k), Z(2, k), c, c.face)
        GText(st, s, "LENGTH", rx + Z(20, k), ay + Z(14, k), f.label, c.black, true)
        GText(st, s, "SPEED", rx + Z(20, k), ay + Z(80, k), f.label, c.black, true)
        GText(st, s, "KEYS", rx + Z(20, k), ay + Z(144, k), f.box, c.navy, true)
        GText(st, s, "Arrows: steer", rx + Z(20, k), ay + Z(170, k), f.box, c.dark, false)
        GText(st, s, "Play or Back: pause", rx + Z(20, k), ay + Z(192, k), f.box, c.dark, false)
        DrawB95Taskbar(st, "Snake")
        slots.score = { x: Z(80, k), y: ay + Z(40, k), align: "l", font: f.value, color: c.black, bg: c.face }
        slots.best = { x: Z(80, k), y: ay + Z(106, k), align: "l", font: f.value, color: c.black, bg: c.face }
        slots.length = { x: rx + Z(20, k), y: ay + Z(40, k), align: "l", font: f.value, color: c.black, bg: c.face }
        slots.speed = { x: rx + Z(20, k), y: ay + Z(106, k), align: "l", font: f.value, color: c.black, bg: c.face }
        ' the counter sits at the right of the window's title bar
        slots.fps = { x: ax + aw + Z(8, k), y: ay - Z(35, k), align: "r", font: f.box, color: c.white, bg: c.navy }
        return slots
    end if

    s.Clear(c.bg)
    ' the title
    if st.look = "rgb" then
        s.DrawRect(Z(60, k), Z(30, k), st.w - Z(120, k), Z(2, k), c.rule)
        DrawRgbTriad(s, Z(60, k), Z(40, k), Z(36, k), Z(14, k), c)
        GText(st, s, "SNAKE // CHANNEL 23", Z(110, k), Z(36, k), f.title, c.green, true)
    else if st.look = "phosphor" then
        GText(st, s, "[ 23  SNAKE ]", Z(60, k), Z(36, k), f.title, c.full, true)
    else if st.look = "vcr" then
        GText(st, s, "CH 23  SNAKE", Z(60, k), Z(36, k), f.title, c.white, true)
    else
        GText(st, s, "SNAKE", Z(60, k), Z(34, k), f.title, c.accent, true)
        GText(st, s, "CH 23", Z(200, k), Z(44, k), f.label, c.soft, false)
    end if

    ' the arena's frame
    borderW = Z(3, k)
    s.DrawRect(ax - borderW, ay - borderW, aw + 2 * borderW, ah + 2 * borderW, kit.border)

    ' left: the score and the best
    lx = Z(60, k)
    lw = ax - lx - Z(40, k)
    if lw > Z(280, k) then lw = Z(280, k)
    SnakeBox(st, s, a, "SCORE", lx, ay, lw, Z(100, k))
    SnakeBox(st, s, a, "BEST", lx, ay + Z(120, k), lw, Z(100, k))

    ' right: the length, the speed and the keys
    rx = ax + aw + Z(40, k)
    rw = st.w - rx - Z(60, k)
    if rw > Z(280, k) then rw = Z(280, k)
    SnakeBox(st, s, a, "LENGTH", rx, ay, rw, Z(100, k))
    SnakeBox(st, s, a, "SPEED", rx, ay + Z(120, k), rw, Z(100, k))
    s.DrawRect(rx, ay + Z(240, k), rw, Z(110, k), kit.border)
    s.DrawRect(rx + Z(2, k), ay + Z(242, k), rw - Z(4, k), Z(106, k), kit.field)
    GText(st, s, "KEYS", rx + Z(16, k), ay + Z(254, k), f.box, kit.label, true)
    GText(st, s, "Arrows: steer", rx + Z(16, k), ay + Z(284, k), f.box, kit.value, false)
    GText(st, s, "Play or Back: pause", rx + Z(16, k), ay + Z(314, k), f.box, kit.value, false)

    slots.score = { x: lx + Z(16, k), y: ay + Z(44, k), align: "l", font: f.value, color: kit.value, bg: kit.field }
    slots.best = { x: lx + Z(16, k), y: ay + Z(164, k), align: "l", font: f.value, color: kit.value, bg: kit.field }
    slots.length = { x: rx + Z(16, k), y: ay + Z(44, k), align: "l", font: f.value, color: kit.value, bg: kit.field }
    slots.speed = { x: rx + Z(16, k), y: ay + Z(164, k), align: "l", font: f.value, color: kit.value, bg: kit.field }
    slots.fps = SnakeBar(st, s, a)

    ' the scanlines go over everything but the arena, which gets its own over the moving parts
    if ScanlinesOn(st) then DrawScanlines(st, s)
    DrawSnakeArena(st, s, a)
    return slots
end function

' A box with its label at the top, for a number.
sub SnakeBox(st as object, s as object, a as object, label as string, x as integer, y as integer, w as integer, h as integer)
    k = st.k
    s.DrawRect(x, y, w, h, a.kit.border)
    s.DrawRect(x + Z(2, k), y + Z(2, k), w - Z(4, k), h - Z(4, k), a.kit.field)
    GText(st, s, label, x + Z(16, k), y + Z(14, k), a.fonts.label, a.kit.label, true)
end sub

' The keys and the version along the bottom, in the look's way. Returns the place of the
' frames-per-second counter, beside the version.
function SnakeBar(st as object, s as object, a as object) as object
    k = st.k
    c = st.kit
    f = a.fonts
    left = Z(60, k)
    right = st.w - Z(60, k)
    hints = "ARROWS STEER  ·  PLAY OR BACK PAUSES"
    hintColor = c.dim
    versionColor = c.dim
    bg = c.bg
    if st.look = "cable" then
        top = Z(668, k)
        s.DrawRect(0, top, st.w, st.h - top, c.bar)
        ty = top + (st.h - top - f.bar.GetOneLineHeight()) \ 2
        hintColor = c.soft
        versionColor = c.soft
        bg = c.bar
    else
        rule = c.rule
        if st.look = "phosphor" then
            rule = c.full
            hintColor = c.full
        else if st.look = "vcr" then
            rule = c.white
            hintColor = c.white
            versionColor = c.white
        end if
        s.DrawRect(left, Z(646, k), right - left, Z(2, k), rule)
        ty = Z(650, k) + (Z(698, k) - Z(650, k) - f.bar.GetOneLineHeight()) \ 2
    end if
    GText(st, s, hints, left, ty, f.bar, hintColor, false)
    vw = GTextWidth(st, st.version, f.bar, false)
    GText(st, s, st.version, right - vw, ty, f.bar, versionColor, false)
    return { x: right - vw - Z(24, k), y: ty, align: "r", font: f.bar, color: versionColor, bg: bg }
end function

' What each number reads now.
function SnakeValueTexts(st as object, g as object) as object
    return {
        score: g.score.ToStr(),
        best: st.snake.bestText,
        length: g.snake.Count().ToStr(),
        speed: g.speed.ToStr() + " ms"
    }
end function

' Each number as a picture, made again only when it changes.
sub UpdateSnakeValues(st as object, a as object, g as object)
    texts = SnakeValueTexts(st, g)
    for each name in texts
        text = texts[name]
        slot = a.slots[name]
        if slot <> invalid then
            old = a.vals[name]
            fresh = (old = invalid)
            if not fresh then fresh = (old.text <> text)
            if fresh then
                bg = slot.bg
                ' over the chrome's scanlines, a see-through picture keeps them showing
                if ScanlinesOn(st) then bg = &h00000000
                a.vals[name] = TextPicture(st, text, slot.font, slot.color, bg, true)
            end if
        end if
    end for
end sub

sub UpdateSnakeFps(st as object, a as object)
    slot = a.slots.fps
    if not ShowsFps() or slot = invalid then return
    f = st.snake.fps
    ms = f.timer.TotalMilliseconds()
    if ms < 1000 then return
    shown = 0
    if ms > 0 then shown = Int(f.frames * 1000 / ms + 0.5)
    f.frames = 0
    f.timer.Mark()
    bg = slot.bg
    if ScanlinesOn(st) then bg = &h00000000
    a.fpsPic = TextPicture(st, shown.ToStr() + " FPS", slot.font, slot.color, bg, false)
end sub

' The end-of-run banner, as a picture made once per text, or invalid.
function SnakeBannerPic(st as object, a as object) as dynamic
    sn = st.snake
    if sn.view <> "signal" then return invalid
    text = "GAME OVER"
    if sn.signalHow = "won" then text = "ARENA CLEAR"
    pic = a.banners[text]
    if pic = invalid then
        pic = TextPicture(st, " " + text + " ", a.fonts.banner, a.kit.bannerFg, a.kit.bannerBg, true)
        a.banners[text] = pic
    end if
    return pic
end function

' --- A frame ---

sub DrawSnakePlay(st as object)
    s = st.screen
    sn = st.snake
    if s = invalid or sn.g = invalid then return
    g = sn.g
    a = SnakeArt(st)
    k = st.k
    kit = a.kit
    ax = a.arenaX
    ay = a.arenaY
    p = a.P
    b = a.B
    ' every picture is brought up to date first (Roku OS 16.3 refuses a change to a picture
    ' between drawing it and showing the frame)...
    UpdateSnakeValues(st, a, g)
    UpdateSnakeFps(st, a)
    banner = SnakeBannerPic(st, a)

    ' ...then the frame: the chrome without blending (quicker), the food and the snake...
    if a.chrome <> invalid then
        s.SetAlphaEnable(false)
        s.DrawObject(0, 0, a.chrome)
    else
        SnakeChromeFor(st, s, a)
        s.SetAlphaEnable(false)
    end if
    fx = ax + g.food.x * p + Z(2, k)
    fy = ay + g.food.y * p + Z(2, k)
    fw = b - Z(2, k)
    s.DrawRect(fx, fy, fw, fw, kit.food)
    s.DrawRect(fx + Z(2, k), fy + Z(2, k), fw - Z(4, k), fw - Z(4, k), kit.flash)
    for i = 0 to g.snake.Count() - 1
        seg = g.snake[i]
        sx = ax + seg.x * p + Z(1, k)
        sy = ay + seg.y * p + Z(1, k)
        segColor = kit.snake
        if i = 0 then segColor = kit.head
        s.DrawRect(sx, sy, b, b, segColor)
        if i = 0 then
            ' the head's eyes, looking the way it goes
            eye = Z(3, k)
            if eye < 2 then eye = 2
            if g.dir.x = 1 then
                s.DrawRect(sx + b - eye - Z(2, k), sy + Z(3, k), eye, eye, kit.field)
                s.DrawRect(sx + b - eye - Z(2, k), sy + b - eye - Z(3, k), eye, eye, kit.field)
            else if g.dir.x = -1 then
                s.DrawRect(sx + Z(2, k), sy + Z(3, k), eye, eye, kit.field)
                s.DrawRect(sx + Z(2, k), sy + b - eye - Z(3, k), eye, eye, kit.field)
            else if g.dir.y = 1 then
                s.DrawRect(sx + Z(3, k), sy + b - eye - Z(2, k), eye, eye, kit.field)
                s.DrawRect(sx + b - eye - Z(3, k), sy + b - eye - Z(2, k), eye, eye, kit.field)
            else
                s.DrawRect(sx + Z(3, k), sy + Z(2, k), eye, eye, kit.field)
                s.DrawRect(sx + b - eye - Z(3, k), sy + Z(2, k), eye, eye, kit.field)
            end if
        end if
    end for

    ' ...then, blended, the numbers, the end-of-run banner and the arena's scanlines
    s.SetAlphaEnable(true)
    for each name in ["score", "best", "length", "speed"]
        slot = a.slots[name]
        if slot <> invalid then DrawPic(s, a.vals[name], slot.x, slot.y, slot.align)
    end for
    if banner <> invalid then
        bw = banner.w + Z(40, k)
        bh = banner.h + Z(36, k)
        bx = (st.w - bw) \ 2
        by = ay + (a.arenaH - bh) \ 2
        s.DrawRect(bx - Z(3, k), by - Z(3, k), bw + Z(6, k), bh + Z(6, k), kit.border)
        s.DrawRect(bx, by, bw, bh, kit.bannerBg)
        DrawPic(s, banner, st.w \ 2, by + Z(18, k) + banner.pad, "c")
    end if
    if a.scanTile <> invalid then s.DrawObject(ax, ay, a.scanTile)
    if a.fpsPic <> invalid then DrawPic(s, a.fpsPic, a.slots.fps.x, a.slots.fps.y, a.slots.fps.align)
    s.SwapBuffers()
    sn.fps.frames = sn.fps.frames + 1
end sub

' Guide preview autoplay box
function SnakeDemo(st as object) as object
    if st.snakeDemo = invalid then
        st.snakeDemo = SnakeDemoNew(GameSeed())
        st.snakeDemoTimer = CreateObject("roTimespan")
    end if
    ms = st.snakeDemoTimer.TotalMilliseconds()
    if ms >= 100 then
        n = (ms + 10) \ 110
        if ms > 2000 then n = 1
        for i = 1 to n
            SnakeDemoStep(st.snakeDemo, 110)
        end for
        st.snakeDemoTimer.Mark()
    end if
    return st.snakeDemo
end function

sub DrawSnakePreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    d = SnakeDemo(st)
    g = d.g
    k = st.k
    kit = SnakeKit(st)
    f = st.fonts

    mP = Z(7, k)
    if mP < 4 then mP = 4
    mB = mP - 1
    ww = g.cols * mP
    wh = g.rows * mP

    titleFont = f.pageTitle
    statFont = f.pageInfo
    if st.look = "cable" then statFont = f.barNum
    if st.look = "phosphor" then statFont = f.row
    title = "SNAKE"
    if st.look = "b95" then title = "Snake"

    gap = Z(24, k)
    textW = GTextWidth(st, title, titleFont, true)
    if ww + gap + textW > w - Z(16, k) then
        titleFont = statFont
        textW = GTextWidth(st, title, titleFont, true)
    end if
    if textW < Z(140, k) then textW = Z(140, k)

    ox = x + (w - ww - gap - textW) \ 2
    oy = y + (h - wh) \ 2

    frameColor = kit.border
    if st.look = "cable" then frameColor = &h8FB4FFFF
    if st.look = "vcr" then frameColor = st.kit.white
    if st.look = "phosphor" then frameColor = st.kit.full
    if st.look = "b95" then frameColor = &h808080FF

    inset = Z(2, k)
    s.DrawRect(ox - inset - Z(1, k), oy - inset - Z(1, k), ww + 2 * inset + Z(2, k), wh + 2 * inset + Z(2, k), frameColor)
    s.DrawRect(ox, oy, ww, wh, kit.field)

    ' Draw food
    s.DrawRect(ox + g.food.x * mP, oy + g.food.y * mP, mB, mB, kit.food)

    ' Draw snake
    for i = 0 to g.snake.Count() - 1
        seg = g.snake[i]
        c = kit.snake
        if i = 0 then c = kit.head
        s.DrawRect(ox + seg.x * mP, oy + seg.y * mP, mB, mB, c)
    end for

    ' Text info beside mini board
    tx = ox + ww + gap
    ty = oy + Z(4, k)
    tColor = kit.bannerFg
    if st.look = "cable" then tColor = &hFFD23FFF
    if st.look = "phosphor" then tColor = kit.head
    if st.look = "vcr" then tColor = &hFFFFFFFF
    if st.look = "b95" then tColor = &h000000FF

    GText(st, s, title, tx, ty, titleFont, tColor, true)
    ty = ty + titleFont.GetOneLineHeight() + Z(6, k)

    infoColor = kit.label

    GText(st, s, "SCORE: " + g.score.ToStr(), tx, ty, statFont, infoColor, false)
    ty = ty + statFont.GetOneLineHeight() + Z(4, k)
    GText(st, s, "LENGTH: " + g.snake.Count().ToStr(), tx, ty, statFont, infoColor, false)
    ty = ty + statFont.GetOneLineHeight() + Z(4, k)
    GText(st, s, "SPEED: " + g.speed.ToStr() + " ms", tx, ty, statFont, infoColor, false)
end sub
