---
name: run-tests
description: Run the EditMode tests of Dark Fantasy Merchant, with the Unity Editor open (through the Unity CLI) or closed (batch mode), and read their results. Use after changing code or assets, and before reporting work as done.
---

# Run the tests

Tests use the Unity Test Framework; EditMode tests are in `Assets/Tests/EditMode`. Compilation happens in the Unity Editor. Run the commands below from the project folder, through Bash; `$SCRATCH` stands for the session's scratchpad directory.

## Editor open (the usual case: `unity status` shows it `ready`)

Batch mode is impossible then: the Editor holds the project. Drive the open Editor with the Unity CLI:

1. Check play mode: `unity command eval 'return UnityEditor.EditorApplication.isPlaying;' --result-only`. In play mode a refresh reloads the domain, which wipes the session's state. Exit it first with `unity command editor_stop` (Claude may do so without asking).
2. After editing `.cs` or `.asset` files on disk: `unity recompile --format json`. It refreshes the assets, recompiles, and lists each compile error with its file and line (exit `0` compiled, `6` errors, `7` Editor unreachable). Do not run the tests on code that does not compile.
3. Run the tests. The call is synchronous and returns the results (about a minute for the whole suite):

   ```bash
   unity command run_tests --mode editor --timeout 600 --result-only > "$SCRATCH/tests.json"
   ```

   - `--filter <text>` keeps the tests whose name contains it (case-insensitive): a fixture (`LeavingRuleTests`), a method, a namespace.
   - `--timeout 600` is the CLI's wait, in seconds. The Editor stops a run after 300 s; to raise that, pass it after `--`: `... -- --timeout 900`.
   - The result is several hundred kB (one entry per test): write it to a file in the scratchpad and filter it, never read it whole.
4. Read the summary and the failures. Go by `Summary.Failed`, not by the exit code:

   ```bash
   python -c "import json,sys; d=json.load(open(sys.argv[1])); print(d['Summary']); [print(r['FullName'], '\n ', r['Message'], '\n ', (r['StackTrace'] or '')[:800]) for r in d['Results'] if r['Status'] != 'Passed']" "$SCRATCH/tests.json"
   ```

   If the command failed instead (`success: false`), read `errors[0].message`, then `unity command console --level error --result-only` for the details.

## Editor closed (batch mode)

`unity test` runs the tests in batch mode and waits for the end of the run (exit `0` passed, `8` test failures, `6` no verdict: compile error, crash, timeout):

```bash
unity test . --mode EditMode --output Logs/TestResults.xml
unity test . --mode EditMode --filter "Namespace.ClassName.MethodName" --output Logs/TestResults.xml
```

Then read the `<test-run ...>` attributes and the failed `test-case` elements of `Logs/TestResults.xml`.

Without the CLI, run the Editor directly. `Unity.exe` is a GUI executable, so `& $unity ...` returns before the run ends; wrap it in `Start-Process -Wait -PassThru` and read `ExitCode` (`0` passed, `2` test failures, `1` compile error):

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs\TestResults.xml -logFile Logs\test.log
```
