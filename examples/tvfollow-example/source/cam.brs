' Rukus Cam on channel 9 (nine lives): a security camera on Rukus's room, live. Like the VCR it
' has a picture of its own, the same in every look, laid out on the 1280 x 720 grid (scaled by
' st.k).
'
' The room is pixel art, 160 x 90 of its pixels, each 8 grid units (Rukus's own pixel size). It's
' drawn into a small picture once a minute and stretched over the screen. The window shows the
' hour: sunrise from 6, day from 8 (the sun crossing the window and lying on the floor), sunset
' from 17, and from 20 to 6 the camera switches to infrared: grey-green, the moon out, Rukus's
' eyes shining. Rukus is the screensaver's (saver.brs): the same walks, sits, grooms, loafs, naps
' and stretches, wandering off now and then and coming back, and the stuck pixel he chases. OK
' throws him a treat from the camera, as treat cameras do: one at a time, nine a day. While one
' is coming or waiting it's all he thinks about: no wandering off and no napping, and he dashes
' for it, crunches it and licks his lips.
'
' The camera's words are VT323 with a hard shadow: CAM 1 at the top left (and IR at night), REC
' at the top right with its dot blinking, the date and time at the bottom left, and for a few
' seconds after a key, what the keys do. While it's on, Rukus the screensaver stays away (he's
' on already) and the TV is kept awake as for b0r3d FM. While the TV's own screensaver may have
' blanked the screen, Rukus carries on but the camera is drawn only once a second, so if the TV
' is still lit after all, the picture and its clock still move.

' The camera: the screensaver's scene (its sv: Rukus, the stuck pixel, his z's, the pop) with the
' room, the treats and the words added. Made the first time the guide shows the channel, and
' kept, so the preview and the channel show the same Rukus.
function CamState(st as object) as object
    if st.cam <> invalid then return st.cam
    cam = {
        frame: CreateObject("roTimespan"),
        clockRead: CreateObject("roTimespan"),
        clockTimer: CreateObject("roTimespan"),
        hintTimer: CreateObject("roTimespan"),
        blink: CreateObject("roTimespan"),
        blank: false,
        r: { state: "away", timer: 0.6, x: -136.0, floor: 600.0, faceLeft: false, t: 0.0, acts: 0, lift: 0.0, goal: 0.0, jump: invalid, zTimer: 0.0 },
        px: invalid,
        pxTimer: 25 + Rnd(0) * 25,
        pxCount: 0,
        pxColors: [&hFF3B30FF, &h34FF5CFF, &h3D7BFFFF],
        pop: invalid,
        zs: [],
        hearts: [],
        toss: invalid,
        phase: "",
        roomKey: "",
        room: invalid,
        roomNear: invalid,
        roomSmooth: invalid,
        runs: invalid,
        ink: invalid,
        treatDay: "",
        treatsUsed: 0,
        words: {},
        wordsK: -1,
        timeText: "",
        timePic: invalid,
        timePicK: -1,
        hintText: "",
        hintPic: invalid,
        hintPicK: -1
    }
    st.cam = cam
    CamChores(st, cam)
    return cam
end function

' Turning the channel on: the camera's picture, and what the keys do for a few seconds.
sub OpenCam(st as object)
    cam = CamState(st)
    st.mode = "cam"
    st.clock = ClockNow()
    cam.clockRead.Mark()
    cam.clockTimer.Mark()
    cam.hintTimer.Mark()
    cam.frame.Mark()
    cam.blank = false
    CamChores(st, cam)
    print "b0r3d TV Rukus Cam: on"
    DrawCamScreen(st)
end sub

' Back to the guide. The camera's word pictures go (the guide's preview writes its own small
' words); Rukus, his room and a treat on its way stay, so the preview shows him carrying on.
sub CloseCam(st as object)
    cam = st.cam
    if cam <> invalid then
        cam.words = {}
        cam.wordsK = -1
        cam.timePic = invalid
        cam.timeText = ""
        cam.hintPic = invalid
        cam.hintText = ""
    end if
    st.mode = "guide"
    print "b0r3d TV Rukus Cam: off"
    Redraw(st)
end sub

' OK throws a treat; Back goes back to the guide. Any key shows what the keys do, and brings the
' picture back if the TV's own screensaver had blanked it.
sub CamKey(st as object, code as integer)
    cam = st.cam
    if cam = invalid then return
    cam.blank = false
    if code = 0 then
        CloseCam(st)
        return
    end if
    cam.hintTimer.Mark()
    if code = 6 then CamThrowTreat(st, cam)
end sub

' Main's loop calls this on every turn on channel 9: the clock four times a second (so the
' seconds shown never skip one); once a second the chores (the TV kept awake, as for b0r3d FM),
' the room's hour, and whether the TV's own screensaver may have blanked the screen (at its own
' time, no margin); and a frame about 30 times a second. While the screen may be blank, Rukus
' still moves on about 10 times a second but the camera is drawn once a second; a key, or the
' TV's screensaver ending (ScreensaverEvent), brings the 30 frames straight back.
sub CamTurn(st as object)
    cam = st.cam
    if cam = invalid then return
    if cam.clockRead.TotalMilliseconds() >= 250 then
        cam.clockRead.Mark()
        st.clock = ClockNow()
    end if
    if cam.clockTimer.TotalMilliseconds() >= 1000 then
        cam.clockTimer.Mark()
        if MediaChores(st, true) then return
        CamChores(st, cam)
        cam.blank = TvMayBeBlank(st, 0)
        if cam.blank then DrawCamScreen(st)
    end if
    if cam.frame.TotalMilliseconds() < 30 then return
    CamAdvance(cam)
    if not cam.blank then DrawCamScreen(st)
end sub

' How long Main's loop waits on channel 9: what's left of a frame, or a tenth of a second while
' the screen may be blank.
function CamWaitMs(st as object) as integer
    cam = st.cam
    if cam = invalid then return 33
    if cam.blank then return 100
    due = 33 - cam.frame.TotalMilliseconds()
    if due < 1 then return 1
    if due > 33 then return 33
    return due
end function

' Everything moves on by the time since the last step (a tenth of a second at most).
sub CamAdvance(cam as object)
    dt = cam.frame.TotalMilliseconds() / 1000
    cam.frame.Mark()
    if dt > 0.1 then dt = 0.1
    CamStep(cam, dt)
end sub

' Once a second: a new day's treats, a new hour's light (his colours too), and the room drawn
' again when the minute changes (the sun, the moon and the clouds move on).
sub CamChores(st as object, cam as object)
    c = st.clock
    if c.isoDate <> cam.treatDay then CamReadTreats(st, cam)
    ph = CamPhase(st)
    if ph <> cam.phase then
        cam.phase = ph
        cam.runs = CamRukusRuns(ph)
        cam.ink = CamInk(ph)
    end if
    key = ph + " " + c.hours.ToStr() + ":" + c.mins.ToStr()
    if key <> cam.roomKey then
        cam.roomKey = key
        CamBuildRoom(st, cam)
    end if
end sub

' --- Rukus and the treats ---

' Everything moves on by dt seconds: a treat in the air, Rukus (the screensaver's moves), the
' stuck pixel, his z's and the hearts. While a treat is coming or waiting, CamWantsTreat has its
' say before his move and after it, so not even one frame shows him dozing off or walking away.
sub CamStep(cam as object, dt as dynamic)
    CamStepToss(cam, dt)
    r = cam.r
    hadTreat = false
    if cam.px <> invalid then hadTreat = cam.px.DoesExist("treat")
    waiting = hadTreat or cam.toss <> invalid
    if waiting then CamWantsTreat(cam)
    StepRukus(cam, dt)
    if hadTreat and cam.px = invalid then
        ' crunch: the crumbs fly (the pounce's sparks, in the treat's colour), a heart, and he
        ' licks his lips
        r.state = "groom"
        r.t = 0
        r.timer = 2.4
        hx = r.x + 88
        if r.faceLeft then hx = r.x + 24
        cam.hearts.Push({ x: hx, y: r.floor - 132, t: 0.0 })
    else if waiting then
        CamWantsTreat(cam)
    end if
    StepPixel(cam, dt)
    StepZs(cam, dt)
    if cam.hearts.Count() > 0 then
        kept = []
        for each heart in cam.hearts
            heart.t = heart.t + dt
            heart.y = heart.y - 24 * dt
            if heart.t < 1.6 then kept.Push(heart)
        end for
        cam.hearts = kept
    end if
end sub

' OK: a treat flies from the camera to a spot along his floor, not right where he is, so there's
' a dash for it. One at a time, nine a day. Out of the room, he comes back in on the line it will
' land on. A stuck pixel on the floor goes (unless he's pouncing on it).
sub CamThrowTreat(st as object, cam as object)
    if cam.treatsUsed >= 9 then return
    if cam.toss <> invalid then return
    if cam.px <> invalid then
        if cam.px.DoesExist("treat") then return
    end if
    cam.treatsUsed = cam.treatsUsed + 1
    RegWrite("camtreats", cam.treatDay + "|" + cam.treatsUsed.ToStr())
    r = cam.r
    if r.state = "away" then RukusEnter(cam)
    tx = 80 + Rnd(0) * 1100
    for tries = 1 to 6
        if Abs(tx - (r.x + 64)) > 220 then exit for
        tx = 80 + Rnd(0) * 1100
    end for
    cam.toss = { t: 0.0, dur: 0.8, x1: tx, y1: r.floor - 8 }
    if r.state <> "pounce" then cam.px = invalid
    if cam.pxTimer < 30 then cam.pxTimer = 30
    CamWantsTreat(cam)
    print "b0r3d TV Rukus Cam: a treat ("; cam.treatsUsed; " of 9 today)"
end sub

' A treat is coming or waiting, so it's all he thinks about: he doesn't wander off (one more act
' at least), a nap ends, and if he was walking into or out of the room he stops where he is.
' Once it's down and he's noticed it (StepPixel, WantsPixel), he dashes for it. Out of the room
' (only if something sent him off), he comes back in on the treat's line.
sub CamWantsTreat(cam as object)
    r = cam.r
    if r.acts < 1 then r.acts = 1
    if r.state = "away" then
        RukusEnter(cam)
        if cam.px <> invalid then
            r.floor = cam.px.y + 8
        else if cam.toss <> invalid then
            r.floor = cam.toss.y1 + 8
        end if
    end if
    if r.state = "nap" or r.state = "enter" or r.state = "leave" then
        r.state = "sit"
        r.t = 0
        r.timer = 2.0
    end if
end sub

' The treat in the air; when it lands it's what he goes for (CamStep sees to the rest).
sub CamStepToss(cam as object, dt as dynamic)
    tz = cam.toss
    if tz = invalid then return
    tz.t = tz.t + dt
    if tz.t < tz.dur then return
    cam.toss = invalid
    cam.px = { x: tz.x1, y: tz.y1, notice: 0.2 + Rnd(0) * 0.3, color: cam.ink.treat, treat: true }
end sub

' Treats thrown today, kept on the TV as "<day>|<count>" (a new day starts again at none).
sub CamReadTreats(st as object, cam as object)
    day = st.clock.isoDate
    cam.treatDay = day
    cam.treatsUsed = 0
    saved = RegRead("camtreats", "")
    bar = Instr(1, saved, "|")
    if bar > 1 then
        if Left(saved, bar - 1) = day then cam.treatsUsed = Mid(saved, bar + 1).ToInt()
    end if
end sub

' What the keys do, in the brackets the other channels use. While a treat is coming or waiting,
' OK can't throw another, and the line says so.
function CamHintText(cam as object) as string
    n = 9 - cam.treatsUsed
    waiting = cam.toss <> invalid
    if cam.px <> invalid then
        if cam.px.DoesExist("treat") then waiting = true
    end if
    if waiting then
        if n <= 0 then return "THAT WAS THE LAST ONE TODAY   [BACK] GUIDE"
        return "ONE TREAT AT A TIME (" + n.ToStr() + " LEFT TODAY)   [BACK] GUIDE"
    end if
    if n <= 0 then return "NO TREATS LEFT TODAY   [BACK] GUIDE"
    if n = 1 then return "[OK] THROW A TREAT (THE LAST ONE TODAY)   [BACK] GUIDE"
    return "[OK] THROW A TREAT (" + n.ToStr() + " LEFT TODAY)   [BACK] GUIDE"
end function

' --- The hour's light ---

' "dawn" from 6, "day" from 8, "dusk" from 17, "night" from 20 to 6 (infrared), by the TV's clock.
function CamPhase(st as object) as string
    h = 12
    if st.clock <> invalid then
        if st.clock.hours <> invalid then h = st.clock.hours
    end if
    if h >= 6 and h < 8 then return "dawn"
    if h >= 8 and h < 17 then return "day"
    if h >= 17 and h < 20 then return "dusk"
    return "night"
end function

' A colour as the camera sees it at this hour: as it is by day, dimmer and warmer at dawn and
' at dusk, grey-green in infrared at night.
function CamTint(c as integer, ph as string) as integer
    if ph = "day" then return c
    r = (c >> 24) and &hFF
    g = (c >> 16) and &hFF
    b = (c >> 8) and &hFF
    if ph = "night" then
        v = (0.3 * r + 0.59 * g + 0.11 * b) * 0.85 + 6
        return CamRgb(v * 0.84, v, v * 0.87)
    end if
    if ph = "dawn" then return CamRgb(r * 0.9, g * 0.8, b * 0.84)
    return CamRgb(r * 0.88, g * 0.7, b * 0.6)
end function

function CamRgb(r as dynamic, g as dynamic, b as dynamic) as integer
    out = &hFF
    shift = 24
    for each v in [r, g, b]
        n = Int(v + 0.5)
        if n < 0 then n = 0
        if n > 255 then n = 255
        out = out or (n << shift)
        shift = shift - 8
    end for
    return out
end function

' Rukus's poses in the hour's light (his own colours, not the look's), as the screensaver's runs.
' In infrared his eyes shine.
function CamRukusRuns(ph as string) as object
    base = RukusColors()
    colors = {}
    for each code in base
        colors.AddReplace(code, CamTint(base[code], ph))
    end for
    if ph = "night" then colors.AddReplace("3", &hF4FFF4FF)
    poses = RukusPoses()
    runs = {}
    for each name in poses
        runs[name + "L"] = SpriteRuns(poses[name], colors, false)
        runs[name + "R"] = SpriteRuns(poses[name], colors, true)
    end for
    return runs
end function

' The colours of what moves in the room: his z's, the hearts and the treats.
function CamInk(ph as string) as object
    return {
        z: CamTint(&hD8DCE6FF, ph),
        heart: CamTint(&hFF5C7CFF, ph),
        treat: CamTint(&hB8743CFF, ph),
        treatHi: CamTint(&hE0A060FF, ph)
    }
end function

' --- The room ---

' The room's colours by day (CamTint shades them for the hour).
function CamRoomColours() as object
    return {
        wallUp: &h6B7B8CFF, wallLow: &h56636FFF, rail: &h8A96A3FF, railShadow: &h4A5560FF,
        groove: &h48535EFF, shelfShadow: &h5A6877FF, base: &hD8D2C4FF, baseShadow: &h9C968AFF,
        floorA: &h8A5A36FF, floorB: &h82532FFF, seam: &h4E321FFF,
        frameWood: &h4A3424FF, sea: &h3A6EA8FF, deep: &h2E5A90FF, sand: &hD8C08AFF,
        fish: &hF28C28FF, black: &h1A1A1AFF, bubble: &hE8F4FFFF, weed: &h4E9A4AFF,
        shelf: &h7A5638FF, bracket: &h4A3424FF, pot: &hB8643CFF, potRim: &hD27A50FF,
        leaf: &h4E9A4AFF, leafDark: &h357A36FF,
        book1: &hC04848FF, book2: &h3E6EA8FF, book3: &hD8B040FF, book4: &h5A8A5AFF, book5: &h7A5A9AFF,
        winFrame: &hE8E4DAFF, winShadow: &hB8B4AAFF, sill: &hD8D2C4FF,
        curtain: &hA84848FF, fold: &h883636FF, rod: &h3A3A3AFF, finial: &h2A2A2AFF,
        rope: &hC8A878FF, ropeLine: &hA8885AFF, postWood: &h6A5038FF, postHi: &h8A6A4AFF,
        toyString: &h3A3A3AFF, pom: &hE06080FF,
        rugEdge: &hE2C9A0FF, rug: &h8E3B46FF, rugLine: &hC89A70FF, rugMark: &hA84C58FF,
        bedOut: &h3D4E7AFF, bedRim: &h5A6EA0FF, bedIn: &h6F86B8FF,
        bowlRim: &hE06A5CFF, bowl: &hC4463AFF, bowlBase: &h8E2E26FF, kibble: &h7A4A2AFF, kibbleHi: &h9A6A3AFF,
        dishRim: &hD8D8E0FF, water: &h5AA0D8FF, waterHi: &h9ACCF0FF, dish: &hA8A8B4FF, dishBase: &h7A7A86FF,
        yarn: &hD86A9AFF, yarnLine: &hA84A76FF
    }
end function

function CamPalette(ph as string) as object
    base = CamRoomColours()
    if ph = "day" then return base
    p = {}
    for each name in base
        p[name] = CamTint(base[name], ph)
    end for
    return p
end function

' The sky through the window at each hour, top band first (not shaded: it's the light itself).
function CamSkyColours(ph as string) as object
    if ph = "dawn" then return { bands: [&h3E4A88FF, &h7A6496FF, &hD88A98FF, &hF2B482FF], town: &h3E3858FF, sun: &hFFD27AFF, sunHi: &hFFF0C0FF }
    if ph = "dusk" then return { bands: [&h2C2E68FF, &h6A4888FF, &hCC5E78FF, &hEE9658FF], town: &h2E2646FF, sun: &hFFB05AFF, sunHi: &hFFE0A0FF, lit: &hF4D27AFF }
    if ph = "night" then return { bands: [&h121814FF, &h141B16FF, &h171F19FF, &h1A231CFF], town: &h0C100DFF, moon: &hD8E4DAFF, star: &h8A988DFF }
    return { bands: [&h5E9BD8FF, &h6FA9E0FF, &h82B8E8FF, &h98C8EEFF], town: &h5A6A80FF, sun: &hFFD860FF, sunHi: &hFFF4C0FF, cloud: &hF4F8FFFF, cloudShade: &hD8E4F2FF }
