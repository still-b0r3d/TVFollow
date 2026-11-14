' The RGB LED lights look: the preview box is a TV hung on a dark wall with an LED strip hidden
' behind it, its colours washing out onto the wall as a soft halo that flows slowly round the
' screen, one lap every 30 seconds. Everything else stays quiet: white type on the dark wall, the
' focused row lit by a short LED bar in the colour of the light beside it. The Settings pages
' sit on a panel with the same light round it.
'
' The soft shapes are pictures made once by tools/make-rgb-light.js (app/images/rgb/), at the
' size for a 1920 x 1080 screen: the glow round the TV and round the panel (at half size), the
' focused row's light, the badge, the remote's keys, the star and the arrows. Each frame draws
' the LED colours as four stretched slices of the colourway's loop plus a flat rect in each
' corner, then the glow picture on top, which turns them into light on the wall (the picture is
' see-through where the light is strong and plain wall further out). Colour cycle draws one flat
' colour instead. Positions below are full-HD numbers, scaled to the screen by RgbPx.
' Its colours are RgbKit() in looks.brs.

' A full-HD number at this screen's size (two thirds of it on a 1280 x 720 screen).
function RgbPx(v as dynamic, k as dynamic) as integer
    return Int(v * k / 1.5 + 0.5)
end function

' One time round the colourway's loop, in its pictures' pixels (tools/make-rgb-light.js). Each
' ring picture holds it twice: 2048 pixels, the most a Roku picture may be on a side.
function RgbLoopPx() as integer
    return 1024
end function

' The light goes once round in this many milliseconds; Colour cycle goes once through the hues.
function RgbLapMs() as integer
    return 30000
end function

function RgbCycleMs() as integer
    return 36000
end function

' The colourway's pictures: rainbow, neon or aurora. Colour cycle takes its colours from the
' rainbow, one at a time.
function RgbWayName(colour as integer) as string
    if colour = 1 then return "neon"
    if colour = 2 then return "aurora"
    return "rainbow"
end function

' The look's lettering: Barlow Condensed throughout. The names the other screens ask every
' look for (the tuning banner, the games' preview boxes, the pages) are all here.
function RgbFonts(st as object) as object
    k = st.k
    return {
        brand: Barlow(st, "extrabold", RgbPx(62, k)),
        badge: Barlow(st, "extrabold", RgbPx(33, k)),
        clock: Barlow(st, "semibold", RgbPx(50, k)),
        date: Barlow(st, "semibold", RgbPx(27, k)),
        listNum: Barlow(st, "semibold", RgbPx(32, k)),
        listName: Barlow(st, "semibold", RgbPx(39, k)),
        listNameOn: Barlow(st, "bold", RgbPx(39, k)),
        big: Barlow(st, "extrabold", RgbPx(156, k)),
        bigHint: Barlow(st, "semibold", RgbPx(33, k)),
        title: Barlow(st, "bold", RgbPx(64, k)),
        listing: Barlow(st, "medium", RgbPx(33, k)),
        note: Barlow(st, "semibold", RgbPx(31, k)),
        key: Barlow(st, "bold", RgbPx(24, k)),
        hint: Barlow(st, "semibold", RgbPx(27, k)),
        ver: Barlow(st, "medium", RgbPx(26, k)),
        page: Barlow(st, "semibold", RgbPx(31, k)),
        label: Barlow(st, "semibold", RgbPx(33, k)),
        labelOn: Barlow(st, "bold", RgbPx(33, k)),
        value: Barlow(st, "medium", RgbPx(33, k)),
        valueOn: Barlow(st, "bold", RgbPx(33, k)),
        ' the names the shared screens use
        pageTitle: Barlow(st, "bold", RgbPx(39, k)),
        pageRow: Barlow(st, "semibold", RgbPx(33, k)),
        pageChoice: Barlow(st, "medium", RgbPx(33, k)),
        pageLabel: Barlow(st, "semibold", RgbPx(27, k)),
        pageInfo: Barlow(st, "medium", RgbPx(27, k)),
        row: Barlow(st, "semibold", RgbPx(33, k)),
        head: Barlow(st, "semibold", RgbPx(27, k)),
        barNum: Barlow(st, "semibold", RgbPx(27, k)),
        bar: Barlow(st, "semibold", RgbPx(27, k)),
        ' the tuning banner's (tune.brs): its box is laid out for big numbers
        num: Barlow(st, "bold", RgbPx(84, k)),
        sign: Barlow(st, "extrabold", RgbPx(60, k)),
        banner: Barlow(st, "semibold", RgbPx(33, k)),
        name: Barlow(st, "bold", RgbPx(39, k)),
        now: Barlow(st, "medium", RgbPx(33, k)),
        sub: Barlow(st, "semibold", RgbPx(27, k)),
        time: Barlow(st, "semibold", RgbPx(50, k)),
        logo: Barlow(st, "extrabold", RgbPx(62, k))
    }
end function

' ---- the pictures ----

