' The b0r3d 95 look: a desktop with icons and Rukus, the "b0r3d TV Guide" window (title bar,
' menu bar, the channel list with a scrollbar, a preview box, Open and Settings... buttons and a
' status bar) and a taskbar with Start and the clock. Settings and its pages open as dialogs.
' Laid out on the 1280 x 720 grid (scaled by st.k). W95FA has no bold, no middle dot and no
' ellipsis, so bold is the text drawn twice a pixel apart and B95Text draws the dot itself.

function B95Kit(index as integer) as object
    desktops = [&h008080FF, &h3A6EA5FF, &h5A5A7AFF]
    return {
        bg: desktops[index],
        face: &hC0C0C0FF,
        light: &hDFDFDFFF,
        white: &hFFFFFFFF,
        shadow: &h808080FF,
        dark: &h0A0A0AFF,
        black: &h000000FF,
        navy: &h000080FF,
        teal: &h008080FF,
        red: &hFF0000FF,
        noise: [&h000000FF, &h555555FF, &hAAAAAAFF, &hFFFFFFFF]
    }
end function

function B95Fonts(st as object) as object
    k = st.k
    return {
        title: W95(st, Z(21, k)),
        menu: W95(st, Z(20, k)),
        row: W95(st, Z(22, k)),
        small: W95(st, Z(18, k)),
        body: W95(st, Z(19, k)),
        name: W95(st, Z(26, k)),
        now: W95(st, Z(20, k)),
        button: W95(st, Z(20, k)),
        page: W95(st, Z(21, k)),
        pageTitle: W95(st, Z(21, k)),
        pageRow: W95(st, Z(21, k)),
        pageChoice: W95(st, Z(21, k)),
        pageLabel: W95(st, Z(19, k)),
        pageInfo: W95(st, Z(19, k)),
        bar: W95(st, Z(18, k))
    }
end function

