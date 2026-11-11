' The stand-in screen's script (AsideScene.xml).

' The splash, a quarter of the screen's size, sets off on a slow loop round the screen.
sub init()
    m.top.backgroundURI = ""
    m.top.backgroundColor = "0x000000FF"
    w = 1280
    h = 720
    size = m.top.currentDesignResolution
    if size <> invalid then
        w = size.width
        h = size.height
    end if
    logo = m.top.findNode("logo")
    logo.width = w \ 4
    logo.height = h \ 4
    maxX = w - w \ 4 - 40
    maxY = h - h \ 4 - 40
    m.top.findNode("path").keyValue = [[40, 40], [maxX, maxY \ 3], [maxX \ 2, maxY], [40, maxY \ 2], [40, 40]]
    m.top.findNode("drift").control = "start"
    m.top.setFocus(true)
end sub

' Any key, pressed: Main is watching keyPressed. Every key is kept here, Back included, so
' b0r3d TV stays open.
function onKeyEvent(key as string, press as boolean) as boolean
    if press then m.top.keyPressed = key
    return true
end function
