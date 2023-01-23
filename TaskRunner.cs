namespace WebStunnel; 

internal class TaskRunner {
    private readonly List<Task> newTasks;
    private readonly SemaphoreSlim sem, mutex;
    private readonly bool active;

    internal TaskRunner() {
        sem = new SemaphoreSlim(0);
        mutex = new SemaphoreSlim(1);
        active = true;

        newTasks = new List<Task> { WaitLoop() };
    }

    internal async Task AddTask(Task task) {
        await PrivateAdd(task);
        sem.Release();
    }

    internal async Task RunAll() {
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

        if (active) {
            await PrivateAdd(WaitLoop());
        }
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