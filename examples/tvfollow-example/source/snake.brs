' Snake on channel 23: its rules. Pure BrightScript without UI, audio or registry
' dependencies, so tests/snake-tests.brs can test it on a PC; snakeplay.brs puts it
' on the TV and snakedraw.brs draws it.
'
' The board is 20 x 20 cells, matching the b0r3d snake arcade game (snake.html).
' The snake starts in the center, length 3, moving right. Moving past an edge wraps
' around to the opposite side. Eating food increases score by 1, grows length by 1,
' and speeds up movement from 140 ms down to 70 ms. Colliding with one's own body
' ends the run.

function SnakeModes() as object
    return ["wrap", "walls"]
end function

function SnakeRules() as object
    return {
        cols: 20,
        rows: 20,
        baseSpeed: 140,
        minSpeed: 70,
        speedDec: 3
    }
end function

function SnakeNew(seed as integer, mode = "wrap" as string) as object
    if mode <> "walls" then mode = "wrap"
    r = SnakeRules()
    rng = GameRng(seed)
    midX = r.cols \ 2
    midY = r.rows \ 2
    snake = [
        { x: midX, y: midY },
        { x: midX - 1, y: midY },
        { x: midX - 2, y: midY }
    ]
    food = SnakePlaceFood(snake, r.cols, r.rows, rng)
    return {
        mode: mode,
        cols: r.cols,
        rows: r.rows,
        baseSpeed: r.baseSpeed,
        minSpeed: r.minSpeed,
        speedDec: r.speedDec,
        snake: snake,
        dir: { x: 1, y: 0 },
        nextDir: { x: 1, y: 0 },
        turnQueue: [],
        food: food,
        score: 0,
        speed: r.baseSpeed,
        state: "play",
        elapsed: 0,
        tickClock: 0,
        rng: rng,
        events: []
    }
end function

' Places food on a random cell not occupied by the snake.
function SnakePlaceFood(snake as object, cols as integer, rows as integer, rng as object) as object
    for attempt = 1 to 100
        fx = GameRand(rng, cols)
        fy = GameRand(rng, rows)
        hit = false
        for each seg in snake
            if seg.x = fx and seg.y = fy then
                hit = true
                exit for
            end if
        end for
        if not hit then return { x: fx, y: fy }
    end for
    ' Fallback when board is densely packed: gather all empty coordinates
    empty = []
    for y = 0 to rows - 1
        for x = 0 to cols - 1
            hit = false
            for each seg in snake
                if seg.x = x and seg.y = y then
                    hit = true
                    exit for
                end if
            end for
            if not hit then empty.Push({ x: x, y: y })
        end for
    end for
    if empty.Count() = 0 then return { x: 0, y: 0 }
    idx = GameRand(rng, empty.Count())
    return empty[idx]
end function

' Buffers up to two turns ahead so rapid U-turns execute cleanly over consecutive ticks.
' Repeats and direct 180-degree reversals against the latest queued turn are ignored.
function SnakeTurnDir(g as object, nx as integer, ny as integer) as boolean
    if g = invalid or g.state <> "play" then return false
    if g.turnQueue = invalid then g.turnQueue = []
    ref = g.dir
    if g.turnQueue.Count() > 0 then ref = g.turnQueue[g.turnQueue.Count() - 1]
    if nx = ref.x and ny = ref.y then return false
    if g.snake.Count() > 1 and nx = -ref.x and ny = -ref.y then return false
    if g.turnQueue.Count() >= 2 then return false
    g.turnQueue.Push({ x: nx, y: ny })
    g.nextDir = g.turnQueue[0]
    return true
end function

