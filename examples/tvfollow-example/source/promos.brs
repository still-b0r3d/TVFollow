' Commercial Breaks on channel 24: late-night station ads for b0r3d's own games and a public
' service announcement, one card every 8 seconds, in every look. Left and right skip, OK or
' Play pauses, Back goes to the guide. Each ad says only what its game really has, beside a
' picture from the game itself (Ruckus himself for the announcement): pkg:/images/promos/, cut
' from the owner's own art. pixels marks pixel art, which is only ever blown up by whole numbers.

function PromoSpots() as object
    return [
        {
            tag: "NOW PLAYING ON b0r3d.org",
            title: "CHAINSAW SQUIRREL",
            sub: "ONE SQUIRREL. ONE CHAINSAW. A WHOLE FOREST.",
            bullet1: "Fell the trees realm after realm, and take on each realm's boss.",
            bullet2: "Saws, bombs and upgrades to collect along the way.",
            cta: "PLAY IT FREE ON b0r3d.org",
            accent: &hFF3B5CFF,
            pic: "cs.jpg",
            picName: "squirrel.jpg",
            pixels: false
        },
        {
            tag: "NEW ON YOUR DESKTOP",
            title: "b0r3d 95",
            sub: "A DESKTOP FROM 1995, RIGHT IN YOUR BROWSER.",
            bullet1: "Pinball tables, solitaire, a paint program and a calculator.",
            bullet2: "A dial-up web browser, screensavers and online high scores.",
            cta: "START IT UP AT b0r3d.org/95",
            accent: &h39FF88FF,
            pic: "b95.jpg",
            picName: "paint.jpg",
            pixels: false
        },
        {
            tag: "TONIGHT'S CREATURE FEATURE",
            title: "z0mbie crystal",
            sub: "A CITY FULL OF THE DEAD, ONE BLOCK AT A TIME.",
            bullet1: "A roguelike run across the city, with Ruckus in it somewhere.",
            bullet2: "Every run is a new map, and a run can be saved and picked up later.",
            cta: "PLAY IT ON b0r3d.org",
            accent: &h00E5FFFF,
            pic: "zc.png",
            picName: "creature.png",
            pixels: true
        },
        {
            tag: "A PUBLIC SERVICE ANNOUNCEMENT",
            title: "DON'T TOUCH THAT DIAL",
            sub: "b0r3d TV'S OWN CHANNELS: 3, 9 AND 20 TO 24.",
            bullet1: "A VCR, a cat cam, three games, a radio station and these commercials.",
            bullet2: "Leave the remote alone for a while and Ruckus comes by.",
            cta: "THIS HAS BEEN A TEST. STAY TUNED.",
            accent: &hFFD700FF,
            pic: "psa.jpg",
            picName: "ruckus.jpg",
            pixels: false
        },
        {
            tag: "ALSO PLAYING ON b0r3d.org",
            title: "RUCKUS: LOCKED & LOAFED",
            sub: "ONE CAT AND HIS DUAL GATLING RIG VS. THE ROBO-VACS.",
            bullet1: "Hold off the horde for ten minutes a stage, then the boss.",
            bullet2: "Three stages, each with its own online high scores.",
            cta: "PLAY IT ON b0r3d.org",
            accent: &hFF7700FF,
            pic: "ll.jpg",
            picName: "loafed.jpg",
            pixels: false
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
    ' the guide's copy of an ad's picture isn't needed while the channel is up, and a picture
    ' file that wouldn't load gets another try
    st.promoPreview = invalid
    st.promoNoPic = invalid
    st.promo.timer.Mark()
    st.promo.frame.Mark()
    st.promo.clockTimer.Mark()
    st.mode = "promo"
    Redraw(st)
end sub

sub ClosePromo(st as object)
    ' the channel's picture goes with it
    st.promoPic = invalid
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

' Text wrapped at the spaces to fit maxW in the look's own lettering, at most maxLines lines; the
' last one is cut to fit when there's more.
function PromoWrap(st as object, text as string, font as object, maxW as integer, maxLines as integer, bold as boolean) as object
    lines = []
    line = ""
    words = text.Split(" ")
    n = words.Count()
    i = 0
    while i < n
        attempt = words[i]
        if line <> "" then attempt = line + " " + words[i]
        if line = "" or GTextWidth(st, attempt, font, bold) <= maxW then
            line = attempt
            i = i + 1
        else if lines.Count() < maxLines - 1 then
            lines.Push(PromoTrim(st, line, font, maxW, bold))
            line = ""
        else
            ' no lines left: the rest goes on this one, cut to fit below
            while i < n
                line = line + " " + words[i]
                i = i + 1
            end while
        end if
    end while
    lines.Push(PromoTrim(st, line, font, maxW, bold))
    return lines
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

' An ad's picture, made the size it's drawn at: loaded from pkg:/images/promos/ and, when it's
' drawn at another size (at 720p, in the guide's preview box, pixel art blown up by a whole
' number so it stays crisp), copied once into a picture of that size, so a frame is a plain copy.
' Only the ad on screen is kept, one for the channel ("screen") and one for the guide's preview
' box ("preview", let go by Redraw once the guide stops showing it); another ad lets go of the old
' picture before loading its own. Invalid when it can't be had (no picture memory, or a file that
' won't load, which only that ad goes without): the ad is then text only.
function PromoPicture(st as object, slot as string, spot as object, boxW as integer, boxH as integer) as dynamic
    if spot.pic = "" or boxW < 16 or boxH < 16 then return invalid
    key = spot.pic + "|" + boxW.ToStr() + "|" + boxH.ToStr()
    held = st.promoPic
    if slot = "preview" then held = st.promoPreview
    if held <> invalid then
        if held.key = key then return held.bmp
    end if
    if slot = "preview" then st.promoPreview = invalid else st.promoPic = invalid
    ' a file that wouldn't load isn't tried again until the channel is opened again (OpenPromo)
    if st.promoNoPic <> invalid then
        if st.promoNoPic.DoesExist(spot.pic) then return invalid
    end if
    if PicturesFailing(st) then return invalid
    src = invalid
    try
        src = CreateObject("roBitmap", "pkg:/images/promos/" + spot.pic)
    catch e
        src = invalid
    end try
    bw = 0
    bh = 0
    if src <> invalid then
        bw = src.GetWidth()
        bh = src.GetHeight()
    end if
    if bw < 1 or bh < 1 then
        ' only this ad goes without its picture; PictureFailed is for running out of memory
        print "b0r3d TV: the ad picture "; spot.pic; " didn't load"
        if st.promoNoPic = invalid then st.promoNoPic = {}
        st.promoNoPic[spot.pic] = true
        return invalid
    end if
    sc = boxW / bw
    if boxH / bh < sc then sc = boxH / bh
    sharp = spot.pixels and sc >= 1
    if sharp then sc = Int(sc)
    pw = Int(bw * sc + 0.5)
    ph = Int(bh * sc + 0.5)
    pic = src
    if pw <> bw or ph <> bh then
        pic = NewPicture(pw, ph, false)
        area = invalid
        if pic <> invalid then area = CreateObject("roRegion", src, 0, 0, bw, bh)
        if area = invalid then
            PictureFailed(st)
            return invalid
        end if
        if sharp then area.SetScaleMode(0) else area.SetScaleMode(1)
        pic.DrawScaledObject(0, 0, pw / bw, ph / bh, area)
    end if
    held = { key: key, bmp: pic }
    if slot = "preview" then st.promoPreview = held else st.promoPic = held
    return pic
end function

' An ad's picture on the screen; the phosphor look sees it in its own glow (the tint multiplies).
sub PromoBlit(st as object, s as object, pic as object, x as integer, y as integer)
    if st.look = "phosphor" then
        s.DrawObject(x, y, pic, st.kit.full)
    else
        s.DrawObject(x, y, pic)
    end if
end sub

' The look's own frame round an ad's picture at x, y (w by h), drawn before the picture: b0r3d
' 95 opens it in a window (only a sunken edge in the guide's small box), the VCR look gives it a
' white edge and a hard shadow, the RGB LED lights a lit edge, the cable guide and the phosphor
' terminal a bezel in the ad's colour. big is the channel's own screen.
sub DrawPromoBezel(st as object, s as object, spot as object, x as integer, y as integer, w as integer, h as integer, big as boolean)
    k = st.k
    c = st.kit
    t = Z(2, k)
    if t < 1 then t = 1
    if st.look = "b95" then
        if big then
            ' PromoWindowTop says how far this reaches above the picture
            side = 4 * t + Z(4, k)
            top = side + Z(40, k)
            B95Raised(s, x - side, y - top, w + 2 * side, h + top + side, t, c)
            DrawB95TitleBar(st, x - side + 2 * t + Z(2, k), y - top + 2 * t + Z(2, k), w + 2 * side - 4 * t - Z(4, k), spot.picName, false)
        end if
        B95Sunken(s, x - 2 * t, y - 2 * t, w + 4 * t, h + 4 * t, t, c, c.black)
        return
    end if
    if st.look = "vcr" then
        e = t
        if big then
            s.DrawRect(x - e + Z(6, k), y - e + Z(6, k), w + 2 * e, h + 2 * e, c.shadow)
        end if
        s.DrawRect(x - e, y - e, w + 2 * e, h + 2 * e, c.white)
        return
    end if
    if st.look = "rgb" then
        e = t
        if big then e = Z(4, k)
        s.DrawRect(x - e, y - e, w + 2 * e, h + 2 * e, c.frame)
        if big then DrawRgbEdge(st, s, x - e, y - e, w + 2 * e, h + 2 * e)
        return
    end if
    accent = PromoAccent(st, spot)
    if big then
        s.DrawRect(x - Z(4, k), y - Z(4, k), w + Z(8, k), h + Z(8, k), accent)
        s.DrawRect(x - Z(2, k), y - Z(2, k), w + Z(4, k), h + Z(4, k), c.bg)
    else
        s.DrawRect(x - t, y - t, w + 2 * t, h + 2 * t, accent)
    end if
end sub

' How far b0r3d 95's window (its title bar and edges) reaches above an ad's picture.
function PromoWindowTop(st as object) as integer
    if st.look <> "b95" then return 0
    t = Z(2, st.k)
    if t < 1 then t = 1
    return 4 * t + Z(4, st.k) + Z(40, st.k)
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
' the card's time running out along its bottom edge. The ad's picture stands in a column of its
' own on the right; without one (no picture memory) the words take the card's whole width. Every
' line is wrapped or cut to fit its column.
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
    barH = Z(6, k)

    ' the picture, 4:3 (720 by 540 at 1080p), standing in the middle of its column
    pic = PromoPicture(st, "screen", spot, Z(480, k), Z(360, k))
    if pic <> invalid then
        colW = Z(512, k)
        textW = textW - colW - Z(16, k)
        pw = pic.GetWidth()
        ph = pic.GetHeight()
        lift = PromoWindowTop(st)
        picX = cx + cw - Z(36, k) - colW + (colW - pw) \ 2
        picY = cy + lift + (ch - barH - lift - ph) \ 2
        DrawPromoBezel(st, s, spot, picX, picY, pw, ph, true)
        PromoBlit(st, s, pic, picX, picY)
    end if

    ' the tag, in a box
    tag = PromoTrim(st, spot.tag, f.badge, textW - Z(24, k), true)
    badgeW = GTextWidth(st, tag, f.badge, true) + Z(24, k)
    badgeH = f.badge.GetOneLineHeight() + Z(10, k)
    s.DrawRect(px, py, badgeW, badgeH, accent)
    s.DrawRect(px + Z(2, k), py + Z(2, k), badgeW - Z(4, k), badgeH - Z(4, k), kit.cardBg)
    GText(st, s, tag, px + Z(12, k), py + Z(4, k), f.badge, ink, true)
    py = py + badgeH + Z(16, k)

    for each line in PromoWrap(st, spot.title, f.title, textW, 2, true)
        GText(st, s, line, px, py, f.title, kit.title, true)
        py = py + f.title.GetOneLineHeight()
    end for
    py = py + Z(12, k)
    for each line in PromoWrap(st, spot.sub, f.sub, textW, 2, true)
        GText(st, s, line, px, py, f.sub, ink, true)
        py = py + f.sub.GetOneLineHeight()
    end for
    py = py + Z(24, k)

    s.DrawRect(px, py, textW, Z(2, k), kit.rule)
    py = py + Z(20, k)

    ' where to find it, at the bottom; the two points stop short of it
    ctaH = Z(44, k)
    ctaY = cy + ch - ctaH - Z(26, k)
    lineH = f.bullet.GetOneLineHeight()
    for each point in [spot.bullet1, spot.bullet2]
        if py + lineH > ctaY - Z(12, k) then exit for
        s.DrawRect(px, py + Z(8, k), Z(8, k), Z(8, k), accent)
        for each line in PromoWrap(st, point, f.bullet, textW - Z(20, k), 2, false)
            if py + lineH > ctaY - Z(12, k) then exit for
            GText(st, s, line, px + Z(20, k), py, f.bullet, kit.bullet, false)
            py = py + lineH + Z(4, k)
        end for
        py = py + Z(10, k)
    end for

    s.DrawRect(px, ctaY, textW, ctaH, kit.ctaRule)
    s.DrawRect(px + Z(2, k), ctaY + Z(2, k), textW - Z(4, k), ctaH - Z(4, k), kit.ctaBg)
    GText(st, s, PromoTrim(st, spot.cta, f.cta, textW - Z(36, k), true), px + Z(18, k), ctaY + (ctaH - f.cta.GetOneLineHeight()) \ 2, f.cta, ink, true)

    ' the card's time, along its bottom edge (full while paused)
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

' The guide's preview box: the ad that's on now (a new one every 8 s by the clock), its name and
' its picture when the box has room for one, else its line and where to find it, cut to fit.
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

    ' the picture under the ad's name, in the middle of the room left
    room = y + h - Z(10, k) - ty
    pic = invalid
    if room >= Z(72, k) then pic = PromoPicture(st, "preview", spot, tw - Z(8, k), room - Z(8, k))
    if pic <> invalid then
        pw = pic.GetWidth()
        ph = pic.GetHeight()
        picX = x + (w - pw) \ 2
        picY = ty + Z(4, k) + (room - Z(8, k) - ph) \ 2
        DrawPromoBezel(st, s, spot, picX, picY, pw, ph, false)
        PromoBlit(st, s, pic, picX, picY)
        return
    end if

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