' The look's pictures, loaded once, and the colourway's loops (loaded again when the colourway
' changes). A picture that won't load is left out; without the loops, the light is drawn in the
' colourway's accent colour (RgbLoopColour).
function RgbArt(st as object) as object
    a = st.rgbArt
    if a = invalid then
        a = {
            tvGlow: RgbRegion(RgbPicture("tv-glow")),
            panelGlow: RgbRegion(RgbPicture("panel-glow")),
            rowLight: RgbRegion(RgbPicture("row-light")),
            panelRow: RgbRegion(RgbPicture("panel-row-light")),
            badge: RgbRegion(RgbPicture("badge")),
            star: RgbRegion(RgbPicture("star")),
            arrowR: RgbRegion(RgbPicture("arrow-r")),
            arrowL: RgbRegion(RgbPicture("arrow-l")),
            capL: invalid,
            capM: invalid,
            capR: invalid,
            way: "",
            rings: invalid,
            loop: invalid
        }
        ' a remote key's outline in three slices: the left and right ends, and the middle
        ' column that stretches to the key's width
        cap = RgbPicture("cap")
        if cap <> invalid then
            a.capL = RgbPart(cap, 0, 0, 12, 40)
            a.capM = RgbPart(cap, 12, 0, 1, 40)
            a.capR = RgbPart(cap, 13, 0, 12, 40)
        end if
        st.rgbArt = a
    end if
    way = RgbWayName(st.colour)
    if a.way <> way then
        a.way = way
        n = 2 * RgbLoopPx()
        a.rings = {
            h: RgbRing(way + "-h", n, 1),
            hr: RgbRing(way + "-hr", n, 1),
            v: RgbRing(way + "-v", 1, n),
            vr: RgbRing(way + "-vr", 1, n)
        }
        ' the loop's colours, for the corners, the focused row and the note
        a.loop = invalid
        if a.rings.h <> invalid then
            bytes = a.rings.h.GetByteArray(0, 0, RgbLoopPx(), 1)
            if bytes <> invalid then
                if bytes.Count() >= RgbLoopPx() * 4 then a.loop = bytes
            end if
        end if
    end if
    return a
end function

' A loop picture, only if it is the size the drawing expects (else invalid).
function RgbRing(name as string, w as integer, h as integer) as dynamic
    bmp = RgbPicture(name)
    if bmp = invalid then return invalid
    if bmp.GetWidth() <> w or bmp.GetHeight() <> h then
        print "b0r3d TV: the RGB look's picture "; name; " is "; bmp.GetWidth(); " x "; bmp.GetHeight(); ", not "; w; " x "; h
        return invalid
    end if
    return bmp
end function

' Part of a picture as a region that scales smoothly.
function RgbPart(bmp as object, x as integer, y as integer, w as integer, h as integer) as dynamic
    r = CreateObject("roRegion", bmp, x, y, w, h)
    if r <> invalid then r.SetScaleMode(1)
    return r
end function

' The look's pictures and the text made into pictures are let go (another look was picked).
sub ForgetRgbArt(st as object)
    st.rgbArt = invalid
    st.rgbRukus = invalid
    st.rgbText = invalid
    st.rgbPanelSpec = invalid
end sub

' A letter-spaced line of text made once into a see-through picture, so a frame draws it with
' one call instead of one a letter. fontKey names the font in RgbFonts. The pictures are kept for
' this screen size; a long run of new texts starts the keeping again.
function RgbSpacedPic(st as object, text as string, font as object, fontKey as string, colour as integer, sp as integer) as dynamic
    if st.rgbText = invalid then
        st.rgbText = {}
        st.rgbText.SetModeCaseSensitive()
    end if
    key = fontKey + "|" + Str(st.k) + "|" + colour.ToStr() + "|" + sp.ToStr() + "|" + text
    pic = st.rgbText.Lookup(key)
    if pic <> invalid then return pic
    if PicturesFailing(st) then return invalid
    w = SpacedWidth(text, font, sp) + 4
    h = font.GetOneLineHeight() + 2
    if w < 1 then w = 1
    pic = NewPicture(w, h, true)
    if pic = invalid then
        PictureFailed(st)
        return invalid
    end if
    pic.Clear(&h00000000)
    DrawSpaced(pic, text, 1, 1, colour, font, sp)
    if st.rgbText.Count() > 60 then st.rgbText.Clear()
    st.rgbText.AddReplace(key, pic)
    return pic
end function

' Draws a letter-spaced line at x, y (the line's top), from its picture when there's one.
' Returns how wide it is.
function DrawRgbSpaced(st as object, text as string, x as integer, y as integer, colour as integer, font as object, fontKey as string, sp as integer) as integer
    pic = RgbSpacedPic(st, text, font, fontKey, colour, sp)
    if pic <> invalid then
        st.screen.DrawObject(x - 1, y - 1, pic)
        return pic.GetWidth() - 4
    end if
    DrawSpaced(st.screen, text, x, y, colour, font, sp)
    return SpacedWidth(text, font, sp)
