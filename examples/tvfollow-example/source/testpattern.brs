' Test Pattern, the brick breaker on channel 21: its rules. Nothing here draws, reads the
' remote or touches the registry, so tests/tp-tests.brs can play it on a PC; tpplay.brs puts
' it on the TV.
'
' The field is 1120 x 548 (units of the 1280 x 720 layout), wide because the TV is. Every wall
' is TV colour bars: a brick takes the bar of its column, eight bars across (white, yellow,
' cyan, green, magenta, red, blue, black). The ball is a square pixel. Bombs blast the bricks
' around them; capsule bricks drop little TVs: two more balls, a wider paddle, a sticky paddle
' (12 s), a slower ball (8 s), a life, or a mystery. Campaign is six walls; Endless makes new
' ones that get faster. Times are in milliseconds, distances in field units, speeds in units a
' second.

function TpRules() as object
    return {
        w: 1120,
        h: 548,
        margin: 28,
        gap: 6,
        brickTop: 50,
        brickH: 22,
        paddleH: 14,
        paddleY: 506,
        ball: 14,
        baseSpeed: 280,
        paddleStart: 360,
        paddleAccel: 1800,
        paddleMax: 1100,
        capsuleSpeed: 150,
        capsuleW: 34,
        capsuleH: 26,
        maxBalls: 6,
        stickyMs: 12000,
        slowMs: 8000,
        wideFactor: 1.5,
        slowFactor: 0.6,
        bombPoints: 25,
        capsulePoints: 25,
        bombDelay: 50,
        endlessStep: 0.05,
        endlessCap: 2.0,
        stepMs: 8,
        mystery: ["multi", "multi", "wide", "wide", "sticky", "sticky", "slow", "slow", "life"]
    }
end function

' Lives, the paddle's width, the ball's speed and the points, per difficulty.
function TpDifficulty(name as string) as object
    if name = "easy" then return { lives: 5, paddleW: 200, speed: 0.85, score: 0.5 }
    if name = "hard" then return { lives: 2, paddleW: 116, speed: 1.2, score: 1.5 }
    return { lives: 3, paddleW: 150, speed: 1.0, score: 1.0 }
end function

function TpModes() as object
    return ["campaign", "endless"]
end function

function TpDifficulties() as object
    return ["easy", "normal", "hard"]
end function

' The six campaign walls, eight bricks across: "." empty, 1-7 a brick worth that many tens,
' B a bomb, and a capsule brick: M more balls, W wide, S sticky, Z slow, L a life, P a
' mystery. Each wall's ball is a little faster than the last.
function TpCampaign() as object
    return [
        { speed: 1.0, rows: ["66666666", "55555555", "44444444", "333WS333", "22222222", "11111111"] },
        { speed: 1.1, rows: ["2.2.2.2.", ".3.3.3.3", "2.2.2.2.", ".3.P.P.3", "2.2.2.2.", ".3.3.3.3", "2.2.2.2."] },
        { speed: 1.2, rows: ["...MM...", "..3333..", "..3333..", ".444444.", ".44ZW44.", "55555555", "55555555"] },
        { speed: 1.3, rows: ["...66...", "..5BB5..", ".444444.", "33B33B33", ".4S44S4.", "..5BB5..", "...66..."] },
        { speed: 1.4, rows: ["22222222", "2......2", "2.M..B.2", "2..LS..2", "2.B..M.2", "2......2", "22222222"] },
        { speed: 1.5, rows: ["77777777", "6B6.6B6.", ".5WMMZ5.", "444B4B44", "44B4B444", ".5ZMMW5.", "6.B6B6.6", "33333333"] }
    ]
end function

' A campaign wall for the wide field: every brick is two bricks side by side. A capsule brick
' keeps its capsule on the left and is a plain 30-point brick on the right, so a wall drops as
' many capsules as before.
function TpWideWall(rows as object) as object
    out = []
    for each row in rows
        wide = ""
        for i = 1 to Len(row)
            ch = Mid(row, i, 1)
            if Instr(1, "MWSZLP", ch) > 0 then
                wide = wide + ch + "3"
            else
                wide = wide + ch + ch
            end if
        end for
        out.Push(wide)
    end for
    return out
end function

