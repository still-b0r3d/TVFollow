' The TV's inputs, through TVFollow. A Roku TV won't let an app switch its inputs, so TVFollow
' (on the owner's PC, with "Let the app switch the TV's inputs" ticked in its developer tools)
' does it. While b0r3d TV is on screen, TVFollow reads this app's console (the app log) and says
' hello with an input event: tvfollow=hello, a token, the input the PC is on (pc), and the TV's
' inputs (inputs, then input1, name1, input2, name2...). Only then do the inputs become channels.
' Opening one prints "TVFollow ask <token> <input>" to the console, and TVFollow switches the TV.

' Asks TVFollow for a hello; it sends one anyway a few seconds after b0r3d TV opens. Counted, so
' the guide asks again only a couple of times while none comes (Tick).
sub GreetTvFollow(st as object)
    st.tvfAsked = st.tvfAsked + 1
    print "TVFollow hello"
end sub

' An input event. True when it was TVFollow's hello, kept in st.tvf.
function TakeTvFollowHello(st as object, info as object) as boolean
    if type(info) <> "roAssociativeArray" then return false
    if AsText(info.tvfollow) <> "hello" then return false
    list = []
    count = AsText(info.inputs).ToInt()
    if count > 20 then count = 20
    for i = 1 to count
        id = AsText(info["input" + i.ToStr()])
        if IsTvInput(id) then list.Push({ id: id, name: AsText(info["name" + i.ToStr()]) })
    end for
    st.tvf = { token: AsText(info.token), pc: AsText(info.pc), inputs: list }
    print "b0r3d TV: TVFollow said hello, with "; list.Count(); " inputs"
    return true
end function

' The channels again, with the TV's inputs from TVFollow's hello in front of the apps.
sub AddInputChannels(st as object)
    editing = ""
    if st.csel >= 0 and st.csel < st.all.Count() then editing = st.all[st.csel].id
    channels = InputChannels(st.tvf)
    channels.Append(st.apps)
    st.built.list = channels
    ApplyChannelPrefs(st)
    ' the highlight goes to the channel opened last (an input, say), unless it was moved already
    if not st.moved then
        i = IndexOfId(st.channels, RegRead("last", ""))
        if i >= 0 then st.focus = i
    end if
    ' the channel list being edited keeps its place, also while Ruckus or the stand-in is over it
    if editing <> "" then st.csel = IndexOfId(st.all, editing)
    if st.csel > st.all.Count() - 1 then st.csel = st.all.Count() - 1
    if st.csel < 0 then st.csel = 0
end sub

function InputChannels(tvf as dynamic) as object
    out = []
    if tvf = invalid then return out
    for each item in tvf.inputs
        label = item.name
        if label = "" then label = InputLabel(item.id)
        onNow = "TV input, switched by TVFollow"
        if item.id = tvf.pc then onNow = "Your PC, switched by TVFollow"
        out.Push(MakeChannel(item.id, label, InputSign(item.id, label, tvf.pc), onNow, "input"))
    end for
    return out
end function

' A name for an input the TV gave none: "HDMI 2", "Live TV", "AV", else its id.
function InputLabel(id as string) as string
    if Left(id, 12) = "tvinput.hdmi" then return "HDMI " + Mid(id, 13)
    if id = "tvinput.dtv" then return "Live TV"
    if id = "tvinput.cvbs" then return "AV"
    return id
end function

' PC for the input the PC is on, ANT for the antenna, else a sign from the input's name; an HDMI
' input still called "HDMI 2" gets HDMI2.
function InputSign(id as string, label as string, pc as string) as string
    if id = pc then return "PC"
    if id = "tvinput.dtv" then return "ANT"
    callSign = MakeSign(label)
    if Left(id, 12) = "tvinput.hdmi" and (callSign = "HDMI" or callSign = "APP") then callSign = "HDMI" + Mid(id, 13)
    return callSign
end function

' OK on an input channel: the ask goes to TVFollow through the console.
sub AskTvFollow(st as object, ch as object)
    print "TVFollow ask "; st.tvf.token; " "; ch.id
end sub
