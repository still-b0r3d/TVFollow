using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TVFollow.Tests
{
    // The tests open real windows. On the desktop someone is working on, a window takes the
    // keyboard as it opens: what was being typed just then lands in a test window, lost to
    // whoever typed it, and a test that counts key presses fails. So the tests run on a Windows
    // desktop of their own, which nobody sees or types on. Windows behave there as they always
    // do (they load, come forward and take the focus among themselves); they just never show
    // up on the real desktop, and nothing typed there reaches them.
    static class OwnDesktop
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr deviceMode, int flags, uint access, IntPtr security);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetThreadDesktop(IntPtr desktop);
        [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);

        const uint GenericAll = 0x10000000;

        // Runs the work on a new thread that sits on a desktop of its own, and waits for it. A
        // new thread, because one that has a window already can't move to another desktop, and
        // [STAThread] gives the first thread a hidden window. (It has to stay on Main for that:
        // the first such thread of a program gets the window, and the tests' thread isn't it.)
        // If Windows won't make the desktop, the work runs all the same, on the desktop it was
        // started from, after a line that says so.
        public static int Run(Func<int> work)
        {
            int result = 1;
            IntPtr desktop = CreateDesktop("TVFollowTests-" + Guid.NewGuid().ToString("N"), IntPtr.Zero, IntPtr.Zero, 0, GenericAll, IntPtr.Zero);
            int error = desktop == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
            var thread = new Thread(() =>
            {
                if (desktop != IntPtr.Zero && !SetThreadDesktop(desktop)) error = Marshal.GetLastWin32Error();
                if (desktop == IntPtr.Zero || error != 0)
                    Console.WriteLine("note: no desktop of their own for the tests' windows (Windows said " + error +
                        "), so they open off the screen of this one. Don't type while the tests run: it would go to them.");
                result = work();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            return result;
        }
    }
}
