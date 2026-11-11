' Stuck Pixels, the falling-block game on channel 20: its rules. Nothing here draws, reads the
' remote or touches the registry, so tests/stuck-tests.brs can play it move by move on a PC;
' stuckplay.brs puts it on the TV.
'
' The well is 12 pixels wide and 18 tall. The pieces are clusters of 3 and 5 pixels. Now and
' then a piece comes with a stuck pixel in it (it flickers). A row with a stuck pixel has to be
' filled twice: the first time, the stuck pixel pops out (extra points) and leaves a hole; fill
' that, and the row clears.
'
' The modes: Marathon (10 lines a level, faster each level, until the well's screen loses its
' signal), Sprint 40 (40 lines against the clock) and Cleanup (25 lines, starting over rows of
' static). Times are in milliseconds. Cells: 0 empty, 1 a pixel, 2 a stuck pixel, 3 static.

function SpRules() as object
    return {
        cols: 12,
        rows: 18,
        dasDelay: 170,
        dasRate: 50,
        softRate: 45,
        lockDelay: 500,
        lockMovesMax: 15,
        linesPerLevel: 10,
        sprintLines: 40,
        cleanupLines: 25,
        clearPoints: [0, 100, 300, 500, 800, 1200],
        fixPoints: 200,
        cleanupBonus: 1000,
        stuckChance: 10,
        stuckPerLevel: 1,
        stuckMax: 20,
        staticFill: 55,
        kicks: [0, -1, 1, -2, 2]
    }
end function

' How long a piece takes to fall one row at a level: a second at level 0, 80 ms less each
' level, never under a tenth of a second.
function SpDropMs(level as integer) as integer
    ms = 1000 - level * 80
    if ms < 100 then ms = 100
    return ms
end function

' Cleanup's choice of how many rows of static the well starts with.
function SpStaticChoices() as object
    return [0, 2, 4, 6, 8, 10]
end function

function SpModes() as object
    return ["marathon", "sprint", "cleanup"]
end function

' The pieces, each drawn in its square box (# is a pixel) the way it comes in, flat side
' down. copies: how many of it go in each bag of pieces.
function SpPieceDefs() as object
    return [
        { name: "bar3", copies: 2, rows: ["...", "###", "..."] },
        { name: "elbow3", copies: 2, rows: ["#.", "##"] },
        { name: "bar5", copies: 1, rows: [".....", ".....", "#####", ".....", "....."] },
        { name: "hook", copies: 1, rows: ["#...", "####", "....", "...."] },
        { name: "hookR", copies: 1, rows: ["...#", "####", "....", "...."] },
        { name: "flag", copies: 1, rows: ["##.", "###", "..."] },
        { name: "flagR", copies: 1, rows: [".##", "###", "..."] },
        { name: "tee", copies: 1, rows: [".#.", ".#.", "###"] },
        { name: "cup", copies: 1, rows: ["#.#", "###", "..."] },
        { name: "corner", copies: 1, rows: ["#..", "#..", "###"] },
        { name: "fork", copies: 1, rows: [".#..", "####", "....", "...."] },
        { name: "forkR", copies: 1, rows: ["..#.", "####", "....", "...."] }
    ]
end function

' Each piece with its four turns worked out: cells[turn] is a list of [row, col] in its box,
' always in the same order, so a stuck pixel stays the same pixel as the piece turns.
function SpShapes() as object
    shapes = []
    for each def in SpPieceDefs()
        size = def.rows.Count()
        first = []
        for r = 0 to size - 1
            for c = 0 to size - 1
                if Mid(def.rows[r], c + 1, 1) = "#" then first.Push([r, c])
            end for
        end for
        turns = [first]
        for t = 1 to 3
            prev = turns[t - 1]
            turned = []
            for each cell in prev
                ' a quarter turn to the right: the row becomes the column, counted from the right
                turned.Push([cell[1], size - 1 - cell[0]])
            end for
            turns.Push(turned)
        end for
        top = size
        for each cell in first
            if cell[0] < top then top = cell[0]
        end for
        shapes.Push({ name: def.name, copies: def.copies, size: size, top: top, cells: turns })
    end for
    return shapes
end function