end function

' The room at this hour into a new picture, 160 x 90 (the old one may still be on the screen, so
' it isn't drawn over).
sub CamBuildRoom(st as object, cam as object)
    b = NewPicture(160, 90, false)
    if b = invalid then return
    ph = cam.phase
    p = CamPalette(ph)
    hours = 12
    mins = 0
    if st.clock <> invalid then
        hours = st.clock.hours
        mins = st.clock.mins
    end if

    ' the wall: paint above the rail, panelling below it, the skirting board
    CamBox(b, 0, 0, 160, 34, p.wallUp)
    CamBox(b, 0, 34, 160, 2, p.rail)
    CamBox(b, 0, 36, 160, 1, p.railShadow)
    CamBox(b, 0, 37, 160, 14, p.wallLow)
    for gx = 8 to 152 step 16
        CamBox(b, gx, 38, 1, 12, p.groove)
    end for
    CamBox(b, 0, 51, 160, 2, p.base)
    CamBox(b, 0, 53, 160, 1, p.baseShadow)

    CamPictureFrame(b, p)
    CamShelf(b, p)
    CamWindow(b, p, ph, hours, mins)
    CamFloor(b, p, ph)
    CamThings(b, p)

    cam.room = b
    cam.roomNear = CreateObject("roRegion", b, 0, 0, 160, 90)
    if cam.roomNear <> invalid then cam.roomNear.SetScaleMode(0)
    cam.roomSmooth = CreateObject("roRegion", b, 0, 0, 160, 90)
    if cam.roomSmooth <> invalid then cam.roomSmooth.SetScaleMode(1)
