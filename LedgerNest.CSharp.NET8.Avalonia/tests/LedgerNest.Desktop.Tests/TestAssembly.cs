// Limit concurrent SQLite setup/crypto work while keeping every fixture independent.
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = 2)]
