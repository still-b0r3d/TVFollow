' The late-night cable guide look: a preview window, the focused channel's details, a grid
' of channels and a bar of remote hints. Laid out on a 1280 x 720 grid and scaled by st.k.
' Its colours are CableKit() in looks.brs.

' How many channel rows each look's guide shows at once.
function GuideRowsFor(st as object) as integer
    if st.look = "cable" then return 6
    if st.look = "b95" then return 13
    return 12
end function

' This look's fonts, at the screen's scale.
function CableFonts(st as object) as object
    k = st.k
    return {
        logo: Barlow(st, "extrabold", Z(46, k)),
        badge: Barlow(st, "extrabold", Z(24, k)),
        time: Vt323(st, Z(48, k)),
        date: Barlow(st, "semibold", Z(20, k)),
        num: Vt323(st, Z(76, k)),
        sign: Barlow(st, "extrabold", Z(60, k)),
        name: Barlow(st, "bold", Z(32, k)),
        now: Barlow(st, "medium", Z(23, k)),
        hint: Barlow(st, "bold", Z(23, k)),
        big: Barlow(st, "extrabold", Z(104, k)),
        bigHint: Barlow(st, "semibold", Z(24, k)),
        head: Barlow(st, "bold", Z(20, k)),
        cellNum: Vt323(st, Z(36, k)),
        cellSign: Barlow(st, "extrabold", Z(27, k)),
        cellNow: Barlow(st, "semibold", Z(25, k)),
        bar: Barlow(st, "bold", Z(21, k)),
        barNum: Vt323(st, Z(26, k)),
        pageTitle: Barlow(st, "extrabold", Z(30, k)),
        pageRow: Barlow(st, "semibold", Z(25, k)),
        pageChoice: Barlow(st, "bold", Z(25, k)),
        pageLabel: Barlow(st, "semibold", Z(23, k)),
        pageInfo: Vt323(st, Z(27, k)),
        banner: Barlow(st, "bold", Z(23, k))
    }
end function

