' b0r3d Boards: the TV games' online tables at still.b0r3d.org, through its /api/<board>/start,
' /submit and /scores. Each TV game has boards of its own (BoardKeyFor); nothing goes into the
' website's games' boards.
'
' A run asks for a run token as it starts; a score that makes the TV's own top 10 goes up with
' it when the initials are saved, and the token is spent. The scores page asks for the board's
' table each time it opens. Everything is asynchronous: roUrlTransfer answers come to st.port
' (BoardUrlEvent). The last table each board sent is kept in the registry, so the page has
' something to show when the network doesn't answer.

function BoardBaseUrl() as string
    return "https://still.b0r3d.org/api/"
end function

' The board for a game's mode, or "" when the mode keeps no online table (Stuck Pixels'
' Sprint 40 and Cleanup: only Marathon posts).
function BoardKeyFor(game as string, mode as string) as string
    if game = "stuck" then
        if mode = "marathon" then return "b0r3dtv_stuck_pixels"
    else if game = "testpat" then
        if mode = "endless" then return "b0r3dtv_test_pattern_endless"
        return "b0r3dtv_test_pattern"
    else if game = "snake" then
        if mode = "walls" then return ""
        return "b0r3dtv_snake"
    end if
    return ""
end function

' "b0r3dTV/<version> (<build>)" from the manifest and the build stamp: plain ASCII, unlike the
' version line's middle dot.
function BoardUserAgent() as string
    info = CreateObject("roAppInfo")
    return "b0r3dTV/" + info.GetValue("major_version") + "." + info.GetValue("minor_version") + " (" + BuildStamp() + ")"
end function

' A roUrlTransfer for still.b0r3d.org that answers on st.port, or invalid.
function NewBoardTransfer(st as object, path as string) as dynamic
    xfer = CreateObject("roUrlTransfer")
    if xfer = invalid then return invalid
    xfer.SetCertificatesFile("common:/certs/ca-bundle.crt")
    xfer.InitClientCertificates()
    xfer.SetPort(st.port)
    xfer.RetainBodyOnError(true)
    if st.boardAgent = invalid then st.boardAgent = BoardUserAgent()
    xfer.AddHeader("User-Agent", st.boardAgent)
    xfer.SetUrl(BoardBaseUrl() + path)
    return xfer
end function

' Asks for a board's table (unless that's already being asked). Its answer, or its failure,
' shows on the scores page (OnlineScoreRows).
sub FetchOnlineScores(st as object, boardKey as string)
    if boardKey = "" then return
    if st.boardReqs = invalid then st.boardReqs = {}
    if st.boardAsk = invalid then st.boardAsk = {}
    for each id in st.boardReqs
        req = st.boardReqs[id]
        if req.kind = "scores" and req.boardKey = boardKey then return
    end for
    st.boardAsk[boardKey] = "asking"
    xfer = NewBoardTransfer(st, boardKey + "/scores")
    if xfer = invalid then
        st.boardAsk[boardKey] = "failed"
        return
    end if
    reqId = xfer.GetIdentity().ToStr()
    st.boardReqs[reqId] = { kind: "scores", boardKey: boardKey, xfer: xfer }
    if not xfer.AsyncGetToString() then
        st.boardReqs.Delete(reqId)
        st.boardAsk[boardKey] = "failed"
    end if
end sub

' Asks for a run token as a run starts. Any token left from an earlier run goes first, so a
' start that fails leaves none to be used again.
sub RequestRunToken(st as object, boardKey as string)
    if boardKey = "" then return
    if st.boardReqs = invalid then st.boardReqs = {}
    if st.runTokens = invalid then st.runTokens = {}
    if st.boardPost = invalid then st.boardPost = {}
    st.runTokens.Delete(boardKey)
    st.boardPost.Delete(boardKey)
    xfer = NewBoardTransfer(st, boardKey + "/start")
    if xfer = invalid then return
    xfer.AddHeader("Content-Type", "application/json")
    reqId = xfer.GetIdentity().ToStr()
    st.boardReqs[reqId] = { kind: "token", boardKey: boardKey, xfer: xfer }
    if not xfer.AsyncPostFromString("{}") then
        st.boardReqs.Delete(reqId)
    end if
end sub

' Posts a finished run's score with its run token, which is spent by it. The boards refuse a
' score without one, so with no token (the start didn't reach the server) nothing is sent; a
' run with no points isn't sent either. Records what happened so the results screen can show it.
sub SubmitOnlineScore(st as object, boardKey as string, initials as string, score as integer)
    if boardKey = "" then return
    if st.boardReqs = invalid then st.boardReqs = {}
    if st.runTokens = invalid then st.runTokens = {}
    if st.boardPost = invalid then st.boardPost = {}
    if score <= 0 then
        st.boardPost[boardKey] = "zero"
        return
    end if
    token = st.runTokens[boardKey]
    st.runTokens.Delete(boardKey)
    if token = invalid then
        print "b0r3d TV: no run token for "; boardKey; ", so the score stays on the TV"
        asking = false
        for each id in st.boardReqs
            req = st.boardReqs[id]
            if req.kind = "token" and req.boardKey = boardKey then asking = true
        end for
        if asking then
            st.boardPost[boardKey] = "notoken"
        else
            st.boardPost[boardKey] = "offline"
        end if
        return
    end if
    xfer = NewBoardTransfer(st, boardKey + "/submit")
    if xfer = invalid then
        st.boardPost[boardKey] = "failed"
        return
    end if
    xfer.AddHeader("Content-Type", "application/json")
    payload = { name: initials, score: score, runToken: token }
    reqId = xfer.GetIdentity().ToStr()
    st.boardReqs[reqId] = { kind: "submit", boardKey: boardKey, xfer: xfer }
    st.boardPost[boardKey] = "sending"
    if not xfer.AsyncPostFromString(FormatJson(payload)) then
        st.boardReqs.Delete(reqId)
        st.boardPost[boardKey] = "failed"
    end if
end sub

' A board's answer (a roUrlEvent from one of the transfers above).
sub BoardUrlEvent(st as object, msg as object)
    if st.boardReqs = invalid then return
    reqId = msg.GetSourceIdentity().ToStr()
    if not st.boardReqs.DoesExist(reqId) then return
    req = st.boardReqs[reqId]
    st.boardReqs.Delete(reqId)
    if st.boardAsk = invalid then st.boardAsk = {}
    if st.boardPost = invalid then st.boardPost = {}

    code = msg.GetResponseCode()
    data = invalid
    if code >= 200 and code < 300 then data = ParseJson(msg.GetString())
    if type(data) <> "roAssociativeArray" then
        print "b0r3d TV: board "; req.kind; " ("; req.boardKey; ") answered "; code; ": "; msg.GetFailureReason()
        if req.kind = "scores" then
            ' a 4xx is the server saying no (a board it doesn't have); anything else, no answer
            st.boardAsk[req.boardKey] = "failed"
            if code >= 400 and code < 500 then st.boardAsk[req.boardKey] = "unavailable"
            if ScoresPageVisible(st, req.boardKey) then Redraw(st)
        else if req.kind = "submit" then
            st.boardPost[req.boardKey] = "failed"
            if code >= 400 and code < 500 then st.boardPost[req.boardKey] = "rejected"
            if ScoresPageVisible(st, req.boardKey) or ResultsPageVisible(st, req.boardKey) then Redraw(st)
        end if
        return
    end if
    if req.kind = "token" then
        token = AsText(data.runToken)
        if token <> "" then
            if st.runTokens = invalid then st.runTokens = {}
            st.runTokens[req.boardKey] = token
        end if
    else if req.kind = "scores" or req.kind = "submit" then
        if type(data.scores) = "roArray" then SaveOnlineScoresCache(req.boardKey, data.scores)
        if req.kind = "scores" then st.boardAsk[req.boardKey] = "ok"
        if req.kind = "submit" then
            st.boardPost[req.boardKey] = "ok"
            if data.ok = false then st.boardPost[req.boardKey] = "rejected"
        end if
        if ScoresPageVisible(st, req.boardKey) or ResultsPageVisible(st, req.boardKey) then Redraw(st)
    end if
