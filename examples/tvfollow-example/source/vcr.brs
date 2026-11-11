' The VCR menu look: an on-screen menu in blocky white letters with hard black shadows, a yellow
' cursor, a big green channel number and a picture-in-picture box. Laid out on the 1280 x 720
' grid (scaled by st.k); its colours are VcrKit() in looks.brs. Silkscreen is only sharp at
' multiples of 8 pixels, so Silk() rounds every size to that.

function VcrFonts(st as object) as object
    k = st.k
    return {
        brand: Silk(st, Z(30, k), true),
        sub: Silk(st, Z(22, k), false),
        ch: Silk(st, Z(64, k), true),
        chSign: Silk(st, Z(26, k), false),
        row: Silk(st, Z(26, k), false),
        big: Silk(st, Z(84, k), true),
        pipHint: Silk(st, Z(20, k), false),
        name: Silk(st, Z(30, k), true),
        now: Silk(st, Z(20, k), false),
        bar: Silk(st, Z(22, k), false),
        clock: Silk(st, Z(34, k), true),
        pageTitle: Silk(st, Z(30, k), true),
        pageRow: Silk(st, Z(22, k), false),
        pageChoice: Silk(st, Z(22, k), false),
        pageLabel: Silk(st, Z(16, k), false),
        pageInfo: Silk(st, Z(16, k), false)
    }
end function

