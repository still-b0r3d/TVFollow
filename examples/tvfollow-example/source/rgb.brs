' The RGB LED lights look: a dark case lit by strips of colour-changing LEDs, with animated
' diodes, rainbow waves running along the strips and a lit frame round the preview.
' Laid out on the 1280 x 720 grid (scaled by st.k). Its colours are RgbKit() in looks.brs.

function RgbFonts(st as object) as object
    k = st.k
    return {
        brand: Silk(st, Z(28, k), true),
        sub: Silk(st, Z(18, k), false),
        ch: Silk(st, Z(56, k), true),
        chSign: Silk(st, Z(24, k), false),
        row: Barlow(st, "bold", Z(26, k)),
        rowN: Silk(st, Z(22, k), true),
        big: Silk(st, Z(80, k), true),
        pipHint: Silk(st, Z(18, k), false),
        name: Barlow(st, "extrabold", Z(34, k)),
        now: Barlow(st, "medium", Z(22, k)),
        bar: Silk(st, Z(18, k), false),
        clock: Silk(st, Z(30, k), true),
        pageTitle: Silk(st, Z(28, k), true),
        pageRow: Barlow(st, "bold", Z(24, k)),
        pageChoice: Silk(st, Z(20, k), true),
        pageLabel: Barlow(st, "medium", Z(22, k)),
        pageInfo: Barlow(st, "medium", Z(20, k)),
        num: Silk(st, Z(56, k), true),
        sign: Silk(st, Z(36, k), false),
        banner: Silk(st, Z(22, k), false),
        logo: Silk(st, Z(40, k), true),
        badge: Silk(st, Z(22, k), true),
        time: Silk(st, Z(30, k), true),
        date: Silk(st, Z(18, k), false),
        head: Silk(st, Z(20, k), true),
        cellNum: Silk(st, Z(22, k), true),
        cellSign: Silk(st, Z(24, k), false),
        cellNow: Barlow(st, "medium", Z(20, k)),
        barNum: Silk(st, Z(20, k), true)
    }
end function

' 36-hue color palettes for multi-color changing LED light animations
function RgbRainbowPalette() as object
    return [
        &hFF0033FF, &hFF1A00FF, &hFF3300FF, &hFF5500FF,
        &hFF7700FF, &hFF9900FF, &hFFBB00FF, &hFFDD00FF,
        &hFFFF00FF, &hDDFF00FF, &hBBFF00FF, &h88FF00FF,
        &h44FF00FF, &h00FF00FF, &h00FF44FF, &h00FF88FF,
        &h00FFCCFF, &h00FFFFFF, &h00CCFFFF, &h0099FFFF,
        &h0066FFFF, &h0033FFFF, &h0000FFFF, &h3300FFFF,
        &h6600FFFF, &h9900FFFF, &hBB00FFFF, &hDD00FFFF,
        &hFF00FFFF, &hFF00CCFF, &hFF0099FF, &hFF0077FF,
        &hFF0055FF, &hFF0033FF, &hFF0011FF, &hFF0022FF
    ]
end function

function RgbNeonPalette() as object
    return [
        &hFF007FFF, &hFF0099FF, &hFF00CCFF, &hFF00FFFF,
        &hCC00FFFF, &h9900FFFF, &h7000FFFF, &h4400FFFF,
        &h1133FFFF, &h0066FFFF, &h00A8FFFF, &h00E5FFFF,
        &h00FFFFFF, &h00FFCCFF, &h00FF99FF, &h00FF55FF,
        &h22FF00FF, &h55FF00FF, &h88FF00FF, &hCCFF00FF,
        &hFFFF00FF, &hFFDD00FF, &hFFAA00FF, &hFF6600FF,
        &hFF3300FF, &hFF1122FF, &hFF0055FF, &hFF007FFF,
        &hFF00A6FF, &hD600FFFF, &hA600FFFF, &h6600FFFF,
        &h0044FFFF, &h0088FFFF, &h00D5FFFF, &h00FFB2FF
    ]
end function

