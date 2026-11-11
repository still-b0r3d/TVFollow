' The screen layout shared by Settings, the channel list, the TV check and About, painted in
' the current look. A spec says what to show:
'   title   the screen's name
'   rows    { label, value, kind, dim }: kind "choice" (Left/Right change it), "open" (OK opens
'           it) or "toggle"; dim draws the row fainter
'   sel     the highlighted row
'   info    true for a page of answers (labels and wrapped values, nothing to highlight)
'   hint    the line of remote hints at the bottom

sub DrawPanel(st as object, spec as object)
    if st.look = "b95" then
        DrawB95Panel(st, spec)
    else if st.look = "phosphor" then
        DrawPhosphorPanel(st, spec)
    else if st.look = "vcr" then
        DrawVcrPanel(st, spec)
    else if st.look = "rgb" then
        DrawRgbPanel(st, spec)
    else
        DrawCablePanel(st, spec)
    end if
end sub

' The first row to show so the highlight stays on screen.
function ListTop(sel as integer, count as integer, visible as integer) as integer
    first = sel - 3
    if first > count - visible then first = count - visible
    if first < 0 then first = 0
    return first
end function

' A page of answers: each label with its value wrapped beside it, down to the bottom.
sub DrawInfoRows(st as object, rows as object, x as integer, valueX as integer, right as integer, top as integer, bottom as integer, labelColor as dynamic, valueColor as dynamic, shadow as dynamic)
    s = st.screen
    f = st.fonts
    k = st.k
    sh = 0
    if shadow <> invalid then sh = Z(2, k)
    stepY = f.pageInfo.GetOneLineHeight() + Z(5, k)
    drop = (stepY - f.pageLabel.GetOneLineHeight()) \ 2
    valueW = right - valueX
    y = top
    for each line in rows
        if y + stepY <= bottom then
            if sh > 0 then
                DrawShadowed(s, line.label, x, y + drop, labelColor, shadow, f.pageLabel, sh)
            else
                s.DrawText(line.label, x, y + drop, labelColor, f.pageLabel)
            end if
        end if
        for each part in WrapText(line.value, f.pageInfo, valueW)
            if y + stepY <= bottom then
                part = TrimToWidth(part, f.pageInfo, valueW)
                if sh > 0 then
                    DrawShadowed(s, part, valueX, y, valueColor, shadow, f.pageInfo, sh)
                else
                    s.DrawText(part, valueX, y, valueColor, f.pageInfo)
                end if
            end if
            y = y + stepY
        end for
        y = y + Z(3, k)
    end for
end sub