' One step in the current (or next queued) direction.
function SnakeStep(g as object) as boolean
    if g = invalid or g.state <> "play" then return false
    if g.turnQueue <> invalid and g.turnQueue.Count() > 0 then
        g.dir = g.turnQueue.Shift()
        if g.turnQueue.Count() > 0 then
            g.nextDir = g.turnQueue[0]
        else
            g.nextDir = g.dir
        end if
    else
        g.dir = g.nextDir
    end if
    head = { x: g.snake[0].x + g.dir.x, y: g.snake[0].y + g.dir.y }

    if g.mode = "walls" then
        if head.x < 0 or head.x >= g.cols or head.y < 0 or head.y >= g.rows then
            g.state = "over"
            g.events.Push({ kind: "over" })
            return false
        end if
    else
        ' Wrap around edges
        if head.x < 0 then head.x = g.cols - 1
        if head.x >= g.cols then head.x = 0
        if head.y < 0 then head.y = g.rows - 1
        if head.y >= g.rows then head.y = 0
    end if

    ' Collision check: tail vacates its position on this tick unless food is eaten
    willEat = (head.x = g.food.x and head.y = g.food.y)
    bodyCount = g.snake.Count()
    if not willEat then bodyCount = bodyCount - 1
    for i = 0 to bodyCount - 1
        seg = g.snake[i]
        if seg.x = head.x and seg.y = head.y then
            g.state = "over"
            g.events.Push({ kind: "over" })
            return false
        end if
    end for

    g.snake.Unshift(head)

    if willEat then
        g.score = g.score + 1
        g.speed = g.baseSpeed - g.score * g.speedDec
        if g.speed < g.minSpeed then g.speed = g.minSpeed
        g.events.Push({ kind: "eat", score: g.score })
        if g.snake.Count() >= g.cols * g.rows then
            g.state = "won"
            g.events.Push({ kind: "won" })
        else
            g.food = SnakePlaceFood(g.snake, g.cols, g.rows, g.rng)
        end if
    else
        g.snake.Pop()
    end if
    return true
end function

' Advances time by ms milliseconds, running steps as they fall due.
sub SnakeTick(g as object, ms as integer)
    if g = invalid or g.state <> "play" then return
    g.elapsed = g.elapsed + ms
    g.tickClock = g.tickClock + ms
    while g.tickClock >= g.speed and g.state = "play"
        g.tickClock = g.tickClock - g.speed
        SnakeStep(g)
    end while
end sub

' Autopilot for the guide's preview box and demo.
sub SnakeAutoplay(g as object)
    if g = invalid or g.state <> "play" then return
    candidates = [
        { x: 0, y: -1 },
        { x: 0, y: 1 },
        { x: -1, y: 0 },
        { x: 1, y: 0 }
    ]
    head = g.snake[0]
    bestDir = invalid
    bestDist = 999999
    bestKeeps = false

    for each c in candidates
        if not (g.snake.Count() > 1 and c.x = -g.dir.x and c.y = -g.dir.y) then
            nx = head.x + c.x
            ny = head.y + c.y
            inBounds = true
            if g.mode = "walls" then
                if nx < 0 or nx >= g.cols or ny < 0 or ny >= g.rows then inBounds = false
            else
                nx = (nx + g.cols) mod g.cols
                ny = (ny + g.rows) mod g.rows
            end if
            if inBounds then
                willEat = (nx = g.food.x and ny = g.food.y)
                bodyCount = g.snake.Count()
                if not willEat then bodyCount = bodyCount - 1
                hit = false
                for i = 0 to bodyCount - 1
                    seg = g.snake[i]
                    if seg.x = nx and seg.y = ny then
                        hit = true
                        exit for
                    end if
                end for
                if not hit then
                    dx = Abs(nx - g.food.x)
                    dy = Abs(ny - g.food.y)
                    if g.mode <> "walls" then
                        if g.cols - dx < dx then dx = g.cols - dx
                        if g.rows - dy < dy then dy = g.rows - dy
                    end if
                    dist = dx + dy
                    keepsDir = (c.x = g.dir.x and c.y = g.dir.y)

                    if dist < bestDist or (dist = bestDist and keepsDir) or bestDir = invalid then
                        bestDist = dist
                        bestDir = c
                        bestKeeps = keepsDir
                    end if
                end if
            end if
        end if
    end for

    if bestDir <> invalid then
        SnakeTurnDir(g, bestDir.x, bestDir.y)
    end if
end sub

' A demo run that plays on its own in the preview box.
function SnakeDemoNew(seed as integer) as object
    g = SnakeNew(seed)
    return { g: g, clock: CreateObject("roTimespan"), restarts: 0 }
end function

sub SnakeDemoStep(d as object, ms as integer)
    if d = invalid then return
    if d.g.state <> "play" then
        d.g = SnakeNew(GameSeed())
        d.restarts = d.restarts + 1
    end if
    SnakeAutoplay(d.g)
    SnakeTick(d.g, ms)
end sub
