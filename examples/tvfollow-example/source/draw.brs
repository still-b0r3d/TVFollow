' Drawing helpers shared by every screen. Symbols the fonts don't have (arrows, the star on
' the remote's options key, the play triangle) are drawn as shapes.

' Text with extra room between the letters, like letter-spacing on a web page.
sub DrawSpaced(s as object, text as string, x as integer, y as integer, color as dynamic, font as object, sp as integer)
    if sp <= 0 then
        s.DrawText(text, x, y, color, font)
        return
    end if
    cx = x
    for i = 1 to Len(text)
        ch = Mid(text, i, 1)
        s.DrawText(ch, cx, y, color, font)
        cx = cx + font.GetOneLineWidth(ch, 99999) + sp
    end for
end sub

function SpacedWidth(text as string, font as object, sp as integer) as integer
    if sp <= 0 then return font.GetOneLineWidth(text, 99999)
    n = Len(text)
    w = 0
    for i = 1 to n
        w = w + font.GetOneLineWidth(Mid(text, i, 1), 99999)
    end for
    if n > 1 then w = w + sp * (n - 1)
    return w
end function

' Right edge at rx.
sub DrawRight(s as object, text as string, rx as integer, y as integer, color as dynamic, font as object)
    s.DrawText(text, rx - font.GetOneLineWidth(text, 99999), y, color, font)
end sub

sub DrawRightSpaced(s as object, text as string, rx as integer, y as integer, color as dynamic, font as object, sp as integer)
    DrawSpaced(s, text, rx - SpacedWidth(text, font, sp), y, color, font, sp)
end sub

' Centred in a box.
sub DrawCentered(s as object, text as string, x as integer, y as integer, w as integer, h as integer, color as dynamic, font as object)
    DrawCenteredSpaced(s, text, x, y, w, h, color, font, 0)
end sub

sub DrawCenteredSpaced(s as object, text as string, x as integer, y as integer, w as integer, h as integer, color as dynamic, font as object, sp as integer)
    tw = SpacedWidth(text, font, sp)
    th = font.GetOneLineHeight()
    DrawSpaced(s, text, x + (w - tw) \ 2, y + (h - th) \ 2, color, font, sp)
end sub

' Left edge at x, centred on a box's height.
sub DrawMiddle(s as object, text as string, x as integer, y as integer, h as integer, color as dynamic, font as object)
    DrawMiddleSpaced(s, text, x, y, h, color, font, 0)
end sub

sub DrawMiddleSpaced(s as object, text as string, x as integer, y as integer, h as integer, color as dynamic, font as object, sp as integer)
    DrawSpaced(s, text, x, y + (h - font.GetOneLineHeight()) \ 2, color, font, sp)
end sub

' Draws one piece of text and returns where the next one starts.
function DrawRun(s as object, x as integer, y as integer, text as string, color as dynamic, font as object, sp as integer, gap as integer) as integer
    DrawSpaced(s, text, x, y, color, font, sp)
    return x + SpacedWidth(text, font, sp) + gap
end function

' Cuts text that doesn't fit, ending it with "…".
function TrimToWidth(text as string, font as object, maxW as integer) as string
    if font.GetOneLineWidth(text, 99999) <= maxW then return text
    t = text
    while Len(t) > 1
        t = Left(t, Len(t) - 1)
        if font.GetOneLineWidth(t + "…", 99999) <= maxW then return t + "…"
    end while
    return t
end function

' Splits text into lines that fit, at the spaces.
function WrapText(text as string, font as object, maxW as integer) as object
    lines = []
    line = ""
    for each word in text.Split(" ")
        attempt = word
        if line <> "" then attempt = line + " " + word
        if line = "" or font.GetOneLineWidth(attempt, 99999) <= maxW then
            line = attempt
        else
            lines.Push(line)
            line = word
        end if
    end for
    if line <> "" or lines.Count() = 0 then lines.Push(line)
    return lines
end function

sub DrawFrame(s as object, x as integer, y as integer, w as integer, h as integer, t as integer, color as dynamic)
    s.DrawRect(x, y, w, t, color)
    s.DrawRect(x, y + h - t, w, t, color)
    s.DrawRect(x, y, t, h, color)
    s.DrawRect(x + w - t, y, t, h, color)
end sub

' A chevron (^ or v), centred on cx, cy, each arm reaching arm pixels to the side.
sub DrawChevron(s as object, cx as integer, cy as integer, arm as integer, thick as integer, up as boolean, color as dynamic)
    h = thick \ 2
    for i = 0 to arm
        if up then
            y = cy - arm \ 2 + i
        else
            y = cy + arm \ 2 - i
        end if
        s.DrawRect(cx - i - h, y - h, thick, thick, color)
        s.DrawRect(cx + i - h, y - h, thick, thick, color)
    end for
