' Commercial Breaks on channel 24: late-night station ads for b0r3d's own games and a public
' service announcement, one card every 8 seconds, in every look. Left and right skip, OK or
' Play pauses, Back goes to the guide. Each ad says only what its game really has.

function PromoSpots() as object
    return [
        {
            tag: "NOW PLAYING ON b0r3d.org",
            title: "CHAINSAW SQUIRREL",
            sub: "ONE SQUIRREL. ONE CHAINSAW. A WHOLE FOREST.",
            bullet1: "Fell the trees realm after realm, and take on each realm's boss.",
            bullet2: "Saws, bombs and upgrades to collect along the way.",
            cta: "PLAY IT FREE ON b0r3d.org",
            accent: &hFF3B5CFF
        },
        {
            tag: "NEW ON YOUR DESKTOP",
            title: "b0r3d 95",
            sub: "A DESKTOP FROM 1995, RIGHT IN YOUR BROWSER.",
            bullet1: "Pinball tables, solitaire, a paint program and a calculator.",
            bullet2: "A dial-up web browser, screensavers and online high scores.",
            cta: "START IT UP AT b0r3d.org/95",
            accent: &h39FF88FF
        },
        {
            tag: "TONIGHT'S CREATURE FEATURE",
            title: "z0mbie crystal",
            sub: "A CITY FULL OF THE DEAD, ONE BLOCK AT A TIME.",
            bullet1: "A roguelike run across the city, with Ruckus in it somewhere.",
            bullet2: "Every run is a new map, and a run can be saved and picked up later.",
            cta: "PLAY IT ON b0r3d.org",
            accent: &h00E5FFFF
        },
        {
            tag: "A PUBLIC SERVICE ANNOUNCEMENT",
            title: "DON'T TOUCH THAT DIAL",
            sub: "b0r3d TV'S OWN CHANNELS: 3, 9 AND 20 TO 24.",
            bullet1: "A VCR, a cat cam, three games, a radio station and these commercials.",
            bullet2: "Leave the remote alone for a while and Ruckus comes by.",
            cta: "THIS HAS BEEN A TEST. STAY TUNED.",
            accent: &hFFD700FF
        },
        {
            tag: "ALSO PLAYING ON b0r3d.org",
            title: "RUCKUS: LOCKED & LOAFED",
            sub: "ONE CAT AND HIS DUAL GATLING RIG VS. THE ROBO-VACS.",
            bullet1: "Hold off the horde for ten minutes a stage, then the boss.",
            bullet2: "Three stages, each with its own online high scores.",
            cta: "PLAY IT ON b0r3d.org",
            accent: &hFF7700FF
        }
    ]
end function

function NewPromoState() as object
    return {
        idx: 0,
        paused: false,
        timer: CreateObject("roTimespan"),
        frame: CreateObject("roTimespan"),
        clockTimer: CreateObject("roTimespan"),
        slideDuration: 8000,
        spots: PromoSpots()
    }
end function

sub OpenPromo(st as object)
    if st.promo = invalid then st.promo = NewPromoState()
    st.promo.timer.Mark()
    st.promo.frame.Mark()
    st.promo.clockTimer.Mark()
    st.mode = "promo"
    Redraw(st)
end sub

sub ClosePromo(st as object)
    st.mode = "guide"
    Redraw(st)
end sub

function PromoFonts(st as object) as object
    key = st.look + "|" + Str(st.k)
    if st.promoFonts = invalid or st.promoFontsKey <> key then
        st.promoFonts = BuildPromoFonts(st)
        st.promoFontsKey = key
    end if
    return st.promoFonts
end function

