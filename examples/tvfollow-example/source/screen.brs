' The screen: 1920 x 1080 when asked for and the TV allows it, else 1280 x 720. The looks are
' laid out on a 1280 x 720 grid and scaled by st.k (1.5 at 1920 x 1080), so text is drawn at
' the TV's own size instead of being stretched afterwards.

sub OpenScreen(st as object, want as string)
    st.screen = invalid
    st.fonts = invalid
    st.scanBmp = invalid
    screen = invalid
    st.fhdRefused = false
    st.fhdHow = ""
    if want = "fhd" then
        ' The documented way first: a screen without a size matches the TV's menus when the
        ' manifest lists their size. Then the undocumented one: asking for 1920 x 1080.
        screen = TryScreen(0, 0)
        if screen <> invalid then
            if screen.GetWidth() < 1920 then screen = invalid
        end if
        if screen <> invalid then
            st.fhdHow = "matches the TV"
        else
            screen = TryScreen(1920, 1080)
            if screen <> invalid then st.fhdHow = "asked for"
        end if
        if screen = invalid then st.fhdRefused = true
    end if
    if screen = invalid then screen = TryScreen(1280, 720)
    ' none at all: only when coming back from stepping aside fails (see ComeBack)
    if screen = invalid then
        print "b0r3d TV: the TV wouldn't open a 2D screen"
        return
    end if
    screen.SetMessagePort(st.port)
    screen.SetAlphaEnable(true)
    st.screen = screen
    st.w = screen.GetWidth()
    st.h = screen.GetHeight()
    st.k = st.w / 1280
    st.res = "hd"
    if st.w >= 1920 then st.res = "fhd"
    LoadLook(st)
end sub

' The Screen size setting: switches between 1920 x 1080 and 1280 x 720. The pick is kept.
sub SetScreenSize(st as object, want as string)
    OpenScreen(st, want)
    RegWrite("res", want)
end sub

' A double-buffered screen of that size (0 x 0: the TV's own size), or invalid.
function TryScreen(w as integer, h as integer) as dynamic
    screen = invalid
    try
        if w = 0 then
            screen = CreateObject("roScreen", true)
        else
            screen = CreateObject("roScreen", true, w, h)
        end if
    catch e
        screen = invalid
    end try
    return screen
end function

' A spot on the 1280 x 720 layout, scaled to the real screen.
function Z(v as dynamic, k as dynamic) as integer
    return Int(v * k + 0.5)
end function
