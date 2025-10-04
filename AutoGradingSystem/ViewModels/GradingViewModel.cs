using AutoGrading.Common.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AutoGradingSystem.ViewModels
{
    public partial class GradingViewModel : ObservableObject
    {
        [ObservableProperty]
        private string testCasesFolder = "";

        [ObservableProperty]
        private string studentClientPath = "";

        [ObservableProperty]
        private string studentServerPath = "";

        [ObservableProperty]
        private string saveLogFolder = "";

        [ObservableProperty]
        private ObservableCollection<GradingResultItem> resultData = new();

        private ProcessRunner? clientRunner;
        private ProcessRunner? serverRunner;
        private const int OutputDelayMs = 500;

        public GradingViewModel()
        {
        }

        [RelayCommand]
        private void SelectTestCasesFolder()
        {
            var dlg = new VistaFolderBrowserDialog();
            if (dlg.ShowDialog() == true) TestCasesFolder = dlg.SelectedPath;
        }

        [RelayCommand]
        private void SelectStudentClient()
        {
            var dlg = new OpenFileDialog { Filter = "Executables/DLLs (*.exe;*.dll)|*.exe;*.dll" };
            if (dlg.ShowDialog() == true) StudentClientPath = dlg.FileName;
        }

        [RelayCommand]
        private void SelectStudentServer()
        {
            var dlg = new OpenFileDialog { Filter = "Executables/DLLs (*.exe;*.dll)|*.exe;*.dll" };
            if (dlg.ShowDialog() == true) StudentServerPath = dlg.FileName;
        }

        [RelayCommand]
        private void SelectSaveLogFolder()
        {
            var dlg = new VistaFolderBrowserDialog();
            if (dlg.ShowDialog() == true) SaveLogFolder = dlg.SelectedPath;
        }

        [RelayCommand]
        private async Task StartGrading()
        {
            if (string.IsNullOrEmpty(TestCasesFolder) || string.IsNullOrEmpty(StudentClientPath) || string.IsNullOrEmpty(StudentServerPath) || string.IsNullOrEmpty(SaveLogFolder))
            {
                MessageBox.Show("All fields required.", "Error");
                return;
            }
            if (!Directory.Exists(TestCasesFolder) || !File.Exists(StudentClientPath) || !File.Exists(StudentServerPath) || !Directory.Exists(SaveLogFolder))
            {
                MessageBox.Show("Invalid file or directory paths.", "Error");
                return;
            }

            var headerPath = Path.Combine(TestCasesFolder, "header.xlsx");
            if (!File.Exists(headerPath))
            {
                MessageBox.Show("Header file not found.", "Error");
                return;
            }

            var barem = HeaderManager.ReadBarem(headerPath);
            ResultData.Clear();
            int totalMaxPoints = barem.Values.Sum();
            int totalAwardedPoints = 0;
            var allTestResults = new Dictionary<string, List<(int Step, string Input, string ExpectedClient, string ExpectedServer, string ActualClient, string ActualServer, bool Success)>>();

            foreach (var testCase in barem.Keys)
            {
                ResultData.Clear(); // Clear for UI display per test case
                var testCaseFolder = Path.Combine(TestCasesFolder, testCase);
                var testCasePath = Path.Combine(testCaseFolder, "testcase.xlsx");
                if (!File.Exists(testCasePath)) continue;

                var steps = ExcelExporter.ReadTestCaseFile(testCasePath);
                serverRunner = new ProcessRunner();
                clientRunner = new ProcessRunner();

                serverRunner.OnStdoutLine += line => { };
                clientRunner.OnStdoutLine += line => { };

                try
                {
                    await serverRunner.StartAsync(StudentServerPath);
                    await clientRunner.StartAsync(StudentClientPath);

                    var currentResults = new List<(int Step, string Input, string ExpectedClient, string ExpectedServer, string ActualClient, string ActualServer, bool Success)>();
                    foreach (var step in steps)
                    {
                        await clientRunner.WriteInputAsync(step.Input);

                        await Task.Delay(OutputDelayMs);

                        var clientDelta = clientRunner.ReadAndClearBuffer();
                        var serverDelta = serverRunner?.ReadAndClearBuffer() ?? "";

                        var success = DiffUtil.AreOutputsEqual(step.ServerOutput, serverDelta) && DiffUtil.AreOutputsEqual(step.ClientOutput, clientDelta);
                        currentResults.Add((step.Step, step.Input, step.ClientOutput, step.ServerOutput, clientDelta, serverDelta, success));

                        ResultData.Add(new GradingResultItem
                        {
                            StepNumber = step.Step,
                            InputText = step.Input,
                            OutputServer = serverDelta,
                            OutputClient = clientDelta,
                            ExpectedServer = step.ServerOutput,
                            ExpectedClient = step.ClientOutput
                        });
                    }

                    var awardedPoints = (int)Math.Round(currentResults.Count(r => r.Success) * (barem[testCase] / (double)steps.Count)); totalAwardedPoints += awardedPoints;
                    allTestResults[testCase] = currentResults;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Grading failed for {testCase}: {ex.Message}", "Error");
                }
                finally
                {
                    Cleanup();
                }
            }

            var resultPath = Path.Combine(SaveLogFolder, $"TestResult-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
            ExcelExporter.CreateResultSpreadsheet(resultPath, allTestResults, barem, totalAwardedPoints, totalMaxPoints);

            MessageBox.Show("Grading completed. Results saved to " + resultPath, "Success");
        }

        [RelayCommand]
        private void EndProcesses()
        {
            Cleanup();
        }

        public void Cleanup()
        {
            clientRunner?.Dispose();
            serverRunner?.Dispose();
            clientRunner = null;
            serverRunner = null;
        }
    }

    public class GradingResultItem
    {
        public int StepNumber { get; set; }
        public string InputText { get; set; } = "";
        public string OutputServer { get; set; } = "";
        public string OutputClient { get; set; } = "";
        public string ExpectedServer { get; set; } = "";
        public string ExpectedClient { get; set; } = "";
    }
}