sub DrawB95Guide(st as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    count = st.channels.Count()
    ch = invalid
    if count > 0 then ch = st.channels[st.focus]
    s.Clear(c.bg)
    DrawB95Desktop(st, ch, "b0r3d TV Guide")

    ' The window, its title bar and its menu bar.
    wx = Z(180, k)
    wy = Z(18, k)
    ww = Z(1260, k) - wx
    wh = Z(662, k) - wy
    B95Raised(s, wx, wy, ww, wh, t, c)
    DrawB95TitleBar(st, wx + Z(4, k), wy + Z(4, k), ww - Z(8, k), "b0r3d TV Guide", true)
    menuY = Z(64, k)
    mx = wx + Z(14, k)
    for each word in ["File", "View", "Channels", "Help"]
        DrawB95Underlined(st, word, mx, menuY + (Z(30, k) - f.menu.GetOneLineHeight()) \ 2, f.menu)
        mx = mx + f.menu.GetOneLineWidth(word, 99999) + Z(22, k)
    end for

    ' The channel list in a sunken white field, with a scrollbar.
    lx = Z(190, k)
    ly = Z(102, k)
    lw = Z(682, k) - lx
    lh = Z(620, k) - ly
    B95Sunken(s, lx, ly, lw, lh, t, c, c.white)
    sbw = Z(26, k)
    rx = lx + Z(6, k)
    rw = lw - Z(10, k) - sbw
    rows = GuideRowsFor(st)
    first = TopRow(st.focus, count, rows)
    for r = 0 to rows - 1
        i = first + r
        if i >= count then exit for
        row = st.channels[i]
        ry = Z(108 + r * 38, k)
        rh = Z(146 + r * 38, k) - ry
        fg = c.black
        if i = st.focus then
            s.DrawRect(rx, ry, rw, rh, c.navy)
            fg = c.white
        end if
        DrawB95Icon(s, B95IconFor(st, row), rx + Z(10, k), ry + (rh - Z(22, k)) \ 2, Z(24, k), c)
        ty = ry + (rh - f.row.GetOneLineHeight()) \ 2
        B95Text(s, row.n, rx + Z(46, k), ty, fg, f.row, k, false)
        B95Text(s, B95Trim(row.name, f.row, rw - Z(104, k), k, false), rx + Z(92, k), ty, fg, f.row, k, false)
    end for
    DrawB95Scrollbar(st, lx + lw - Z(4, k) - sbw, ly + Z(4, k), sbw, lh - Z(8, k), st.focus, count)

    ' The preview, in an etched group box.
    gx = Z(694, k)
    gy = Z(112, k)
    gw = Z(1250, k) - gx
    gh = Z(436, k) - gy
    DrawB95Group(st, gx, gy, gw, gh, "Preview")
    px = gx + Z(14, k)
    py = gy + Z(22, k)
    pw = Z(512, k)
    ph = Z(288, k)
    s.DrawRect(px, py, pw, ph, c.black)
    B95Edge(s, px, py, pw, ph, t, c.shadow, c.white)
    DrawB95Preview(st, ch, px + t, py + t, pw - 2 * t, ph - 2 * t)

    ' The focused channel, and the two buttons.
    ix = Z(698, k)
    if ch <> invalid then
        iy = Z(448, k)
        B95Text(s, B95Trim(ch.n + "  " + ch.name, f.name, Z(1246, k) - ix, k, true), ix, iy, c.black, f.name, k, true)
        B95Text(s, B95Trim(ChannelListing(st, ch), f.now, Z(1246, k) - ix, k, false), ix, iy + f.name.GetOneLineHeight() + Z(8, k), c.black, f.now, k, false)
    end if
    bw = Z(150, k)
    bh = Z(44, k)
    by = Z(572, k)
    s.DrawRect(ix, by, bw, bh, c.black)
    B95Button(s, ix + t, by + t, bw - 2 * t, bh - 2 * t, t, c)
    B95Centered(st, "Open", ix, by, bw, bh, f.button)
    bx2 = ix + bw + Z(12, k)
    B95Button(s, bx2, by, bw, bh, t, c)
    B95Centered(st, "Settings...", bx2, by, bw, bh, f.button)

    ' The status bar: how many channels, the hint (or what just happened), the version.
    statusY = Z(628, k)
    statusH = Z(658, k) - statusY
    c1x = wx + Z(6, k)
    c1w = Z(200, k)
    c3w = Z(250, k)
    c3x = wx + ww - Z(6, k) - c3w
    c2x = c1x + c1w + Z(4, k)
    c2w = c3x - Z(4, k) - c2x
    for each cell in [[c1x, c1w], [c2x, c2w], [c3x, c3w]]
        B95Edge(s, cell[0], statusY, cell[1], statusH, t, c.shadow, c.white)
    end for
    textY = statusY + (statusH - f.small.GetOneLineHeight()) \ 2
    counted = count.ToStr() + " channels"
    if count = 1 then counted = "1 channel"
    B95Text(s, counted, c1x + Z(8, k), textY, c.black, f.small, k, false)
    hint = ""
    if ch <> invalid then hint = OkWord(ch)
    if st.note <> "" then hint = st.note
    B95Text(s, B95Trim(hint, f.small, c2w - Z(16, k), k, false), c2x + Z(8, k), textY, c.black, f.small, k, false)
    B95Text(s, B95Trim(st.version, f.small, c3w - Z(16, k), k, false), c3x + Z(8, k), textY, c.black, f.small, k, false)
    s.SwapBuffers()
end sub

' The desktop behind every b0r3d 95 screen: one column of icons down the left (the TV's inputs,
' up to four, then b0r3d's own channels in their order, then Settings), the focused channel's
' label lit (or the open channel's, or Settings'), Rukus by the taskbar, and the taskbar itself.
' The column has to end above Rukus: when it wouldn't, the icons move closer together, then
' b0r3d FM and Commercial Breaks leave the desktop (they're in the guide), then inputs, from the
' last. While a game is being played Rukus stays away: in a game he only comes on while it's
' paused.
sub DrawB95Desktop(st as object, ch as dynamic, task as string)
    s = st.screen
    c = st.kit
    k = st.k
    x = Z(14, k)
    top = Z(22, k)
    colW = Z(148, k)
    isz = Z(56, k)
    sc = Z(6, k)
    bottom = Z(682, k) - 9 * sc - Z(8, k)
    openId = B95OpenId(st)
    inputs = []
    own = []
    extras = 0
    for each item in st.channels
        lit = (item.id = openId)
        if ch <> invalid and st.mode = "guide" then lit = (ch.id = item.id)
        icon = { kind: B95IconFor(st, item), label: item.name, lit: lit, extra: item.kind <> "game" }
        if item.kind = "input" then
            if inputs.Count() < 4 then inputs.Push(icon)
        else if HasPreview(item) then
            own.Push(icon)
            if icon.extra then extras = extras + 1
        end if
    end for
    gear = { kind: "settings", label: "Settings", lit: st.mode <> "guide" and openId = "", extra: false }
    inputsKept = inputs.Count()
    extrasKept = extras
    gap = Z(20, k)
    while true
        icons = B95DesktopIcons(inputs, inputsKept, own, extrasKept, gear)
        if top + B95IconsHeight(st, icons, colW, isz, gap) <= bottom then exit while
        if gap > Z(6, k) then
            gap = Z(6, k)
        else if extrasKept > 0 then
            extrasKept = extrasKept - 1
        else if inputsKept > 0 then
            inputsKept = inputsKept - 1
        else
            exit while
        end if
    end while
    y = top
    for each icon in icons
        y = DrawB95DesktopIcon(st, icon.kind, icon.label, x, y, colW, isz, icon.lit) + gap
    end for
    playing = (st.mode = "stuck" and StuckBusy(st)) or (st.mode = "testpat" and TpBusy(st)) or (st.mode = "snake" and SnakeBusy(st))
    if not playing then DrawSprite(s, RukusLoaf(), RukusColorsFor(st), Z(34, k), Z(682, k) - 9 * sc, sc)
    DrawB95Taskbar(st, task)