' A new run. mode: "marathon", "sprint" or "cleanup"; startLevel 0 to 9; staticRows: Cleanup's
' rows of static; seed: the random numbers' seed.
function SpNew(mode as string, startLevel as integer, staticRows as integer, seed as integer) as object
    rules = SpRules()
    g = {
        rules: rules,
        cols: rules.cols,
        rows: rules.rows,
        mode: mode,
        startLevel: startLevel,
        level: startLevel,
        staticRows: 0,
        lines: 0,
        score: 0,
        fixed: 0,
        stuckNow: 0,
        pieces: 0,
        elapsed: 0,
        state: "play",
        rng: GameRng(seed),
        bag: [],
        shapes: SpShapes(),
        board: [],
        cur: invalid,
        upcoming: invalid,
        held: invalid,
        holdUsed: false,
        fallT: 0,
        lockT: 0,
        lockMoves: 0,
        lowest: 0,
        dasDir: 0,
        dasT: 0,
        softHeld: false,
        softT: 0,
        events: []
    }
    for r = 0 to g.rows - 1
        g.board.Push(SpEmptyRow(g))
    end for
    if mode = "cleanup" then SpFillStatic(g, staticRows)
    g.upcoming = SpDraw(g)
    SpSpawn(g)
    return g
end function

function SpEmptyRow(g as object) as object
    row = []
    for c = 0 to g.cols - 1
        row.Push(0)
    end for
    return row
end function

' Cleanup's start: the bottom rows full of static with gaps (about half of each row), never a
' full row and never an empty one, so every row needs work.
sub SpFillStatic(g as object, count as integer)
    if count > g.rows - 6 then count = g.rows - 6
    if count < 0 then count = 0
    g.staticRows = count
    for r = g.rows - count to g.rows - 1
        filled = 0
        for c = 0 to g.cols - 1
            if GameRand(g.rng, 100) < g.rules.staticFill then
                g.board[r][c] = 3
                filled = filled + 1
            end if
        end for
        if filled = g.cols then g.board[r][GameRand(g.rng, g.cols)] = 0
        if filled = 0 then g.board[r][GameRand(g.rng, g.cols)] = 3
    end for
end sub

' The next piece from the bag (every piece, shuffled, then a new bag), and whether it comes
' with a stuck pixel: 10 in 100 at level 0, one more per level, at most 20.
function SpDraw(g as object) as object
    if g.bag.Count() = 0 then SpFillBag(g)
    p = g.bag.Pop()
    stuck = -1
    chance = g.rules.stuckChance + g.rules.stuckPerLevel * g.level
    if chance > g.rules.stuckMax then chance = g.rules.stuckMax
    if GameRand(g.rng, 100) < chance then stuck = GameRand(g.rng, g.shapes[p].cells[0].Count())
    return { p: p, stuck: stuck }
end function

sub SpFillBag(g as object)
    bag = []
    for i = 0 to g.shapes.Count() - 1
        for n = 1 to g.shapes[i].copies
            bag.Push(i)
        end for
    end for
    for i = bag.Count() - 1 to 1 step -1
        j = GameRand(g.rng, i + 1)
        swap = bag[i]
        bag[i] = bag[j]
        bag[j] = swap
    end for
    g.bag = bag
end sub

' The upcoming piece comes in at the top, in the middle.
sub SpSpawn(g as object)
    piece = g.upcoming
    g.upcoming = SpDraw(g)
    SpPlace(g, piece)
end sub

sub SpPlace(g as object, piece as object)
    shape = g.shapes[piece.p]
    g.cur = { p: piece.p, stuck: piece.stuck, rot: 0, row: -shape.top, col: (g.cols - shape.size) \ 2 }
    g.lowest = g.cur.row
    g.lockT = 0
    g.lockMoves = 0
    g.fallT = 0
    if not SpFits(g, g.cur.p, 0, g.cur.row, g.cur.col) then SpEnd(g, "over")
end sub

' Whether a piece fits with its box's top left corner at row, col. Above the well is open.
function SpFits(g as object, p as integer, rot as integer, row as integer, col as integer) as boolean
    for each cell in g.shapes[p].cells[rot]
        r = row + cell[0]
        c = col + cell[1]
        if c < 0 or c >= g.cols or r >= g.rows then return false
        if r >= 0 then
            if g.board[r][c] <> 0 then return false
        end if
    end for
    return true
end function

' The piece in play's pixels in the well: [row, col] each, in its cells' order.
function SpCells(g as object) as object
    out = []
    c = g.cur
    if c = invalid then return out
    for each cell in g.shapes[c.p].cells[c.rot]
        out.Push([c.row + cell[0], c.col + cell[1]])
    end for
    return out
end function