sub DrawCablePanel(st as object, spec as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    s.Clear(c.bg)
    left = Z(40, k)
    right = Z(1240, k)
    bandY = Z(24, k)
    bandH = Z(80, k) - bandY
    s.DrawRect(left, bandY, right - left, bandH, c.frame)
    DrawMiddleSpaced(s, UCase(spec.title), left + Z(16, k), bandY, bandH, c.ink, f.pageTitle, Z(2, k))
    DrawRight(s, st.version, right - Z(16, k), bandY + (bandH - f.barNum.GetOneLineHeight()) \ 2, c.ink, f.barNum)
    if spec.info then
        DrawInfoRows(st, spec.rows, left + Z(16, k), Z(330, k), right - Z(16, k), Z(100, k), Z(656, k), c.soft, c.white, invalid)
    else
        visible = 11
        count = spec.rows.Count()
        first = ListTop(spec.sel, count, visible)
        for r = 0 to visible - 1
            i = first + r
            if i >= count then exit for
            row = spec.rows[i]
            ry = Z(100 + r * 50, k)
            rh = Z(146 + r * 50, k) - ry
            bg = c.cell3
            fgL = c.soft
            fgV = c.white
            if IsDim(row) then
                fgL = c.dimText
                fgV = c.dimText
            end if
            if i = spec.sel then
                bg = c.accent
                fgL = c.ink
                fgV = c.ink
            end if
            s.DrawRect(left, ry, right - left, rh, bg)
            DrawMiddle(s, row.label, left + Z(18, k), ry, rh, fgL, f.pageRow)
            DrawRowValue(st, row, i = spec.sel, right - Z(18, k), ry, rh, fgV, invalid)
        end for
    end if
    top = Z(668, k)
    s.DrawRect(0, top, st.w, st.h - top, c.bar)
    DrawMiddle(s, spec.hint, left, top, st.h - top, c.soft, f.bar)
    s.SwapBuffers()
end sub

sub DrawPhosphorPanel(st as object, spec as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    s.Clear(c.bg)
    left = Z(48, k)
    right = Z(1232, k)
    DrawPhosphorHeader(st, "B0R3D TV // " + UCase(spec.title), false)
    if spec.info then
        DrawInfoRows(st, spec.rows, left, Z(330, k), right, Z(104, k), Z(636, k), c.dim, c.full, invalid)
    else
        visible = 12
        count = spec.rows.Count()
        first = ListTop(spec.sel, count, visible)
        for r = 0 to visible - 1
            i = first + r
            if i >= count then exit for
            row = spec.rows[i]
            ry = Z(108 + r * 42, k)
            rh = Z(146 + r * 42, k) - ry
            fg = c.full
            lead = c.lead
            if IsDim(row) then fg = c.lead
            if i = spec.sel then
                s.DrawRect(left, ry, right - left, rh, c.full)
                fg = c.bg
                lead = c.bg
            end if
            ty = ry + (rh - f.pageRow.GetOneLineHeight()) \ 2
            if i = spec.sel then s.DrawText(">", left + Z(12, k), ty, fg, f.pageRow)
            label = UCase(row.label)
            s.DrawText(label, left + Z(40, k), ty, fg, f.pageRow)
            vx = DrawRowValue(st, row, i = spec.sel, right - Z(12, k), ry, rh, fg, invalid)
            DrawDots(s, left + Z(40, k) + f.pageRow.GetOneLineWidth(label, 99999) + Z(12, k), vx - Z(12, k), ry + rh \ 2 + Z(5, k), Z(2, k), Z(5, k), lead)
        end for
    end if
    s.DrawRect(left, Z(646, k), right - left, Z(2, k), c.full)
    DrawMiddle(s, UCase(spec.hint), left, Z(650, k), Z(698, k) - Z(650, k), c.dim, f.bar)
    if st.scan then DrawScanlines(st)
    s.SwapBuffers()
end sub

sub DrawVcrPanel(st as object, spec as object)
    s = st.screen
    f = st.fonts
    c = st.kit
    k = st.k
    sh = Z(3, k)
    s.Clear(c.bg)
    DrawVcrTitle(st, UCase(spec.title))
    x = Z(56, k)
    right = Z(1224, k)
    if spec.info then
        DrawInfoRows(st, spec.rows, x, Z(360, k), right, Z(132, k), Z(636, k), c.yellow, c.white, c.shadow)
    else
        visible = 12
        count = spec.rows.Count()
        first = ListTop(spec.sel, count, visible)
        for r = 0 to visible - 1
            i = first + r
            if i >= count then exit for
            row = spec.rows[i]
            ry = Z(132 + r * 41, k)
            rh = Z(170 + r * 41, k) - ry
            fg = c.white
            if IsDim(row) then fg = c.grey
            if i = spec.sel then
                fg = c.yellow
                tw = Z(20, k)
                th = Z(22, k)
                DrawTriangleRight(s, x + sh, ry + (rh - th) \ 2 + sh, tw, th, c.shadow)
                DrawTriangleRight(s, x, ry + (rh - th) \ 2, tw, th, c.yellow)
            end if
            ty = ry + (rh - f.pageRow.GetOneLineHeight()) \ 2
            DrawShadowed(s, UCase(row.label), x + Z(36, k), ty, fg, c.shadow, f.pageRow, sh)
            DrawRowValue(st, row, i = spec.sel, right, ry, rh, fg, c.shadow)
        end for
    end if
    ty = Z(650, k) + (Z(48, k) - f.bar.GetOneLineHeight()) \ 2
    DrawShadowed(s, UCase(spec.hint), x, ty, c.white, c.shadow, f.bar, sh)
    s.SwapBuffers()
end sub

' A row's value, right-aligned at rx. A highlighted choice gets arrows on both sides; a row that
' opens a page gets one arrow. Returns where the value starts (with its arrows).
function DrawRowValue(st as object, row as object, focused as boolean, rx as integer, ry as integer, rh as integer, color as dynamic, shadow as dynamic) as integer
    s = st.screen
    f = st.fonts
    k = st.k
    font = f.pageChoice
    value = row.value
    if st.look <> "cable" then value = UCase(value)
    th = Z(14, k)
    tw = Z(10, k)
    gap = Z(12, k)
    mid = ry + (rh - th) \ 2
    x = rx
    if focused and (row.kind = "choice" or row.kind = "open") then
        x = x - tw
        DrawArrow(s, x, mid, tw, th, true, color, shadow, k)
        x = x - gap
    end if
    vw = font.GetOneLineWidth(value, 99999)
    x = x - vw
    ty = ry + (rh - font.GetOneLineHeight()) \ 2
    if shadow <> invalid then
        DrawShadowed(s, value, x, ty, color, shadow, font, Z(3, k))
    else
        s.DrawText(value, x, ty, color, font)
    end if
    if focused and row.kind = "choice" then
        x = x - gap - tw
        DrawArrow(s, x, mid, tw, th, false, color, shadow, k)
    end if
    return x
end function

' Whether a row asks to be drawn fainter (a hidden channel).
function IsDim(row as object) as boolean
    if not row.DoesExist("dim") then return false
    return row.dim
end function

' A small solid arrow pointing right (or left), with a hard shadow when shadow is a colour.
sub DrawArrow(s as object, x as integer, y as integer, w as integer, h as integer, pointsRight as boolean, color as dynamic, shadow as dynamic, k as dynamic)
    if shadow <> invalid then DrawArrowShape(s, x + Z(3, k), y + Z(3, k), w, h, pointsRight, shadow)
    DrawArrowShape(s, x, y, w, h, pointsRight, color)
end sub

sub DrawArrowShape(s as object, x as integer, y as integer, w as integer, h as integer, pointsRight as boolean, color as dynamic)
    for col = 0 to w - 1
        tall = Int(h * (w - col) / w + 0.5)
        cx = x + col
        if not pointsRight then cx = x + w - 1 - col
        if tall > 0 then s.DrawRect(cx, y + (h - tall) \ 2, 1, tall, color)
    end for
end sub