' A run. mode: "campaign" or "endless"; difficulty: "easy", "normal" or "hard".
function TpNew(mode as string, difficulty as string, seed as integer) as object
    rules = TpRules()
    diff = TpDifficulty(difficulty)
    g = {
        rules: rules,
        mode: mode,
        difficulty: difficulty,
        diff: diff,
        rng: GameRng(seed),
        wall: 0,
        score: 0,
        lives: diff.lives,
        elapsed: 0,
        state: "ready",
        bricks: [],
        grid: [],
        cols: 0,
        rows: 0,
        brickW: 0.0,
        balls: [],
        capsules: [],
        blasts: [],
        paddle: { x: 0.0, w: diff.paddleW + 0.0, dir: 0, heldMs: 0 },
        wide: false,
        stickyMs: 0,
        slowMs: 0,
        lastStyle: "",
        events: [],
        carry: 0
    }
    TpStartWall(g, 0)
    return g
end function

' Wall n (from 0): its bricks, a fresh paddle in the middle and the ball resting on it.
sub TpStartWall(g as object, n as integer)
    g.wall = n
    if g.mode = "campaign" then
        TpBuildBricks(g, TpWideWall(TpCampaign()[n].rows))
    else
        TpBuildBricks(g, TpEndlessWall(g, n + 1))
    end if
    g.capsules = []
    g.blasts = []
    g.wide = false
    g.stickyMs = 0
    g.slowMs = 0
    g.paddle.w = g.diff.paddleW + 0.0
    g.paddle.x = (g.rules.w - g.paddle.w) / 2
    g.paddle.dir = 0
    g.paddle.heldMs = 0
    TpServe(g)
end sub

' The ball rests on the middle of the paddle until OK launches it.
sub TpServe(g as object)
    r = g.rules
    g.balls = [{ x: 0.0, y: r.paddleY - r.ball - 1.0, vx: 0.0, vy: 0.0, speed: 0.0, stuck: true, dx: 0.0 }]
    TpCarryStuck(g)
    g.state = "ready"
end sub

' The ball's speed on this wall: the wall's own (campaign) or 5 in 100 more each wall up to
' twice (endless), times the difficulty's.
function TpSpeed(g as object) as float
    r = g.rules
    if g.mode = "campaign" then
        mult = TpCampaign()[g.wall].speed
    else
        mult = 1 + r.endlessStep * g.wall
        if mult > r.endlessCap then mult = r.endlessCap
    end if
    return r.baseSpeed * mult * g.diff.speed
end function

' Bricks from rows of characters (see TpCampaign), spread over the field's width; each takes
' the colour bar of its column.
sub TpBuildBricks(g as object, rows as object)
    r = g.rules
    g.rows = rows.Count()
    g.cols = Len(rows[0])
    g.brickW = (r.w - 2 * r.margin - r.gap * (g.cols - 1)) / g.cols
    g.bricks = []
    g.grid = []
    for row = 0 to g.rows - 1
        line = []
        for col = 0 to g.cols - 1
            ch = Mid(rows[row], col + 1, 1)
            brick = invalid
            if ch <> "." then
                kind = "plain"
                cap = ""
                points = 0
                if ch = "B" then
                    kind = "bomb"
                    points = r.bombPoints
                else if Instr(1, "MWSZLP", ch) > 0 then
                    kind = "capsule"
                    cap = TpCapsuleFor(ch)
                    points = r.capsulePoints
                else
                    points = ch.ToInt() * 10
                end if
                brick = {
                    row: row,
                    col: col,
                    x: r.margin + col * (g.brickW + r.gap),
                    y: r.brickTop + row * (r.brickH + r.gap),
                    alive: true,
                    kind: kind,
                    cap: cap,
                    points: points,
                    bar: col * 8 \ g.cols
                }
                g.bricks.Push(brick)
            end if
            line.Push(brick)
        end for
        g.grid.Push(line)
    end for
end sub

function TpCapsuleFor(ch as string) as string
    if ch = "M" then return "multi"
    if ch = "W" then return "wide"
    if ch = "S" then return "sticky"
    if ch = "Z" then return "slow"
    if ch = "L" then return "life"
    return "mystery"
end function

' --- Endless walls: a shape picked at random (never the same twice running), with more rows,
' more columns, more bombs and more points as the walls go by, and always a capsule. ---