function SpGrounded(g as object) as boolean
    c = g.cur
    return not SpFits(g, c.p, c.rot, c.row + 1, c.col)
end function

' --- Moving ---

' A key pressed: "left", "right", "down" (soft drop), "drop" (all the way down), "cw" and
' "ccw" (a quarter turn either way) or "hold". A direction (or Down) pressed again while it's
' still held, as a remote that repeats keys would send it, changes nothing: the slide goes on.
sub SpPress(g as object, action as string)
    if g.state <> "play" then return
    if action = "left" or action = "right" then
        dir = -1
        if action = "right" then dir = 1
        if g.dasDir = dir then return
        g.dasDir = dir
        g.dasT = 0
        if SpMove(g, 0, dir) then SpEvent(g, "move", invalid)
    else if action = "down" then
        if g.softHeld then return
        g.softHeld = true
        g.softT = 0
        SpSoftDrop(g)
    else if action = "drop" then
        SpHardDrop(g)
    else if action = "cw" then
        if SpTurn(g, 1) then SpEvent(g, "turn", invalid)
    else if action = "ccw" then
        if SpTurn(g, 3) then SpEvent(g, "turn", invalid)
    else if action = "hold" then
        SpHold(g)
    end if
end sub

' A key let go: sliding or dropping stops.
sub SpRelease(g as object, action as string)
    if action = "left" and g.dasDir = -1 then g.dasDir = 0
    if action = "right" and g.dasDir = 1 then g.dasDir = 0
    if action = "down" then g.softHeld = false
end sub

' Every key let go at once (when the game pauses).
sub SpLetGo(g as object)
    g.dasDir = 0
    g.dasT = 0
    g.softHeld = false
    g.softT = 0
end sub

function SpMove(g as object, dRow as integer, dCol as integer) as boolean
    c = g.cur
    if not SpFits(g, c.p, c.rot, c.row + dRow, c.col + dCol) then return false
    c.row = c.row + dRow
    c.col = c.col + dCol
    SpMoved(g)
    return true
end function

' A quarter turn (turns 1 to the right, 3 to the left). Against a wall or another piece it
' tries a step to the side, then two.
function SpTurn(g as object, turns as integer) as boolean
    c = g.cur
    rot = (c.rot + turns) mod 4
    for each kick in g.rules.kicks
        if SpFits(g, c.p, rot, c.row, c.col + kick) then
            c.rot = rot
            c.col = c.col + kick
            SpMoved(g)
            return true
        end if
    end for
    return false
end function

' After a move or a turn. A piece on the ground gets half a second before it sets, and each
' move there starts that half second again, 15 times at most; reaching a lower row than before
' resets the count.
sub SpMoved(g as object)
    c = g.cur
    if c.row > g.lowest then
        g.lowest = c.row
        g.lockMoves = 0
        g.lockT = 0
        return
    end if
    if (g.lockT > 0 or SpGrounded(g)) and g.lockMoves < g.rules.lockMovesMax then
        g.lockMoves = g.lockMoves + 1
        g.lockT = 0
    end if
end sub

function SpSoftDrop(g as object) as boolean
    if not SpMove(g, 1, 0) then return false
    g.score = g.score + 1
    g.fallT = 0
    return true
end function

sub SpHardDrop(g as object)
    c = g.cur
    dist = 0
    while SpFits(g, c.p, c.rot, c.row + 1, c.col)
        c.row = c.row + 1
        dist = dist + 1
    end while
    g.score = g.score + 2 * dist
    SpEvent(g, "drop", dist)
    SpLock(g)
end sub

' Replay puts the piece aside, or swaps it with the one put aside, once a piece.
sub SpHold(g as object)
    if g.state <> "play" or g.holdUsed then return
    g.holdUsed = true
    out = { p: g.cur.p, stuck: g.cur.stuck }
    SpEvent(g, "hold", invalid)
    if g.held = invalid then
        g.held = out
        SpSpawn(g)
    else
        piece = g.held
        g.held = out
        SpPlace(g, piece)
    end if
end sub

' --- Time ---