end sub

' Whether a game's scores page for this board is on screen.
function ScoresPageVisible(st as object, boardKey as string) as boolean
    if st.mode = "stuck" and st.sp <> invalid then
        if st.sp.view = "scores" then return BoardKeyFor("stuck", st.sp.scoresMode) = boardKey
    end if
    if st.mode = "testpat" and st.tp <> invalid then
        if st.tp.view = "scores" then return BoardKeyFor("testpat", st.tp.scoresMode) = boardKey
    end if
    if st.mode = "snake" and st.snake <> invalid then
        if st.snake.view = "scores" then return BoardKeyFor("snake", AsText(st.snake.scoresMode)) = boardKey
    end if
    return false
end function

' Whether a game's results page for this board is on screen, so it updates once /submit answers.
function ResultsPageVisible(st as object, boardKey as string) as boolean
    if boardKey = "" then return false
    if st.mode = "stuck" and st.sp <> invalid then
        if st.sp.view = "results" and st.sp.result <> invalid then
            return BoardKeyFor("stuck", st.sp.result.mode) = boardKey
        end if
    end if
    if st.mode = "testpat" and st.tp <> invalid then
        if st.tp.view = "results" and st.tp.result <> invalid then
            return BoardKeyFor("testpat", st.tp.result.mode) = boardKey
        end if
    end if
    if st.mode = "snake" and st.snake <> invalid then
        if st.snake.view = "results" and st.snake.result <> invalid then
            return BoardKeyFor("snake", AsText(st.snake.result.mode)) = boardKey
        end if
    end if
    return false