function TpEndlessWall(g as object, n as integer) as object
    rows = 5 + (n - 1) \ 2
    if rows > 9 then rows = 9
    pairs = 8
    if n >= 4 then pairs = 8 + GameRand(g.rng, 3)
    cols = pairs * 2
    styles = ["solid", "checker", "pyramid", "diamond", "hollow", "columns", "stripes", "noise"]
    style = styles[GameRand(g.rng, styles.Count())]
    if style = g.lastStyle then style = styles[(TpIndexOf(styles, style) + 1) mod styles.Count()]
    g.lastStyle = style
    cells = TpWallCells(g, style, rows, pairs)
    filled = 0
    for each line in cells
        for each filledCell in line
            if filledCell then filled = filled + 1
        end for
    end for
    if filled < 8 then cells = TpWallCells(g, "solid", rows, pairs)
    bombP = 4 + n
    if bombP > 14 then bombP = 14
    capP = 3
    baseDigit = 1 + (n - 1) \ 4
    letters = "MMWWSSZZLPP"
    out = []
    plain = []
    caps = 0
    for row = 0 to rows - 1
        text = ""
        for col = 0 to cols - 1
            if not cells[row][col \ 2] then
                text = text + "."
            else
                roll = GameRand(g.rng, 100)
                if roll < bombP then
                    text = text + "B"
                else if roll < bombP + capP then
                    text = text + Mid(letters, GameRand(g.rng, Len(letters)) + 1, 1)
                    caps = caps + 1
                else
                    digit = baseDigit + (rows - 1 - row)
                    if digit > 7 then digit = 7
                    text = text + digit.ToStr()
                    plain.Push([row, col])
                end if
            end if
        end for
        out.Push(text)
    end for
    if caps = 0 and plain.Count() > 0 then
        spot = plain[GameRand(g.rng, plain.Count())]
        line = out[spot[0]]
        letter = Mid(letters, GameRand(g.rng, Len(letters)) + 1, 1)
        out[spot[0]] = Left(line, spot[1]) + letter + Mid(line, spot[1] + 2)
    end if
    return out
end function

' Which pairs of columns are filled in each row, for a shape.
function TpWallCells(g as object, style as string, rows as integer, pairs as integer) as object
    half = (pairs - 1) / 2
    middle = (rows - 1) / 2
    cells = []
    for row = 0 to rows - 1
        line = []
        for p = 0 to pairs - 1
            cell = true
            if style = "checker" then
                cell = ((row + p) mod 2 = 0)
            else if style = "pyramid" then
                cell = (Abs(p - half) <= (row + 1) / rows * (half + 0.5))
            else if style = "diamond" then
                cell = (Abs(p - half) <= (1 - Abs(row - middle) / (middle + 0.5)) * (half + 0.5))
            else if style = "hollow" then
                cell = (row = 0 or row = rows - 1 or p = 0 or p = pairs - 1 or (row = Int(middle + 0.5) and p > 1 and p < pairs - 2))
            else if style = "columns" then
                cell = (p mod 2 = 0 or row = 0)
            else if style = "stripes" then
                cell = ((row + p) mod 4 < 2)
            else if style = "noise" then
                cell = (GameRand(g.rng, 100) < 65)
            end if
            line.Push(cell)
        end for
        cells.Push(line)
    end for
    return cells
end function

function TpIndexOf(list as object, item as string) as integer
    for i = 0 to list.Count() - 1
        if list[i] = item then return i
    end for
    return -1
end function

' --- The remote ---

' A key pressed: "left" or "right" (the paddle moves while it's held, faster and faster), or
' "launch" (OK: serves the ball, lets go of a stuck one, or goes on to the next wall).
sub TpPress(g as object, action as string)
    if g.state = "over" or g.state = "won" then return
    if action = "left" or action = "right" then
        dir = -1
        if action = "right" then dir = 1
        if g.paddle.dir = dir then return
        g.paddle.dir = dir
        g.paddle.heldMs = 0
    else if action = "launch" then
        if g.state = "clear" then
            TpStartWall(g, g.wall + 1)
            TpEvent(g, "next", g.wall)
        else if g.state = "ready" then
            TpRelease(g, "stuck")
            g.state = "play"
            TpEvent(g, "launch", invalid)
        else if g.state = "play" then
            TpLetGoBalls(g)
        end if
    end if
