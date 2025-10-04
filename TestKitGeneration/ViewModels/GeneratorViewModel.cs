using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AutoGrading.Common.Services;
using Ookii.Dialogs.Wpf;
using TestKitGeneration.Views;

namespace TestKitGeneration.ViewModels
{
    /// <summary>
    /// ViewModel for test case generation with multi-screen flow: Setup -> View -> Generate.
    /// Manages client/server paths, test case creation, and output capture.
    /// Expansion: Add path validation, progress indicators, or test case deletion.
    /// </summary>
    public partial class GeneratorViewModel : ObservableObject
    {
        [ObservableProperty] private string clientPath = "";
        [ObservableProperty] private string serverPath = "";
        [ObservableProperty] private string saveLocation = "";
        [ObservableProperty] private string testCaseName = "";
        [ObservableProperty] private string consoleOutput = "";
        [ObservableProperty] private string stageLabel = "Current Stage: 1";
        [ObservableProperty] private string inputText = "";
        [ObservableProperty] private string totalPoints = "0";
        [ObservableProperty] private List<string> currentTestCases = new();
        [ObservableProperty] private UserControl currentScreen;

        private ProcessRunner? clientRunner;
        private ProcessRunner? serverRunner;
        private int currentStage = 1;
        private readonly List<string> inputs = new();
        private readonly List<(string ClientOut, string ServerOut)> stepOutputs = new();
        private const int OutputDelayMs = 500;  // Expansion: Configurable delay

        public GeneratorViewModel()
        {
            CurrentScreen = new SetupScreen();
        }

        [RelayCommand]
        private void SelectClient()
        {
            var dlg = new OpenFileDialog { Filter = "Executables/DLLs (*.exe;*.dll)|*.exe;*.dll" };
            if (dlg.ShowDialog() == true) ClientPath = dlg.FileName;
        }

        [RelayCommand]
        private void SelectServer()
        {
            var dlg = new OpenFileDialog { Filter = "Executables/DLLs (*.exe;*.dll)|*.exe;*.dll" };
            if (dlg.ShowDialog() == true) ServerPath = dlg.FileName;
        }

        [RelayCommand]
        private void SelectSaveLocation()
        {
            var dlg = new VistaFolderBrowserDialog();
            if (dlg.ShowDialog() == true) SaveLocation = dlg.SelectedPath;
        }

        [RelayCommand]
        private void ProceedToView()
        {
            if (string.IsNullOrEmpty(ClientPath) || string.IsNullOrEmpty(ServerPath) || string.IsNullOrEmpty(SaveLocation))
            {
                MessageBox.Show("All fields required.", "Error");
                return;
            }
            if (!File.Exists(ClientPath) || !File.Exists(ServerPath) || !Directory.Exists(SaveLocation))
            {
                MessageBox.Show("Invalid file or directory paths.", "Error");
                return;
            }

            // Load existing test cases
            CurrentTestCases = Directory.GetDirectories(SaveLocation)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()
                .ToList();

            CurrentScreen = new ViewScreen();
        }

        [RelayCommand]
        private void CreateNewTestCase()
        {
            // Clear previous test case data
            TestCaseName = "";
            ConsoleOutput = "";
            StageLabel = "Current Stage: 1";
            InputText = "";
            TotalPoints = "0";
            inputs.Clear();
            stepOutputs.Clear();
            currentStage = 1;

            CurrentScreen = new GenerateScreen();
        }

        [RelayCommand]
        private async Task Start()
        {
            if (string.IsNullOrEmpty(TestCaseName))
            {
                MessageBox.Show("Test case name required.", "Error");
                return;
            }

            var testCaseFolder = Path.Combine(SaveLocation, TestCaseName);
            if (Directory.Exists(testCaseFolder))
            {
                if (MessageBox.Show($"Folder '{TestCaseName}' exists. Overwrite?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;
            }
            Directory.CreateDirectory(testCaseFolder);

            serverRunner = new ProcessRunner();
            clientRunner = new ProcessRunner();

            serverRunner.OnStdoutLine += line => AppendConsole(line);
            clientRunner.OnStdoutLine += line => AppendConsole(line);

            try
            {
                await serverRunner.StartAsync(ServerPath);
                await clientRunner.StartAsync(ClientPath);
                AppendConsole("Processes started.\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start: {ex.Message}", "Error");
                Cleanup();
            }
        }

        [RelayCommand]
        private async Task SubmitInput()
        {
            if (clientRunner == null || !clientRunner.IsRunning)
            {
                MessageBox.Show("Processes not running. Click Start.", "Error");
                return;
            }

            inputs.Add(InputText);
            await clientRunner.WriteInputAsync(InputText);
            await Task.Delay(OutputDelayMs);

            var clientDelta = clientRunner.ReadAndClearBuffer();
            var serverDelta = serverRunner?.ReadAndClearBuffer() ?? "";

            stepOutputs.Add((clientDelta, serverDelta));

            InputText = "";
            currentStage++;
            StageLabel = $"Current Stage: {currentStage}";
        }

        [RelayCommand]
        private void Record()
        {
            if (clientRunner == null || !clientRunner.IsRunning)
            {
                MessageBox.Show("Processes must be running.", "Error");
                return;
            }

            if (!int.TryParse(TotalPoints, out int points) || points <= 0)
            {
                MessageBox.Show("Enter a valid positive integer for Total Points.", "Error");
                return;
            }

            var steps = new List<(int Step, string Input, string ServerOutput, string ClientOutput)>();
            for (int i = 0; i < inputs.Count; i++)
            {
                steps.Add((i + 1, inputs[i], stepOutputs[i].ServerOut, stepOutputs[i].ClientOut));
            }

            var testCaseFolder = Path.Combine(SaveLocation, TestCaseName);
            var xlsxPath = Path.Combine(testCaseFolder, "testcase.xlsx");
            ExcelExporter.CreateTestCaseFile(xlsxPath, TestCaseName, steps);

            var headerPath = Path.Combine(SaveLocation, "header.xlsx");
            HeaderManager.UpdateOrAddBaremEntry(headerPath, TestCaseName, points);

            AppendConsole("Test case recorded.\n");
            Cleanup();

            // Return to View Screen
            CurrentTestCases = Directory.GetDirectories(SaveLocation)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()
                .ToList();
            CurrentScreen = new ViewScreen();

            MessageBox.Show("Recorded successfully.", "Success");
        }

        [RelayCommand]
        private void EndProcesses()
        {
            Cleanup();
            AppendConsole("Processes ended.\n");
        }

        [RelayCommand]
        private void BackToView()
        {
            Cleanup();
            CurrentTestCases = Directory.GetDirectories(SaveLocation)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()
                .ToList();
            CurrentScreen = new ViewScreen();
        }

        private void AppendConsole(string text)
        {
            ConsoleOutput += text;
        }

        private void Cleanup()
        {
            clientRunner?.Dispose();
            serverRunner?.Dispose();
            clientRunner = null;
            serverRunner = null;
            inputs.Clear();
            stepOutputs.Clear();
            currentStage = 1;
            StageLabel = "Current Stage: 1";
        }
    }
}