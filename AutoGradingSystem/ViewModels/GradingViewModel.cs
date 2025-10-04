using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AutoGrading.Common.Models;
using AutoGrading.Common.Services;
using Ookii.Dialogs.Wpf;

namespace AutoGradingSystem.ViewModels
{
    /// <summary>
    /// ViewModel for grading. Loads test cases, runs processes, compares per step, saves results.
    /// Expansion: Add parallel grading, custom comparers, or timeout settings.
    /// </summary>
    public partial class GradingViewModel : ObservableObject
    {
        [ObservableProperty] private string testCasesFolder = "";
        [ObservableProperty] private string studentClientPath = "";
        [ObservableProperty] private string studentServerPath = "";
        [ObservableProperty] private string saveLogFolder = "";
        [ObservableProperty] private string consoleOutput = "";

        private ProcessRunner? currentClientRunner;
        private ProcessRunner? currentServerRunner;
        private const int OutputDelayMs = 500;

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

            var headerPath = Path.Combine(TestCasesFolder, "header.xlsx");
            if (!File.Exists(headerPath))
            {
                MessageBox.Show("header.xlsx not found.", "Error");
                return;
            }

            var (_, barem) = HeaderManager.ReadGlobalHeader(headerPath);

            var testCases = LoadTestCases(barem);
            if (!testCases.Any())
            {
                MessageBox.Show("No valid test cases found.", "Error");
                return;
            }

            var resultFolder = Path.Combine(SaveLogFolder, $"TestResult-{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(resultFolder);

            int totalAwarded = 0;
            int grandTotal = testCases.Sum(tc => tc.TotalPoints);

            foreach (var tc in testCases)
            {
                AppendConsole($"Running {tc.Name}...\n");

                var stepResults = new List<(int Step, string ExpectedClient, string ExpectedServer, string ActualClient, string ActualServer, bool Success)>();
                int awarded = 0;
                double pointsPerStep = (double)tc.TotalPoints / tc.Inputs.Count;

                currentServerRunner = new ProcessRunner();
                currentClientRunner = new ProcessRunner();

                currentServerRunner.OnStdoutLine += line => AppendConsole(line);
                currentClientRunner.OnStdoutLine += line => AppendConsole(line);

                try
                {
                    await currentServerRunner.StartAsync(StudentServerPath);
                    await currentClientRunner.StartAsync(StudentClientPath);

                    for (int i = 0; i < tc.Inputs.Count; i++)
                    {
                        await currentClientRunner.WriteInputAsync(tc.Inputs[i]);
                        await Task.Delay(OutputDelayMs);

                        var actualClient = currentClientRunner.ReadAndClearBuffer();
                        var actualServer = currentServerRunner.ReadAndClearBuffer();

                        var expClient = tc.ExpectedOutputs[i].ExpectedClientOutput;
                        var expServer = tc.ExpectedOutputs[i].ExpectedServerOutput;

                        var diffClient = DiffUtil.GetUnifiedDiffString(expClient, actualClient);
                        var diffServer = DiffUtil.GetUnifiedDiffString(expServer, actualServer);

                        bool success = diffClient == null && diffServer == null;
                        stepResults.Add((i + 1, expClient, expServer, actualClient, actualServer, success));

                        if (success) awarded += (int)Math.Ceiling(pointsPerStep);
                        else
                        {
                            if (diffClient != null) AppendConsole($"Client diff: {diffClient}\n");
                            if (diffServer != null) AppendConsole($"Server diff: {diffServer}\n");
                        }
                    }

                    var resultPath = Path.Combine(resultFolder, $"{tc.Name}result.xlsx");
                    ExcelExporter.CreateResultSpreadsheet(resultPath, stepResults, tc.Name, awarded, tc.TotalPoints);

                    AppendConsole($"{tc.Name} completed. Awarded {awarded}/{tc.TotalPoints}\n");
                    totalAwarded += awarded;
                }
                catch (Exception ex)
                {
                    AppendConsole($"Error in {tc.Name}: {ex.Message}\n");
                }
                finally
                {
                    CleanupCurrent();
                }
            }

            AppendConsole($"Total: {totalAwarded}/{grandTotal}\n");
            MessageBox.Show("Grading completed.", "Success");
        }

        [RelayCommand]
        private void EndProcesses()
        {
            CleanupCurrent();
            AppendConsole("Processes ended.\n");
        }

        private List<TestCase> LoadTestCases(Dictionary<string, int> barem)
        {
            var testCases = new List<TestCase>();
            foreach (var dir in Directory.GetDirectories(TestCasesFolder))
            {
                var name = Path.GetFileName(dir);
                var xlsx = Path.Combine(dir, "testcase.xlsx");
                if (File.Exists(xlsx) && barem.TryGetValue(name, out int points))
                {
                    var steps = ExcelExporter.ReadTestCaseFile(xlsx);
                    var tc = new TestCase { Name = name, FolderPath = dir, TotalPoints = points };
                    tc.Inputs = steps.Select(s => s.Input).ToList();
                    tc.ExpectedOutputs = steps.Select(s => (s.ClientOutput, s.ServerOutput)).ToList();
                    testCases.Add(tc);
                }
            }
            return testCases;
        }

        private void AppendConsole(string text)
        {
            ConsoleOutput += text;
        }

        private void CleanupCurrent()
        {
            currentClientRunner?.Dispose();
            currentServerRunner?.Dispose();
            currentClientRunner = null;
            currentServerRunner = null;
        }
    }
}