end sub

sub TpRelease(g as object, action as string)
    if action = "left" and g.paddle.dir = -1 then g.paddle.dir = 0
    if action = "right" and g.paddle.dir = 1 then g.paddle.dir = 0
    if action = "stuck" then TpLetGoBalls(g)
end sub

sub TpLetGo(g as object)
    g.paddle.dir = 0
    g.paddle.heldMs = 0
end sub

' Every ball resting on the paddle leaves it, at an angle by where it sits: straight up from
' the middle, up to 60 degrees off at the ends, with a little wobble.
sub TpLetGoBalls(g as object)
    p = g.paddle
    for each b in g.balls
        if b.stuck then
            hit = b.dx / (p.w / 2)
            if hit > 1 then hit = 1
            if hit < -1 then hit = -1
            wobble = (GameRand(g.rng, 1000) / 1000.0 * 0.5 - 0.25) * (1 - Abs(hit))
            angle = hit * 1.0471976 + wobble
            speed = b.speed
            if speed <= 0 then speed = TpSpeed(g)
            b.speed = speed
            b.vx = Sin(angle) * speed
            b.vy = -Cos(angle) * speed
            b.stuck = false
        end if
    end for
end sub

' --- Time ---

' The game moves on by dt milliseconds, in steps of 8 ms so a fast ball can't pass through a
' brick.
sub TpTick(g as object, dt as integer)
    if g.state = "over" or g.state = "won" then return
    if g.state = "play" then g.elapsed = g.elapsed + dt
    ms = g.carry + dt
    stepMs = g.rules.stepMs
    while ms >= stepMs
        ms = ms - stepMs
        TpStep(g, stepMs)
        if g.state = "over" or g.state = "won" then exit while
    end while
    g.carry = ms
end sub

sub TpStep(g as object, dt as integer)
    r = g.rules
    p = g.paddle
    sec = dt / 1000.0
    ' the paddle: it starts slow and speeds up while the key is held
    if p.dir <> 0 then
        p.heldMs = p.heldMs + dt
        speed = r.paddleStart + r.paddleAccel * p.heldMs / 1000.0
        if speed > r.paddleMax then speed = r.paddleMax
        p.x = p.x + p.dir * speed * sec
        if p.x < 0 then p.x = 0.0
        if p.x > r.w - p.w then p.x = r.w - p.w
    end if
    TpCarryStuck(g)
    if g.state <> "play" then return
    ' the timed capsules wear off; a sticky paddle that wears off lets its balls go
    if g.stickyMs > 0 then
        g.stickyMs = g.stickyMs - dt
        if g.stickyMs <= 0 then
            g.stickyMs = 0
            TpLetGoBalls(g)
        end if
    end if
    if g.slowMs > 0 then
        g.slowMs = g.slowMs - dt
        if g.slowMs < 0 then g.slowMs = 0
    end if
    TpMoveBalls(g, sec)
    if g.state <> "play" then return
    TpMoveCapsules(g, sec)
    TpBlasts(g, dt)
end sub

' Balls resting on the paddle ride along with it.
sub TpCarryStuck(g as object)
    r = g.rules
    p = g.paddle
    for each b in g.balls
        if b.stuck then
            b.x = p.x + p.w / 2 + b.dx - r.ball / 2
            b.y = r.paddleY - r.ball - 1.0
        end if
    end for
end sub

sub TpMoveBalls(g as object, sec as float)
    r = g.rules
    p = g.paddle
    s = r.ball
    slow = 1.0
    if g.slowMs > 0 then slow = r.slowFactor
    kept = []
    for each b in g.balls
        lost = false
        if not b.stuck then
            px = b.x
            py = b.y
            b.x = b.x + b.vx * sec * slow
            b.y = b.y + b.vy * sec * slow
            ' the field's sides and top
            if b.x < 0 then
                b.x = 0.0
                b.vx = Abs(b.vx)
            else if b.x + s > r.w then
                b.x = r.w - s
                b.vx = -Abs(b.vx)
            end if
            if b.y < 0 then
                b.y = 0.0
                b.vy = Abs(b.vy)
            end if
            ' gone once any of it would be past the bottom, so it is never drawn outside
            if b.y + s > r.h then
                lost = true
            else
                TpPaddleHit(g, b)
                if not b.stuck then TpBrickHit(g, b, px, py)
            end if
        end if
        if lost then
            TpEvent(g, "lost", invalid)
        else
            kept.Push(b)
        end if
    end for
    ' the last brick went during this step: the wall's end has already put the balls away
    if g.state <> "play" then return
    g.balls = kept
    if g.balls.Count() = 0 then TpLoseLife(g)