function RgbAuroraPalette() as object
    return [
        &h00FF88FF, &h00FFAAFF, &h00FFCCFF, &h00FFEEFF,
        &h00FFFFFF, &h00EEFFFF, &h00CCFFFF, &h00AAFFFF,
        &h0088FFFF, &h0066FFFF, &h0044FFFF, &h0022FFFF,
        &h2200FFFF, &h4400FFFF, &h6600FFFF, &h8800FFFF,
        &h7700FFFF, &h5500FFFF, &h3322FFFF, &h0055FFFF,
        &h0088FFFF, &h00BBFFFF, &h00D8FFFF, &h00F0FFFF,
        &h00FFAAFF, &h00FF88FF, &h11FF66FF, &h33FF44FF,
        &h55FF22FF, &h77FF00FF, &h44FF44FF, &h00FF77FF,
        &h00FFAAFF, &h00FFDDFF, &h00E5FFFF, &h00B8FFFF
    ]
end function

function RgbPalette(mode as integer) as object
    if mode = 1 then return RgbNeonPalette()
    if mode = 2 then return RgbAuroraPalette()
    return RgbRainbowPalette()
end function

' The colour of the LED at this offset along the wave, right now. A frame asks for a few
' hundred, so the colour's palette is made once and kept (st.rgbPal) until the colour changes.
function RgbLedColor(st as object, offset as integer) as integer
    pal = st.rgbPal
    if pal = invalid then
        pal = RgbPalette(st.colour)
    else if st.rgbPalFor <> st.colour then
        pal = RgbPalette(st.colour)
    end if
    st.rgbPal = pal
    st.rgbPalFor = st.colour
    n = pal.Count()
    if n = 0 then return &hFFFFFFFF
    if st.rgbTimer = invalid then st.rgbTimer = CreateObject("roTimespan")
    ms = st.rgbTimer.TotalMilliseconds()
    if st.colour = 3 then
        ' Spectrum Cycle: all LEDs transition in sync together through the color spectrum
        idx = (ms \ 70) mod n
        return pal[idx]
    end if
    ' Traveling Wave: color shifts across physical LED positions
    idx = ((ms \ 45) + offset) mod n
    if idx < 0 then idx = idx + n
    return pal[idx]
end function

' One lit LED: its body (a pixel wider all round, for the glow) and a white core.
sub DrawLedNode(s as object, x as integer, y as integer, size as integer, color as integer)
    s.DrawRect(x - 1, y - 1, size + 2, size + 2, color)
    ' the hot spot in the middle
    if size >= 4 then
        core = size \ 3
        if core < 1 then core = 1
        cx = x + (size - core) \ 2
        cy = y + (size - core) \ 2
        s.DrawRect(cx, cy, core, core, &hFFFFFFFF)
    end if
end sub

' Draws a horizontal addressable LED light strip with glowing diodes
sub DrawLedStripH(s as object, st as object, x as integer, y as integer, w as integer, h as integer, spacing as integer, ledSize as integer, offsetStart as integer)
    ' Diffuser track
    trackH = ledSize + 2
    trackY = y + (h - trackH) \ 2
    s.DrawRect(x, trackY, w, trackH, st.kit.track)
    count = (w - ledSize) \ spacing
    if count < 1 then count = 1
    for i = 0 to count
        lx = x + i * spacing
        if lx + ledSize > x + w then exit for
        ly = y + (h - ledSize) \ 2
        col = RgbLedColor(st, offsetStart + i)
        DrawLedNode(s, lx, ly, ledSize, col)
    end for
end sub

' Draws a vertical addressable LED light strip with glowing diodes
sub DrawLedStripV(s as object, st as object, x as integer, y as integer, w as integer, h as integer, spacing as integer, ledSize as integer, offsetStart as integer)
    ' Diffuser track
    trackW = ledSize + 2
    trackX = x + (w - trackW) \ 2
    s.DrawRect(trackX, y, trackW, h, st.kit.track)
    count = (h - ledSize) \ spacing
    if count < 1 then count = 1
    for i = 0 to count
        ly = y + i * spacing
        if ly + ledSize > y + h then exit for
        lx = x + (w - ledSize) \ 2
        col = RgbLedColor(st, offsetStart + i)
        DrawLedNode(s, lx, ly, ledSize, col)
    end for
