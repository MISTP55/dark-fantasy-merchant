---
name: run-tests
description: Run the EditMode tests of Dark Fantasy Merchant, with the Unity Editor open (through unity-mcp) or closed (batch mode), and read their results. Use after changing code or assets, and before reporting work as done.
---

# Run the tests

Tests use the Unity Test Framework; EditMode tests are in `Assets/Tests/EditMode`. There is no CLI build or lint step: compilation happens in the Unity Editor.

## Editor open (the usual case: `Temp/UnityLockfile` exists)

Batch mode is impossible then. Go through the `unity-mcp` tool `Unity_RunCommand`:

1. Check `EditorApplication.isPlaying`. In play mode the run fails without writing results, and a refresh reloads the domain while playing, which wipes the session's state. Exit play mode first (`EditorApplication.isPlaying = false`, as its own call), or ask the user when the session may be one they are playing.
2. After editing `.cs` or `.asset` files on disk, call `AssetDatabase.Refresh()` as its own command (it recompiles and reimports). Then check `EditorUtility.scriptCompilationFailed` and `Unity_GetConsoleLogs`.
3. In another call: `ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }))` (`UnityEditor.TestTools.TestRunner.Api`). It returns at once; the run is asynchronous. Keep the command to that plain call: registering an `ICallbacks` is refused by the tool.
4. The results are written to `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Dark Fantasy Merchant\TestResults.xml`: wait for its modification time to change, then read the `<test-run ...>` attributes and the failed `test-case` elements.

## Editor closed (batch mode)

`Unity.exe` is a GUI executable, so `& $unity ...` returns before the run ends; wrap it in `Start-Process -Wait -PassThru` and read `ExitCode` (`0` passed, `2` test failures, `1` compile error):

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"

# All EditMode tests
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs\TestResults.xml -logFile Logs\test.log

# Single test, fixture, or namespace
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.ClassName.MethodName" -testResults Logs\TestResults.xml -logFile Logs\test.log
```
