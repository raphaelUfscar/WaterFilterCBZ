global using Xunit;

// SensorParameterRegistry is a process-wide singleton whose ranges several test classes read
// (directly and via SensorViewModel). Disabling cross-class parallelization keeps the
// configuration tests, which mutate it, from racing those readers. The suite is small and fast.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