end sub

' A ball coming down onto the paddle bounces off it at an angle by where it hit (straight up in
' the middle, 60 degrees at the ends), or stays on it while the paddle is sticky.
sub TpPaddleHit(g as object, b as object)
    r = g.rules
    p = g.paddle
    s = r.ball
    if b.vy <= 0 then return
    if b.y + s < r.paddleY or b.y > r.paddleY + r.paddleH then return
    if b.x + s < p.x or b.x > p.x + p.w then return
    center = b.x + s / 2 - (p.x + p.w / 2)
    if g.stickyMs > 0 then
        if center > p.w / 2 then center = p.w / 2
        if center < -p.w / 2 then center = -p.w / 2
        b.stuck = true
        b.dx = center
        b.vx = 0.0
        b.vy = 0.0
        ' (not TpCarryStuck: this runs inside TpMoveBalls' loop over the balls, and a second
        ' loop over the same list would end the first one)
        b.x = p.x + p.w / 2 + b.dx - s / 2
        b.y = r.paddleY - s - 1.0
        TpEvent(g, "paddle", invalid)
        return
    end if
    hit = center / (p.w / 2)
    if hit > 1 then hit = 1
    if hit < -1 then hit = -1
    angle = hit * 1.0471976
    b.vx = Sin(angle) * b.speed
    b.vy = -Cos(angle) * b.speed
    b.y = r.paddleY - s - 0.5
    TpEvent(g, "paddle", invalid)
end sub

' Of the bricks the ball overlaps (only the few around it are looked at), the one it overlaps
' most goes, and the ball turns back off the face it came in through: px, py is where it was
' before this step, so a ball that was below the brick hit its underside, one that was beside
' it hit a side, and one that was off a corner hit the face it crossed last. (Going by the
' overlap alone would turn a fast ball rising under a gap sideways now and then.)
sub TpBrickHit(g as object, b as object, px as float, py as float)
    r = g.rules
    s = r.ball
    stepX = g.brickW + r.gap
    stepY = r.brickH + r.gap
    c1 = Int((b.x - r.margin) / stepX)
    c2 = Int((b.x + s - r.margin) / stepX)
    r1 = Int((b.y - r.brickTop) / stepY)
    r2 = Int((b.y + s - r.brickTop) / stepY)
    best = invalid
    bestArea = 0.0
    bestOx = 0.0
    bestOy = 0.0
    for row = r1 to r2
        if row >= 0 and row < g.rows then
            for col = c1 to c2
                if col >= 0 and col < g.cols then
                    k = g.grid[row][col]
                    if k <> invalid then
                        if k.alive then
                            ox = TpMin(b.x + s, k.x + g.brickW) - TpMax(b.x, k.x)
                            oy = TpMin(b.y + s, k.y + r.brickH) - TpMax(b.y, k.y)
                            if ox > 0 and oy > 0 and ox * oy > bestArea then
                                best = k
                                bestArea = ox * oy
                                bestOx = ox
                                bestOy = oy
                            end if
                        end if
                    end if
                end if
            end for
        end if
    end for
    if best = invalid then return
    viaEnd = (py + s <= best.y or py >= best.y + r.brickH)
    viaSide = (px + s <= best.x or px >= best.x + g.brickW)
    if viaEnd and viaSide then
        sideways = (bestOx * Abs(b.y - py) < bestOy * Abs(b.x - px))
    else if viaSide then
        sideways = true
    else if viaEnd then
        sideways = false
    else
        sideways = (bestOx < bestOy)
    end if
    if sideways then
        b.vx = -b.vx
        if b.x + s / 2 < best.x + g.brickW / 2 then
            b.x = b.x - bestOx
        else
            b.x = b.x + bestOx
        end if
    else
        b.vy = -b.vy
        if b.y + s / 2 < best.y + r.brickH / 2 then
            b.y = b.y - bestOy
        else
            b.y = b.y + bestOy
        end if
    end if
    TpKill(g, best)
    TpCheckClear(g)