end sub

' Draws the signature RGB triad [R][G][B] color pips. Compatible with existing calls.
sub DrawRgbTriad(s as object, x as integer, y as integer, w as integer, h as integer, kit as object, st = invalid as dynamic)
    if st <> invalid then
        ' Draw as active multi-color animated LED diodes
        d = h
        if d < 4 then d = 4
        gap = 4
        for i = 0 to 2
            col = RgbLedColor(st, i * 4)
            DrawLedNode(s, x + i * (d + gap), y, d, col)
        end for
    else
        segW = w \ 3
        s.DrawRect(x, y, segW, h, kit.red)
        s.DrawRect(x + segW, y, segW, h, kit.green)
        s.DrawRect(x + 2 * segW, y, w - 2 * segW, h, kit.blue)
    end if
end sub

' Draws chassis corner brackets around a box.
sub DrawRgbBrackets(s as object, x as integer, y as integer, w as integer, h as integer, thick as integer, arm as integer, color as dynamic)
    ' top-left
    s.DrawRect(x, y, arm, thick, color)
    s.DrawRect(x, y, thick, arm, color)
    ' top-right
    s.DrawRect(x + w - arm, y, arm, thick, color)
    s.DrawRect(x + w - thick, y, thick, arm, color)
    ' bottom-left
    s.DrawRect(x, y + h - thick, arm, thick, color)
    s.DrawRect(x, y + h - arm, thick, arm, color)
    ' bottom-right
    s.DrawRect(x + w - arm, y + h - thick, arm, thick, color)
    s.DrawRect(x + w - thick, y + h - arm, thick, arm, color)
end sub