end sub

sub CamBox(b as object, x as integer, y as integer, w as integer, h as integer, c as integer)
    b.DrawRect(x, y, w, h, c)
end sub

' A framed picture of a fish (he likes it).
sub CamPictureFrame(b as object, p as object)
    CamBox(b, 14, 10, 24, 18, p.frameWood)
    CamBox(b, 16, 12, 20, 14, p.sea)
    CamBox(b, 16, 21, 20, 3, p.deep)
    CamBox(b, 16, 24, 20, 2, p.sand)
    CamBox(b, 23, 15, 6, 1, p.fish)
    CamBox(b, 22, 16, 8, 3, p.fish)
    CamBox(b, 23, 19, 6, 1, p.fish)
    CamBox(b, 30, 16, 1, 3, p.fish)
    CamBox(b, 31, 15, 1, 2, p.fish)
    CamBox(b, 31, 18, 1, 2, p.fish)
    CamBox(b, 24, 16, 1, 1, p.black)
    CamBox(b, 19, 13, 1, 1, p.bubble)
    CamBox(b, 20, 15, 1, 1, p.bubble)
    CamBox(b, 34, 19, 1, 5, p.weed)
    CamBox(b, 33, 21, 1, 3, p.weed)
end sub

' A shelf with a plant and some books.
sub CamShelf(b as object, p as object)
    CamBox(b, 48, 26, 28, 1, p.shelfShadow)
    CamBox(b, 48, 24, 28, 2, p.shelf)
    CamBox(b, 51, 26, 2, 3, p.bracket)
    CamBox(b, 71, 26, 2, 3, p.bracket)
    CamBox(b, 50, 19, 7, 5, p.pot)
    CamBox(b, 49, 18, 9, 1, p.potRim)
    CamBox(b, 47, 12, 4, 3, p.leaf)
    CamBox(b, 52, 9, 3, 7, p.leaf)
    CamBox(b, 56, 11, 4, 3, p.leaf)
    CamBox(b, 49, 14, 9, 4, p.leaf)
    CamBox(b, 50, 15, 2, 2, p.leafDark)
    CamBox(b, 55, 14, 2, 2, p.leafDark)
    CamBox(b, 53, 10, 1, 4, p.leafDark)
    CamBox(b, 60, 16, 2, 8, p.book1)
    CamBox(b, 62, 15, 3, 9, p.book2)
    CamBox(b, 65, 17, 2, 7, p.book3)
    CamBox(b, 67, 16, 2, 8, p.book4)
    CamBox(b, 69, 20, 5, 4, p.book5)
