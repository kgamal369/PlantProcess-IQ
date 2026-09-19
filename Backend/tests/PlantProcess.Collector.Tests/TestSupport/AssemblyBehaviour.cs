using Xunit;

// The OPC UA acceptance tests bind TCP ports and start in-process servers, so they run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]