' Bits the games share: a random number generator with its own seed (so a run can be played
' again move for move, in the tests too), the high-score tables, the letters for initials and
' the way numbers and times are written. Nothing here draws, reads the remote or touches the
' registry, so tests/stuck-tests.brs can run it on a PC.

' Park and Miller's minimal generator, worked out so it never overflows 32 bits.
function GameRng(seed as integer) as object
    s = seed mod 2147483646
    if s < 0 then s = -s
    if s = 0 then s = 1
    return { seed: s }
end function

' The next number from 0 to n - 1.
function GameRand(rng as object, n as integer) as integer
    s = rng.seed
    hi = s \ 127773
    lo = s mod 127773
    s = 16807 * lo - 2836 * hi
    if s <= 0 then s = s + 2147483647
    rng.seed = s
    if n <= 1 then return 0
    return s mod n
end function

' A fresh seed for a real run: the clock and the TV's own random numbers.
function GameSeed() as integer
    now = CreateObject("roDateTime")
    return (now.AsSeconds() mod 100000) * 9973 + now.GetMilliseconds() * 31 + Rnd(9999)
end function

' --- High-score tables: up to 10 entries, best first. Each entry is { i: initials, v: the value
' it ranks by, plus whatever the game keeps with it }. lowFirst: a smaller value ranks higher
' (a time). ---

function ScoreTableSize() as integer
    return 10
end function

' The place (0 for the top) a result would take, or -1 when it misses the table. It only goes
' above results that are strictly worse, so on a tie the older one stays ahead.
function ScoreRank(list as object, value as integer, lowFirst as boolean) as integer
    for i = 0 to list.Count() - 1
        if lowFirst then
            if value < list[i].v then return i
        else
            if value > list[i].v then return i
        end if
    end for
    if list.Count() < ScoreTableSize() then return list.Count()
    return -1
end function

' Puts an entry in its place and cuts the table to 10. Returns the place, or -1.
function ScoreInsert(list as object, entry as object, lowFirst as boolean) as integer
    place = ScoreRank(list, entry.v, lowFirst)
    if place < 0 then return -1
    kept = []
    for i = 0 to list.Count() - 1
        if i = place then kept.Push(entry)
        kept.Push(list[i])
    end for
    if place >= list.Count() then kept.Push(entry)
    while kept.Count() > ScoreTableSize()
        kept.Pop()
    end while
    list.Clear()
    list.Append(kept)
    return place
end function

' A table read back from the registry, with anything broken left out.
function CleanScoreTable(raw as dynamic) as object
    list = []
    if type(raw) <> "roArray" then return list
    for each entry in raw
        if type(entry) = "roAssociativeArray" then
            if GetInterface(entry.i, "ifString") <> invalid and GetInterface(entry.v, "ifInt") <> invalid then
                list.Push(entry)
            end if
        end if
        if list.Count() >= ScoreTableSize() then exit for
    end for
    return list
end function

' --- Initials: three of these letters. ---

function InitialsLetters() as string
    return "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
end function

' The letter delta steps from this one, going round.
function NextLetter(letter as string, delta as integer) as string
    all = InitialsLetters()
    n = Len(all)
    i = Instr(1, all, letter) - 1
    if i < 0 then i = 0
    i = ((i + delta) mod n + n) mod n
    return Mid(all, i + 1, 1)
end function

' Three letters from what was saved, else "AAA".
function CleanInitials(text as string) as string
    out = ""
    all = InitialsLetters()
    for i = 1 to 3
        letter = UCase(Mid(text, i, 1))
        if letter = "" or Instr(1, all, letter) = 0 then letter = "A"
        out = out + letter
    end for
    return out
end function

' --- Writing numbers. ---

' 12345 becomes "12,345".
function GroupDigits(n as integer) as string
    neg = n < 0
    if neg then n = -n
    digits = n.ToStr()
    out = ""
    count = 0
    for i = Len(digits) to 1 step -1
        if count > 0 and count mod 3 = 0 then out = "," + out
        out = Mid(digits, i, 1) + out
        count = count + 1
    end for
    if neg then out = "-" + out
    return out
end function

' Milliseconds as minutes, seconds and tenths: 83456 becomes "1:23.4".
function TimeWord(ms as integer) as string
    if ms < 0 then ms = 0
    tenths = ms \ 100
    secs = tenths \ 10
    mins = secs \ 60
    secs = secs mod 60
    ss = secs.ToStr()
    if secs < 10 then ss = "0" + ss
    return mins.ToStr() + ":" + ss + "." + (tenths mod 10).ToStr()
end function

' "1st", "2nd", "3rd", "4th"... for a place counted from 0.
function PlaceWord(place as integer) as string
    n = place + 1
    ending = "th"
    if n mod 100 < 11 or n mod 100 > 13 then
        if n mod 10 = 1 then ending = "st"
        if n mod 10 = 2 then ending = "nd"
        if n mod 10 = 3 then ending = "rd"
    end if
    return n.ToStr() + ending
end function