end sub

' The window: the sky at this hour over the town's roofs, the frame's bars, the sill, and the
' curtains drawn back. The sky is cols 98 to 137, rows 9 to 39.
sub CamWindow(b as object, p as object, ph as string, hours as integer, mins as integer)
    sky = CamSkyColours(ph)
    CamBox(b, 96, 7, 44, 35, p.winFrame)
    bands = [[9, 7], [16, 7], [23, 8], [31, 9]]
    for i = 0 to 3
        CamBox(b, 98, bands[i][0], 40, bands[i][1], sky.bands[i])
    end for
    if ph = "day" then
        ' the sun crosses the window from 8 to 17; two clouds drift by, a pixel a minute
        frac = (hours + mins / 60 - 8) / 9
        CamSun(b, 99 + Int(frac * 34), 11 + Int(Abs(frac - 0.5) * 18), sky)
        CamCloud(b, 90 + (mins + 10) mod 56, 13, sky)
        CamCloud(b, 90 + (mins + 38) mod 56, 19, sky)
    else if ph = "dawn" then
        CamSun(b, 101, 25, sky)
    else if ph = "dusk" then
        CamSun(b, 131, 25, sky)
    else
        ' a few stars, and the moon across the window from 20 to 6
        for each star in [[101, 11], [106, 14], [111, 10], [121, 12], [127, 16], [133, 11], [103, 19], [114, 18], [130, 20], [136, 14]]
            CamBox(b, star[0], star[1], 1, 1, sky.star)
        end for
        frac = (((hours + 4) mod 24) + mins / 60) / 10
        if frac > 1 then frac = 1
        CamMoon(b, 99 + Int(frac * 34), 12 + Int(Abs(frac - 0.5) * 8), sky)
    end if
    for each house in [[98, 32, 5], [103, 29, 4], [107, 34, 4], [111, 27, 6], [119, 31, 5], [124, 28, 4], [128, 33, 5], [133, 30, 5]]
        CamBox(b, house[0], house[1], house[2], 40 - house[1], sky.town)
    end for
    if ph = "dusk" then
        for each lit in [[104, 31], [113, 30], [114, 33], [125, 30], [134, 32], [120, 34]]
            CamBox(b, lit[0], lit[1], 1, 1, sky.lit)
        end for
    end if
    CamBox(b, 98, 9, 40, 1, p.winShadow)
    CamBox(b, 98, 9, 1, 31, p.winShadow)
    CamBox(b, 117, 9, 2, 31, p.winFrame)
    CamBox(b, 98, 24, 40, 2, p.winFrame)
    CamBox(b, 94, 42, 48, 2, p.sill)
    CamBox(b, 95, 44, 46, 1, p.railShadow)
    CamBox(b, 88, 5, 10, 42, p.curtain)
    CamBox(b, 138, 5, 10, 42, p.curtain)
    for each fx in [90, 93, 96, 140, 143, 146]
        CamBox(b, fx, 6, 1, 41, p.fold)
    end for
    CamBox(b, 86, 4, 64, 1, p.rod)
    CamBox(b, 85, 3, 2, 3, p.finial)
    CamBox(b, 149, 3, 2, 3, p.finial)
