// Integration tests boot an in-process web host via WebApplicationFactory. Those hosts share some
// process-global state (notably JwtSecurityTokenHandler's static claim-type maps), so running test
// classes in parallel can cause flaky, non-deterministic auth failures. Disable parallelization to
// keep the suite deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
