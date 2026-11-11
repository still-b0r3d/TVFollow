' Pixel art. Rukus, loafing (the same sprite as b0r3d 95's buddy). In the rows a dot is
' clear; the letters are looked up in RukusColors().

function RukusLoaf() as object
    return [
        "..k...k.........",
        "..kk.kk.........",
        ".k12121kkkkkk...",
        ".k2k1k2k212121k.",
        ".k11s11k1212121k",
        ".kkOTkkk1111111k",
        ".kVUVVUV1111111k",
        ".k111111111222kk",
        "..kkkkkkkkkkkkk."
    ]
end function

' Rukus's poses for the screensaver, 16 x 16 and facing left (the same sprites as b0r3d 95's
' buddy). The letters are the same as in RukusLoaf().
function RukusPoses() as object
    return {
        stand: [
            "................", "................", "................", "................",
            "................", "..k...k.........", "..kk.kk.......k.", ".k12121k.....k1k",
            ".k3k1k3kkkkkkk1k", ".k11s11k212121kk", "..kk1kk1121211k.", "..k112111111211k",
            "..k11111111111k.", "..k1k11kkk11k1k.", "..kkkkk...kkkk..", "................"
        ],
        walk: [
            "................", "................", "................", "................",
            "................", "..k...k.........", "..kk.kk.......k.", ".k12121k.....k1k",
            ".k3k1k3kkkkkkk1k", ".k11s11k212121kk", "..kk1kk1121211k.", "..k112111111211k",
            "..k11111111111k.", "..kk11kk..kk11k.", ".kkkkk....kkkkk.", "................"
        ],
        sit: [
            "................", "................", "................", "...k...k........",
            "...kk.kk........", "..k12121k.......", "..k3k1k3k.......", "..k11s11kk......",
            "...kkOTk12k.....", "...kVUVV121k....", "...kVVUV1121k...", "...kVUVV11121k..",
            "...k1k1k111121k.", "..kk1k1k1111111k", "..k222222kkkkkk.", "...kkkkkk......."
        ],
        groom: [
            "................", "................", "................", "................",
            "...k...k........", "...kk.kk........", "..k12121k.......", "..k2k1k2kk......",
            "..kk1s11k2k.....", "..k1kOTk121k....", "..k1kVUV1121k...", "...kVVUV11121k..",
            "...kVUVV111121k.", "..kk1k1k1111111k", "..k222222kkkkkk.", "...kkkkkk......."
        ],
        loaf: [
            "................", "................", "................", "................",
            "................", "................", "..k...k.........", "..kk.kk.........",
            ".k12121kkkkkk...", ".k2k1k2k212121k.", ".k11s11k1212121k", ".kkOTkkk1111111k",
            ".kVUVVUV1111111k", ".k111111111222kk", "..kkkkkkkkkkkkk.", "................"
        ],
        loaf2: [
            "................", "................", "................", "................",
            "................", "................", "..k...k.........", "..kk.kk......kk.",
            ".k12121kkkkkk12k", ".k2k1k2k21212k2k", ".k11s11k12121k1k", ".kkOTkkk11111kkk",
            ".kVUVVUV1111111k", ".k111111111111kk", "..kkkkkkkkkkkkk.", "................"
        ],
        stretch: [
            "................", "................", "................", "..............k.",
            ".............k1k", ".............k2k", "............k1k.", "..........kkk2k.",
            "........kk12121k", "...k...k2121211k", "...kk.kk1211111k", "..k12121kVUV11k.",
            "..k3k1k3kVVU1k..", "..k11s11kk1kk1k.", "kkkkOTkk.k1kk1k.", "k11111k..kkkkkk."
        ]
    }
end function

' Fur 1, stripes 2, eyes 3, nose s, outline k; the marks O, T and V are fur, U is stripe.
function RukusColors() as object
    fur = &h9A9AA2FF
    stripe = &h5C5C66FF
    colors = {}
    colors.AddReplace("k", &h14121CFF)
    colors.AddReplace("1", fur)
    colors.AddReplace("O", fur)
    colors.AddReplace("T", fur)
    colors.AddReplace("V", fur)
    colors.AddReplace("2", stripe)
    colors.AddReplace("U", stripe)
    colors.AddReplace("3", &hC8E060FF)
    colors.AddReplace("s", &hE8B890FF)
    return colors
end function

sub DrawSprite(s as object, rows as object, colors as object, x as integer, y as integer, scale as integer)
    for r = 0 to rows.Count() - 1
        pattern = rows[r]
        for c = 0 to pattern.Len() - 1
            px = pattern.Mid(c, 1)
            if colors.DoesExist(px) then s.DrawRect(x + c * scale, y + r * scale, scale, scale, colors[px])
        end for
    end for
end sub
