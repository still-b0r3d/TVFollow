' The build stamp: the short hash of the commit the app folder holds, shown as "Build: abc1234"
' after the version. The app goes to the TV straight from its folder, so a local git hook (kept
' with the other hooks, outside the repo) writes the hash into app/build.txt after every
' commit, checkout, merge and rebase; git leaves that file out of the repo. A folder without it,
' or with something else in it, says "dev".
function BuildStamp() as string
    ' looked for first, so a folder without it doesn't log a failed read
    fs = CreateObject("roFileSystem")
    if fs = invalid then return "dev"
    if not fs.Exists("pkg:/build.txt") then return "dev"
    text = ""
    try
        text = ReadAsciiFile("pkg:/build.txt").Trim()
    catch e
        text = ""
    end try
    if Len(text) < 4 or Len(text) > 40 then return "dev"
    for i = 1 to Len(text)
        ch = Mid(text, i, 1)
        if not ((ch >= "0" and ch <= "9") or (ch >= "a" and ch <= "f")) then return "dev"
    end for
    return text
end function

' The play screens' frames-per-second counter, so smoothness can be read on the TV. Every
' build goes to the TV from the app folder as a test build, so every build shows it.
function ShowsFps() as boolean
    return true
end function