end function

' The same, with its right edge at rx.
sub DrawRgbRightSpaced(st as object, text as string, rx as integer, y as integer, colour as integer, font as object, fontKey as string, sp as integer)
    DrawRgbSpaced(st, text, rx - RgbSpacedWidth(st, text, font, fontKey, colour, sp), y, colour, font, fontKey, sp)
end sub

' How wide a letter-spaced line is (making its picture if it isn't made yet).
function RgbSpacedWidth(st as object, text as string, font as object, fontKey as string, colour as integer, sp as integer) as integer
    pic = RgbSpacedPic(st, text, font, fontKey, colour, sp)
    if pic <> invalid then return pic.GetWidth() - 4
    return SpacedWidth(text, font, sp)
end function

function RgbPicture(name as string) as dynamic
    bmp = invalid
    try
        bmp = CreateObject("roBitmap", "pkg:/images/rgb/" + name + ".png")
    catch e
        bmp = invalid
    end try
    if bmp = invalid then print "b0r3d TV: the RGB look's picture "; name; " didn't load"
    return bmp
end function

' A whole picture as a region that scales smoothly.
function RgbRegion(bmp as dynamic) as dynamic
    if bmp = invalid then return invalid
    r = CreateObject("roRegion", bmp, 0, 0, bmp.GetWidth(), bmp.GetHeight())
    if r <> invalid then r.SetScaleMode(1)
    return r
end function

' ---- the light's colours ----

' How far the light has gone round, from 0 to 1.
function RgbFlow(st as object) as float
    if st.rgbTimer = invalid then st.rgbTimer = CreateObject("roTimespan")
    ms = st.rgbTimer.TotalMilliseconds()
    return (ms mod RgbLapMs()) / RgbLapMs()
end function

' The colour at u round the colourway's loop (u wraps at 1). Without the loop's picture, the
' colourway's accent colour.
function RgbLoopColour(st as object, u as float) as integer
    a = RgbArt(st)
    bytes = a.loop
    n = RgbLoopPx()
    if bytes = invalid then return st.kit.accent
    i = Int((u - Int(u)) * n)
    if i < 0 then i = 0
    if i > n - 1 then i = n - 1
    return (bytes[i * 4] << 24) or (bytes[i * 4 + 1] << 16) or (bytes[i * 4 + 2] << 8) or &hFF
end function

' Colour cycle's one colour right now.
function RgbCycleColour(st as object) as integer
    if st.rgbTimer = invalid then st.rgbTimer = CreateObject("roTimespan")
    ms = st.rgbTimer.TotalMilliseconds()
    return RgbLoopColour(st, (ms mod RgbCycleMs()) / RgbCycleMs())
end function

' The LED colour on a lit rectangle's left edge at height y (lights the focused row).
function RgbEdgeColour(st as object, x as integer, y as integer, w as integer, h as integer, atY as integer) as integer
    if st.colour = 3 then return RgbCycleColour(st)
    yy = atY
    if yy < y then yy = y
    if yy > y + h then yy = y + h
    p = 2 * (w + h)
    along = 2 * w + h + (y + h - yy)
    return RgbLoopColour(st, along / p - RgbFlow(st))
end function

' A colour taken f of the way toward white (f 0 to 1), so a light's colour reads as text.
function RgbLift(c as integer, f as float) as integer
    out = &hFF
    for each sh in [24, 16, 8]
        v = (c >> sh) and &hFF
        out = out or (Int(v + (255 - v) * f + 0.5) << sh)
    end for
    return out
end function

' A colour taken f of the way toward another (both opaque).
function RgbMix(c as integer, toward as integer, f as float) as integer
    out = &hFF
    for each sh in [24, 16, 8]
        v = (c >> sh) and &hFF
        t = (toward >> sh) and &hFF
        out = out or (Int(v + (t - v) * f + 0.5) << sh)
    end for
    return out
end function

' Where a slice of the loop starts (in its pictures' pixels) and how long it is: u0 is where it
' starts round the loop, len the slice's length on screen, p the whole way round on screen.
function RgbSpan(u0 as float, length as integer, p as integer) as object
    n = RgbLoopPx()
    start = Int((u0 - Int(u0)) * n + 0.5)
    if start >= n then start = start - n
    count = Int(length * n / p + 0.5)
    if count < 1 then count = 1
    if count > n then count = n
    return { start: start, count: count }
end function

