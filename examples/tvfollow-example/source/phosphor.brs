' The phosphor terminal look: one glowing colour on black, with scanlines. A header with the
' date and clock, the channel list with dotted leaders, a framed preview, the focused channel
' with a blinking cursor, and a bar of remote hints. Laid out on the 1280 x 720 grid (scaled by
' st.k); its colours are PhosphorKit() in looks.brs.

function PhosphorFonts(st as object) as object
    k = st.k
    return {
        title: Vt323(st, Z(44, k)),
        date: Vt323(st, Z(30, k)),
        clock: Vt323(st, Z(44, k)),
        row: Vt323(st, Z(30, k)),
        head: Vt323(st, Z(24, k)),
        big: Vt323(st, Z(120, k)),
        hint: Vt323(st, Z(28, k)),
        detail: Vt323(st, Z(40, k)),
        detailSmall: Vt323(st, Z(30, k)),
        bar: Vt323(st, Z(28, k)),
        pageTitle: Vt323(st, Z(44, k)),
        pageRow: Vt323(st, Z(30, k)),
        pageChoice: Vt323(st, Z(30, k)),
        pageLabel: Vt323(st, Z(30, k)),
        pageInfo: Vt323(st, Z(27, k)),
        banner: Vt323(st, Z(40, k))
    }
end function

sub DrawPhosphorGuide(st as object)
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
    line = Z(2, k)
    glow = Z(1, k)

    ' The header: title, date and clock over a rule, with Ruckus loafing on it.
    titleEnd = DrawPhosphorHeader(st, "B0R3D TV // CHANNEL GUIDE", true)
    sc = Z(7, k)
    DrawSprite(s, RukusLoaf(), RukusColorsFor(st), titleEnd + Z(24, k), Z(86, k) - 9 * sc, sc)

    ' The channel list.
    rows = GuideRowsFor(st)
    first = TopRow(st.focus, count, rows)
    lw = Z(584, k)
    for r = 0 to rows - 1
        i = first + r
        if i >= count then exit for
        row = st.channels[i]
        ry = Z(108 + r * 42, k)
        rh = Z(146 + r * 42, k) - ry
        fg = c.full
        lead = c.lead
        if i = st.focus then
            s.DrawRect(left, ry, lw, rh, c.full)
            fg = c.bg
            lead = c.bg
        end if
        ty = ry + (rh - f.row.GetOneLineHeight()) \ 2
        if i = st.focus then s.DrawText(">", left + Z(12, k), ty, fg, f.row)
        s.DrawText(row.n, left + Z(40, k), ty, fg, f.row)
        s.DrawText(row.sign, left + Z(92, k), ty, fg, f.row)
        name = TrimToWidth(row.name, f.row, Z(340, k))
        nx = left + lw - Z(12, k) - f.row.GetOneLineWidth(name, 99999)
        s.DrawText(name, nx, ty, fg, f.row)
        DrawDots(s, left + Z(200, k), nx - Z(12, k), ry + rh \ 2 + Z(5, k), line, Z(5, k), lead)
    end for

    ' The preview box.
    bx = Z(672, k)
    by = Z(112, k)
    bw = right - bx
    bh = Z(418, k) - by
    DrawFrame(s, bx, by, bw, bh, line, c.full)
    hh = Z(30, k)
    s.DrawRect(bx, by + hh - line, bw, line, c.full)
    if ch <> invalid then
        ht = by + (hh - f.head.GetOneLineHeight()) \ 2
        DrawGlow(s, "PREVIEW :: CH " + ch.n, bx + Z(12, k), ht, c.full, c.glow, f.head, glow)
        DrawGlow(s, ch.sign, bx + bw - Z(12, k) - f.head.GetOneLineWidth(ch.sign, 99999), ht, c.full, c.glow, f.head, glow)
    end if
    if ch = invalid then
        DrawCentered(s, "NO CHANNELS YET", bx, by + hh, bw, bh - hh, c.full, f.detail)
    else if HasPreview(ch) then
        DrawGamePreview(st, ch, bx + line, by + hh, bw - 2 * line, bh - hh - line)
    else
        bigH = f.big.GetOneLineHeight()
        hintH = f.hint.GetOneLineHeight()
        top = by + hh + (bh - hh - bigH - Z(6, k) - hintH) \ 2
        sw = SpacedWidth(ch.sign, f.big, Z(6, k))
        sx = bx + (bw - sw) \ 2
        DrawGlowSpaced(s, ch.sign, sx, top, c.full, c.glow, f.big, glow, Z(6, k))
        hint = TrimToWidth(UCase("OK opens " + ch.name), f.hint, bw - Z(24, k))
        DrawCentered(s, hint, bx, top + bigH + Z(6, k), bw, hintH, c.dim, f.hint)
    end if

    ' The focused channel, its line of what's on, and the hint with a blinking cursor.
    dx = Z(672, k)
    dy = Z(440, k)
    if ch <> invalid then
        dw = right - dx
        DrawGlow(s, TrimToWidth("> " + UCase(ch.name), f.detail, dw), dx, dy, c.full, c.glow, f.detail, glow)
        dy = dy + f.detail.GetOneLineHeight() + Z(6, k)
        s.DrawText(TrimToWidth("  " + UCase(ChannelListing(st, ch)), f.detailSmall, dw), dx, dy, c.dim, f.detailSmall)
        dy = dy + f.detailSmall.GetOneLineHeight() + Z(6, k)
        hint = UCase(OkWord(ch))
        if st.note <> "" then hint = UCase(st.note)
        hint = TrimToWidth("  " + hint, f.detailSmall, dw - Z(30, k))
        DrawGlow(s, hint, dx, dy, c.full, c.glow, f.detailSmall, glow)
        if st.clock.second mod 2 = 0 then
            cx = dx + f.detailSmall.GetOneLineWidth(hint, 99999) + Z(8, k)
            s.DrawRect(cx, dy + (f.detailSmall.GetOneLineHeight() - Z(26, k)) \ 2, Z(14, k), Z(26, k), c.full)
        end if
    end if

    ' The bar.
    where = ""
    if ch <> invalid then where = "CH " + ch.n + " :: " + (st.focus + 1).ToStr() + "/" + count.ToStr()
    DrawPhosphorBar(st, "[OK] TUNE IN", "SETTINGS", where)
    if st.scan then DrawScanlines(st)
    s.SwapBuffers()