end sub

' The channel whose icon is lit while it's open, or "".
function B95OpenId(st as object) as string
    if st.mode = "stuck" then return "game.stuck"
    if st.mode = "testpat" then return "game.test"
    if st.mode = "snake" then return "game.snake"
    if st.mode = "fm" then return "radio.fm"
    if st.mode = "promo" then return "promo.breaks"
    return ""
end function

' The desktop's icons, top to bottom: the first inputsKept inputs, b0r3d's own channels (only the
' first extrasKept of the ones that aren't games), and Settings.
function B95DesktopIcons(inputs as object, inputsKept as integer, own as object, extrasKept as integer, gear as object) as object
    icons = []
    for i = 0 to inputsKept - 1
        icons.Push(inputs[i])
    end for
    seen = 0
    for each icon in own
        if icon.extra then
            seen = seen + 1
            if seen <= extrasKept then icons.Push(icon)
        else
            icons.Push(icon)
        end if
    end for
    icons.Push(gear)
    return icons
end function

' How tall a column of these icons is, gap pixels apart (as DrawB95DesktopIcon draws them).
function B95IconsHeight(st as object, icons as object, colW as integer, isz as integer, gap as integer) as integer
    f = st.fonts
    k = st.k
    lineH = f.small.GetOneLineHeight() + Z(4, k)
    total = 0
    for each icon in icons
        lines = WrapText(B95Clean(icon.label), f.small, colW - Z(12, k)).Count()
        if lines > 2 then lines = 2
        total = total + Int(isz * 26 / 28) + Z(6, k) + lines * lineH + gap
    end for
    return total - gap
end function

' One desktop icon with its label under it (two lines at most). Returns where the next starts.
function DrawB95DesktopIcon(st as object, kind as string, label as string, x as integer, y as integer, colW as integer, isz as integer, lit as boolean) as integer
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    DrawB95Icon(s, kind, x + (colW - isz) \ 2, y, isz, c)
    ly = y + Int(isz * 26 / 28) + Z(6, k)
    maxW = colW - Z(12, k)
    lines = WrapText(B95Clean(label), f.small, maxW)
    lh = f.small.GetOneLineHeight()
    for i = 0 to lines.Count() - 1
        if i > 1 then exit for
        text = lines[i]
        if i = 1 and lines.Count() > 2 then text = text + " " + lines[2]
        text = B95Trim(text, f.small, maxW, k, false)
        tw = B95Width(text, f.small, k, false)
        tx = x + (colW - tw) \ 2
        if lit then s.DrawRect(tx - Z(6, k), ly, tw + Z(12, k), lh + Z(4, k), c.navy)
        B95Text(s, text, tx, ly + Z(2, k), c.white, f.small, k, false)
        ly = ly + lh + Z(4, k)
    end for
    return ly
end function

' The taskbar: Start, the open window's button pressed in, and the tray with the clock.
sub DrawB95Taskbar(st as object, task as string)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    top = Z(680, k)
    s.DrawRect(0, top, st.w, st.h - top, c.face)
    s.DrawRect(0, top, st.w, t, c.white)
    by = top + Z(4, k)
    bh = st.h - Z(4, k) - by
    textY = by + (bh - f.button.GetOneLineHeight()) \ 2
    sx = Z(4, k)
    startW = Z(48, k) + B95Width("Start", f.button, k, true)
    B95Button(s, sx, by, startW, bh, t, c)
    DrawB95Icon(s, "pc", sx + Z(8, k), by + (bh - Z(18, k)) \ 2, Z(22, k), c)
    B95Text(s, "Start", sx + Z(38, k), textY, c.black, f.button, k, true)
    tx = sx + startW + Z(8, k)
    tw = Z(260, k)
    s.DrawRect(tx, by, tw, bh, c.light)
    B95Edge(s, tx, by, tw, bh, t, c.dark, c.white)
    B95Text(s, B95Trim(task, f.body, tw - Z(20, k), k, true), tx + Z(10, k), by + (bh - f.body.GetOneLineHeight()) \ 2, c.black, f.body, k, true)
    clock = ClockText(st, false)
    cw = f.body.GetOneLineWidth(clock, 99999)
    trayW = cw + Z(52, k)
    trayX = st.w - Z(4, k) - trayW
    B95Edge(s, trayX, by, trayW, bh, t, c.shadow, c.white)
    spx = trayX + Z(12, k)
    spy = by + bh \ 2
    s.DrawRect(spx, spy - Z(3, k), Z(4, k), Z(7, k), c.black)
    for i = 0 to Z(5, k)
        s.DrawRect(spx + Z(4, k) + i, spy - Z(3, k) - i, 1, Z(7, k) + 2 * i, c.black)
    end for
    B95Text(s, clock, trayX + Z(40, k), by + (bh - f.body.GetOneLineHeight()) \ 2, c.black, f.body, k, false)
