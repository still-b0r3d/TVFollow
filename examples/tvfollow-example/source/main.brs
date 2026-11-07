' TVFollow example: a small Roku app to try pushing apps from TVFollow, and a place to
' start your own.
'
' What it shows is in components/MainScene.xml, and what it does in components/MainScene.brs.
' The lines it prints show up in TVFollow's App log.

sub Main()
    print "TVFollow example: starting"
    screen = CreateObject("roSGScreen")
    port = CreateObject("roMessagePort")
    screen.SetMessagePort(port)
    screen.CreateScene("MainScene")
    screen.Show()
    while true
        msg = wait(0, port)
        if type(msg) = "roSGScreenEvent" and msg.IsScreenClosed() then
            print "TVFollow example: closed"
            return
        end if
    end while
end sub
