namespace WebStunnel; 

internal class TaskRunner {
    private readonly List<Task> _newTasks;
    private readonly SemaphoreSlim _sem, _mutex;
    private readonly bool _active;

    internal TaskRunner() {
        _sem = new SemaphoreSlim(0);
        _mutex = new SemaphoreSlim(1);
        _active = true;

        _newTasks = new List<Task> { WaitLoop() };
    }

    internal async Task AddTask(Task task) {
        await PrivateAdd(task);
        _sem.Release();
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
        await _sem.WaitAsync();

        if (_active) {
            await PrivateAdd(WaitLoop());
        }
    }

    private async Task PrivateAdd(Task task) {
        await _mutex.WaitAsync();
        try {
            _newTasks.Add(task);
        } finally {
            _mutex.Release();
        }
    }

    private async Task<Task[]> GetTasks() {
        await _sem.WaitAsync();
        try {
            var a = _newTasks.ToArray();
            _newTasks.Clear();
            return a;
        } finally {
            _sem.Release();
        }
    }
}