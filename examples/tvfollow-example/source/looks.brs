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
    if look = "rgb" then return ["Rainbow Wave", "Neon Pulse", "Aurora Wave", "Spectrum Cycle"]
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

' The RGB LED lights look's four colourways: Rainbow Wave, Neon Pulse, Aurora Wave, Spectrum Cycle.
function RgbKit(index as integer) as object
    sets = [
        {
            bg: &h07090EFF,
            well: &h020306FF,
            card: &h0E1320FF,
            highlight: &h182236FF,
            track: &h141A28FF,
            rule: &h1E283CFF,
            frame: &h1E283CFF,
            red: &hFF0033FF,
            green: &h00FF66FF,
            blue: &h0088FFFF,
            accent: &hFFCC00FF,
            white: &hF4F8FFFF,
            soft: &hC8D4E8FF,
            dim: &h64748CFF,
            noise: [&h07090EFF, &h1E283CFF, &h0088FFFF, &hF4F8FFFF]
        },
        {
            bg: &h0A0612FF,
            well: &h040208FF,
            card: &h160D26FF,
            highlight: &h281642FF,
            track: &h221238FF,
            rule: &h3A1E60FF,
            frame: &h3A1E60FF,
            red: &hFF007FFF,
            green: &h39FF14FF,
            blue: &h00F0FFFF,
            accent: &hFFD700FF,
            white: &hFFFFFFFF,
            soft: &hE0D0F8FF,
            dim: &h7C5E9EFF,
            noise: [&h0A0612FF, &h3A1E60FF, &hFF007FFF, &h00F0FFFF]
        },
        {
            bg: &h050D12FF,
            well: &h020608FF,
            card: &h0B1820FF,
            highlight: &h122632FF,
            track: &h0F202AFF,
            rule: &h1A3644FF,
            frame: &h1A3644FF,
            red: &h00FFAAFF,
            green: &h00FF66FF,
            blue: &h0088FFFF,
            accent: &h00EEFFFF,
            white: &hE8F8FAFF,
            soft: &hB4D8E2FF,
            dim: &h587888FF,
            noise: [&h050D12FF, &h1A3644FF, &h00FFAAFF, &hE8F8FAFF]
        },
        {
            bg: &h06070AFF,
            well: &h020204FF,
            card: &h0C0E16FF,
            highlight: &h161A28FF,
            track: &h121520FF,
            rule: &h1E2232FF,
            frame: &h1E2232FF,
            red: &hFF3333FF,
            green: &h33FF66FF,
            blue: &h3388FFFF,
            accent: &hFFAA00FF,
            white: &hF0F4FCFF,
            soft: &hC0C8D8FF,
            dim: &h606878FF,
            noise: [&h06070AFF, &h1E2232FF, &hFFAA00FF, &hF0F4FCFF]
        }
    ]
    idx = index mod sets.Count()
    if idx < 0 then idx = idx + sets.Count()
    return sets[idx]
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