end sub

' A window's title bar: navy, the TV icon, the title, and its buttons (a dialog has only close).
sub DrawB95TitleBar(st as object, x as integer, y as integer, w as integer, title as string, threeButtons as boolean)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    h = Z(38, k)
    s.DrawRect(x, y, w, h, c.navy)
    DrawB95Icon(s, "pc", x + Z(8, k), y + (h - Z(20, k)) \ 2, Z(24, k), c)
    B95Text(s, B95Trim(title, f.title, w - Z(160, k), k, true), x + Z(42, k), y + (h - f.title.GetOneLineHeight()) \ 2, c.white, f.title, k, true)
    bw = Z(30, k)
    bh = Z(26, k)
    by = y + (h - bh) \ 2
    bx = x + w - Z(6, k) - bw
    B95Button(s, bx, by, bw, bh, t, c)
    cx = bx + bw \ 2
    cy = by + bh \ 2
    for i = -4 to 4
        s.DrawRect(cx + Z(i, k) - t \ 2, cy + Z(i, k) - t \ 2, t + 1, t + 1, c.black)
        s.DrawRect(cx - Z(i, k) - t \ 2, cy + Z(i, k) - t \ 2, t + 1, t + 1, c.black)
    end for
    if threeButtons then
        bx = bx - Z(4, k) - bw
        B95Button(s, bx, by, bw, bh, t, c)
        DrawFrame(s, bx + (bw - Z(14, k)) \ 2, by + (bh - Z(11, k)) \ 2, Z(14, k), Z(11, k), t, c.black)
        s.DrawRect(bx + (bw - Z(14, k)) \ 2, by + (bh - Z(11, k)) \ 2, Z(14, k), Z(4, k), c.black)
        bx = bx - bw
        B95Button(s, bx, by, bw, bh, t, c)
        s.DrawRect(bx + (bw - Z(12, k)) \ 2, by + bh - Z(9, k), Z(12, k), Z(3, k), c.black)
    end if
end sub