end sub

' A rectangle cut to the sky's part of the window.
sub CamSkyBox(b as object, x as integer, y as integer, w as integer, h as integer, c as integer)
    x2 = x + w
    y2 = y + h
    if x < 98 then x = 98
    if y < 9 then y = 9
    if x2 > 138 then x2 = 138
    if y2 > 40 then y2 = 40
    if x2 > x and y2 > y then b.DrawRect(x, y, x2 - x, y2 - y, c)
end sub

sub CamSun(b as object, x as integer, y as integer, sky as object)
    CamSkyBox(b, x + 1, y, 3, 1, sky.sun)
    CamSkyBox(b, x, y + 1, 5, 3, sky.sun)
    CamSkyBox(b, x + 1, y + 4, 3, 1, sky.sun)
    CamSkyBox(b, x + 1, y + 1, 2, 2, sky.sunHi)
end sub

sub CamCloud(b as object, x as integer, y as integer, sky as object)
    CamSkyBox(b, x + 2, y, 4, 1, sky.cloud)
    CamSkyBox(b, x, y + 1, 9, 1, sky.cloud)
    CamSkyBox(b, x + 1, y + 2, 9, 1, sky.cloudShade)
end sub

' A crescent: the full moon, then the sky's own colour over most of it.
sub CamMoon(b as object, x as integer, y as integer, sky as object)
    CamSkyBox(b, x + 1, y, 3, 1, sky.moon)
    CamSkyBox(b, x, y + 1, 5, 3, sky.moon)
    CamSkyBox(b, x + 1, y + 4, 3, 1, sky.moon)
    dark = sky.bands[0]
    if y + 3 > 15 then dark = sky.bands[1]
    CamSkyBox(b, x + 2, y + 1, 3, 3, dark)
end sub

' The floorboards, and the sun through the window lying on them by day (low and orange at dusk).
sub CamFloor(b as object, p as object, ph as string)
    for board = 0 to 8
        y = 54 + board * 4
        c = p.floorA
        if board mod 2 = 1 then c = p.floorB
        CamBox(b, 0, y, 160, 3, c)
        CamBox(b, 0, y + 3, 160, 1, p.seam)
        jx = (board * 23 + 7) mod 40
        while jx < 160
            CamBox(b, jx, y, 1, 3, p.seam)
            jx = jx + 40
        end while
    end for
    if ph <> "day" and ph <> "dusk" then return
    warm = &hFFF0C8FF
    f = 0.26
    if ph = "dusk" then
        warm = &hFF9A50FF
        f = 0.3
    end if
    for y = 55 to 86
        board = (y - 54) \ 4
        x0 = Int(100 - (y - 54) * 0.9)
        x1 = Int(137 - (y - 54) * 0.55)
        if (y - 54) mod 4 = 3 then
            CamBox(b, x0, y, x1 - x0, 1, RgbMix(p.seam, warm, f))
        else
            c = p.floorA
            if board mod 2 = 1 then c = p.floorB
            CamBox(b, x0, y, x1 - x0, 1, RgbMix(c, warm, f))
            jx = (board * 23 + 7) mod 40
            while jx < 160
                if jx >= x0 and jx < x1 then CamBox(b, jx, y, 1, 1, RgbMix(p.seam, warm, f))
                jx = jx + 40
            end while
        end if
    end for
end sub

