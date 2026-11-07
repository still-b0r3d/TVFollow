' The example's screen: the TV's name, a clock, and a line in the App log for each button
' pressed on the remote.

sub init()
    info = CreateObject("roDeviceInfo")
    m.top.findNode("detail").text = "This app came from your PC, through TVFollow, to " + info.GetFriendlyName() + "."
    m.clock = m.top.findNode("clock")
    m.tick = m.top.findNode("tick")
    m.tick.observeField("fire", "showTime")
    m.tick.control = "start"
    showTime()
    m.top.setFocus(true)
    print "TVFollow example: on the screen of a "; info.GetModel()
end sub

sub showTime()
    now = CreateObject("roDateTime")
    now.ToLocalTime()
    hours = now.GetHours()
    minutes = now.GetMinutes()
    suffix = "AM"
    if hours >= 12 then suffix = "PM"
    hours = hours mod 12
    if hours = 0 then hours = 12
    m.clock.text = hours.ToStr() + ":" + Right("0" + minutes.ToStr(), 2) + " " + suffix
end sub

function onKeyEvent(key as string, press as boolean) as boolean
    if press then print "TVFollow example: you pressed "; key
    return false
end function