end sub

function TpMin(a as float, b as float) as float
    if a < b then return a
    return b
end function

function TpMax(a as float, b as float) as float
    if a > b then return a
    return b
end function

' A brick goes: its points (times the difficulty's), a bomb's blast a moment later, a capsule
' brick's capsule.
sub TpKill(g as object, k as object)
    k.alive = false
    g.score = g.score + Int(k.points * g.diff.score + 0.5)
    TpEvent(g, "brick", k)
    if k.kind = "bomb" then
        g.blasts.Push({ row: k.row, col: k.col, ms: g.rules.bombDelay })
    else if k.kind = "capsule" then
        cap = k.cap
        if cap = "mystery" then cap = g.rules.mystery[GameRand(g.rng, g.rules.mystery.Count())]
        r = g.rules
        g.capsules.Push({ x: k.x + g.brickW / 2 - r.capsuleW / 2, y: k.y, kind: cap })
    end if
end sub

' A bomb's blast takes the eight bricks around it (another bomb among them blasts in turn).
sub TpBlasts(g as object, dt as integer)
    if g.blasts.Count() = 0 then return
    waiting = []
    due = []
    for each bl in g.blasts
        bl.ms = bl.ms - dt
        if bl.ms <= 0 then
            due.Push(bl)
        else
            waiting.Push(bl)
        end if
    end for
    g.blasts = waiting
    for each bl in due
        TpEvent(g, "bomb", bl)
        for dr = -1 to 1
            for dc = -1 to 1
                row = bl.row + dr
                col = bl.col + dc
                if (dr <> 0 or dc <> 0) and row >= 0 and row < g.rows and col >= 0 and col < g.cols then
                    k = g.grid[row][col]
                    if k <> invalid then
                        if k.alive then TpKill(g, k)
                    end if
                end if
            end for
        end for
    end for
    TpCheckClear(g)
end sub

' Capsules fall; one the paddle catches does its thing, one that falls past is gone as soon as
' any of it would be below the field.
sub TpMoveCapsules(g as object, sec as float)
    if g.capsules.Count() = 0 then return
    r = g.rules
    p = g.paddle
    kept = []
    for each cap in g.capsules
        cap.y = cap.y + r.capsuleSpeed * sec
        caught = (cap.y + r.capsuleH >= r.paddleY and cap.y <= r.paddleY + r.paddleH and cap.x + r.capsuleW >= p.x and cap.x <= p.x + p.w)
        if caught then
            TpApply(g, cap.kind)
        else if cap.y + r.capsuleH <= r.h then
            kept.Push(cap)
        end if
    end for
    g.capsules = kept
end sub

sub TpApply(g as object, kind as string)
    r = g.rules
    if kind = "multi" then
        TpMoreBalls(g)
    else if kind = "wide" then
        g.wide = true
        TpPaddleWidth(g, g.diff.paddleW * r.wideFactor)
    else if kind = "sticky" then
        g.stickyMs = r.stickyMs
    else if kind = "slow" then
        g.slowMs = r.slowMs
    else if kind = "life" then
        g.lives = g.lives + 1
    end if
    TpEvent(g, "catch", kind)
end sub

' Two more balls from a moving one (or the one on the paddle), 0.4 rad to either side of its
' way, six balls at most.
sub TpMoreBalls(g as object)
    if g.balls.Count() = 0 then return
    src = g.balls[0]
    for each b in g.balls
        if not b.stuck then
            src = b
            exit for
        end if
    end for
    speed = src.speed
    if speed <= 0 then speed = TpSpeed(g)
    base = -1.5707963
    if not src.stuck then base = TpAtan2(src.vy, src.vx)
    for each turn in [-0.4, 0.4]
        if g.balls.Count() >= g.rules.maxBalls then exit for
        a = base + turn
        g.balls.Push({ x: src.x, y: src.y, vx: Cos(a) * speed, vy: Sin(a) * speed, speed: speed, stuck: false, dx: 0.0 })
    end for