sub DrawCableGuide(st as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    count = st.channels.Count()
    ch = invalid
    if count > 0 then ch = st.channels[st.focus]
    s.Clear(c.bg)

    ' The preview window shows the focused channel.
    DrawFrame(s, Z(40, k), Z(30, k), Z(560, k) - Z(40, k), Z(322, k) - Z(30, k), Z(4, k), c.frame)
    px = Z(44, k)
    py = Z(34, k)
    pw = Z(556, k) - px
    ph = Z(318, k) - py
    s.DrawRect(px, py, pw, ph, c.tile)
    if ch = invalid then
        DrawCentered(s, "NO CHANNELS YET", px, py, pw, ph, c.white, f.name)
    else if HasPreview(ch) then
        DrawGamePreview(st, ch, px, py, pw, ph)
    else
        bigH = f.big.GetOneLineHeight()
        hintH = f.bigHint.GetOneLineHeight()
        gap = Z(10, k)
        top = py + (ph - bigH - gap - hintH) \ 2
        DrawCenteredSpaced(s, ch.sign, px, top, pw, bigH, c.white, f.big, Z(6, k))
        DrawCentered(s, TrimToWidth("OK opens " + ch.name, f.bigHint, pw - Z(24, k)), px, top + bigH + gap, pw, hintH, c.soft, f.bigHint)
    end if

    ' The focused channel's details, the logo and the clock.
    x = Z(592, k)
    right = Z(1240, k)
    logoY = Z(26, k)
    DrawSpaced(s, "b0r3d", x, logoY, c.white, f.logo, Z(1, k))
    bx = x + SpacedWidth("b0r3d", f.logo, Z(1, k)) + Z(10, k)
    bw = f.badge.GetOneLineWidth("TV", 99999) + Z(20, k)
    bh = f.badge.GetOneLineHeight() + Z(2, k)
    by = logoY + (f.logo.GetOneLineHeight() - bh) \ 2
    s.DrawRect(bx, by, bw, bh, c.accent)
    DrawCentered(s, "TV", bx, by, bw, bh, c.ink, f.badge)
    timeY = Z(28, k)
    DrawRight(s, ClockText(st, false), right, timeY, c.white, f.time)
    DrawRightSpaced(s, st.clock.date, right, timeY + f.time.GetOneLineHeight() + Z(2, k), c.soft, f.date, Z(2, k))
    if ch <> invalid then
        ny = Z(97, k)
        s.DrawText(ch.n, x, ny, c.accent, f.num)
        sx = x + f.num.GetOneLineWidth(ch.n, 99999) + Z(18, k)
        sy = ny + f.num.GetAscent() - f.sign.GetAscent()
        s.DrawText(ch.sign, sx, sy, c.white, f.sign)
        s.DrawText(TrimToWidth(ch.name, f.name, right - x), x, Z(181, k), c.text, f.name)
        s.DrawText(TrimToWidth(ChannelListing(st, ch), f.now, right - x), x, Z(227, k), c.soft, f.now)
    end if
    sc = Z(7, k)
    rukusX = right - 16 * sc
    hint = ""
    if ch <> invalid then hint = OkWord(ch)
    if st.note <> "" then hint = st.note
    s.DrawText(TrimToWidth(hint, f.hint, rukusX - x - Z(12, k)), x, Z(322, k) - f.hint.GetOneLineHeight(), c.accent, f.hint)
    DrawSprite(s, RukusLoaf(), RukusColorsFor(st), rukusX, Z(322, k) - 9 * sc, sc)

    ' The grid: a header, then one row per channel, scrolling with the highlight.
    gx = Z(40, k)
    w1 = Z(136, k) - gx
    x2 = Z(140, k)
    w2 = Z(310, k) - x2
    x3 = Z(314, k)
    w3 = right - x3
    hy = Z(340, k)
    hh = Z(374, k) - hy
    s.DrawRect(gx, hy, w1, hh, c.frame)
    s.DrawRect(x2, hy, w2, hh, c.frame)
    s.DrawRect(x3, hy, w3, hh, c.frame)
    DrawCenteredSpaced(s, "CH", gx, hy, w1, hh, c.ink, f.head, Z(2, k))
    DrawMiddleSpaced(s, "CALL", x2 + Z(14, k), hy, hh, c.ink, f.head, Z(2, k))
    DrawMiddleSpaced(s, "ON NOW · " + ClockText(st, false), x3 + Z(16, k), hy, hh, c.ink, f.head, Z(2, k))
    rows = GuideRowsFor(st)
    first = TopRow(st.focus, count, rows)
    for r = 0 to rows - 1
        i = first + r
        if i >= count then exit for
        row = st.channels[i]
        ry = Z(378 + r * 48, k)
        rh = Z(422 + r * 48, k) - ry
        fg = c.text
        b1 = c.cell1
        b2 = c.cell2
        b3 = c.cell3
        if i = st.focus then
            fg = c.ink
            b1 = c.accent
            b2 = c.accent
            b3 = c.accent
        end if
        s.DrawRect(gx, ry, w1, rh, b1)
        s.DrawRect(x2, ry, w2, rh, b2)
        s.DrawRect(x3, ry, w3, rh, b3)
        DrawCentered(s, row.n, gx, ry, w1, rh, fg, f.cellNum)
        DrawMiddleSpaced(s, row.sign, x2 + Z(14, k), ry, rh, fg, f.cellSign, Z(1, k))
        DrawMiddle(s, TrimToWidth(ChannelListing(st, row), f.cellNow, w3 - Z(32, k)), x3 + Z(16, k), ry, rh, fg, f.cellNow)
    end for

    DrawGuideBar(st, ch, count)
    s.SwapBuffers()
end sub

' The first row shown, so the highlight sits third from the top while the list scrolls.
function TopRow(focus as integer, count as integer, rows as integer) as integer
    first = focus - 2
    if first > count - rows then first = count - rows
    if first < 0 then first = 0
    return first
end function

' The bar of remote hints, the version and where the highlight is.
sub DrawGuideBar(st as object, ch as dynamic, count as integer)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    top = Z(668, k)
    s.DrawRect(0, top, st.w, st.h - top, c.bar)
    bw = Z(48, k)
    bh = Z(44, k)
    bt = top + (st.h - top - bh) \ 2
    ax = Z(40, k)
    s.DrawRect(ax, bt, bw, bh, c.cell1)
    DrawChevron(s, ax + bw \ 2, bt + bh \ 2, Z(6, k), Z(3, k), true, c.white)
    ax = ax + bw + Z(14, k)
    s.DrawRect(ax, bt, bw, bh, c.cell1)
    DrawChevron(s, ax + bw \ 2, bt + bh \ 2, Z(6, k), Z(3, k), false, c.white)
    hx = ax + bw + Z(14, k)
    ty = top + (st.h - top - f.bar.GetOneLineHeight()) \ 2
    mid = top + (st.h - top) \ 2
    sp = Z(1, k)
    gap = Z(14, k)
    far = Z(32, k)
    hx = DrawRun(s, hx, ty, "MOVE", c.soft, f.bar, sp, far)
    hx = DrawRun(s, hx, ty, "OK", c.white, f.bar, sp, gap)
    hx = DrawRun(s, hx, ty, "TUNE IN", c.soft, f.bar, sp, far)
    DrawStar(s, hx + Z(8, k), mid, Z(8, k), Z(3, k), c.white)
    hx = hx + Z(16, k) + gap
    DrawRun(s, hx, ty, "SETTINGS", c.soft, f.bar, sp, 0)

    right = Z(1240, k)
    nty = top + (st.h - top - f.barNum.GetOneLineHeight()) \ 2
    where = ""
    if ch <> invalid then where = "CH " + ch.n + " · " + (st.focus + 1).ToStr() + " OF " + count.ToStr()
    DrawRight(s, where, right, nty, c.soft, f.barNum)
    DrawRight(s, st.version, right - f.barNum.GetOneLineWidth(where, 99999) - Z(28, k), nty, c.soft, f.barNum)
end sub