function BuildPromoFonts(st as object) as object
    k = st.k
    if st.look = "phosphor" then
        return {
            header: Vt323(st, Z(36, k)),
            title: Vt323(st, Z(44, k)),
            sub: Vt323(st, Z(28, k)),
            badge: Vt323(st, Z(26, k)),
            bullet: Vt323(st, Z(26, k)),
            cta: Vt323(st, Z(28, k)),
            bar: Vt323(st, Z(24, k)),
            preview: Vt323(st, Z(26, k))
        }
    end if
    if st.look = "vcr" then
        return {
            header: Silk(st, Z(24, k), true),
            title: Silk(st, Z(30, k), true),
            sub: Silk(st, Z(20, k), false),
            badge: Silk(st, Z(18, k), true),
            bullet: Silk(st, Z(18, k), false),
            cta: Silk(st, Z(18, k), true),
            bar: Silk(st, Z(18, k), false),
            preview: Silk(st, Z(18, k), true)
        }
    end if
    if st.look = "b95" then
        return {
            header: W95(st, Z(22, k)),
            title: W95(st, Z(26, k)),
            sub: W95(st, Z(20, k)),
            badge: W95(st, Z(18, k)),
            bullet: W95(st, Z(19, k)),
            cta: W95(st, Z(19, k)),
            bar: W95(st, Z(18, k)),
            preview: W95(st, Z(18, k))
        }
    end if
    if st.look = "rgb" then
        return {
            header: Barlow(st, "bold", Z(24, k)),
            title: Barlow(st, "extrabold", Z(36, k)),
            sub: Barlow(st, "semibold", Z(22, k)),
            badge: Barlow(st, "bold", Z(18, k)),
            bullet: Barlow(st, "medium", Z(22, k)),
            cta: Barlow(st, "bold", Z(20, k)),
            bar: Barlow(st, "semibold", Z(18, k)),
            preview: Barlow(st, "bold", Z(20, k))
        }
    end if
    return {
        header: Barlow(st, "extrabold", Z(30, k)),
        title: Barlow(st, "extrabold", Z(36, k)),
        sub: Barlow(st, "semibold", Z(22, k)),
        badge: Barlow(st, "bold", Z(20, k)),
        bullet: Barlow(st, "medium", Z(22, k)),
        cta: Barlow(st, "bold", Z(21, k)),
        bar: Barlow(st, "bold", Z(20, k)),
        preview: Barlow(st, "bold", Z(20, k))
    }
end function

function PromoWaitMs(st as object) as integer
    if st.promo = invalid then return 500
    if st.promo.paused then return 250
    return 66
end function

