namespace WebStunnel; 

#warning should not be used
internal class TaskRunner {
    private readonly List<Task> newTasks;
    private readonly SemaphoreSlim sem, mutex;

    internal TaskRunner() {
        sem = new SemaphoreSlim(0);
        mutex = new SemaphoreSlim(1);

        newTasks = new List<Task> { WaitLoop() };
    }

    internal async Task AddTask(Task task) {
        await PrivateAdd(task);
        sem.Release();
    }

    internal async Task RunAll( ) {
        var activeTasks = new List<Task>();
        while (true) {
            activeTasks.AddRange(await GetTasks());
            if (activeTasks.Count == 0)
                break;

            var doneTask = await Task.WhenAny(activeTasks);
            await doneTask;
            activeTasks.Remove(doneTask);
        }
    }

    private async Task WaitLoop() {
        await sem.WaitAsync();
            await PrivateAdd(WaitLoop());
    }

    private async Task PrivateAdd(Task task) {
        await mutex.WaitAsync();
        try {
            newTasks.Add(task);
        } finally {
            mutex.Release();
        }
    }

    private async Task<Task[]> GetTasks() {
        await sem.WaitAsync();
        try {
            var a = newTasks.ToArray();
            newTasks.Clear();
            return a;
        } finally {
            sem.Release();
        }
    }
}