using System.Threading;

namespace AdvancedPS.Core.System
{
    public static class APSStats
    {
        private static int _activeOperations;
        private static int _activeTasks;
        
        public static int ActiveOperationsCount => _activeOperations;
        public static int ActiveTasksCount => _activeTasks;

        internal static void RegisterOperation()
        {
            Interlocked.Increment(ref _activeOperations);
        }
        internal static void UnregisterOperation()
        {
            Interlocked.Decrement(ref _activeOperations);
        }
        
        internal static void RegisterTask()
        {
            Interlocked.Increment(ref _activeTasks);
        }
        internal static void UnregisterTask()
        {
            Interlocked.Decrement(ref _activeTasks);
        }


        public static void Reset()
        {
            _activeOperations = 0;
            _activeTasks = 0;
        }
    }
}