' Main's loop calls this on every turn on channel 24: once a second the clock and the chores
' (the TV kept awake while the ads play; paused, Ruckus may come on as in a paused game), the
' next card when one's time is up, and a frame about 15 times a second (once a second while
' paused, for the clock).
sub PromoTurn(st as object)
    p = st.promo
    if p = invalid then return
    if p.clockTimer.TotalMilliseconds() >= 1000 then
        p.clockTimer.Mark()
        st.clock = ClockNow()
        if MediaChores(st, not p.paused) then return
    end if

    advanced = false
    if not p.paused and p.timer.TotalMilliseconds() >= p.slideDuration then
        p.idx = (p.idx + 1) mod p.spots.Count()
        p.timer.Mark()
        advanced = true
    end if

    due = 66
    if p.paused then due = 1000
    if advanced or p.frame.TotalMilliseconds() >= due then
        p.frame.Mark()
        Redraw(st)
    end if
end sub

sub PromoKey(st as object, code as integer)
    p = st.promo
    if p = invalid then return
    cnt = p.spots.Count()
    if code = 0 then
        ClosePromo(st)
    else if code = 4 or code = 8 then
        ' the card before
        p.idx = (p.idx - 1 + cnt) mod cnt
        p.timer.Mark()
        Redraw(st)
    else if code = 5 or code = 9 then
        ' the next card
        p.idx = (p.idx + 1) mod cnt
        p.timer.Mark()
        Redraw(st)
    else if code = 6 or code = 13 then
        ' pause, or carry on with a fresh 8 s for the card
        p.paused = not p.paused
        if not p.paused then p.timer.Mark()
        Redraw(st)
    end if
end sub

' Text cut to fit, in the look's way (W95FA's own on b0r3d 95).
function PromoTrim(st as object, text as string, font as object, maxW as integer, bold as boolean) as string
    if st.look = "b95" then return B95Trim(text, font, maxW, st.k, bold)
    return TrimToWidth(text, font, maxW)
end function

' An ad's own colour, where the look allows one: the phosphor look has only its glow, and on
' b0r3d 95's grey the bright colours wouldn't read, so its text is navy there.
function PromoAccent(st as object, spot as object) as dynamic
    if st.look = "phosphor" then return st.kit.full
    return spot.accent
end function

function PromoTextAccent(st as object, spot as object) as dynamic
    if st.look = "b95" then return st.kit.navy
    return PromoAccent(st, spot)
end function

function PromoKit(st as object) as object
    c = st.kit
    if st.look = "phosphor" then
        return {
            bg: c.bg,
            cardBg: c.bg,
            header: c.full,
            rule: c.lead,
            title: c.full,
            sub: c.soft,
            bullet: c.dim,
            hint: c.dim,
            ctaBg: c.bg,
            ctaRule: c.lead,
            barTrack: c.lead,
            previewCard: c.bg
        }
    end if
    if st.look = "vcr" then
        return {
            bg: c.bg,
            cardBg: &h0B1A7AFF,
            header: c.yellow,
            rule: c.white,
            title: c.white,
            sub: c.yellow,
            bullet: c.white,
            hint: c.grey,
            ctaBg: &h071256FF,
            ctaRule: c.white,
            barTrack: c.shadow,
            previewCard: &h0B1A7AFF
        }
    end if
    if st.look = "b95" then
        return {
            bg: c.bg,
            cardBg: c.face,
            header: c.black,
            rule: c.shadow,
            title: c.black,
            sub: c.dark,
            bullet: c.dark,
            hint: c.dark,
            ctaBg: c.light,
            ctaRule: c.shadow,
            barTrack: c.shadow,
            previewCard: c.face
        }
    end if
    if st.look = "rgb" then
        return {
            bg: c.bg,
            cardBg: c.card,
            header: c.white,
            rule: c.rule,
            title: c.white,
            sub: c.soft,
            bullet: c.soft,
            hint: c.dim,
            ctaBg: c.well,
            ctaRule: c.rule,
            barTrack: c.rule,
            previewCard: c.card
        }
    end if
    return {
        bg: c.bg,
        cardBg: c.cell1,
        header: c.accent,
        rule: c.tile,
        title: c.white,
        sub: c.soft,
        bullet: c.text,
        hint: c.dimText,
        ctaBg: c.tile,
        ctaRule: c.frame,
        barTrack: c.bar,
        previewCard: c.cell1
    }
end function

' The full screen on channel 24: a header, the card, and the keys.
sub DrawPromoScreen(st as object)
    s = st.screen
    p = st.promo
    if s = invalid or p = invalid then return
    k = st.k
    spot = p.spots[p.idx]
    f = PromoFonts(st)
    kit = PromoKit(st)

    s.Clear(kit.bg)
    hx = Z(60, k)
    hy = Z(36, k)
    statusWord = "AUTO-PLAY"
    if p.paused then statusWord = "PAUSED"
    headerText = "COMMERCIAL BREAKS // CH 24 [" + (p.idx + 1).ToStr() + "/" + p.spots.Count().ToStr() + " - " + statusWord + "]"
    if st.look = "rgb" then
        ' the look's own way of saying it: plain words between dots, the LED bar beside them
        headerText = "COMMERCIAL BREAKS  ·  CH 24  ·  " + (p.idx + 1).ToStr() + "/" + p.spots.Count().ToStr() + " " + statusWord
        DrawRgbLedBar(st, s, hx, hy + Z(2, k), f.header.GetOneLineHeight() - Z(4, k))
        GText(st, s, PromoTrim(st, headerText, f.header, st.w - 2 * hx - Z(22, k), true), hx + Z(22, k), hy, f.header, kit.header, true)
    else
        if st.look = "b95" then DrawB95Taskbar(st, "Commercial Breaks")
        GText(st, s, PromoTrim(st, headerText, f.header, st.w - 2 * hx, true), hx, hy, f.header, kit.header, true)
    end if

    cx = Z(100, k)
    cy = Z(90, k)
    cw = st.w - 2 * cx
    ch = st.h - cy - Z(90, k)
    DrawPromoCard(st, s, spot, cx, cy, cw, ch, p.timer.TotalMilliseconds(), p.slideDuration, p.paused, kit, f)

    barY = Z(652, k)
    s.DrawRect(hx, barY - Z(8, k), st.w - 2 * hx, Z(2, k), kit.rule)
    hintText = "[<>] SKIP   [OK/PLAY] PAUSE   [BACK] GUIDE"
    GText(st, s, hintText, hx, barY, f.bar, kit.hint, false)
    if ScanlinesOn(st) then DrawScanlines(st)
    s.SwapBuffers()
end sub

' One ad's card: its tag, title and line, two points about the game, and where to find it, with
' the card's time running out along its bottom edge. Every line is cut to fit the card.
sub DrawPromoCard(st as object, s as object, spot as object, cx as integer, cy as integer, cw as integer, ch as integer, elapsed as integer, duration as integer, paused as boolean, kit as object, f as object)
    k = st.k
    borderW = Z(3, k)
    accent = PromoAccent(st, spot)
    ink = PromoTextAccent(st, spot)
    if st.look = "b95" then
        B95Raised(s, cx, cy, cw, ch, Z(3, k), st.kit)
    else
        s.DrawRect(cx - borderW, cy - borderW, cw + 2 * borderW, ch + 2 * borderW, accent)
        s.DrawRect(cx, cy, cw, ch, kit.cardBg)
    end if

    px = cx + Z(36, k)
    py = cy + Z(30, k)
    textW = cw - 2 * Z(36, k)

    ' the tag, in a box
    tag = PromoTrim(st, spot.tag, f.badge, textW - Z(24, k), true)
    badgeW = GTextWidth(st, tag, f.badge, true) + Z(24, k)
    badgeH = f.badge.GetOneLineHeight() + Z(10, k)
    s.DrawRect(px, py, badgeW, badgeH, accent)
    s.DrawRect(px + Z(2, k), py + Z(2, k), badgeW - Z(4, k), badgeH - Z(4, k), kit.cardBg)
    GText(st, s, tag, px + Z(12, k), py + Z(4, k), f.badge, ink, true)
    py = py + badgeH + Z(16, k)

    GText(st, s, PromoTrim(st, spot.title, f.title, textW, true), px, py, f.title, kit.title, true)
    py = py + f.title.GetOneLineHeight() + Z(12, k)
    GText(st, s, PromoTrim(st, spot.sub, f.sub, textW, true), px, py, f.sub, ink, true)
    py = py + f.sub.GetOneLineHeight() + Z(24, k)

    s.DrawRect(px, py, textW, Z(2, k), kit.rule)
    py = py + Z(20, k)

    for each line in [spot.bullet1, spot.bullet2]
        s.DrawRect(px, py + Z(8, k), Z(8, k), Z(8, k), accent)
        GText(st, s, PromoTrim(st, line, f.bullet, textW - Z(20, k), false), px + Z(20, k), py, f.bullet, kit.bullet, false)
        py = py + f.bullet.GetOneLineHeight() + Z(14, k)
    end for

    ' where to find it
    ctaH = Z(44, k)
    ctaY = cy + ch - ctaH - Z(26, k)
    s.DrawRect(px, ctaY, textW, ctaH, kit.ctaRule)
    s.DrawRect(px + Z(2, k), ctaY + Z(2, k), textW - Z(4, k), ctaH - Z(4, k), kit.ctaBg)
    GText(st, s, PromoTrim(st, spot.cta, f.cta, textW - Z(36, k), true), px + Z(18, k), ctaY + (ctaH - f.cta.GetOneLineHeight()) \ 2, f.cta, ink, true)

    ' the card's time, along its bottom edge (full while paused)
    barH = Z(6, k)
    barY = cy + ch - barH
    s.DrawRect(cx, barY, cw, barH, kit.barTrack)
    fillW = cw
    if not paused then
        progress = elapsed / duration
        if progress > 1.0 then progress = 1.0
        fillW = Int(cw * progress + 0.5)
    end if
    s.DrawRect(cx, barY, fillW, barH, accent)
end sub

' The guide's preview box: the ad that's on now (a new one every 8 s by the clock), cut to fit.
sub DrawPromoPreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    k = st.k
    spots = PromoSpots()
    now = 0
    if st.clock <> invalid then now = st.clock.minute * 60 + st.clock.second
    spot = spots[(now \ 8) mod spots.Count()]
    f = PromoFonts(st)
    kit = PromoKit(st)
    accent = PromoAccent(st, spot)
    ink = PromoTextAccent(st, spot)
    ' DemoWaitMs waits on this: the box is drawn again twice a second
    if st.promoDemoTimer = invalid then st.promoDemoTimer = CreateObject("roTimespan")
    st.promoDemoTimer.Mark()

    inset = Z(2, k)
    s.DrawRect(x, y, w, h, accent)
    s.DrawRect(x + inset, y + inset, w - 2 * inset, h - 2 * inset, kit.previewCard)
    tx = x + Z(12, k)
    ty = y + Z(10, k)
    tw = w - Z(24, k)
    GText(st, s, PromoTrim(st, "COMMERCIAL BREAKS", f.badge, tw, true), tx, ty, f.badge, ink, true)
    ty = ty + f.badge.GetOneLineHeight() + Z(6, k)
    GText(st, s, PromoTrim(st, spot.title, f.preview, tw, true), tx, ty, f.preview, kit.title, true)
    ty = ty + f.preview.GetOneLineHeight() + Z(6, k)
    for each line in WrapText(spot.sub, f.badge, tw)
        if ty + f.badge.GetOneLineHeight() > y + h - Z(8, k) then exit for
        GText(st, s, PromoTrim(st, line, f.badge, tw, false), tx, ty, f.badge, kit.sub, false)
        ty = ty + f.badge.GetOneLineHeight() + Z(4, k)
    end for
    ty = ty + Z(6, k)
    if ty + f.badge.GetOneLineHeight() <= y + h - Z(8, k) then
        GText(st, s, PromoTrim(st, spot.cta, f.badge, tw, false), tx, ty, f.badge, ink, false)
    end if
end sub