' His things: the scratching post (a toy hanging off it), the rug, his bed, his food and water,
' and a ball of yarn in the sun.
sub CamThings(b as object, p as object)
    CamBox(b, 149, 25, 7, 26, p.rope)
    for ry = 26 to 50 step 2
        CamBox(b, 149, ry, 7, 1, p.ropeLine)
    end for
    CamBox(b, 146, 22, 13, 3, p.postWood)
    CamBox(b, 146, 22, 13, 1, p.postHi)
    CamBox(b, 145, 51, 15, 4, p.postWood)
    CamBox(b, 145, 51, 15, 1, p.postHi)
    CamBox(b, 147, 25, 1, 8, p.toyString)
    CamBox(b, 146, 33, 3, 3, p.pom)

    CamBox(b, 30, 62, 44, 19, p.rugEdge)
    CamBox(b, 28, 63, 48, 17, p.rugEdge)
    CamBox(b, 30, 64, 44, 15, p.rug)
    CamBox(b, 32, 66, 40, 1, p.rugLine)
    CamBox(b, 32, 76, 40, 1, p.rugLine)
    for mx = 36 to 68 step 8
        CamBox(b, mx, 70, 3, 3, p.rugMark)
        CamBox(b, mx + 1, 69, 1, 5, p.rugMark)
    end for
    for fy = 64 to 78 step 2
        CamBox(b, 27, fy, 1, 1, p.rugEdge)
        CamBox(b, 76, fy, 1, 1, p.rugEdge)
    end for

    CamBox(b, 6, 76, 14, 1, p.bedOut)
    CamBox(b, 4, 77, 18, 1, p.bedOut)
    CamBox(b, 2, 78, 22, 8, p.bedOut)
    CamBox(b, 4, 86, 18, 1, p.bedOut)
    CamBox(b, 6, 87, 14, 1, p.bedOut)
    CamBox(b, 6, 77, 14, 1, p.bedRim)
    CamBox(b, 7, 79, 12, 1, p.bedIn)
    CamBox(b, 6, 80, 14, 4, p.bedIn)
    CamBox(b, 7, 84, 12, 1, p.bedIn)

    CamBox(b, 144, 77, 4, 1, p.kibble)
    CamBox(b, 143, 78, 6, 1, p.kibble)
    CamBox(b, 144, 78, 1, 1, p.kibbleHi)
    CamBox(b, 147, 77, 1, 1, p.kibbleHi)
    CamBox(b, 140, 79, 12, 1, p.bowlRim)
    CamBox(b, 141, 80, 10, 3, p.bowl)
    CamBox(b, 142, 83, 8, 1, p.bowlBase)
    CamBox(b, 125, 81, 10, 1, p.dishRim)
    CamBox(b, 126, 82, 8, 1, p.water)
    CamBox(b, 128, 82, 2, 1, p.waterHi)
    CamBox(b, 126, 83, 8, 2, p.dish)
    CamBox(b, 127, 85, 6, 1, p.dishBase)

    CamBox(b, 109, 70, 4, 1, p.yarn)
    CamBox(b, 108, 71, 6, 4, p.yarn)
    CamBox(b, 109, 75, 4, 1, p.yarn)
    CamBox(b, 109, 72, 4, 1, p.yarnLine)
    CamBox(b, 110, 74, 3, 1, p.yarnLine)
    CamBox(b, 104, 75, 4, 1, p.yarn)
    CamBox(b, 101, 76, 3, 1, p.yarn)
    CamBox(b, 99, 76, 1, 1, p.yarn)
end sub

' --- Drawing ---