end sub

' The remote's options key: a six-spoked star.
sub DrawStar(s as object, cx as integer, cy as integer, r as integer, thick as integer, color as dynamic)
    h = thick \ 2
    for each degrees in [90, 30, 150]
        rad = degrees * 3.14159265 / 180
        dx = Cos(rad)
        dy = Sin(rad)
        for t = -r to r
            s.DrawRect(cx + Int(t * dx + 0.5) - h, cy - Int(t * dy + 0.5) - h, thick, thick, color)
        end for
    end for
end sub

' Text with a soft glow (the phosphor look): the glow colour a step to each side, then the text.
sub DrawGlow(s as object, text as string, x as integer, y as integer, color as dynamic, glowColor as dynamic, font as object, off as integer)
    if off > 0 then
        s.DrawText(text, x - off, y, glowColor, font)
        s.DrawText(text, x + off, y, glowColor, font)
    end if
    s.DrawText(text, x, y, color, font)
end sub

sub DrawGlowSpaced(s as object, text as string, x as integer, y as integer, color as dynamic, glowColor as dynamic, font as object, off as integer, sp as integer)
    if off > 0 then
        DrawSpaced(s, text, x - off, y, glowColor, font, sp)
        DrawSpaced(s, text, x + off, y, glowColor, font, sp)
    end if
    DrawSpaced(s, text, x, y, color, font, sp)
end sub

' A glowing piece of text in the kit's colours; returns where the next one starts.
function DrawGlowRun(s as object, x as integer, y as integer, text as string, kit as object, font as object, off as integer, gap as integer) as integer
    DrawGlow(s, text, x, y, kit.full, kit.glow, font, off)
    return x + font.GetOneLineWidth(text, 99999) + gap
end function

' A dotted line from x1 to x2: squares of size pixels, one every stepPx.
sub DrawDots(s as object, x1 as integer, x2 as integer, y as integer, size as integer, stepPx as integer, color as dynamic)
    x = x1
    while x + size <= x2
        s.DrawRect(x, y, size, size, color)
        x = x + stepPx
    end while
end sub

' Text with a hard shadow down and to the right (the VCR look).
sub DrawShadowed(s as object, text as string, x as integer, y as integer, color as dynamic, shadow as dynamic, font as object, off as integer)
    s.DrawText(text, x + off, y + off, shadow, font)
    s.DrawText(text, x, y, color, font)
end sub

sub DrawShadowedRight(s as object, text as string, rx as integer, y as integer, color as dynamic, shadow as dynamic, font as object, off as integer)
    DrawShadowed(s, text, rx - font.GetOneLineWidth(text, 99999), y, color, shadow, font, off)
end sub

' A filled triangle pointing right, w wide and h tall, with its left edge at x and top at y.
sub DrawTriangleRight(s as object, x as integer, y as integer, w as integer, h as integer, color as dynamic)
    for col = 0 to w - 1
        tall = Int(h * (w - col) / w + 0.5)
        if tall > 0 then s.DrawRect(x + col, y + (h - tall) \ 2, 1, tall, color)
    end for
end sub

' The remote's play key: a triangle pointing right, size pixels tall.
sub DrawPlay(s as object, x as integer, cy as integer, size as integer, color as dynamic)
    w = Int(size * 0.87 + 0.5)
    if w < 1 then w = 1
    for col = 0 to w - 1
        tall = Int(size * (w - col) / w + 0.5)
        if tall > 0 then s.DrawRect(x + col, cy - tall \ 2, 1, tall, color)
    end for
end sub

' Creates a picture (roBitmap) with or without alpha blending. Returns invalid when out of memory.
function NewPicture(w as integer, h as integer, alpha as boolean) as dynamic
    bmp = invalid
    try
        bmp = CreateObject("roBitmap", { width: w, height: h, AlphaEnable: alpha })
    catch e
        bmp = invalid
    end try
    if bmp = invalid then print "b0r3d TV: no picture memory left for a "; w; " x "; h; " picture"
    return bmp
end function

' For pictures that are only a saving (the RGB look's lettering made into pictures, Rukus's loaf
' on its TV, Rukus Cam's words): after one couldn't be made, none is tried again for 5 seconds,
' so a TV short of picture memory isn't asked (and the log isn't told) on every frame. Meanwhile
' the RGB look draws its lettering and Rukus straight on the screen, and the camera leaves its
' words out.
sub PictureFailed(st as object)
    st.picFail = CreateObject("roTimespan")
end sub

function PicturesFailing(st as object) as boolean
    if st.picFail = invalid then return false
    if st.picFail.TotalMilliseconds() < 5000 then return true
    st.picFail = invalid
    return false
end function