sub DrawVcrGuide(st as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    sh = Z(3, k)
    count = st.channels.Count()
    ch = invalid
    if count > 0 then ch = st.channels[st.focus]
    s.Clear(c.bg)

    DrawVcrTitle(st, "CHANNEL MENU")

    ' The big channel number, top right.
    right = Z(1224, k)
    if ch <> invalid then
        DrawVcrChannel(st, ch, Z(26, k))
    end if

    ' The channel list with the yellow cursor.
    x = Z(56, k)
    rows = GuideRowsFor(st)
    first = TopRow(st.focus, count, rows)
    for r = 0 to rows - 1
        i = first + r
        if i >= count then exit for
        row = st.channels[i]
        ry = Z(132 + r * 41, k)
        rh = Z(170 + r * 41, k) - ry
        fg = c.white
        if i = st.focus then
            fg = c.yellow
            tw = Z(20, k)
            th = Z(22, k)
            DrawTriangleRight(s, x + sh, ry + (rh - th) \ 2 + sh, tw, th, c.shadow)
            DrawTriangleRight(s, x, ry + (rh - th) \ 2, tw, th, c.yellow)
        end if
        ty = ry + (rh - f.row.GetOneLineHeight()) \ 2
        DrawShadowed(s, row.n, x + Z(36, k), ty, fg, c.shadow, f.row, sh)
        DrawShadowed(s, TrimToWidth(UCase(row.name), f.row, Z(496, k)), x + Z(104, k), ty, fg, c.shadow, f.row, sh)
    end for

    ' The picture-in-picture box: a white frame with a hard shadow.
    px = Z(700, k)
    py = Z(170, k)
    pw = right - px
    ph = Z(472, k) - py
    s.DrawRect(px + Z(4, k), py + Z(4, k), pw, ph, c.shadow)
    s.DrawRect(px, py, pw, ph, c.white)
    b = Z(6, k)
    s.DrawRect(px + b, py + b, pw - 2 * b, ph - 2 * b, c.pip)
    if ch = invalid then
        DrawCentered(s, "NO CHANNELS", px, py, pw, ph, c.white, f.name)
    else if HasPreview(ch) then
        DrawGamePreview(st, ch, px + b, py + b, pw - 2 * b, ph - 2 * b)
    else
        bigH = f.big.GetOneLineHeight()
        hintH = f.pipHint.GetOneLineHeight()
        gap = Z(16, k)
        top = py + (ph - bigH - gap - hintH) \ 2
        bw = f.big.GetOneLineWidth(ch.sign, 99999)
        DrawShadowed(s, ch.sign, px + (pw - bw) \ 2, top, c.white, c.shadow, f.big, Z(4, k))
        hint = TrimToWidth("OK: OPEN " + UCase(ch.name), f.pipHint, pw - 2 * b - Z(24, k))
        hw = f.pipHint.GetOneLineWidth(hint, 99999)
        DrawShadowed(s, hint, px + (pw - hw) \ 2, top + bigH + gap, c.white, c.shadow, f.pipHint, sh)
    end if
    sc = Z(6.5, k)
    DrawSprite(s, RukusLoaf(), RukusColorsFor(st), Z(740, k), Z(174, k) - 9 * sc, sc)

    ' The focused channel's name, what's on, and any note.
    if ch <> invalid then
        dx = px
        dy = Z(500, k)
        DrawShadowed(s, TrimToWidth(UCase(ch.name), f.name, pw), dx, dy, c.white, c.shadow, f.name, sh)
        dy = dy + f.name.GetOneLineHeight() + Z(8, k)
        DrawShadowed(s, TrimToWidth(UCase(ChannelListing(st, ch)), f.now, pw), dx, dy, c.white, c.shadow, f.now, sh)
        if st.note <> "" then
            dy = dy + f.now.GetOneLineHeight() + Z(8, k)
            DrawShadowed(s, TrimToWidth(UCase(st.note), f.now, pw), dx, dy, c.yellow, c.shadow, f.now, sh)
        end if
    end if

    DrawVcrBar(st, ":SELECT", "OK:TUNE", ":SETUP", true)
    s.SwapBuffers()
end sub

' The title shared by the VCR screens: a white play triangle, "b0r3d TV" and a line under it.
sub DrawVcrTitle(st as object, subtitle as string)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    sh = Z(3, k)
    x = Z(56, k)
    y = Z(34, k)
    bh = f.brand.GetOneLineHeight()
    tw = Z(22, k)
    th = Z(24, k)
    DrawTriangleRight(s, x + sh, y + (bh - th) \ 2 + sh, tw, th, c.shadow)
    DrawTriangleRight(s, x, y + (bh - th) \ 2, tw, th, c.white)
    DrawShadowed(s, "b0r3d TV", x + tw + Z(12, k), y, c.white, c.shadow, f.brand, sh)
    DrawShadowed(s, subtitle, x, y + bh + Z(6, k), c.white, c.shadow, f.sub, sh)
end sub

' The big green channel number and the call sign under it, top right.
sub DrawVcrChannel(st as object, ch as object, y as integer)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    sh = Z(3, k)
    right = Z(1224, k)
    DrawShadowedRight(s, "CH " + ch.n, right, y, c.green, c.shadow, f.ch, sh)
    DrawShadowedRight(s, ch.sign, right, y + f.ch.GetOneLineHeight() + Z(4, k), c.white, c.shadow, f.chSign, sh)
end sub

' The bar shared by the VCR screens: two outlined arrow keys, three hints, and the clock.
sub DrawVcrBar(st as object, moveHint as string, okHint as string, starHint as string, clock as boolean)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    sh = Z(3, k)
    x = Z(56, k)
    top = Z(646, k)
    bh = Z(44, k)
    bw = Z(52, k)
    by = top + (Z(48, k) - bh) \ 2
    line = Z(3, k)
    for each up in [true, false]
        DrawFrame(s, x + sh, by + sh, bw, bh, line, c.shadow)
        DrawFrame(s, x, by, bw, bh, line, c.white)
        DrawChevron(s, x + bw \ 2 + sh, by + bh \ 2 + sh, Z(6, k), Z(4, k), up, c.shadow)
        DrawChevron(s, x + bw \ 2, by + bh \ 2, Z(6, k), Z(4, k), up, c.white)
        x = x + bw + Z(12, k)
    end for
    ty = by + (bh - f.bar.GetOneLineHeight()) \ 2
    DrawShadowed(s, moveHint, x, ty, c.white, c.shadow, f.bar, sh)
    x = x + f.bar.GetOneLineWidth(moveHint, 99999) + Z(30, k)
    DrawShadowed(s, okHint, x, ty, c.white, c.shadow, f.bar, sh)
    x = x + f.bar.GetOneLineWidth(okHint, 99999) + Z(30, k)
    starY = by + bh \ 2
    DrawStar(s, x + Z(9, k) + sh, starY + sh, Z(8, k), Z(3, k), c.shadow)
    DrawStar(s, x + Z(9, k), starY, Z(8, k), Z(3, k), c.white)
    x = x + Z(20, k)
    DrawShadowed(s, starHint, x, ty, c.white, c.shadow, f.bar, sh)
    right = Z(1224, k)
    if clock then
        ' The clock, with its colon blinking once a second.
        if st.clock24 then
            hh = Left(st.clock.time24, 2)
            rest = Mid(st.clock.time24, 4)
        else
            p = Instr(1, st.clock.hm12, ":")
            hh = Left(st.clock.hm12, p - 1)
            rest = Mid(st.clock.hm12, p + 1) + " " + st.clock.half
        end if
        wh = f.clock.GetOneLineWidth(hh, 99999)
        wc = f.clock.GetOneLineWidth(":", 99999)
        wr = f.clock.GetOneLineWidth(rest, 99999)
        cx = right - wh - wc - wr
        cy = by + (bh - f.clock.GetOneLineHeight()) \ 2
        DrawShadowed(s, hh, cx, cy, c.white, c.shadow, f.clock, sh)
        if st.clock.second mod 2 = 0 then DrawShadowed(s, ":", cx + wh, cy, c.white, c.shadow, f.clock, sh)
        DrawShadowed(s, rest, cx + wh + wc, cy, c.white, c.shadow, f.clock, sh)
    else
        vw = f.bar.GetOneLineWidth(st.version, 99999)
        DrawShadowed(s, st.version, right - vw, ty, c.white, c.shadow, f.bar, sh)
    end if
end sub
