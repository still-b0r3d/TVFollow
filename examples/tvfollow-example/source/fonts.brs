' The fonts: Barlow Condensed in four weights, VT323, Silkscreen and W95FA, all free under the
' SIL Open Font License (each folder in fonts/ carries its OFL.txt). Roku's own font stands in
' for any that doesn't load.

function RegisterFonts() as object
    reg = CreateObject("roFontRegistry")
    for each file in FontFiles()
        if not reg.Register(file) then print "b0r3d TV: could not load the font "; file
    end for
    return reg
end function

function FontFiles() as object
    return [
        "pkg:/fonts/barlow-condensed/BarlowCondensed-Medium.ttf",
        "pkg:/fonts/barlow-condensed/BarlowCondensed-SemiBold.ttf",
        "pkg:/fonts/barlow-condensed/BarlowCondensed-Bold.ttf",
        "pkg:/fonts/barlow-condensed/BarlowCondensed-ExtraBold.ttf",
        "pkg:/fonts/vt323/VT323-Regular.ttf",
        "pkg:/fonts/silkscreen/Silkscreen-Regular.ttf",
        "pkg:/fonts/silkscreen/Silkscreen-Bold.ttf",
        "pkg:/fonts/w95fa/W95FA.otf"
    ]
end function

' The family names the TV really registered. Roku can say yes to a file it didn't load, so
' every font is looked up by these names.
function FontFamilies(reg as object) as object
    names = []
    try
        for each name in reg.GetFamilies()
            names.Push(name)
        end for
    catch e
        print "b0r3d TV: could not list the fonts: "; e.message
    end try
    return names
end function

' Barlow Condensed in one of its weights: medium, semibold, bold or extrabold. Each weight
' registers as its own family, except Bold, which is the family's bold face.
function Barlow(st as object, weight as string, size as integer) as object
    heavy = (weight = "bold" or weight = "extrabold")
    family = "Barlow Condensed"
    bold = true
    if weight = "medium" then
        family = "Barlow Condensed Medium"
        bold = false
    else if weight = "semibold" then
        family = "Barlow Condensed SemiBold"
        bold = false
    else if weight = "extrabold" then
        family = "Barlow Condensed ExtraBold"
        bold = false
    end if
    font = FontOf(st, family, size, bold)
    if font = invalid then font = FontOf(st, "Barlow Condensed", size, heavy)
    if font = invalid then font = st.fontReg.GetDefaultFont(size, heavy, false)
    return font
end function

function Vt323(st as object, size as integer) as object
    font = FontOf(st, "VT323", size, false)
    if font = invalid then font = st.fontReg.GetDefaultFont(size, false, false)
    return font
end function

' W95FA, the b0r3d 95 look's pixel font. It has one weight.
function W95(st as object, size as integer) as object
    font = FontOf(st, "W95FA", size, false)
    if font = invalid then font = st.fontReg.GetDefaultFont(size, false, false)
    return font
end function

' Silkscreen is drawn on an 8-pixel grid, so it's only sharp at multiples of 8 pixels.
function Silk(st as object, size as integer, bold as boolean) as object
    px = Int(size / 8 + 0.5) * 8
    if px < 8 then px = 8
    font = FontOf(st, "Silkscreen", px, bold)
    if font = invalid then font = st.fontReg.GetDefaultFont(px, bold, false)
    return font
end function

function FontOf(st as object, family as string, size as integer, bold as boolean) as dynamic
    if not HasName(st.families, family) then return invalid
    return st.fontReg.GetFont(family, size, bold, false)
end function

function HasName(list as object, name as string) as boolean
    for each item in list
        if LCase(item) = LCase(name) then return true
    end for
    return false
end function
