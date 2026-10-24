using System.Threading;

namespace TVFollow
{
    // Only one TVFollow follows the monitors at a time, installed or portable. When setup saves
    // new settings, the new copy asks the running one to stop and takes over.
    static class Instance
    {
        // a trial copy (TVFOLLOW_HOME) keeps to itself
        public static string SuffixFor(bool trial, string dir)
        {
            return trial ? "." + ((uint)dir.ToLowerInvariant().GetHashCode()).ToString("x8") : "";
        }

        static string Suffix { get { return SuffixFor(Settings.IsTrial, Settings.Dir); } }

        public static string MutexName { get { return @"Local\TVFollow" + Suffix; } }
        public static string QuitName { get { return @"Local\TVFollow.Quit" + Suffix; } }

        public static bool IsRunning()
        {
            Mutex mutex;
            if (!Mutex.TryOpenExisting(MutexName, out mutex)) return false;
            mutex.Dispose();
            return true;
        }

        public static bool Acquire(Mutex mutex, int ms)
        {
            try { return mutex.WaitOne(ms); }
            catch (AbandonedMutexException) { return true; } // the last copy ended without letting go
        }

        public static void AskToQuit()
        {
            using (var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitName)) quit.Set();
        }
    }
}