end function

' One dim row for a game's results screen, showing whether the run went to still.b0r3d.org.
function OnlinePostRow(st as object, boardKey as string, res as object) as object
    if boardKey = "" then return { label: "Online board", value: "This TV only", kind: "info", dim: true }
    if not res.saved then return { label: "Online board", value: "Needs TV top 10 to post", kind: "info", dim: true }
    if res.score <= 0 then return { label: "Online board", value: "Zero points isn't posted", kind: "info", dim: true }
    post = ""
    if st.boardPost <> invalid then post = AsText(st.boardPost[boardKey])
    if post = "sending" then return { label: "Online board", value: "Posting to still.b0r3d.org...", kind: "info", dim: true }
    if post = "ok" then return { label: "Online board", value: "Posted to still.b0r3d.org", kind: "info", dim: true }
    if post = "offline" then return { label: "Online board", value: "Not posted (offline at start)", kind: "info", dim: true }
    if post = "notoken" then return { label: "Online board", value: "Not posted (no run token)", kind: "info", dim: true }
    if post = "rejected" then return { label: "Online board", value: "still.b0r3d.org declined it", kind: "info", dim: true }
    return { label: "Online board", value: "still.b0r3d.org didn't answer", kind: "info", dim: true }
end function

' A board's table for the scores page: place, initials and points, best first. With nothing
' to show, one line says why.
function OnlineScoreRows(st as object, boardKey as string) as object
    rows = []
    list = LoadOnlineScoresCache(boardKey)
    for i = 0 to list.Count() - 1
        rows.Push({ label: PlaceWord(i) + "  " + list[i].name, value: GroupDigits(list[i].score) + " points" })
    end for
    ask = ""
    if st.boardAsk <> invalid then ask = AsText(st.boardAsk[boardKey])
    if rows.Count() = 0 then
        if ask = "asking" then
            rows.Push({ label: "Asking", value: "still.b0r3d.org..." })
        else if ask = "failed" then
            rows.Push({ label: "No answer", value: "still.b0r3d.org didn't answer. Try again later." })
        else if ask = "unavailable" then
            rows.Push({ label: "Not available", value: "This online board isn't available." })
        else
            rows.Push({ label: "None yet", value: "No scores on this board yet" })
        end if
    else if ask = "failed" or ask = "unavailable" then
        rows.Push({ label: "Offline", value: "The last table still.b0r3d.org sent", kind: "info", dim: true })
    end if
    return rows
end function

' --- The registry's copy of each board's last table ---

function LoadOnlineScoresCache(boardKey as string) as object
    return CleanOnlineScores(ParseJson(RegRead("cache.board." + boardKey, "[]")))
end function

sub SaveOnlineScoresCache(boardKey as string, list as object)
    RegWrite("cache.board." + boardKey, FormatJson(CleanOnlineScores(list)))
end sub

' At most 10 entries of { name, score }, whatever came in: the name as up to three letters or
' digits (as the server keeps them), the score a whole number of points.
function CleanOnlineScores(list as dynamic) as object
    out = []
    if type(list) <> "roArray" then return out
    for each item in list
        if type(item) = "roAssociativeArray" then
            score = -1
            v = item.score
            if v <> invalid then
                if GetInterface(v, "ifInt") <> invalid then
                    score = v
                else if GetInterface(v, "ifFloat") <> invalid or GetInterface(v, "ifDouble") <> invalid then
                    if v >= 0 and v < 2000000000 then score = Int(v)
                end if
            end if
            if score >= 0 then out.Push({ name: BoardName(item.name), score: score })
        end if
        if out.Count() >= 10 then exit for
    end for
    return out
end function

function BoardName(v as dynamic) as string
    text = UCase(AsText(v))
    name = ""
    for i = 1 to Len(text)
        ch = Mid(text, i, 1)
        if (ch >= "A" and ch <= "Z") or (ch >= "0" and ch <= "9") or ch = "-" then name = name + ch
        if Len(name) = 3 then exit for
    end for
    if name = "" then name = "---"
    return name
end function
