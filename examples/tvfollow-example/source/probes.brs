' The TV's answers to the questions the next steps depend on. They are asked once at the
' start, shown on the TV check page (Settings) and printed to the app log.

function TvAnswers(st as object) as object
    am = CreateObject("roAppManager")
    di = CreateObject("roDeviceInfo")
    return [
        { label: "Every app (hidden call)", value: AskAppList(am) },
        { label: "Home-screen order", value: AskDisplayOrder(am) },
        { label: "Recent apps", value: AskRecentApps(am) },
        { label: "Installed check", value: AskInstalled(am) },
        { label: "Guide built from", value: st.built.source + ", " + st.all.Count().ToStr() + " apps" },
        { label: "TV menus", value: AskMenus(di) },
        { label: "Sounds at once", value: AskSounds() },
        { label: "Fonts", value: JoinNames(st.families) },
        { label: "This TV", value: AskTv(di) }
    ]
end function

' The answers plus the lines that change while b0r3d TV runs: the screen size and how long the
' guide took to draw.
function AllAnswers(st as object) as object
    all = [{ label: "Screen now", value: ScreenAnswer(st) }]
    drawn = "not drawn yet"
    if st.guideMs >= 0 then drawn = st.guideMs.ToStr() + " ms, in the " + LookTitle(st.look) + " look"
    all.Push({ label: "Guide draw time", value: drawn })
    all.Append(st.answers)
    return all
end function

function ScreenAnswer(st as object) as string
    text = st.w.ToStr() + " x " + st.h.ToStr()
    if st.res = "fhd" then
        text = text + " (" + st.fhdHow + ")"
    else if st.fhdRefused then
        text = text + ": the TV said no to 1920 x 1080"
    end if
    return text + ". Settings, Screen size switches it."
end function

function AskAppList(am as object) as string
    try
        list = am.GetAppList()
        if type(list) <> "roArray" then return "answered with " + type(list) + ", not a list"
        if list.Count() = 0 then return "answered with an empty list"
        return "works: " + list.Count().ToStr() + " entries, " + Fields(list[0])
    catch e
        return NotHere(e)
    end try
end function

function AskDisplayOrder(am as object) as string
    try
        return Describe(am.GetAppDisplayOrder())
    catch e
        return NotHere(e)
    end try
end function

function AskRecentApps(am as object) as string
    try
        return Describe(am.GetRecentApps())
    catch e
        return NotHere(e)
    end try
end function

' How many known apps the documented one-by-one check finds.
function AskInstalled(am as object) as string
    found = []
    total = 0
    for each app in KnownApps()
        if app.kind = "app" then
            total = total + 1
            if IsInstalled(am, app.id) then found.Push(app.name)
        end if
    end for
    text = found.Count().ToStr() + " of " + total.ToStr() + " known apps found"
    if found.Count() > 0 then text = text + ": " + JoinNames(found)
    return text
end function

function AskMenus(di as object) as string
    text = ""
    try
        ui = di.GetUIResolution()
        text = AsText(ui.name) + " " + AsText(ui.width) + " x " + AsText(ui.height)
    catch e
        text = "size unknown"
    end try
    try
        text = text + ", display " + di.GetDisplayMode()
    catch e
    end try
    return text
end function

function AskSounds() as string
    try
        sound = CreateObject("roAudioResource", "select")
        if sound = invalid then return "could not load a sound"
        return sound.MaxSimulStreams().ToStr()
    catch e
        return NotHere(e)
    end try
end function

function AskTv(di as object) as string
    text = ""
    try
        os = di.GetOSVersion()
        text = "Roku OS " + AsText(os.major) + "." + AsText(os.minor) + "." + AsText(os.revision) + " build " + AsText(os.build)
    catch e
        text = "Roku OS unknown"
    end try
    try
        text = text + ", " + di.GetModelDisplayName() + " (" + di.GetModel() + ")"
    catch e
    end try
    return text
end function

' A call the TV doesn't have. The full message goes to the app log; the page says it short.
function NotHere(e as object) as string
    print "b0r3d TV: "; e.message
    if Instr(1, e.message, "not found") > 0 then return "not on this TV"
    return "not on this TV (" + e.message + ")"
end function

' What kind of answer a call gave: a list and its size, or something else.
function Describe(v as dynamic) as string
    if type(v) = "roArray" then
        text = "works: " + v.Count().ToStr() + " entries"
        if v.Count() > 0 then text = text + ", " + Fields(v[0])
        return text
    end if
    if type(v) = "roAssociativeArray" then return "works: " + Fields(v)
    return "answered with " + type(v)
end function

' The field names of one answer, or its type.
function Fields(v as dynamic) as string
    if type(v) <> "roAssociativeArray" then return "each a " + type(v)
    names = []
    for each key in v
        names.Push(key)
    end for
    return "fields " + JoinNames(names)
end function

function JoinNames(list as object) as string
    text = ""
    for each item in list
        if text <> "" then text = text + ", "
        text = text + AsText(item)
    end for
    if text = "" then text = "none"
    return text
end function
