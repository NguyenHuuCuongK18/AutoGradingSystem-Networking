# AutoGradingSystem-Networking

WPF applications for generating test cases and grading client-server programs.

## Prerequisites
- .NET 8 SDK
- Windows (for WPF)

## Install Packages
- ClosedXML (0.105.0)
- CommunityToolkit.Mvvm (8.4.0)
- DiffPlex (1.9.0)
- Microsoft.VisualStudio.Azure.Containers.Tools.Targets (1.22.1, optional)
- Ookii.Dialogs.Wpf (5.0.1)

Install via NuGet or `dotnet add package`.

## Build
Open `AutoGradingSystem-Networking.sln` in Visual Studio, Build > Build Solution.

## Run
- **Generator**: Set `TestKitGeneration` as startup, press F5.
- **Grader**: Set `AutoGradingSystem` as startup, press F5.

## TestKitGeneration Usage
1. **Setup Screen**: Enter or browse client/server paths (EXE/DLL) and save location.
2. **View Screen**: View existing test cases. Click "Create New Test Case".
3. **Generate Screen**: Enter test case name, start processes, submit inputs, set total points, record to save `testcase.xlsx` and update `header.xlsx`. Click "Back to View" to create another test case.
- Repeat step 3 for multiple test cases in one session.

## AutoGradingSystem Usage
- Enter or browse test cases folder, student client/server paths, save log folder.
- Click "Start Grading" to run tests, view console output, and save results in `TestResult-<DateTime>/`.

## Structure
- **Test cases root**: `header.xlsx` (HeaderInfo: number_of_testcases; PointBarem: testcase:points) + subfolders (`tc01/testcase.xlsx`).
- **Results**: `TestResult-<DateTime>/` with per-test XLSX, failed steps highlighted in red (LightPink).

## Notes
- Each screen in TestKitGeneration is a separate XAML UserControl (SetupScreen, ViewScreen, GenerateScreen).
- Paths can be typed or browsed using Ookii.Dialogs.Wpf for folder selection.
- Stack-based UI prevents input overflow.
- Expansion: Add path validation, custom normalization in `DiffUtil`, or UI styling.