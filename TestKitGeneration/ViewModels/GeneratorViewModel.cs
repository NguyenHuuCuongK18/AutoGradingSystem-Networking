using AutoGrading.Common.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TestKitGeneration.Views;

namespace TestKitGeneration.ViewModels
{
    public partial class GeneratorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string clientPath = "";

        [ObservableProperty]
        private string serverPath = "";

        [ObservableProperty]
        private string saveLocation = "";

        [ObservableProperty]
        private string testCaseName = "";
        partial void OnTestCaseNameChanged(string value)
        {
            if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, @"^[A-Za-z0-9_-]+$"))
            {
                MessageBox.Show("Test case name can only contain letters, numbers, underscores, or hyphens.", "Invalid Input");
                TestCaseName = "";
            }
            Console.WriteLine($"TestCaseName changed to: {value}");
        }

        [ObservableProperty]
        private ObservableCollection<StepDataItem> stepData = new();

        [ObservableProperty]
        private string stageLabel = "Current Stage: 1";

        [ObservableProperty]
        private string inputText = "";

        [ObservableProperty]
        private string totalPoints = "0";
        partial void OnTotalPointsChanged(string value)
        {
            Console.WriteLine($"TotalPoints changed to: {value}");
        }

        [ObservableProperty]
        private ObservableCollection<string> currentTestCases = new();

        [ObservableProperty]
        private UserControl currentScreen;

        private ProcessRunner? clientRunner;
        private ProcessRunner? serverRunner;
        private int currentStage = 1;
        private readonly List<string> inputs = new();
        private readonly List<(string ClientOut, string ServerOut)> stepOutputs = new();
        private const int OutputDelayMs = 500;

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

            CurrentTestCases = new ObservableCollection<string>(Directory.GetDirectories(SaveLocation)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>());

            CurrentScreen = new ViewScreen();
        }

        [RelayCommand]
        private void CreateNewTestCase()
        {
            TestCaseName = "";
            StepData.Clear();
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
                MessageBox.Show("Test case name is required.", "Warning");
                return;
            }
            if (!int.TryParse(TotalPoints, out int points) || points <= 0)
            {
                MessageBox.Show("Total points must be a valid positive integer.", "Warning");
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

            serverRunner.OnStdoutLine += line => { };
            clientRunner.OnStdoutLine += line => { };

            try
            {
                await serverRunner.StartAsync(ServerPath);
                await clientRunner.StartAsync(ClientPath);
                StepData.Clear();
                StepData.Add(new StepDataItem { StepNumber = 1, InputText = "(none project just started)", ServerOutput = "", ClientOutput = "" });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start processes: {ex.Message}", "Error");
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
            StepData.Clear();
            for (int i = 0; i < inputs.Count; i++)
            {
                StepData.Add(new StepDataItem { StepNumber = i + 1, InputText = inputs[i], ServerOutput = stepOutputs[i].ServerOut, ClientOutput = stepOutputs[i].ClientOut });
            }

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

            Cleanup();

            CurrentTestCases = new ObservableCollection<string>(Directory.GetDirectories(SaveLocation)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>());
            CurrentScreen = new ViewScreen();

            MessageBox.Show("Recorded successfully.", "Success");
        }

        [RelayCommand]
        private void EndProcesses()
        {
            Cleanup();
        }

        [RelayCommand]
        private void BackToView()
        {
            Cleanup();
            CurrentTestCases = new ObservableCollection<string>(Directory.GetDirectories(SaveLocation)
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>());
            CurrentScreen = new ViewScreen();
        }

        public void Cleanup()
        {
            clientRunner?.Dispose();
            serverRunner?.Dispose();
            clientRunner = null;
            serverRunner = null;
            inputs.Clear();
            stepOutputs.Clear();
            StepData.Clear();
            currentStage = 1;
            StageLabel = "Current Stage: 1";
        }
    }

    public class StepDataItem
    {
        public int StepNumber { get; set; }
        public string InputText { get; set; } = "";
        public string ServerOutput { get; set; } = "";
        public string ClientOutput { get; set; } = "";
    }
}