' The preview box's picture: an "Opening" dialog for an app, a big monitor or TV for an input.
sub DrawB95Preview(st as object, ch as dynamic, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    s.DrawRect(x, y, w, h, c.teal)
    if ch = invalid then
        B95Centered(st, "No channels yet", x, y, w, h, f.body)
        return
    end if
    if HasPreview(ch) then
        DrawGamePreview(st, ch, x, y, w, h)
        return
    end if
    if ch.kind = "input" then
        isz = Z(120, k)
        ih = Int(isz * 26 / 28)
        nameH = f.name.GetOneLineHeight()
        top = y + (h - ih - Z(14, k) - nameH) \ 2
        DrawB95Icon(s, B95IconFor(st, ch), x + (w - isz) \ 2, top, isz, c)
        label = B95Trim(ch.name, f.name, w - Z(24, k), k, true)
        lw = B95Width(label, f.name, k, true)
        B95Text(s, label, x + (w - lw) \ 2, top + ih + Z(14, k), c.white, f.name, k, true)
        return
    end if
    dw = Z(360, k)
    dh = Z(132, k)
    dx = x + (w - dw) \ 2
    dy = y + (h - dh) \ 2
    B95Raised(s, dx, dy, dw, dh, t, c)
    titleH = Z(30, k)
    s.DrawRect(dx + Z(4, k), dy + Z(4, k), dw - Z(8, k), titleH, c.navy)
    B95Text(s, B95Trim("Opening " + ch.name, f.small, dw - Z(28, k), k, true), dx + Z(12, k), dy + Z(4, k) + (titleH - f.small.GetOneLineHeight()) \ 2, c.white, f.small, k, true)
    B95Text(s, B95Trim("Starting " + ch.name + " on the TV...", f.body, dw - Z(32, k), k, false), dx + Z(16, k), dy + Z(52, k), c.black, f.body, k, false)
    DrawB95Progress(st, dx + Z(16, k), dy + Z(92, k), dw - Z(32, k), Z(22, k), 6)
end sub

' A scrollbar: an arrow button at each end and a thumb that follows the highlight.
sub DrawB95Scrollbar(st as object, x as integer, y as integer, w as integer, h as integer, focus as integer, count as integer)
    s = st.screen
    c = st.kit
    k = st.k
    t = Z(2, k)
    s.DrawRect(x, y, w, h, c.light)
    ah = Z(44, k)
    B95Button(s, x, y, w, ah, t, c)
    DrawB95Arrow(s, x + w \ 2, y + ah \ 2, Z(5, k), true, c.black)
    B95Button(s, x, y + h - ah, w, ah, t, c)
    DrawB95Arrow(s, x + w \ 2, y + h - ah \ 2, Z(5, k), false, c.black)
    thumbH = Z(60, k)
    travel = h - 2 * ah - thumbH
    tpos = 0
    if count > 1 and focus > 0 then tpos = Int(travel * focus / (count - 1))
    B95Button(s, x, y + ah + tpos, w, thumbH, t, c)
end sub

' A sunken progress bar with this many navy blocks in it.
sub DrawB95Progress(st as object, x as integer, y as integer, w as integer, h as integer, blocks as integer)
    s = st.screen
    c = st.kit
    k = st.k
    B95Edge(s, x, y, w, h, Z(2, k), c.shadow, c.white)
    bw = Z(18, k)
    gap = Z(3, k)
    bx = x + Z(4, k)
    for i = 1 to blocks
        if bx + bw > x + w - Z(4, k) then exit for
        s.DrawRect(bx, y + Z(4, k), bw, h - Z(8, k), c.navy)
        bx = bx + bw + gap
    end for
end sub

' An etched group frame with its label sitting on the top line.
sub DrawB95Group(st as object, x as integer, y as integer, w as integer, h as integer, label as string)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    DrawFrame(s, x + t, y + t, w - t, h - t, t, c.white)
    DrawFrame(s, x, y, w - t, h - t, t, c.shadow)
    lw = B95Width(label, f.body, k, false)
    lh = f.body.GetOneLineHeight()
    lx = x + Z(12, k)
    s.DrawRect(lx, y - lh \ 2, lw + Z(12, k), lh, c.face)
    B95Text(s, label, lx + Z(6, k), y - lh \ 2, c.black, f.body, k, false)
end sub

' Settings and its pages as a dialog over the desktop.
sub DrawB95Panel(st as object, spec as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    s.Clear(c.bg)
    DrawB95Desktop(st, invalid, spec.title)
    dx = Z(200, k)
    dy = Z(30, k)
    dw = Z(1080, k) - dx
    dh = Z(660, k) - dy
    B95Raised(s, dx, dy, dw, dh, t, c)
    DrawB95TitleBar(st, dx + Z(4, k), dy + Z(4, k), dw - Z(8, k), spec.title, false)
    bx = dx + Z(16, k)
    by = dy + Z(58, k)
    bw = dw - Z(32, k)
    statusY = dy + dh - Z(42, k)
    listH = statusY - Z(12, k) - by
    B95Sunken(s, bx, by, bw, listH, t, c, c.white)
    if spec.info then
        DrawB95InfoRows(st, spec.rows, bx + Z(16, k), bx + Z(260, k), bx + bw - Z(16, k), by + Z(12, k), by + listH - Z(8, k))
    else
        rowStep = Z(38, k)
        visible = (listH - Z(12, k)) \ rowStep
        count = spec.rows.Count()
        first = ListTop(spec.sel, count, visible)
        rx = bx + Z(6, k)
        rw = bw - Z(12, k)
        if count > visible then
            ' More rows than fit: a scrollbar, as in the guide's list.
            sbw = Z(26, k)
            rw = bw - Z(10, k) - sbw
            DrawB95Scrollbar(st, bx + bw - Z(4, k) - sbw, by + Z(4, k), sbw, listH - Z(8, k), spec.sel, count)
        end if
        for r = 0 to visible - 1
            i = first + r
            if i >= count then exit for
            row = spec.rows[i]
            ry = by + Z(6, k) + r * rowStep
            fg = c.black
            if IsDim(row) then fg = c.shadow
            focused = (i = spec.sel)
            if focused then
                s.DrawRect(rx, ry, rw, rowStep, c.navy)
                fg = c.white
            end if
            ty = ry + (rowStep - f.pageRow.GetOneLineHeight()) \ 2
            B95Text(s, B95Trim(row.label, f.pageRow, rw \ 2, k, false), rx + Z(12, k), ty, fg, f.pageRow, k, false)
            DrawB95Value(st, row, focused, rx + rw - Z(12, k), ry, rowStep, fg, rw \ 2 - Z(24, k))
        end for
    end if
    statusH = Z(30, k)
    B95Edge(s, dx + Z(8, k), statusY, dw - Z(16, k), statusH, t, c.shadow, c.white)
    B95Text(s, B95Trim(spec.hint, f.small, dw - Z(40, k), k, false), dx + Z(16, k), statusY + (statusH - f.small.GetOneLineHeight()) \ 2, c.black, f.small, k, false)
    s.SwapBuffers()
end sub

' A setting's value, right-aligned at rx, with arrows on a highlighted choice (one arrow on a row
' that opens a page).
sub DrawB95Value(st as object, row as object, focused as boolean, rx as integer, ry as integer, rh as integer, color as dynamic, maxW as integer)
    s = st.screen
    f = st.fonts
    k = st.k
    font = f.pageChoice
    th = Z(14, k)
    tw = Z(8, k)
    gap = Z(10, k)
    arrowY = ry + (rh - th) \ 2
    x = rx
    if focused and (row.kind = "choice" or row.kind = "open") then
        x = x - tw
        DrawArrowShape(s, x, arrowY, tw, th, true, color)
        x = x - gap
    end if
    value = B95Trim(row.value, font, maxW, k, false)
    x = x - B95Width(value, font, k, false)
    B95Text(s, value, x, ry + (rh - font.GetOneLineHeight()) \ 2, color, font, k, false)
    if focused and row.kind = "choice" then
        DrawArrowShape(s, x - gap - tw, arrowY, tw, th, false, color)
    end if
end sub

' A page of answers: each label in bold with its value wrapped beside it.
sub DrawB95InfoRows(st as object, rows as object, x as integer, valueX as integer, right as integer, top as integer, bottom as integer)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    stepY = f.pageInfo.GetOneLineHeight() + Z(5, k)
    valueW = right - valueX
    y = top
    for each entry in rows
        if y + stepY <= bottom then B95Text(s, B95Trim(entry.label, f.pageLabel, valueX - x - Z(12, k), k, true), x, y, c.black, f.pageLabel, k, true)
        for each part in WrapText(B95Clean(entry.value), f.pageInfo, valueW)
            if y + stepY <= bottom then B95Text(s, B95Trim(part, f.pageInfo, valueW, k, false), valueX, y, c.black, f.pageInfo, k, false)
            y = y + stepY
        end for
        y = y + Z(3, k)
    end for
end sub

' The tuning banner: a small dialog over the static, its progress bar filling as it tunes.
sub DrawB95Banner(st as object, ch as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    t = Z(2, k)
    dw = Z(440, k)
    dh = Z(150, k)
    dx = (st.w - dw) \ 2
    dy = (st.h - dh) \ 2
    B95Raised(s, dx, dy, dw, dh, t, c)
    titleH = Z(30, k)
    s.DrawRect(dx + Z(4, k), dy + Z(4, k), dw - Z(8, k), titleH, c.navy)
    B95Text(s, "b0r3d TV", dx + Z(12, k), dy + Z(4, k) + (titleH - f.small.GetOneLineHeight()) \ 2, c.white, f.small, k, true)
    B95Text(s, B95Trim("Tuning in " + ch.n + "  " + ch.name + "...", f.body, dw - Z(32, k), k, false), dx + Z(16, k), dy + Z(54, k), c.black, f.body, k, false)
    blocks = 1
    if st.DoesExist("tuneTimer") then
        if st.tuneTimer <> invalid then blocks = 1 + st.tuneTimer.TotalMilliseconds() \ 30
    end if
    DrawB95Progress(st, dx + Z(16, k), dy + Z(96, k), dw - Z(32, k), Z(24, k), blocks)
end sub

' Which picture goes with a channel: a monitor for the PC's input, a TV for other inputs, a
' stack of pixels for Stuck Pixels, colour bars for Test Pattern, a window for apps.
function B95IconFor(st as object, ch as object) as string
    if ch.id = "game.test" then return "test"
    if ch.id = "game.snake" then return "snake"
    if ch.id = "radio.fm" then return "radio"
    if ch.kind = "game" then return "stuck"
    if ch.kind <> "input" then return "app"
    if st.tvf <> invalid then
        if ch.id = st.tvf.pc then return "pc"
    end if
    return "tv"
end function

' A small picture on a 28 x 26 grid, size pixels wide: "pc" (a monitor), "tv" (a TV with rabbit
' ears), "settings" (a panel of sliders), "stuck" (a stack of pixels with a stuck yellow one),
' "test" (colour bars on a little stand) or "app" (a window with a play button).
sub DrawB95Icon(s as object, kind as string, x as integer, y as integer, size as integer, c as object)
    u = size / 28
    if kind = "test" then
        B95Grid(s, x, y, u, 1, 1, 26, 15, c.black)
        bars = [&hC0C0C0FF, &hC0C000FF, &h00C0C0FF, &h00C000FF, &hC000C0FF, &hC00000FF]
        for i = 0 to 5
            B95Grid(s, x, y, u, 2 + i * 4, 2, 4, 13, bars[i])
        end for
        B95Grid(s, x, y, u, 12, 16, 4, 4, c.shadow)
        B95GridBox(s, x, y, u, 7, 20, 14, 4, c.face, c.black)
    else if kind = "radio" then
        B95Grid(s, x, y, u, 2, 6, 24, 16, c.shadow)
        B95Grid(s, x, y, u, 3, 7, 22, 14, c.dark)
        B95Grid(s, x, y, u, 6, 2, 16, 4, c.shadow)
        B95Grid(s, x, y, u, 8, 4, 12, 3, c.face)
        B95Grid(s, x, y, u, 5, 9, 6, 6, c.black)
        B95Grid(s, x, y, u, 17, 9, 6, 6, c.black)
        B95Grid(s, x, y, u, 12, 10, 4, 5, c.navy)
    else if kind = "snake" then
        B95GridBox(s, x, y, u, 2, 2, 24, 22, c.black, c.shadow)
        B95Grid(s, x, y, u, 19, 5, 4, 4, &hFF2244FF)
        B95Grid(s, x, y, u, 5, 17, 14, 4, &h00C000FF)
        B95Grid(s, x, y, u, 15, 10, 4, 11, &h00C000FF)
        B95Grid(s, x, y, u, 8, 10, 11, 4, &h00C000FF)
        B95Grid(s, x, y, u, 8, 5, 4, 8, &h39FF88FF)
        B95Grid(s, x, y, u, 10, 6, 2, 2, c.black)
    else if kind = "stuck" then
        B95GridBox(s, x, y, u, 10, 2, 6, 6, c.teal, c.black)
        B95GridBox(s, x, y, u, 10, 8, 6, 6, c.teal, c.black)
        B95GridBox(s, x, y, u, 4, 14, 6, 6, c.navy, c.black)
        B95GridBox(s, x, y, u, 10, 14, 6, 6, c.navy, c.black)
        B95GridBox(s, x, y, u, 16, 14, 6, 6, &hFFFF00FF, c.black)
        B95GridBox(s, x, y, u, 4, 20, 18, 4, c.navy, c.black)
    else if kind = "pc" then
        B95GridBox(s, x, y, u, 2, 2, 24, 16, c.face, c.black)
        B95Grid(s, x, y, u, 5, 5, 18, 10, c.teal)
        B95Grid(s, x, y, u, 10, 19, 8, 3, c.shadow)
        B95GridBox(s, x, y, u, 6, 22, 16, 3, c.face, c.black)
    else if kind = "tv" then
        for i = 0 to 6
            B95Grid(s, x, y, u, 8 + i, 2 + i, 1.5, 1.5, c.black)
            B95Grid(s, x, y, u, 19 - i, 2 + i, 1.5, 1.5, c.black)
        end for
        B95GridBox(s, x, y, u, 3, 10, 22, 14, c.face, c.black)
        B95Grid(s, x, y, u, 6, 13, 12, 8, c.navy)
        B95Grid(s, x, y, u, 20, 13, 3, 3, c.red)
    else if kind = "settings" then
        B95GridBox(s, x, y, u, 3, 3, 22, 20, c.face, c.black)
        for i = 0 to 2
            B95Grid(s, x, y, u, 6, 8 + i * 5, 16, 1, c.shadow)
            B95Grid(s, x, y, u, 8 + i * 5, 6 + i * 5, 4, 5, c.navy)
        end for
    else
        B95GridBox(s, x, y, u, 2, 3, 24, 20, c.white, c.black)
        B95Grid(s, x, y, u, 2, 3, 24, 5, c.navy)
        for i = 0 to 6
            B95Grid(s, x, y, u, 11 + i, 12 + i / 2, 1, 8 - i, c.teal)
        end for
    end if
end sub

' A rectangle on an icon's grid (u pixels per grid unit).
sub B95Grid(s as object, x as integer, y as integer, u as dynamic, gx as dynamic, gy as dynamic, gw as dynamic, gh as dynamic, color as dynamic)
    px = Int(gx * u + 0.5)
    py = Int(gy * u + 0.5)
    pw = Int((gx + gw) * u + 0.5) - px
    ph = Int((gy + gh) * u + 0.5) - py
    if pw < 1 then pw = 1
    if ph < 1 then ph = 1
    s.DrawRect(x + px, y + py, pw, ph, color)
end sub

' A filled grid rectangle with a one-unit outline.
sub B95GridBox(s as object, x as integer, y as integer, u as dynamic, gx as dynamic, gy as dynamic, gw as dynamic, gh as dynamic, fill as dynamic, outline as dynamic)
    B95Grid(s, x, y, u, gx, gy, gw, gh, outline)
    B95Grid(s, x, y, u, gx + 1, gy + 1, gw - 2, gh - 2, fill)
end sub

' A raised window edge: white then light on the top and left, black then grey on the bottom and
' right, t pixels per line.
sub B95Raised(s as object, x as integer, y as integer, w as integer, h as integer, t as integer, c as object)
    s.DrawRect(x, y, w, h, c.face)
    B95Edge(s, x, y, w, h, t, c.white, c.dark)
    B95Edge(s, x + t, y + t, w - 2 * t, h - 2 * t, t, c.light, c.shadow)
end sub

' A raised button: white on the top and left, black then grey on the bottom and right.
sub B95Button(s as object, x as integer, y as integer, w as integer, h as integer, t as integer, c as object)
    s.DrawRect(x, y, w, h, c.face)
    B95Edge(s, x, y, w, h, t, c.white, c.dark)
    s.DrawRect(x + t, y + h - 2 * t, w - 2 * t, t, c.shadow)
    s.DrawRect(x + w - 2 * t, y + t, t, h - 2 * t, c.shadow)
end sub

' A sunken field: grey then black on the top and left, white then light on the bottom and right.
sub B95Sunken(s as object, x as integer, y as integer, w as integer, h as integer, t as integer, c as object, fill as dynamic)
    if fill <> invalid then s.DrawRect(x, y, w, h, fill)
    B95Edge(s, x, y, w, h, t, c.shadow, c.white)
    B95Edge(s, x + t, y + t, w - 2 * t, h - 2 * t, t, c.dark, c.light)
end sub

' One line around a box: tl on the top and left, br on the bottom and right.
sub B95Edge(s as object, x as integer, y as integer, w as integer, h as integer, t as integer, tl as dynamic, br as dynamic)
    s.DrawRect(x, y, w, t, tl)
    s.DrawRect(x, y, t, h, tl)
    s.DrawRect(x, y + h - t, w, t, br)
    s.DrawRect(x + w - t, y, t, h, br)
end sub

' A small arrow (up or down) for the scrollbar's buttons, centred on cx, cy.
sub DrawB95Arrow(s as object, cx as integer, cy as integer, size as integer, up as boolean, color as dynamic)
    for i = 0 to size - 1
        w = 2 * i + 1
        if up then
            s.DrawRect(cx - i, cy - size \ 2 + i, w, 1, color)
        else
            s.DrawRect(cx - i, cy + size \ 2 - i, w, 1, color)
        end if
    end for
end sub

' A menu word with its first letter underlined.
sub DrawB95Underlined(st as object, word as string, x as integer, y as integer, font as object)
    s = st.screen
    c = st.kit
    k = st.k
    B95Text(s, word, x, y, c.black, font, k, false)
    lineY = y + font.GetAscent() + Z(2, k)
    s.DrawRect(x, lineY, font.GetOneLineWidth(Left(word, 1), 99999), Z(1.5, k), c.black)
end sub

sub B95Centered(st as object, text as string, x as integer, y as integer, w as integer, h as integer, font as object)
    tw = B95Width(text, font, st.k, false)
    B95Text(st.screen, text, x + (w - tw) \ 2, y + (h - font.GetOneLineHeight()) \ 2, st.kit.black, font, st.k, false)
end sub

' W95FA lacks some punctuation: the ellipsis becomes three dots and dashes a hyphen.
function B95Clean(text as string) as string
    return text.Replace("…", "...").Replace("–", "-").Replace("—", "-")
end function

' Text in W95FA, with each middle dot drawn as a small square and bold drawn twice.
sub B95Text(s as object, text as string, x as integer, y as integer, color as dynamic, font as object, k as dynamic, bold as boolean)
    rest = B95Clean(text)
    cx = x
    dot = Z(3, k)
    slot = dot + Z(4, k)
    while true
        p = Instr(1, rest, "·")
        part = rest
        if p > 0 then part = Left(rest, p - 1)
        s.DrawText(part, cx, y, color, font)
        if bold then s.DrawText(part, cx + 1, y, color, font)
        cx = cx + font.GetOneLineWidth(part, 99999)
        if bold then cx = cx + 1
        if p = 0 then exit while
        s.DrawRect(cx + (slot - dot) \ 2, y + Int(font.GetAscent() * 0.6) - dot \ 2, dot, dot, color)
        cx = cx + slot
        rest = Mid(rest, p + 1)
    end while
end sub

function B95Width(text as string, font as object, k as dynamic, bold as boolean) as integer
    rest = B95Clean(text)
    slot = Z(3, k) + Z(4, k)
    w = 0
    while true
        p = Instr(1, rest, "·")
        part = rest
        if p > 0 then part = Left(rest, p - 1)
        w = w + font.GetOneLineWidth(part, 99999)
        if bold then w = w + 1
        if p = 0 then exit while
        w = w + slot
        rest = Mid(rest, p + 1)
    end while
    return w
end function

' Cuts text that doesn't fit and ends it with "..." (W95FA has no ellipsis).
function B95Trim(text as string, font as object, maxW as integer, k as dynamic, bold as boolean) as string
    t = B95Clean(text)
    if B95Width(t, font, k, bold) <= maxW then return t
    while Len(t) > 1
        t = Left(t, Len(t) - 1)
        if B95Width(t + "...", font, k, bold) <= maxW then return t + "..."
    end while
    return t
end function
