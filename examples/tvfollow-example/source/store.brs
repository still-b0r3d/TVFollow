' What b0r3d TV keeps on the TV between runs, in one registry section:
'   look, colour.<look>, scan, clock, sound, res   the settings
'   saver, tvwait                                    the screensaver settings
'   order, vis                                       the channels' order and which are shown
'   last, prev                                       the last two channels opened
'   idlereset, handover, blank, tvsaver, tvsavers    what the screensaver tries did (saver.brs)
'   sp.mode, sp.level, sp.rows, sp.initials          Stuck Pixels' menu picks and last initials
'   sp.best.marathon, sp.best.sprint, sp.best.cleanup  its high scores (stuckplay.brs)
'   tp.mode, tp.diff, tp.initials                    Test Pattern's menu picks and last initials
'   tp.best.campaign, tp.best.endless                its high scores (tpplay.brs)

function RegRead(key as string, fallback as string) as string
    sec = CreateObject("roRegistrySection", "b0r3dtv")
    if sec.Exists(key) then return sec.Read(key)
    return fallback
end function

sub RegWrite(key as string, value as string)
    sec = CreateObject("roRegistrySection", "b0r3dtv")
    sec.Write(key, value)
    sec.Flush()
end sub

' The settings, read into st once at the start.
sub LoadPrefs(st as object)
    st.look = RegRead("look", "cable")
    if not IsLook(st.look) then st.look = "cable"
    st.scan = RegRead("scan", "1") = "1"
    st.clock24 = RegRead("clock", "12") = "24"
    st.sound = RegRead("sound", "1") = "1"
    st.saverPick = RegRead("saver", "keep")
    if not InList(SaverPicks(), st.saverPick) then st.saverPick = "keep"
    st.tvWaitPref = RegRead("tvwait", "5").ToInt()
    if not InList(TvWaitChoices(), st.tvWaitPref) then st.tvWaitPref = 5
end sub

' The colour picked for one look (an index into its colour list).
function ColourIndex(look as string) as integer
    i = RegRead("colour." + look, "0").ToInt()
    if i < 0 or i >= LookColourNames(look).Count() then i = 0
    return i
end function

function OnOff(on as boolean) as string
    if on then return "1"
    return "0"
end function