end sub

' The header shared by the phosphor screens: the title on the left, the date and clock (or the
' version) on the right, over a rule. Returns where the title ends.
function DrawPhosphorHeader(st as object, title as string, clock as boolean) as integer
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    left = Z(48, k)
    right = Z(1232, k)
    top = Z(22, k)
    bottom = Z(84, k)
    glow = Z(1, k)
    ty = top + (bottom - top - f.title.GetOneLineHeight()) \ 2
    DrawGlowSpaced(s, title, left, ty, c.full, c.glow, f.title, glow, Z(2, k))
    titleEnd = left + SpacedWidth(title, f.title, Z(2, k))
    if clock then
        text = ClockText(st, true)
        cw = f.clock.GetOneLineWidth(text, 99999)
        DrawGlow(s, text, right - cw, ty, c.full, c.glow, f.clock, glow)
        dy = ty + f.clock.GetAscent() - f.date.GetAscent()
        DrawRight(s, st.clock.isoDate, right - cw - Z(22, k), dy, c.dim, f.date)
    else
        dy = ty + f.title.GetAscent() - f.date.GetAscent()
        DrawRight(s, st.version, right, dy, c.dim, f.date)
    end if
    s.DrawRect(left, bottom, right - left, Z(2, k), c.full)
    return titleEnd
end function

' The bar shared by the phosphor screens: arrow keys, two hints and something on the right.
sub DrawPhosphorBar(st as object, okHint as string, starHint as string, rightText as string)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    left = Z(48, k)
    right = Z(1232, k)
    line = Z(2, k)
    glow = Z(1, k)
    s.DrawRect(left, Z(646, k), right - left, line, c.full)
    bw = Z(52, k)
    bh = Z(40, k)
    by = Z(698, k) - bh
    DrawFrame(s, left, by, bw, bh, line, c.full)
    DrawChevron(s, left + bw \ 2, by + bh \ 2, Z(6, k), Z(3, k), true, c.full)
    x2 = left + bw + Z(12, k)
    DrawFrame(s, x2, by, bw, bh, line, c.full)
    DrawChevron(s, x2 + bw \ 2, by + bh \ 2, Z(6, k), Z(3, k), false, c.full)
    ty = by + (bh - f.bar.GetOneLineHeight()) \ 2
    tx = x2 + bw + Z(12, k)
    tx = DrawGlowRun(s, tx, ty, "MOVE", c, f.bar, glow, Z(30, k))
    tx = DrawGlowRun(s, tx, ty, okHint, c, f.bar, glow, Z(30, k))
    tx = DrawGlowRun(s, tx, ty, "[", c, f.bar, glow, 0)
    DrawStar(s, tx + Z(9, k), ty + f.bar.GetOneLineHeight() \ 2, Z(7, k), Z(2, k), c.full)
    tx = tx + Z(18, k)
    DrawGlowRun(s, tx, ty, "] " + starHint, c, f.bar, glow, 0)
    if rightText <> "" then
        DrawRight(s, rightText, right, ty, c.dim, f.bar)
        DrawRight(s, st.version, right - f.bar.GetOneLineWidth(rightText, 99999) - Z(24, k), ty, c.dim, f.bar)
    end if
end sub

' Whether the screens get scanlines: only the phosphor look has them, and its Scanlines
' setting turns them off.
function ScanlinesOn(st as object) as boolean
    return st.look = "phosphor" and st.scan
end function

' The phosphor look's scanlines as one see-through picture of the given size (its height is a
' whole number of scanlines at both screen sizes), lined up with the ones over the rest of the
' screen when it's placed at a multiple of the scanline spacing. Made without blending, so its
' dark lines keep their own see-through colour.
function ScanTile(st as object, width as integer, height as integer) as dynamic
    k = st.k
    period = Z(4, k)
    thick = Z(1, k)
    lines = height \ period
    if lines < 1 then lines = 1
    bmp = NewPicture(width, period * lines, false)
    if bmp = invalid then return invalid
    bmp.Clear(&h00000000)
    for i = 0 to lines - 1
        bmp.DrawRect(0, i * period + period - thick, width, thick, &h0000004D)
    end for
    return bmp
end function

' Dark lines over everything, like the gaps between a tube's scanlines: a strip of them as wide
' as the screen (st.scanBmp, kept until the look or the screen size changes) drawn down the
' whole target, so no frame draws hundreds of lines and no second whole-screen picture is
' held next to a game's chrome. With no memory for the strip, the lines are drawn one by one.
sub DrawScanlines(st as object, target = invalid as dynamic)
    if target = invalid then target = st.screen
    if target = invalid then return
    k = st.k
    period = Z(4, k)
    thick = Z(1, k)
    if st.scanBmp = invalid then st.scanBmp = ScanTile(st, st.w, 24 * period)
    if st.scanBmp <> invalid then
        stripH = st.scanBmp.GetHeight()
        y = 0
        while y < st.h
            target.DrawObject(0, y, st.scanBmp)
            y = y + stripH
        end while
        return
    end if
    y = period - thick
    while y < st.h
        target.DrawRect(0, y, st.w, thick, &h0000004D)
        y = y + period
    end while
end sub