' The game moves on by dt milliseconds: sliding while a direction is held, dropping while
' Down is held, falling, and setting once the piece has sat on the ground long enough.
sub SpTick(g as object, dt as integer)
    if g.state <> "play" then return
    r = g.rules
    g.elapsed = g.elapsed + dt
    if g.dasDir <> 0 then
        g.dasT = g.dasT + dt
        n = 0
        while g.dasT >= r.dasDelay and n < 20
            g.dasT = g.dasT - r.dasRate
            n = n + 1
            if SpMove(g, 0, g.dasDir) then
                SpEvent(g, "move", invalid)
            else
                exit while
            end if
        end while
    end if
    if g.softHeld then
        g.softT = g.softT + dt
        n = 0
        while g.softT >= r.softRate and n < 20
            g.softT = g.softT - r.softRate
            n = n + 1
            if not SpSoftDrop(g) then exit while
        end while
    end if
    if SpGrounded(g) then
        g.lockT = g.lockT + dt
        if g.lockT >= r.lockDelay then SpLock(g)
        return
    end if
    g.fallT = g.fallT + dt
    interval = SpDropMs(g.level)
    while g.fallT >= interval
        g.fallT = g.fallT - interval
        if not SpMove(g, 1, 0) then exit while
        if SpGrounded(g) then exit while
    end while
end sub

' --- Setting a piece, and the rows ---

sub SpLock(g as object)
    c = g.cur
    cells = SpCells(g)
    above = false
    for i = 0 to cells.Count() - 1
        r = cells[i][0]
        col = cells[i][1]
        if r < 0 then
            above = true
        else
            v = 1
            if i = c.stuck then v = 2
            g.board[r][col] = v
        end if
    end for
    g.pieces = g.pieces + 1
    SpEvent(g, "lock", cells)
    ' set partly above the well: it's full
    if above then
        SpEnd(g, "over")
        return
    end if
    SpCheckRows(g)
    if g.state <> "play" then return
    g.holdUsed = false
    SpSpawn(g)
end sub

' Full rows: one with a stuck pixel in it stays, and its stuck pixels pop out, leaving holes;
' any other clears, and the rows above come down. Then the score, the level and the goal.
sub SpCheckRows(g as object)
    pops = []
    full = []
    for r = 0 to g.rows - 1
        row = g.board[r]
        isFull = true
        hasStuck = false
        for c = 0 to g.cols - 1
            v = row[c]
            if v = 0 then
                isFull = false
                exit for
            end if
            if v = 2 then hasStuck = true
        end for
        if isFull then
            if hasStuck then
                for c = 0 to g.cols - 1
                    if row[c] = 2 then
                        row[c] = 0
                        pops.Push([r, c])
                    end if
                end for
            else
                full.Push(r)
            end if
        end if
    end for
    mult = g.level + 1
    n = full.Count()
    if n > 0 then
        ' top to bottom: taking a row out and putting an empty one on top leaves the rows
        ' below where they were
        for each r in full
            g.board.Delete(r)
            g.board.Unshift(SpEmptyRow(g))
        end for
        g.lines = g.lines + n
        points = g.rules.clearPoints
        i = n
        if i > points.Count() - 1 then i = points.Count() - 1
        g.score = g.score + points[i] * mult
        SpEvent(g, "clear", full)
    end if
    if pops.Count() > 0 then
        ' each hole comes down a row for every row cleared below it
        for each p in pops
            below = 0
            for each r in full
                if r > p[0] then below = below + 1
            end for
            p[0] = p[0] + below
        end for
        g.fixed = g.fixed + pops.Count()
        g.score = g.score + g.rules.fixPoints * mult * pops.Count()
        SpEvent(g, "fix", pops)
    end if
    g.stuckNow = SpCountStuck(g)
    if n = 0 then return
    if g.mode = "marathon" then
        lv = g.startLevel + g.lines \ g.rules.linesPerLevel
        if lv > g.level then
            g.level = lv
            SpEvent(g, "level", lv)
        end if
    else if g.mode = "sprint" then
        if g.lines >= g.rules.sprintLines then SpEnd(g, "done")
    else if g.mode = "cleanup" then
        if g.lines >= g.rules.cleanupLines then
            g.score = g.score + g.rules.cleanupBonus * mult
            SpEnd(g, "done")
        end if
    end if
end sub

function SpCountStuck(g as object) as integer
    n = 0
    for each row in g.board
        for each v in row
            if v = 2 then n = n + 1
        end for
    end for
    return n
end function

' The run ends: "over" when the well is full (its screen loses its signal), "done" when the
' goal is reached.
sub SpEnd(g as object, how as string)
    if g.state <> "play" then return
    g.state = how
    g.dasDir = 0
    g.softHeld = false
    SpEvent(g, how, invalid)
end sub