' The light round a rectangle (screen pixels): the LED colours along each edge and in each
' corner, then the glow picture, which makes them light on the wall. The rectangle's own inside
' is drawn over afterwards (the TV, or the panel).
sub DrawRgbLight(st as object, x as integer, y as integer, w as integer, h as integer, glow as dynamic)
    s = st.screen
    k = st.k
    a = RgbArt(st)
    if glow = invalid then return
    g = RgbPx(260, k)
    rings = a.rings
    if st.colour = 3 or rings.h = invalid or rings.hr = invalid or rings.v = invalid or rings.vr = invalid then
        s.DrawRect(x - g, y - g, w + 2 * g, h + 2 * g, RgbCycleColour(st))
    else
        f = RgbFlow(st)
        p = 2 * (w + h)
        ' along the top, left to right
        sp = RgbSpan(-f, w, p)
        RgbSlice(s, rings.h, sp.start, 0, sp.count, 1, x, y - g, w / sp.count, g)
        ' down the right side
        sp = RgbSpan(w / p - f, h, p)
        RgbSlice(s, rings.v, 0, sp.start, 1, sp.count, x + w, y, g, h / sp.count)
        ' along the bottom, right to left (drawn left to right from the backwards loop)
        sp = RgbSpan(f - (2 * w + h) / p, w, p)
        RgbSlice(s, rings.hr, sp.start, 0, sp.count, 1, x, y + h, w / sp.count, g)
        ' up the left side (drawn top to bottom from the backwards loop)
        sp = RgbSpan(f, h, p)
        RgbSlice(s, rings.vr, 0, sp.start, 1, sp.count, x - g, y, g, h / sp.count)
        ' each corner in the colour of the LED there
        s.DrawRect(x - g, y - g, g, g, RgbLoopColour(st, -f))
        s.DrawRect(x + w, y - g, g, g, RgbLoopColour(st, w / p - f))
        s.DrawRect(x + w, y + h, g, g, RgbLoopColour(st, (w + h) / p - f))
        s.DrawRect(x - g, y + h, g, g, RgbLoopColour(st, (2 * w + h) / p - f))
    end if
    sc = 2 * k / 1.5
    s.DrawScaledObject(x - g, y - g, sc, sc, glow)
end sub

' A slice of a loop picture, stretched smoothly onto the screen.
sub RgbSlice(s as object, bmp as object, sx as integer, sy as integer, sw as integer, sh as integer, x as integer, y as integer, scaleX as float, scaleY as float)
    r = CreateObject("roRegion", bmp, sx, sy, sw, sh)
    if r = invalid then return
    r.SetScaleMode(1)
    s.DrawScaledObject(x, y, scaleX, scaleY, r)
end sub

' A picture made at the full-HD size, drawn at the screen's (smoothly).
sub RgbDraw(st as object, region as dynamic, x as integer, y as integer, scale as float)
    if region = invalid then return
    st.screen.DrawScaledObject(x, y, scale * st.k / 1.5, scale * st.k / 1.5, region)
end sub

