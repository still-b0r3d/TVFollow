' The looks. Each one has its own guide layout; every other screen (Settings, the channel list,
' the TV check) shares one layout painted in the look's own colours and fonts (panel.brs).

function LookList() as object
    return ["cable", "b95", "phosphor", "vcr", "rgb"]
end function

function IsLook(look as string) as boolean
    for each name in LookList()
        if name = look then return true
    end for
    return false
end function

function LookTitle(look as string) as string
    if look = "b95" then return "b0r3d 95"
    if look = "phosphor" then return "Phosphor terminal"
    if look = "vcr" then return "VCR menu"
    if look = "rgb" then return "RGB LED lights"
    return "Late-night cable guide"
end function

' The colour choices of a look. The cable guide has its one blue.
function LookColourNames(look as string) as object
    if look = "b95" then return ["Teal", "Blue", "Slate"]
    if look = "phosphor" then return ["Green", "Amber", "Cyan"]
    if look = "vcr" then return ["Blue", "Black", "Green"]
    if look = "rgb" then return ["Rainbow wave", "Neon", "Aurora", "Colour cycle"]
    return []
end function

' Loads the current look: its colours (st.kit) and its fonts at the screen's scale (st.fonts).
sub LoadLook(st as object)
    st.colour = ColourIndex(st.look)
    st.scanBmp = invalid
    if st.look = "b95" then
        st.kit = B95Kit(st.colour)
        st.fonts = B95Fonts(st)
    else if st.look = "phosphor" then
        st.kit = PhosphorKit(st.colour)
        st.fonts = PhosphorFonts(st)
    else if st.look = "vcr" then
        st.kit = VcrKit(st.colour)
        st.fonts = VcrFonts(st)
    else if st.look = "rgb" then
        st.kit = RgbKit(st.colour)
        st.fonts = RgbFonts(st)
    else
        st.kit = CableKit()
        st.fonts = CableFonts(st)
    end if
    ' the RGB look's pictures take a few megabytes; another look lets them go. Its text pictures
    ' are made again in the new fonts.
    if st.look = "rgb" then
        st.rgbText = invalid
    else
        ForgetRgbArt(st)
    end if
end sub

sub DrawGuideFor(st as object)
    if st.look = "b95" then
        DrawB95Guide(st)
    else if st.look = "phosphor" then
        DrawPhosphorGuide(st)
    else if st.look = "vcr" then
        DrawVcrGuide(st)
    else if st.look = "rgb" then
        DrawRgbGuide(st)
    else
        DrawCableGuide(st)
    end if
end sub

function CableKit() as object
    return {
        bg: &h0A1640FF,
        tile: &h0D1D52FF,
        frame: &hC9D6FFFF,
        text: &hEEF3FFFF,
        soft: &hC9D6FFFF,
        white: &hFFFFFFFF,
        accent: &hFFD23FFF,
        ink: &h0A1640FF,
        cell1: &h173A9CFF,
        cell2: &h1D46B8FF,
        cell3: &h122F86FF,
        bar: &h06102EFF,
        dimText: &h7F8FC0FF,
        noise: [&h0A1640FF, &h3A4A7AFF, &h8C9AC8FF, &hE6ECFFFF]
    }
end function

' One glowing colour on black, mixed into the shades the look uses.
function PhosphorKit(index as integer) as object
    sets = [
        { full: &h5CFF7AFF, soft: &h45BD5AFF, dim: &h42B557FF, glow: &h338B43FF, lead: &h215A2BFF, faint: &h0B1A0DFF, fur: &h235E2DFF, stripe: &h399C4BFF, nose: &h4ACD62FF },
        { full: &hFFB020FF, soft: &hBC8319FF, dim: &hB47E18FF, glow: &h8A6113FF, lead: &h583F0DFF, faint: &h181406FF, fur: &h5C420EFF, stripe: &h9B6C15FF, nose: &hCD8E1AFF },
        { full: &h5EE6FFFF, soft: &h46AABCFF, dim: &h43A4B4FF, glow: &h347E8AFF, lead: &h225158FF, faint: &h0B1818FF, fur: &h24555CFF, stripe: &h3A8D9BFF, nose: &h4CB9CDFF }
    ]
    kit = sets[index]
    kit.bg = &h040704FF
    kit.noise = [kit.bg, kit.lead, kit.dim, kit.full]
    return kit
end function

function VcrKit(index as integer) as object
    screens = [&h1734E8FF, &h000000FF, &h0B6B3AFF]
    return {
        bg: screens[index],
        white: &hFFFFFFFF,
        shadow: &h000000FF,
        yellow: &hFFE94AFF,
        green: &h5CFF7AFF,
        pip: &h2A46FFFF,
        grey: &hA8A8A8FF,
        noise: [&h1A1A1AFF, &h555555FF, &h9A9A9AFF, &hE6E6E6FF]
    }
end function

' The RGB LED lights look's colours: a dark wall, the TV and the panel a shade darker, white and
' grey type. The light itself comes from the colourway's pictures (rgb.brs); a kit only carries
' the colours the games and the other screens use, so red, green and blue follow the colourway:
' Rainbow wave, Neon, Aurora, Colour cycle.
function RgbKit(index as integer) as object
    ' red, green, blue and the accent for each colourway, in that order
    picks = [
        [&hFF3346FF, &h3EF07AFF, &h2F7DFFFF, &hFFBE2AFF],
        [&hFF2FD2FF, &h20D6FFFF, &h7449FFFF, &h20D6FFFF],
        [&h7258FFFF, &h3BFF84FF, &h1ED9C8FF, &h3BFF84FF],
        [&hFF3346FF, &h3EF07AFF, &h2F7DFFFF, &hFFBE2AFF]
    ]
    idx = index mod picks.Count()
    if idx < 0 then idx = idx + picks.Count()
    p = picks[idx]
    return {
        bg: &h111216FF,
        well: &h0C0D11FF,
        card: &h0B0C0FFF,
        highlight: &h1B1D24FF,
        track: &h17181DFF,
        rule: &h232429FF,
        frame: &h07080AFF,
        red: p[0],
        green: p[1],
        blue: p[2],
        accent: p[3],
        white: &hFFFFFFFF,
        text: &hECEEF2FF,
        soft: &hB9BDC7FF,
        dim: &h80858FFF,
        noise: [&h111216FF, &h2A2C33FF, &h80858FFF, &hECEEF2FF]
    }
end function

' Rukus in the look's colours: his own fur, or shades of the phosphor.
function RukusColorsFor(st as object) as object
    if st.look <> "phosphor" then return RukusColors()
    k = st.kit
    colors = {}
    colors.AddReplace("k", k.full)
    colors.AddReplace("1", k.fur)
    colors.AddReplace("O", k.fur)
    colors.AddReplace("T", k.fur)
    colors.AddReplace("V", k.fur)
    colors.AddReplace("2", k.stripe)
    colors.AddReplace("U", k.stripe)
    colors.AddReplace("3", k.full)
    colors.AddReplace("s", k.nose)
    return colors
end function

' The time as the Clock setting wants it, with or without seconds.
function ClockText(st as object, seconds as boolean) as string
    c = st.clock
    if st.clock24 then
        if seconds then return c.time24 + ":" + c.ss
        return c.time24
    end if
    if seconds then return c.hm12 + ":" + c.ss + " " + c.half
    return c.time
end function