' Something for the screen and the speakers to show or play: "move", "turn", "drop", "hold",
' "lock", "fix", "clear", "level", "over" or "done", with its pixels, rows or number.
sub SpEvent(g as object, kind as string, data as dynamic)
    g.events.Push({ kind: kind, data: data })
end sub

' --- The demo in the guide's preview box: the game plays itself. ---

' Where the demo's player would put the piece in play: every turn and column, dropped straight
' down in its head, scored by the stack it would leave (low, flat, few holes, lines cleared).
' Returns { rot, col }.
function SpBotPick(g as object) as object
    tops = []
    for c = 0 to g.cols - 1
        top = g.rows
        for r = 0 to g.rows - 1
            if g.board[r][c] <> 0 then
                top = r
                exit for
            end if
        end for
        tops.Push(top)
    end for
    fill = []
    for r = 0 to g.rows - 1
        n = 0
        for each v in g.board[r]
            if v <> 0 then n = n + 1
        end for
        fill.Push(n)
    end for
    best = { rot: g.cur.rot, col: g.cur.col }
    bestScore = -1000000.0
    shape = g.shapes[g.cur.p]
    for rot = 0 to 3
        cells = shape.cells[rot]
        minC = shape.size
        maxC = -1
        for each cell in cells
            if cell[1] < minC then minC = cell[1]
            if cell[1] > maxC then maxC = cell[1]
        end for
        for col = -minC to g.cols - 1 - maxC
            ' the lowest row where every pixel sits above its column's stack
            row = g.rows
            for each cell in cells
                limit = tops[col + cell[1]] - 1 - cell[0]
                if limit < row then row = limit
            end for
            score = SpBotScore(g, cells, row, col, tops, fill)
            if score > bestScore then
                bestScore = score
                best = { rot: rot, col: col }
            end if
        end for
    end for
    return best
end function

function SpBotScore(g as object, cells as object, row as integer, col as integer, tops as object, fill as object) as float
    newTops = []
    for each t in tops
        newTops.Push(t)
    end for
    added = {}
    holes = 0
    for each cell in cells
        r = row + cell[0]
        c = col + cell[1]
        if r < 0 then return -999999.0
        key = r.ToStr()
        if added.DoesExist(key) then
            added[key] = added[key] + 1
        else
            added[key] = 1
        end if
        if r < newTops[c] then newTops[c] = r
    end for
    ' holes: empty cells left under the piece's lowest pixel in each column it covers
    for c = 0 to g.cols - 1
        lowest = -1
        for each cell in cells
            if col + cell[1] = c and row + cell[0] > lowest then lowest = row + cell[0]
        end for
        if lowest >= 0 then holes = holes + (tops[c] - 1 - lowest)
    end for
    lines = 0
    for each key in added
        if fill[key.ToInt()] + added[key] = g.cols then lines = lines + 1
    end for
    height = 0
    bumps = 0
    for c = 0 to g.cols - 1
        h = g.rows - newTops[c]
        height = height + h
        if c > 0 then bumps = bumps + Abs(h - (g.rows - newTops[c - 1]))
    end for
    return -0.51 * height + 0.76 * lines * g.cols - 0.36 * holes * 4 - 0.18 * bumps
end function

' A demo run: Marathon from level 2, played by SpBotPick, one step at a time.
function SpDemoNew(seed as integer) as object
    return { g: SpNew("marathon", 2, 0, seed), plan: invalid, piece: -1, rest: 0, seed: seed }
end function

' One step of the demo: the piece turns or slides toward its spot, or falls a row; when it
' sets, the next one gets a plan. After the well fills, it rests a few steps and starts over.
sub SpDemoStep(d as object)
    g = d.g
    g.events.Clear()
    if g.state <> "play" then
        d.rest = d.rest + 1
        if d.rest > 8 then
            d.seed = d.seed + 7919
            d.g = SpNew("marathon", 2, 0, d.seed)
            d.plan = invalid
            d.piece = -1
            d.rest = 0
        end if
        return
    end if
    if d.piece <> g.pieces or d.plan = invalid then
        d.plan = SpBotPick(g)
        d.piece = g.pieces
    end if
    c = g.cur
    if c.rot <> d.plan.rot then
        if SpTurn(g, 1) then return
    end if
    if c.col < d.plan.col then
        if SpMove(g, 0, 1) then return
    else if c.col > d.plan.col then
        if SpMove(g, 0, -1) then return
    end if
    if SpGrounded(g) then
        SpLock(g)
    else
        SpMove(g, 1, 0)
    end if
end sub