' The full screen on channel 9: the room stretched over it, what moves, and the camera's words.
' The scene is all solid colours, so it's drawn without blending (quicker, as the games' own
' full-screen pictures are); the words are see-through pictures, blended.
sub DrawCamScreen(st as object)
    s = st.screen
    cam = st.cam
    if s = invalid or cam = invalid then return
    s.SetAlphaEnable(false)
    CamDrawScene(st, cam, 0, 0, st.k, invalid, false)
    s.SetAlphaEnable(true)
    CamDrawWords(st, cam)
    s.SwapBuffers()
end sub

' The scene with the grid's corner at x0, y0, sc screen pixels a grid unit. clip ([left, top,
' right, bottom]) keeps what moves inside the guide's preview box; smooth stretches the room
' smoothly (the preview's size isn't a whole number of times the room's).
sub CamDrawScene(st as object, cam as object, x0 as integer, y0 as integer, sc as dynamic, clip as dynamic, smooth as boolean)
    s = st.screen
    room = cam.roomNear
    if smooth then room = cam.roomSmooth
    if room <> invalid then
        s.DrawScaledObject(x0, y0, 8 * sc, 8 * sc, room)
    else
        ' no picture memory for the room: a dark one, so what moves never leaves trails
        s.DrawRect(x0, y0, Int(1280 * sc + 0.5), Int(720 * sc + 0.5), &h101410FF)
    end if
    ink = cam.ink
    px = cam.px
    if px <> invalid then
        if px.DoesExist("treat") then
            CamTreat(s, clip, x0, y0, sc, px.x, px.y, 1.0, ink)
        else
            pw = Int(8 * sc + 0.5)
            CamFill(s, clip, x0 + Int(px.x * sc), y0 + Int(px.y * sc), pw, pw, px.color)
        end if
    end if
    if cam.pop <> invalid then CamPop(s, clip, x0, y0, sc, cam.pop)
    CamDrawRukus(s, clip, cam, x0, y0, sc)
    tz = cam.toss
    if tz <> invalid then
        ' from below the camera, in an arc, smaller as it flies off
        q = tz.t / tz.dur
        if q > 1 then q = 1
        tx = 640 + (tz.x1 - 640) * q
        ty = 770 + (tz.y1 - 770) * q - 1200 * q * (1 - q)
        CamTreat(s, clip, x0, y0, sc, tx, ty, 1 + 1.4 * (1 - q), ink)
    end if
    CamZs(s, clip, x0, y0, sc, cam.zs, ink.z)
    CamHearts(s, clip, x0, y0, sc, cam.hearts, ink.heart)
end sub

' A rectangle, cut to clip when there is one.
sub CamFill(s as object, clip as dynamic, x as integer, y as integer, w as integer, h as integer, c as integer)
    if clip <> invalid then
        x2 = x + w
        y2 = y + h
        if x < clip[0] then x = clip[0]
        if y < clip[1] then y = clip[1]
        if x2 > clip[2] then x2 = clip[2]
        if y2 > clip[3] then y2 = clip[3]
        w = x2 - x
        h = y2 - y
    end if
    if w > 0 and h > 0 then s.DrawRect(x, y, w, h, c)
end sub

sub CamDrawRukus(s as object, clip as dynamic, cam as object, x0 as integer, y0 as integer, sc as dynamic)
    r = cam.r
    if r.state = "away" or cam.runs = invalid then return
    side = "R"
    if r.faceLeft then side = "L"
    runs = cam.runs[RukusPose(r) + side]
    if runs = invalid then return
    u = 8 * sc
    ox = x0 + Int(r.x * sc + 0.5)
    oy = y0 + Int((r.floor - 128 - r.lift) * sc + 0.5)
    for each seg in runs
        xa = ox + Int(seg[0] * u + 0.5)
        xb = ox + Int((seg[0] + seg[2]) * u + 0.5)
        ya = oy + Int(seg[1] * u + 0.5)
        yb = oy + Int((seg[1] + 1) * u + 0.5)
        CamFill(s, clip, xa, ya, xb - xa, yb - ya, seg[3])
    end for
end sub

' A treat at gx, gy on the grid (its size times bigger, about its middle, while it flies).
sub CamTreat(s as object, clip as dynamic, x0 as integer, y0 as integer, sc as dynamic, gx as dynamic, gy as dynamic, size as dynamic, ink as object)
    w = 16 * size
    h = 10 * size
    x = x0 + Int((gx - (w - 16) / 2) * sc)
    y = y0 + Int((gy - (h - 10) / 2) * sc)
    CamFill(s, clip, x, y, Int(w * sc + 0.5), Int(h * sc + 0.5), ink.treat)
    CamFill(s, clip, x + Int(2 * size * sc), y + Int(2 * size * sc), Int(6 * size * sc + 0.5), Int(3 * size * sc + 0.5), ink.treatHi)
end sub

' The pounce's sparks (or a treat's crumbs): four flying up and out, shrinking.
sub CamPop(s as object, clip as dynamic, x0 as integer, y0 as integer, sc as dynamic, p as object)
    spread = 6 + p.t * 150
    size = 7 * (1 - p.t / 0.5) + 1
    n = Int(size * sc + 0.5)
    for each d in [[-1, -0.6], [1, -0.6], [-0.4, -1.2], [0.4, -1.2]]
        gx = p.x + 4 + d[0] * spread - size / 2
        gy = p.y + 4 + d[1] * spread - size / 2
        CamFill(s, clip, x0 + Int(gx * sc), y0 + Int(gy * sc), n, n, p.color)
    end for
end sub

' His z's as he naps, each four rows of a little pixel letter.
sub CamZs(s as object, clip as dynamic, x0 as integer, y0 as integer, sc as dynamic, zs as object, c as integer)
    u = Int(3 * sc + 0.5)
    if u < 1 then u = 1
    for each zee in zs
        x = x0 + Int(zee.x * sc)
        y = y0 + Int(zee.y * sc)
        CamFill(s, clip, x, y, 4 * u, u, c)
        CamFill(s, clip, x + 2 * u, y + u, u, u, c)
        CamFill(s, clip, x + u, y + 2 * u, u, u, c)
        CamFill(s, clip, x, y + 3 * u, 4 * u, u, c)
    end for
end sub

' A pixel heart over him after a treat, floating up.
sub CamHearts(s as object, clip as dynamic, x0 as integer, y0 as integer, sc as dynamic, hearts as object, c as integer)
    u = Int(4 * sc + 0.5)
    if u < 1 then u = 1
    for each heart in hearts
        x = x0 + Int(heart.x * sc)
        y = y0 + Int(heart.y * sc)
        CamFill(s, clip, x + u, y, u, u, c)
        CamFill(s, clip, x + 3 * u, y, u, u, c)
        CamFill(s, clip, x, y + u, 5 * u, u, c)
        CamFill(s, clip, x + u, y + 2 * u, 3 * u, u, c)
        CamFill(s, clip, x + 2 * u, y + 3 * u, u, u, c)
    end for
end sub

' --- The camera's words ---

function CamFonts(st as object) as object
    key = Str(st.k)
    if st.camFonts = invalid or st.camFontsKey <> key then
        k = st.k
        st.camFonts = {
            word: Vt323(st, Z(44, k)),
            time: Vt323(st, Z(38, k)),
            hint: Vt323(st, Z(30, k)),
            little: Vt323(st, Z(24, k))
        }
        st.camFontsKey = key
    end if
    return st.camFonts
end function

' A line of the camera's words as a see-through picture: white with a hard black shadow.
function CamWordPic(st as object, text as string, font as object) as dynamic
    off = Z(3, st.k)
    if off < 2 then off = 2
    w = font.GetOneLineWidth(text, 99999) + off + 2
    h = font.GetOneLineHeight() + off + 2
    pic = NewPicture(w, h, true)
    if pic = invalid then return invalid
    pic.Clear(&h00000000)
    DrawShadowed(pic, text, 1, 1, &hF2F2F2FF, &h000000FF, font, off)
    return pic
end function

' A line's picture, or none while the TV is short of picture memory (then no picture is asked
' for again for 5 seconds, and the line is left out meanwhile).
function CamPicOrNone(st as object, text as string, font as object) as dynamic
    if PicturesFailing(st) then return invalid
    pic = CamWordPic(st, text, font)
    if pic = invalid then PictureFailed(st)
    return pic
end function

' A word kept from frame to frame, by name and text (made again at another screen size). If the
' TV is short of picture memory, the word is left out and not asked for again for 5 seconds.
function CamWord(st as object, cam as object, name as string, text as string, font as object) as dynamic
    if cam.wordsK <> st.k then
        cam.words = {}
        cam.wordsK = st.k
    end if
    key = name + "|" + text
    pic = cam.words.Lookup(key)
    if pic = invalid then
        if PicturesFailing(st) then return invalid
        pic = CamWordPic(st, text, font)
        if pic = invalid then
            PictureFailed(st)
        else
            if cam.words.Count() > 24 then cam.words.Clear()
            cam.words.AddReplace(key, pic)
        end if
    end if
    return pic
end function

' CAM 1 (and IR at night), REC with its dot blinking, the date and time, and what the keys do
' for 7 seconds after one. The date and time and the keys' line each have one picture, made
' again when their words change.
sub CamDrawWords(st as object, cam as object)
    s = st.screen
    k = st.k
    f = CamFonts(st)
    top = Z(24, k)
    lx = Z(36, k)
    rx = Z(1244, k)
    camPic = CamWord(st, cam, "cam", "CAM 1", f.word)
    if camPic <> invalid then
        s.DrawObject(lx, top, camPic)
        if cam.phase = "night" then
            irPic = CamWord(st, cam, "ir", "IR", f.word)
            if irPic <> invalid then s.DrawObject(lx + camPic.GetWidth() + Z(14, k), top, irPic)
        end if
    end if
    recPic = CamWord(st, cam, "rec", "REC", f.word)
    if recPic <> invalid then
        recX = rx - recPic.GetWidth()
        s.DrawObject(recX, top, recPic)
        if cam.blink.TotalMilliseconds() mod 1000 < 600 then
            d = Z(22, k)
            CamDot(s, recX - d - Z(10, k), top + (f.word.GetOneLineHeight() - d) \ 2, d)
        end if
    end if
    ' each line is made again when its words change, or while it has no picture (CamPicOrNone
    ' asks at most once in 5 s while the TV is short of picture memory)
    text = st.clock.isoDate + "  " + ClockText(st, true)
    if text <> cam.timeText or cam.timePicK <> k or cam.timePic = invalid then
        cam.timeText = text
        cam.timePicK = k
        cam.timePic = CamPicOrNone(st, text, f.time)
    end if
    floorY = Z(698, k)
    if cam.timePic <> invalid then s.DrawObject(lx, floorY - cam.timePic.GetHeight(), cam.timePic)
    if cam.hintTimer.TotalMilliseconds() < 7000 then
        hint = CamHintText(cam)
        if hint <> cam.hintText or cam.hintPicK <> k or cam.hintPic = invalid then
            cam.hintText = hint
            cam.hintPicK = k
            cam.hintPic = CamPicOrNone(st, hint, f.hint)
        end if
        if cam.hintPic <> invalid then s.DrawObject(rx - cam.hintPic.GetWidth(), floorY - cam.hintPic.GetHeight(), cam.hintPic)
    end if
end sub

' REC's red dot, d across, with a shadow: three overlapping rectangles make it round.
sub CamDot(s as object, x as integer, y as integer, d as integer)
    a = (d * 3) \ 10
    e = d \ 10
    sh = d \ 8
    if sh < 1 then sh = 1
    for each pass in [[&h000000FF, sh], [&hFF2A2AFF, 0]]
        c = pass[0]
        o = pass[1]
        s.DrawRect(x + o + a, y + o, d - 2 * a, d, c)
        s.DrawRect(x + o + e, y + o + e, d - 2 * e, d - 2 * e, c)
        s.DrawRect(x + o, y + o + a, d, d - 2 * a, c)
    end for
end sub

' The guide's preview: the camera's picture, live, as big as fits in the box, with CAM 1 and
' REC small. It moves on by the time since it was last drawn (about 10 times a second).
sub DrawCamPreview(st as object, x as integer, y as integer, w as integer, h as integer)
    s = st.screen
    k = st.k
    cam = CamState(st)
    if cam.clockTimer.TotalMilliseconds() >= 1000 then
        cam.clockTimer.Mark()
        CamChores(st, cam)
    end if
    dt = cam.frame.TotalMilliseconds() / 1000
    cam.frame.Mark()
    if dt > 0.15 then dt = 0.15
    CamStep(cam, dt)
    if st.camDemoTimer = invalid then st.camDemoTimer = CreateObject("roTimespan")
    st.camDemoTimer.Mark()

    sc = w / 1280
    if h / 720 < sc then sc = h / 720
    dw = Int(1280 * sc + 0.5)
    dh = Int(720 * sc + 0.5)
    x0 = x + (w - dw) \ 2
    y0 = y + (h - dh) \ 2
    ' solid colours, unblended, as on the full screen; the guide blends again after it
    s.SetAlphaEnable(false)
    if dw < w or dh < h then s.DrawRect(x, y, w, h, &h000000FF)
    CamDrawScene(st, cam, x0, y0, sc, [x0, y0, x0 + dw, y0 + dh], true)
    s.SetAlphaEnable(true)

    f = CamFonts(st)
    pad = Z(12, k)
    sh = Z(2, k)
    DrawShadowed(s, "CAM 1", x0 + pad, y0 + pad, &hF2F2F2FF, &h000000FF, f.little, sh)
    recX = x0 + dw - pad - f.little.GetOneLineWidth("REC", 99999)
    DrawShadowed(s, "REC", recX, y0 + pad, &hF2F2F2FF, &h000000FF, f.little, sh)
    if cam.blink.TotalMilliseconds() mod 1000 < 600 then
        d = Z(14, k)
        CamDot(s, recX - d - Z(8, k), y0 + pad + (f.little.GetOneLineHeight() - d) \ 2, d)
    end if
end sub