sub DrawRgbGuide(st as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    count = st.channels.Count()
    ch = invalid
    if count > 0 then ch = st.channels[st.focus]
    s.Clear(c.bg)

    left = Z(48, k)
    right = Z(1232, k)
    totalW = right - left

    ' Top glowing multi-color LED light strip across the header
    DrawLedStripH(s, st, left, Z(16, k), totalW, Z(8, k), Z(18, k), Z(6, k), 0)

    ' Header: Animated RGB LED triad, title, and dynamic clock
    triadW = Z(36, k)
    triadH = Z(14, k)
    headY = Z(38, k)
    DrawRgbTriad(s, left, headY + Z(6, k), triadW, triadH, c, st)
    s.DrawText("RGB LED LIGHTS", left + triadW + Z(14, k), headY, c.white, f.brand)

    modeName = LookColourNames("rgb")[st.colour]
    subText = "MULTI-COLOR LEDS · " + UCase(modeName)
    s.DrawText(subText, left + triadW + Z(14, k), headY + Z(32, k), RgbLedColor(st, 2), f.sub)

    clkText = ClockText(st, false)
    clockW = f.clock.GetOneLineWidth(clkText, 99999)
    s.DrawText(clkText, right - clockW, headY + Z(2, k), RgbLedColor(st, 8), f.clock)

    ' Decorative LED track separator under header
    DrawLedStripH(s, st, left, Z(94, k), totalW, Z(6, k), Z(20, k), Z(4, k), 10)

    ' Channel list (left pane)
    listW = Z(600, k)
    rows = GuideRowsFor(st)
    first = TopRow(st.focus, count, rows)
    rowH = Z(44, k)
    listTopY = Z(116, k)

    for r = 0 to rows - 1
        i = first + r
        if i >= count then exit for
        row = st.channels[i]
        ry = listTopY + r * rowH
        rh = rowH - Z(4, k)
        isFocus = (i = st.focus)

        if isFocus then
            ' Active row: glowing dark card with animated multi-color LED runner strip
            s.DrawRect(left, ry, listW, rh, c.highlight)
            ' Left edge: vertical LED diode strip
            DrawLedStripV(s, st, left + Z(2, k), ry + Z(3, k), Z(6, k), rh - Z(6, k), Z(10, k), Z(4, k), r * 4)
            ' Right edge: active glowing LED diode pip
            pipColor = RgbLedColor(st, r * 4 + 6)
            DrawLedNode(s, left + listW - Z(20, k), ry + (rh - Z(8, k)) \ 2, Z(8, k), pipColor)
            numCol = RgbLedColor(st, r * 4)
            nameCol = c.white
        else
            s.DrawRect(left, ry, listW, rh, c.card)
            numCol = c.dim
            nameCol = c.soft
        end if

        ty = ry + (rh - f.row.GetOneLineHeight()) \ 2
        numY = ry + (rh - f.rowN.GetOneLineHeight()) \ 2
        s.DrawText(row.n, left + Z(18, k), numY, numCol, f.rowN)
        s.DrawText(TrimToWidth(row.name, f.row, listW - Z(100, k)), left + Z(76, k), ty, nameCol, f.row)
    end for

    ' Right pane: preview box / picture-in-picture
    pipX = left + listW + Z(32, k)
    pipY = listTopY
    pipW = right - pipX
    pipH = Z(330, k)

    ' Pip well background
    s.DrawRect(pipX, pipY, pipW, pipH, c.well)

    ' Glowing perimeter LED light strip framing the entire preview box!
    ledG = Z(4, k)
    ledSp = Z(16, k)
    ' Top side
    DrawLedStripH(s, st, pipX, pipY - Z(8, k), pipW, Z(6, k), ledSp, ledG, 0)
    ' Right side
    DrawLedStripV(s, st, pipX + pipW + Z(2, k), pipY, Z(6, k), pipH, ledSp, ledG, 10)
    ' Bottom side
    DrawLedStripH(s, st, pipX, pipY + pipH + Z(2, k), pipW, Z(6, k), ledSp, ledG, 20)
    ' Left side
    DrawLedStripV(s, st, pipX - Z(8, k), pipY, Z(6, k), pipH, ledSp, ledG, 30)

    ' Corner brackets in active lead LED color
    DrawRgbBrackets(s, pipX, pipY, pipW, pipH, Z(2, k), Z(18, k), RgbLedColor(st, 0))
    b = Z(4, k)

    if ch = invalid then
        DrawCentered(s, "NO SIGNAL", pipX, pipY, pipW, pipH, c.dim, f.name)
    else if HasPreview(ch) then
        DrawGamePreview(st, ch, pipX + b, pipY + b, pipW - 2 * b, pipH - 2 * b)
    else
        ' Channel callsign & icon display
        bigH = f.big.GetOneLineHeight()
        hintH = f.pipHint.GetOneLineHeight()
        top = pipY + (pipH - bigH - hintH - Z(16, k)) \ 2
        DrawCentered(s, ch.sign, pipX, top, pipW, bigH, c.white, f.big)
        hint = "OK: TUNE " + UCase(ch.name)
        DrawCentered(s, hint, pipX, top + bigH + Z(16, k), pipW, hintH, RgbLedColor(st, 6), f.pipHint)
    end if

    ' Rukus sits on top of the LED-lit preview box
    sc = Z(6, k)
    DrawSprite(s, RukusLoaf(), RukusColorsFor(st), pipX + Z(16, k), pipY - 9 * sc, sc)

    ' Channel details below preview
    if ch <> invalid then
        detY = pipY + pipH + Z(20, k)
        s.DrawText(ch.n + " · " + ch.name, pipX, detY, c.white, f.name)
        nowText = ChannelListing(st, ch)
        s.DrawText(TrimToWidth(nowText, f.now, pipW), pipX, detY + Z(40, k), c.soft, f.now)

        if st.note <> "" then
            noteCol = RgbLedColor(st, 12)
            s.DrawText(TrimToWidth(st.note, f.sub, pipW), pipX, detY + Z(72, k), noteCol, f.sub)
        end if
    end if

    ' Bottom bar: horizontal LED light strip & remote hints
    barY = Z(652, k)
    DrawLedStripH(s, st, left, barY - Z(10, k), totalW, Z(6, k), Z(20, k), Z(4, k), 16)
    hintText = "[OK] OPEN   [*] SETTINGS   [REPLAY] LAST"
    s.DrawText(hintText, left, barY, c.dim, f.bar)
    DrawRight(s, st.version, right, barY, c.dim, f.bar)

    s.SwapBuffers()
end sub

' Screen panels (Settings, Check, High Scores) in RGB LED Lights look
sub DrawRgbPanel(st as object, spec as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    left = Z(48, k)
    right = Z(1232, k)
    totalW = right - left
    s.Clear(c.bg)

    ' Top glowing multi-color LED light strip
    DrawLedStripH(s, st, left, Z(16, k), totalW, Z(8, k), Z(18, k), Z(6, k), 0)

    headY = Z(38, k)
    triadW = Z(36, k)
    triadH = Z(14, k)
    DrawRgbTriad(s, left, headY + Z(6, k), triadW, triadH, c, st)
    s.DrawText("RGB LED LIGHTS", left + triadW + Z(14, k), headY, c.white, f.brand)
    s.DrawText(UCase(spec.title), left + triadW + Z(14, k), headY + Z(32, k), RgbLedColor(st, 4), f.sub)

    clkText = ClockText(st, false)
    DrawRight(s, clkText, right, headY + Z(2, k), RgbLedColor(st, 8), f.clock)

    ' Separator LED track
    DrawLedStripH(s, st, left, Z(94, k), totalW, Z(6, k), Z(20, k), Z(4, k), 10)

    topY = Z(120, k)
    bottomY = Z(636, k)

    if spec.info then
        DrawInfoRows(st, spec.rows, left + Z(16, k), left + Z(340, k), right - Z(16, k), topY, bottomY, RgbLedColor(st, 4), c.white, invalid)
    else
        visible = 11
        count = spec.rows.Count()
        first = ListTop(spec.sel, count, visible)
        rowH = Z(44, k)
        for r = 0 to visible - 1
            i = first + r
            if i >= count then exit for
            row = spec.rows[i]
            ry = topY + r * rowH
            rh = rowH - Z(4, k)
            isSel = (i = spec.sel)

            if isSel then
                s.DrawRect(left, ry, totalW, rh, c.highlight)
                ' Active row LED runner strip
                DrawLedStripV(s, st, left + Z(2, k), ry + Z(3, k), Z(6, k), rh - Z(6, k), Z(10, k), Z(4, k), r * 4)
                fg = c.white
                cursorCol = RgbLedColor(st, r * 4)
            else
                s.DrawRect(left, ry, totalW, rh, c.card)
                fg = c.soft
                if IsDim(row) then fg = c.dim
                cursorCol = c.dim
            end if

            ty = ry + (rh - f.pageRow.GetOneLineHeight()) \ 2
            if isSel then
                s.DrawText(">", left + Z(18, k), ty, cursorCol, f.pageRow)
            end if
            label = row.label
            s.DrawText(label, left + Z(44, k), ty, fg, f.pageRow)

            vx = DrawRowValue(st, row, isSel, right - Z(24, k), ry, rh, fg, invalid)
            dotStart = left + Z(44, k) + f.pageRow.GetOneLineWidth(label, 99999) + Z(16, k)
            if dotStart < vx - Z(16, k) then
                DrawDots(s, dotStart, vx - Z(16, k), ry + rh \ 2, Z(2, k), Z(6, k), c.dim)
            end if
        end for
    end if

    barY = Z(652, k)
    DrawLedStripH(s, st, left, barY - Z(10, k), totalW, Z(6, k), Z(20, k), Z(4, k), 16)
    hint = UCase(spec.hint)
    s.DrawText(hint, left, barY, c.dim, f.bar)
    DrawRight(s, st.version, right, barY, c.dim, f.bar)

    s.SwapBuffers()
end sub