' The focused row's light at x, y: its colour as a flat rect, then the row light on top (a
' half-size picture). The rect is 2 pixels in from the picture's edge, where only the wall shows.
sub DrawRgbRowLight(st as object, region as dynamic, x as integer, y as integer, colour as integer)
    if region = invalid then return
    k = st.k
    w = RgbPx(region.GetWidth() * 2, k)
    h = RgbPx(region.GetHeight() * 2, k)
    inset = RgbPx(2, k)
    if inset < 1 then inset = 1
    st.screen.DrawRect(x + inset, y + inset, w - 2 * inset, h - 2 * inset, colour)
    RgbDraw(st, region, x, y, 2)
end sub

' ---- the parts every screen of the look shares ----

' "b0r3d" and its white TV badge, with the baseline at by. Returns where the badge ends.
function DrawRgbBrand(st as object, x as integer, by as integer) as integer
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    a = RgbArt(st)
    bx = x + DrawRgbSpaced(st, "b0r3d", x, by - f.brand.GetAscent(), c.white, f.brand, "brand", RgbPx(1, k)) + RgbPx(12, k)
    bw = RgbPx(58, k)
    bh = RgbPx(40, k)
    bt = by - RgbPx(43, k)
    if a.badge <> invalid then
        RgbDraw(st, a.badge, bx, bt, 1)
    else
        s.DrawRect(bx, bt, bw, bh, c.white)
    end if
    DrawCentered(s, "TV", bx, bt + RgbPx(1, k), bw, bh, c.bg, f.badge)
    return bx + bw
end function

' The clock with the date before it, right-aligned at rx, on baseline by.
sub DrawRgbClock(st as object, rx as integer, by as integer)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    clock = ClockText(st, false)
    cw = f.clock.GetOneLineWidth(clock, 99999)
    s.DrawText(clock, rx - cw, by - f.clock.GetAscent(), c.text, f.clock)
    DrawRgbRightSpaced(st, st.clock.date, rx - cw - RgbPx(26, k), by - RgbPx(2, k) - f.date.GetAscent(), c.dim, f.date, "date", RgbPx(2.5, k))
end sub

' A remote key's outline at x, y, w wide, from the three slices of the key picture.
sub DrawRgbCap(st as object, x as integer, y as integer, w as integer)
    a = RgbArt(st)
    if a.capL = invalid or a.capM = invalid or a.capR = invalid then return
    s = st.screen
    sc = st.k / 1.5
    side = RgbPx(12, st.k)
    s.DrawScaledObject(x, y, sc, sc, a.capL)
    stretch = w - 2 * side
    if stretch > 0 then s.DrawScaledObject(x + side, y, stretch, sc, a.capM)
    s.DrawScaledObject(x + w - side, y, sc, sc, a.capR)
end sub

' The bar of remote hints along the bottom: a hairline, then each key and its word, and the
' version on the right. keys: [{ cap: "OK", word: "OPEN" }, { cap: "*", ... }]; a key with no cap
' is just its words.
sub DrawRgbHints(st as object, keys as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    a = RgbArt(st)
    lx = RgbPx(96, k)
    rx = RgbPx(1824, k)
    s.DrawRect(lx, RgbPx(960, k), rx - lx, RgbPx(2, k), c.rule)
    by = RgbPx(1022, k)
    capH = RgbPx(40, k)
    capY = by - RgbPx(29, k)
    x = lx
    wordY = by - f.hint.GetAscent()
    for each item in keys
        if item.DoesExist("cap") then
            if item.cap = "*" then
                w = RgbPx(48, k)
                DrawRgbCap(st, x, capY, w)
                RgbDraw(st, a.star, x + (w - RgbPx(24, k)) \ 2, capY + (capH - RgbPx(24, k)) \ 2, 1)
            else
                w = RgbSpacedWidth(st, item.cap, f.key, "key", c.white, RgbPx(1, k)) + RgbPx(26, k)
                DrawRgbCap(st, x, capY, w)
                DrawRgbSpaced(st, item.cap, x + RgbPx(13, k), capY + (capH - f.key.GetOneLineHeight()) \ 2, c.white, f.key, "key", RgbPx(1, k))
            end if
            x = x + w + RgbPx(14, k)
        end if
        x = x + DrawRgbSpaced(st, item.word, x, wordY, c.soft, f.hint, "hint", RgbPx(1.5, k)) + RgbPx(44, k)
    end for
    vw = f.ver.GetOneLineWidth(st.version, 99999)
    s.DrawText(st.version, rx - vw, by - f.ver.GetAscent(), c.dim, f.ver)
end sub

' Ruckus at sc screen pixels a sprite pixel, made once into a picture (one draw a frame instead
' of a hundred).
function RgbRukus(st as object, sc as integer) as dynamic
    if st.rgbRukus <> invalid and st.rgbRukusScale = sc then return st.rgbRukus
    if PicturesFailing(st) then return invalid
    pic = NewPicture(16 * sc, 9 * sc, true)
    if pic = invalid then
        PictureFailed(st)
        return invalid
    end if
    pic.Clear(&h00000000)
    DrawSprite(pic, RukusLoaf(), RukusColorsFor(st), 0, 0, sc)
    st.rgbRukus = pic
    st.rgbRukusScale = sc
    return pic
end function

' ---- the guide ----

sub DrawRgbGuide(st as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    a = RgbArt(st)
    count = st.channels.Count()
    ch = invalid
    if count > 0 then ch = st.channels[st.focus]
    s.Clear(c.bg)

    ' the TV, with its light behind it
    tvX = RgbPx(984, k)
    tvY = RgbPx(212, k)
    tvW = RgbPx(1824, k) - tvX
    tvH = RgbPx(692, k) - tvY
    DrawRgbLight(st, tvX, tvY, tvW, tvH, a.tvGlow)
    bz = RgbPx(10, k)
    DrawFrame(s, tvX, tvY, tvW, tvH, bz, c.frame)
    px = tvX + bz
    py = tvY + bz
    pw = tvW - 2 * bz
    ph = tvH - 2 * bz
    s.DrawRect(px, py, pw, ph, c.well)
    if ch = invalid then
        DrawCentered(s, "NO CHANNELS YET", px, py, pw, ph, c.white, f.title)
    else if HasPreview(ch) then
        DrawGamePreview(st, ch, px, py, pw, ph)
    else
        sp = RgbPx(10, k)
        bigH = f.big.GetOneLineHeight()
        hintH = f.bigHint.GetOneLineHeight()
        gap = RgbPx(6, k)
        top = py + (ph - bigH - gap - hintH) \ 2
        DrawCenteredSpaced(s, ch.sign, px, top, pw, bigH, c.white, f.big, sp)
        DrawCentered(s, TrimToWidth(OkWord(ch), f.bigHint, pw - RgbPx(40, k)), px, top + bigH + gap, pw, hintH, c.dim, f.bigHint)
    end if

    ' Ruckus loafs on top of the TV
    sc = RgbPx(6, k)
    if sc < 1 then sc = 1
    rukus = RgbRukus(st, sc)
    if rukus <> invalid then
        s.DrawObject(tvX + RgbPx(64, k), tvY - 9 * sc, rukus)
    else
        ' no picture memory for him just now: drawn pixel by pixel instead
        DrawSprite(s, RukusLoaf(), RukusColorsFor(st), tvX + RgbPx(64, k), tvY - 9 * sc, sc)
    end if

    DrawRgbBrand(st, RgbPx(96, k), RgbPx(110, k))
    DrawRgbClock(st, RgbPx(1824, k), RgbPx(110, k))

    ' the channel list; the focused row is lit in the colour of the light on the TV's left side
    rows = GuideRowsFor(st)
    first = TopRow(st.focus, count, rows)
    listX = RgbPx(96, k)
    listY = RgbPx(170, k)
    rowH = RgbPx(64, k)
    numRight = listX + RgbPx(66, k)
    nameX = listX + RgbPx(92, k)
    nameW = tvX - RgbPx(60, k) - nameX
    for r = 0 to rows - 1
        i = first + r
        if i >= count then exit for
        row = st.channels[i]
        ry = listY + r * rowH
        rowMid = ry + rowH \ 2
        isFocus = (i = st.focus)
        numColour = c.dim
        nameColour = c.soft
        nameFont = f.listName
        if isFocus then
            lightH = RgbPx(72, k)
            DrawRgbRowLight(st, a.rowLight, listX - RgbPx(16, k), rowMid - lightH \ 2, RgbEdgeColour(st, tvX, tvY, tvW, tvH, rowMid))
            numColour = c.white
            nameColour = c.white
            nameFont = f.listNameOn
        end if
        by = rowMid + RgbPx(14, k)
        nw = f.listNum.GetOneLineWidth(row.n, 99999)
        s.DrawText(row.n, numRight - nw, by - f.listNum.GetAscent(), numColour, f.listNum)
        s.DrawText(TrimToWidth(row.name, nameFont, nameW), nameX, by - nameFont.GetAscent(), nameColour, nameFont)
    end for

    ' the focused channel's details, under the TV
    if ch <> invalid then
        dx = px
        dw = tvX + tvW - dx
        title = ch.n + " · " + ch.name
        s.DrawText(TrimToWidth(title, f.title, dw), dx, RgbPx(806, k) - f.title.GetAscent(), c.white, f.title)
        ' what's on: the half hour in grey, the show in white
        listing = ChannelListing(st, ch)
        ly = RgbPx(858, k) - f.listing.GetAscent()
        lead = RgbListingTime(st)
        if lead <> "" and Left(listing, Len(lead) + 2) = lead + "  " then
            s.DrawText(lead, dx, ly, c.dim, f.listing)
            tx = dx + f.listing.GetOneLineWidth(lead, 99999) + RgbPx(16, k)
            s.DrawText(TrimToWidth(Mid(listing, Len(lead) + 3), f.listing, dx + dw - tx), tx, ly, c.text, f.listing)
        else
            s.DrawText(TrimToWidth(listing, f.listing, dw), dx, ly, c.text, f.listing)
        end if
        ' one accent at a time: the note takes the focused row's light, lifted to read as text
        noteY = RgbPx(906, k) - f.note.GetAscent()
        if st.note <> "" then
            lit = RgbEdgeColour(st, tvX, tvY, tvW, tvH, listY + (st.focus - first) * rowH + rowH \ 2)
            s.DrawText(TrimToWidth(st.note, f.note, dw), dx, noteY, RgbLift(lit, 0.55), f.note)
        else if HasPreview(ch) then
            ' a channel whose box shows a demo says what OK does here instead
            s.DrawText(TrimToWidth(OkWord(ch), f.note, dw), dx, noteY, c.dim, f.note)
        end if
    end if

    DrawRgbHints(st, [{ cap: "OK", word: "OPEN" }, { cap: "*", word: "SETTINGS" }, { cap: "REPLAY", word: "LAST" }])
    s.SwapBuffers()
end sub

' The listing's half hour ("11:30 PM"), so the guide can draw it apart from the show.
function RgbListingTime(st as object) as string
    t = st.clock
    if t = invalid then return ""
    if t.hours = invalid or t.mins = invalid then return ""
    return SlotTimeText(t.hours, (t.mins \ 30) * 30, st.clock24 = true)
end function

' ---- the pages (Settings, the channel list, the TV check, About, the games' menus) ----

sub DrawRgbPanel(st as object, spec as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    a = RgbArt(st)
    s.Clear(c.bg)

    pX = RgbPx(336, k)
    pY = RgbPx(196, k)
    pW = RgbPx(1584, k) - pX
    pH = RgbPx(884, k) - pY
    DrawRgbLight(st, pX, pY, pW, pH, a.panelGlow)
    edge = RgbPx(2, k)
    DrawFrame(s, pX, pY, pW, pH, edge, c.frame)
    s.DrawRect(pX + edge, pY + edge, pW - 2 * edge, pH - 2 * edge, c.card)

    ' the brand, a divider and the page's name; the clock
    brandEnd = DrawRgbBrand(st, RgbPx(96, k), RgbPx(110, k))
    dvX = brandEnd + RgbPx(30, k)
    s.DrawRect(dvX, RgbPx(66, k), RgbPx(2, k), RgbPx(44, k), c.rule)
    DrawRgbSpaced(st, UCase(spec.title), dvX + RgbPx(30, k), RgbPx(104, k) - f.page.GetAscent(), c.soft, f.page, "page", RgbPx(4, k))
    DrawRgbClock(st, RgbPx(1824, k), RgbPx(110, k))

    lx = pX + RgbPx(56, k)
    rx = pX + pW - RgbPx(56, k)
    if spec.info then
        ' a page of answers: the values start close in, so long ones wrap less, and the rows run
        ' nearly to the panel's foot
        DrawInfoRows(st, spec.rows, lx, pX + RgbPx(330, k), rx, pY + RgbPx(24, k), pY + pH - RgbPx(12, k), c.dim, c.text, invalid, true)
    else
        visible = 11
        count = spec.rows.Count()
        first = ListTop(spec.sel, count, visible)
        rowH = RgbPx(58, k)
        top = pY + RgbPx(26, k)
        for r = 0 to visible - 1
            i = first + r
            if i >= count then exit for
            row = spec.rows[i]
            ry = top + r * rowH
            rowMid = ry + rowH \ 2
            isSel = (i = spec.sel)
            ' a hairline between rows, but not next to the lit one
            if r > 0 and not isSel and i - 1 <> spec.sel then s.DrawRect(pX + RgbPx(40, k), ry, pW - RgbPx(80, k), 1, c.rule)
            labelColour = c.text
            valueColour = c.soft
            labelFont = f.label
            valueFont = f.value
            if IsDim(row) then
                labelColour = c.dim
                valueColour = c.dim
            end if
            if isSel then
                lightH = RgbPx(64, k)
                DrawRgbRowLight(st, a.panelRow, pX + RgbPx(6, k), rowMid - lightH \ 2, RgbEdgeColour(st, pX, pY, pW, pH, rowMid))
                labelColour = c.white
                valueColour = c.white
                labelFont = f.labelOn
                valueFont = f.valueOn
            end if
            by = rowMid + RgbPx(12, k)
            s.DrawText(row.label, lx, by - labelFont.GetAscent(), labelColour, labelFont)
            DrawRgbValue(st, row, isSel, rx, rowMid, by, valueColour, valueFont)
        end for
    end if

    DrawRgbHints(st, [{ word: UCase(spec.hint) }])
    s.SwapBuffers()
end sub

' Between real redraws, Tick draws the page again from the rows DrawPanel last gave it, so the
' light can flow without the rows being worked out again 10 times a second. Once a second they
' are worked out afresh (the TV check and the Screensaver check have live answers). False when
' there's no page to draw this way (Tick then redraws as usual).
function RgbRepaint(st as object) as boolean
    if st.look <> "rgb" or st.rgbPanelSpec = invalid or st.rgbPanelTimer = invalid then return false
    if st.rgbPanelMode <> st.mode then return false
    if st.rgbPanelTimer.TotalMilliseconds() >= 1000 then return false
    DrawRgbPanel(st, st.rgbPanelSpec)
    return true
end function

' DrawPanel's note of the page it drew (for RgbRepaint). A page drawn in full counts as the
' games' next light frame too (RgbMenuTurn), so a key's redraw isn't followed by another.
sub RgbRememberPanel(st as object, spec as object)
    st.rgbPanelSpec = spec
    st.rgbPanelMode = st.mode
    if st.rgbPanelTimer = invalid then st.rgbPanelTimer = CreateObject("roTimespan")
    st.rgbPanelTimer.Mark()
    if st.rgbMenuTimer = invalid then st.rgbMenuTimer = CreateObject("roTimespan")
    st.rgbMenuTimer.Mark()
end sub

' The games' menus, pause pages and score pages: a game's own turn draws them in full once a
' second, so in this look its turn also draws the page again at RgbWaitMs's rate from its
' remembered rows (RgbRepaint, as Tick does for Settings), and the light flows there too. Not
' while the TV's own screensaver may have blanked the screen. A game's wait off play is ms (a
' second); this is what it waits instead.
function RgbMenuWaitMs(st as object, ms as integer) as integer
    if st.look <> "rgb" then return ms
    if TvMayBeBlank(st, 0) then return ms
    led = RgbWaitMs(st)
    if led < ms then return led
    return ms
end function

sub RgbMenuTurn(st as object)
    if st.look <> "rgb" then return
    if st.rgbMenuTimer = invalid then st.rgbMenuTimer = CreateObject("roTimespan")
    if st.rgbMenuTimer.TotalMilliseconds() < RgbWaitMs(st) then return
    if TvMayBeBlank(st, 0) then return
    st.rgbMenuTimer.Mark()
    ' how long this takes to draw sets how often it may be drawn (RgbWaitMs), as in Tick
    if st.tickTimer = invalid then st.tickTimer = CreateObject("roTimespan")
    st.tickTimer.Mark()
    if not RgbRepaint(st) then Redraw(st)
    st.tickMs = st.tickTimer.TotalMilliseconds()
end sub

' A row's value, right-aligned at rx, in sentence case. The focused row gets an arrow after a
' value that changes or opens, and one before a value that Left and Right change.
sub DrawRgbValue(st as object, row as object, focused as boolean, rx as integer, rowMid as integer, by as integer, colour as integer, font as object)
    s = st.screen
    k = st.k
    a = RgbArt(st)
    aw = RgbPx(11, k)
    ah = RgbPx(16, k)
    gap = RgbPx(16, k)
    x = rx
    if focused and (row.kind = "choice" or row.kind = "open") then
        x = x - aw
        RgbDraw(st, a.arrowR, x, rowMid - ah \ 2, 1)
        x = x - gap
    end if
    vw = font.GetOneLineWidth(row.value, 99999)
    x = x - vw
    s.DrawText(row.value, x, by - font.GetAscent(), colour, font)
    if focused and row.kind = "choice" then
        x = x - gap - aw
        RgbDraw(st, a.arrowL, x, rowMid - ah \ 2, 1)
    end if
end sub

' ---- the look's touches on the other screens (the games, b0r3d FM, Commercial Breaks) ----

' The light's colour now at the start of its loop (Colour cycle's one colour).
function RgbLitNow(st as object) as integer
    if st.colour = 3 then return RgbCycleColour(st)
    return RgbLoopColour(st, -RgbFlow(st))
end function

' A small lit LED bar standing h tall at x, y, in the light's colour now: the look's mark beside
' a screen's title. Solid colours only, so it can go into the games' prebuilt pictures.
sub DrawRgbLedBar(st as object, target as object, x as integer, y as integer, h as integer)
    c = st.kit
    k = st.k
    lit = RgbLitNow(st)
    w = RgbPx(8, k)
    halo = RgbPx(3, k)
    target.DrawRect(x - halo, y - halo, w + 2 * halo, h + 2 * halo, RgbMix(lit, c.bg, 0.75))
    target.DrawRect(x - 1, y - 1, w + 2, h + 2, RgbMix(lit, c.bg, 0.4))
    target.DrawRect(x, y, w, h, lit)
    target.DrawRect(x + w \ 2 - 1, y + RgbPx(4, k), 2, h - 2 * RgbPx(4, k), RgbLift(lit, 0.7))
end sub

' A lit edge round a box (a game's playing field, b0r3d FM's equaliser): a thin line just
' outside it in the colourway's colours, running round it the way the light does round the TV.
' Solid colours only, so it can go into the games' prebuilt pictures.
sub DrawRgbEdge(st as object, target as object, x as integer, y as integer, w as integer, h as integer)
    k = st.k
    a = RgbArt(st)
    t = RgbPx(3, k)
    if t < 2 then t = 2
    rings = a.rings
    if st.colour = 3 or rings.h = invalid or rings.hr = invalid or rings.v = invalid or rings.vr = invalid then
        lit = RgbCycleColour(st)
        target.DrawRect(x - t, y - t, w + 2 * t, t, lit)
        target.DrawRect(x - t, y + h, w + 2 * t, t, lit)
        target.DrawRect(x - t, y, t, h, lit)
        target.DrawRect(x + w, y, t, h, lit)
        return
    end if
    f = RgbFlow(st)
    p = 2 * (w + h)
    sp = RgbSpan(-f, w, p)
    RgbEdgeSlice(st, target, rings.h, sp, true, x, y - t, w, t)
    sp = RgbSpan(w / p - f, h, p)
    RgbEdgeSlice(st, target, rings.v, sp, false, x + w, y, t, h)
    sp = RgbSpan(f - (2 * w + h) / p, w, p)
    RgbEdgeSlice(st, target, rings.hr, sp, true, x, y + h, w, t)
    sp = RgbSpan(f, h, p)
    RgbEdgeSlice(st, target, rings.vr, sp, false, x - t, y, t, h)
    target.DrawRect(x - t, y - t, t, t, RgbLoopColour(st, -f))
    target.DrawRect(x + w, y - t, t, t, RgbLoopColour(st, w / p - f))
    target.DrawRect(x + w, y + h, t, t, RgbLoopColour(st, (w + h) / p - f))
    target.DrawRect(x - t, y + h, t, t, RgbLoopColour(st, (2 * w + h) / p - f))
end sub

sub RgbEdgeSlice(st as object, target as object, bmp as object, sp as object, across as boolean, x as integer, y as integer, w as integer, h as integer)
    if across then
        r = CreateObject("roRegion", bmp, sp.start, 0, sp.count, 1)
        if r = invalid then return
        r.SetScaleMode(1)
        target.DrawScaledObject(x, y, w / sp.count, h, r)
    else
        r = CreateObject("roRegion", bmp, 0, sp.start, 1, sp.count)
        if r = invalid then return
        r.SetScaleMode(1)
        target.DrawScaledObject(x, y, w, h / sp.count, r)
    end if
end sub
