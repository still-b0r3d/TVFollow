' Late-night listings for the guide: a made-up program for each channel in the ON NOW column,
' changing every half hour by the TV's clock ("11:30 PM  Static Hour"). The shows are all made
' up: none is a real show, and none names one. b0r3d's own channels list what they really have.

function ChannelListing(st as object, ch as dynamic) as string
    if ch = invalid then return ""
    defaultNow = ""
    if ch.now <> invalid then defaultNow = ch.now
    c = st.clock
    if c = invalid then return defaultNow
    if c.hours = invalid or c.mins = invalid then return defaultNow
    slotMin = (c.mins \ 30) * 30
    return SlotTimeText(c.hours, slotMin, st.clock24 = true) + "  " + ShowForChannel(st, ch, c.hours, slotMin \ 30)
end function

' A half hour's start: "11:30 PM", or "23:30" on the 24-hour clock.
function SlotTimeText(hours as integer, mins as integer, is24 as boolean) as string
    if is24 then return TwoDigits(hours) + ":" + TwoDigits(mins)
    h12 = hours mod 12
    if h12 = 0 then h12 = 12
    half = "AM"
    if hours >= 12 then half = "PM"
    return h12.ToStr() + ":" + TwoDigits(mins) + " " + half
end function

' What's on a channel in a half hour of the day.
function ShowForChannel(st as object, ch as object, hour as integer, slotIndex as integer) as string
    if ch = invalid or ch.id = invalid then return ""
    slot = hour * 2 + slotIndex
    ' b0r3d's own channels (the VCR's line follows the tape, as its preview box does)
    if ch.id = "video.vcr" then
        if DeckTapeIn(st) then return "VCR: Tape In"
        return "VCR: No Tape"
    end if
    if ch.id = "cam.rukus" then return "Ruckus Cam: Live from His Room"
    if ch.id = "game.stuck" then return PickShow(StuckShows(), slot)
    if ch.id = "game.test" then return PickShow(TestPatternShows(), slot)
    if ch.id = "game.snake" then return "Snake: Twenty by Twenty, Wraparound"
    if ch.id = "radio.fm" then return "b0r3d FM: Sixteen Songs on a Loop"
    if ch.id = "promo.breaks" then return PickShow(PromoShows(), slot)
    if Left(ch.id, 8) = "tvinput." then return PickShow(InputShows(), slot)
    ' the apps it knows
    id = ch.id
    if id = "837" then return PickShow(VideoSiteShows(), slot)
    if id = "74519" then return PickShow(ClassicMovieShows(), slot)
    if id = "41468" then return PickShow(MatineeShows(), slot)
    if id = "151908" then return PickShow(OvernightShows(), slot)
    if id = "23353" then return PickShow(PublicTvShows(), slot)
    if id = "244655" then return PickShow(SpaceShows(), slot)
    if id = "12" then return PickShow(BingeShows(), slot)
    if id = "13" then return PickShow(ActionShows(), slot)
    if id = "2285" then return PickShow(ComedyShows(), slot)
    if id = "291097" then return PickShow(FamilyShows(), slot)
    if id = "2213" then return "Your Own Videos, Sorted"
    if id = "606242" then return PickShow(BedtimeShows(), slot)
    ' any other app
    return GenericListing(ch, slot)
end function

function PickShow(shows as object, slot as integer) as string
    return shows[slot mod shows.Count()]
end function

function PromoShows() as object
    return [
        "Station Promos: Chainsaw Squirrel and Friends",
        "Infomercial Hour: b0r3d 95",
        "Late-Night Broadcast: As Seen on Channel 24",
        "b0r3d Public Access: Station Bumpers and PSAs",
        "Now Showing: z0mbie crystal",
        "The Midnight Reel: Ads for Games You Can Play"
    ]
end function

function StuckShows() as object
    return [
        "Marathon: The Long Night, Level by Level",
        "Cleanup 25: Clearing the Static",
        "Sprint 40: Against the Clock",
        "Late-Night Falling Blocks",
        "Stuck Pixels: Fill It Twice",
        "Pixel Clusters: Threes and Fives"
    ]
end function

function TestPatternShows() as object
    return [
        "Colour Bars and Calibration Tone",
        "Endless Walls: The Overnight Shift",
        "Campaign: Six Walls Before Sunrise",
        "Signal Interference and White Noise",
        "Bricks, Bombs and Falling Capsules",
        "Overnight Test Card"
    ]
end function

function InputShows() as object
    return [
        "Whatever's Plugged In",
        "Live from the Back of the TV",
        "Your Own Programming",
        "Direct Feed"
    ]
end function

function VideoSiteShows() as object
    return [
        "3 A.M. Rabbit Hole: Forgotten Infomercials",
        "Synthesizer Repairs and Cat Rescues",
        "Unexplained Public Access Tapes",
        "Ten Hours of Relaxing Static",
        "Obscure Retro Computer Speedruns",
        "Deep Dive: How Old TV Tubes Work"
    ]
end function

function ClassicMovieShows() as object
    return [
        "Midnight Sci-Fi: Attack of the Cathode Ray",
        "Midnight Mystery Theater",
        "Classic TV: Unsolved Cases of 1978",
        "Giant Monsters Overnight",
        "B-Movie Marathon: Robot Island",
        "Cult Film: Night of the Living Static"
    ]
end function

function MatineeShows() as object
    return [
        "Straight-to-Tape Action Matinee",
        "Creature Feature: It Came from Sector 4",
        "Kung Fu Theater: The Night Shift Master",
        "Haunted Tape Documentary",
        "Neon Midnight: A Cyber Thriller",
        "Low-Budget Space Explorers"
    ]
end function

function OvernightShows() as object
    return [
        "City Traffic Cam: Midnight Cruising",
        "Old Sitcoms in Grainy Colour",
        "The Western Hour: Showdown at 2 A.M.",
        "Crime Solvers: The Missing Tape",
        "Weather Radar and Smooth Jazz",
        "Detective Hour in Black and White"
    ]
end function

function PublicTvShows() as object
    return [
        "Painting with Phosphor",
        "Cosmic Horizons: Quasars and Voids",
        "Appraisal Hour: 1982 Console TVs",
        "Wildlife After Dark: City Raccoons",
        "Live in the Studio: Synthesizer Quartet",
        "Science Hour: Quantum Tunneling"
    ]
end function

function SpaceShows() as object
    return [
        "Night Pass over the Pacific",
        "Radio Signals from Deep Space",
        "The Moon, Live and Quiet",
        "Deep Field Panorama",
        "Old Flight Footage on a Loop",
        "Solar Flare Watch"
    ]
end function

function BingeShows() as object
    return [
        "The Low-Res Dimension",
        "True Crime: Who Hid the Remote?",
        "Binge Watchers Anonymous: Season 4",
        "Red Neon Rain: An 80s Dystopia",
        "Midnight Kitchen Showdown",
        "Documentary: Life Inside the Picture Tube"
    ]
end function

function ActionShows() as object
    return [
        "Asteroid Highway",
        "Action Matinee: Vigilante in the Mist",
        "Sci-Fi Classic: The Day the Screen Glitched",
        "Midnight Chase: High Speed on the Interstate",
        "Underground Racing Championship",
        "Thriller: The Signal That Wouldn't Die"
    ]
end function

function ComedyShows() as object
    return [
        "Late-Night Cartoon Comedy",
        "Teen Drama: Summer at the Mall",
        "Comedy Vault: Live from the Basement",
        "Crime Investigation: Cold Lead 1994",
        "Overnight Sitcoms: The Laugh Track Years",
        "Indie Cinema: Night Flight"
    ]
end function

function FamilyShows() as object
    return [
        "Vault Classics: Old Cartoon Shorts",
        "Galactic Adventures in Hyperspace",
        "Black-and-White Cartoons of the 1930s",
        "The Deep Ocean Glows",
        "Hero Legends: The Cartoon Years",
        "Visions of the Future, as Seen in 1960"
    ]
end function

function BedtimeShows() as object
    return [
        "Goodnight, Starry Sky",
        "Sleepy Forest Animals: Moonrise",
        "Bedtime Stories with Uncle Owl",
        "Cloud Castle Slumber Party",
        "Gentle Waves for Sweet Dreams",
        "Early Morning Sunrise Cartoons"
    ]
end function

' An app b0r3d TV doesn't have shows for: what the TV's app list says it is, or a time slot.
function GenericListing(ch as object, slot as integer) as string
    if ch.tagline <> invalid then
        if ch.tagline <> "" and ch.tagline <> ch.name then return ch.tagline
    end if
    blocks = [
        "Midnight Broadcast: " + ch.name,
        ch.name + " Overnight",
        "All Night: " + ch.name,
        ch.name + ", Late Edition"
    ]
    return PickShow(blocks, slot)
end function