end sub

function TpAtan2(y as float, x as float) as float
    if x > 0 then return Atn(y / x)
    if x < 0 and y >= 0 then return Atn(y / x) + 3.1415927
    if x < 0 then return Atn(y / x) - 3.1415927
    if y > 0 then return 1.5707963
    if y < 0 then return -1.5707963
    return 0.0
end function

' The paddle takes a new width around its middle, within the field.
sub TpPaddleWidth(g as object, w as float)
    p = g.paddle
    middle = p.x + p.w / 2
    p.w = w
    p.x = middle - w / 2
    if p.x < 0 then p.x = 0.0
    if p.x > g.rules.w - w then p.x = g.rules.w - w
    for each b in g.balls
        if b.stuck then
            if b.dx > w / 2 then b.dx = w / 2
            if b.dx < -w / 2 then b.dx = -w / 2
        end if
    end for
    TpCarryStuck(g)
end sub

' No ball left: a life goes, the capsules wear off and the ball is served again; with no lives
' left the run is over.
sub TpLoseLife(g as object)
    g.lives = g.lives - 1
    g.capsules = []
    if g.lives <= 0 then
        g.lives = 0
        TpEnd(g, "over")
        return
    end if
    g.wide = false
    g.stickyMs = 0
    g.slowMs = 0
    TpPaddleWidth(g, g.diff.paddleW + 0.0)
    TpServe(g)
    TpEvent(g, "life", g.lives)
end sub

' Every brick gone: on to the next wall (OK), or after the sixth campaign wall, the end.
sub TpCheckClear(g as object)
    if g.state <> "play" then return
    for each k in g.bricks
        if k.alive then return
    end for
    g.blasts = []
    if g.mode = "campaign" and g.wall >= TpCampaign().Count() - 1 then
        TpEnd(g, "won")
        return
    end if
    g.state = "clear"
    g.balls = []
    g.capsules = []
    TpLetGo(g)
    TpEvent(g, "clear", g.wall)
end sub

sub TpEnd(g as object, how as string)
    g.state = how
    TpLetGo(g)
    TpEvent(g, how, invalid)
end sub

' For the screen and the speakers: "launch", "paddle", "brick" (with the brick), "bomb",
' "catch" (with the capsule), "lost" (a ball), "life" (a life lost), "clear", "next", "won",
' "over".
sub TpEvent(g as object, kind as string, data as dynamic)
    g.events.Push({ kind: kind, data: data })
end sub

function TpBricksLeft(g as object) as integer
    n = 0
    for each k in g.bricks
        if k.alive then n = n + 1
    end for
    return n
end function

' --- The demo in the guide's preview box: the game plays itself. ---

function TpDemoNew(seed as integer) as object
    return { g: TpNew("endless", "normal", seed), rest: 0, seed: seed, wait: 0 }
end function

' The demo's player keeps the paddle under the lowest ball coming down (else drifts to the
' middle), serves after a moment, and goes on to the next wall; after the end it starts over.
sub TpDemoStep(d as object, dt as integer)
    g = d.g
    g.events.Clear()
    if g.state = "over" or g.state = "won" then
        d.rest = d.rest + dt
        if d.rest > 2500 then
            d.seed = d.seed + 7919
            d.g = TpNew("endless", "normal", d.seed)
            d.rest = 0
        end if
        return
    end if
    if g.state = "ready" or g.state = "clear" then
        d.wait = d.wait + dt
        if d.wait > 900 then
            d.wait = 0
            TpPress(g, "launch")
        end if
    end if
    r = g.rules
    target = r.w / 2
    lowest = -1.0
    for each b in g.balls
        if not b.stuck and b.vy > 0 and b.y > lowest then
            lowest = b.y
            target = b.x + r.ball / 2
        end if
    end for
    p = g.paddle
    middle = p.x + p.w / 2
    want = 0
    if target < middle - p.w * 0.2 then want = -1
    if target > middle + p.w * 0.2 then want = 1
    if want <> p.dir then
        if p.dir = -1 then TpRelease(g, "left")
        if p.dir = 1 then TpRelease(g, "right")
        if want = -1 then TpPress(g, "left")
        if want = 1 then TpPress(g, "right")
    end if
    TpTick(g, dt)
end sub
