﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ABTTestLibrary.Config;
using ABTTestLibrary.Instruments;
using ABTTestLibrary.Logging;
using ABTTestLibrary.TestSupport;
using Microsoft.VisualBasic;
using Serilog;

// NOTE: Update to .Net 7.0 & C# 11.0 when possible.
// - Used .Net FrameWork 4.8 instead of .Net 7.0 because required Texas Instruments
//   TIDP.SAA Fusion Library compiled to .Net FrameWork 2.0, incompatible with .Net 7.0, C# 11.0 & UWP.
// https://www.ti.com/tool/FUSION_USB_ADAPTER_API
// NOTE: Update to UWP instead of WinForms when possible.
// - Chose WinForms due to incompatibility of UWP with .Net Framework, and unfamiliarity with WPF.
// NOTE: With deep gratitude to https://learn.microsoft.com/en-us/docs/ & https://stackoverflow.com/!
namespace ABTTestLibrary {
    public abstract partial class TestForm : Form {
        // TODO: Refactor public (global) instance objects config & instruments into
        // private instance objects which are passed by value or reference as needed.
        protected ConfigLib configLib;
        protected ConfigTest configTest;
        protected Dictionary<String, Instrument> instruments;
        private String _currentTestKey;

        protected TestForm() { InitializeComponent(); }

        protected abstract String RunTest(Test test);
            // https://stackoverflow.com/questions/540066/calling-a-function-from-a-string-in-c-sharp
            // https://www.codeproject.com/Articles/19911/Dynamically-Invoke-A-Method-Given-Strings-with-Met
            // Override with somthing like below:
            // Type type = this.GetType();
            // MethodInfo methodInfo = type.GetMethod(test.ID, BindingFlags.Static | BindingFlags.NonPublic);
            // return (String)methodInfo.Invoke(this, new object[] { test, base.instruments });

        private void Form_Shown(Object sender, EventArgs e) {
            this.configLib = ConfigLib.Get();
            this.instruments = Instrument.Get();
            InstrumentTasks.Test(this.instruments);
            this.ButtonSelectGroup.Enabled = true;
            this.ButtonStart.Enabled = false;
            this.ButtonStop.Enabled = false;
            this.ButtonSaveOutput.Enabled = false;
            this.ButtonOpenTestDataFolder.Enabled = false;
            this.rtfResults.Text = String.Empty;
            this.TextUUTResult.Text = String.Empty;
            this.TextUUTResult.BackColor = Color.White;
        }

        private void ButtonSelectGroup_Click(Object sender, EventArgs e) {
            this.configTest = ConfigTest.Get();
            this.ButtonOpenTestDataFolder.Enabled=true;
            PreRun();
        }

        private void ButtonStart_Clicked(Object sender, EventArgs e) {
            Run();
        }

        private void ButtonStop_Clicked(Object sender, EventArgs e) {
            throw new ABTAbortException($"Operator cancelled via Stop button in Test '{this.configTest.Tests[this._currentTestKey].ID}', '{this.configTest.Tests[this._currentTestKey].Summary}'.");
        }

        private void ButtonSaveOutput_Click(Object sender, EventArgs e) {
            // NOTE: Using RichTextBox instead of TextBox control in ABTTestLibraryForm for below reasons:
            // - RichTextBox doesn't have a character limit, whereas TextBox control limited to 64KByte of characters.
            //   Doubt > 64KBytes necessary, but why risk it?
            // - RichTextBox can display rich text, specifically the color coded text of EventCode.ABORT, EventCode.ERROR, 
            //   EventCode.FAIL, EventCode.PASS & EventCode.UNSET.
            SaveFileDialog sfd = new SaveFileDialog {
                Title = "Save Test Results",
                Filter = "Rich Text Format|*.rtf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                FileName = $"{this.configLib.UUT.Number}_{this.configLib.UUT.SerialNumber}",
                DefaultExt = "rtf",
                CreatePrompt = false,
                OverwritePrompt = true
            };
            DialogResult dr = sfd.ShowDialog();
            if (dr == DialogResult.OK && !String.Equals(sfd.FileName, String.Empty)) this.rtfResults.SaveFile(sfd.FileName);
        }

        private void ButtonOpenTestDataFolder_Click(Object sender, EventArgs e) {
            System.Diagnostics.Process.Start("explorer.exe", this.configLib.Logger.FilePath);
        }

        private void PreRun() {
            this.ButtonSelectGroup.Enabled = true;
            this.ButtonStart.Enabled = true;
            this.ButtonStop.Enabled = false;
            this.ButtonSaveOutput.Enabled = false;
            this.rtfResults.Text = String.Empty;
            this.TextUUTResult.Text = String.Empty;
            this.TextUUTResult.BackColor = Color.White;
            this._currentTestKey = String.Empty;
            this.Text = $"{this.configLib.UUT.Number}, {this.configLib.UUT.Description}, {this.configTest.Group.ID}";
        }

        private void Run() {
            this.configLib.UUT.SerialNumber = Interaction.InputBox(Prompt: "Please enter UUT Serial Number", Title: "Enter Serial Number", DefaultResponse: this.configLib.UUT.SerialNumber);
            if (String.Equals(this.configLib.UUT.SerialNumber, String.Empty)) return;
            InstrumentTasks.Reset(this.instruments);
            this.ButtonSelectGroup.Enabled = false;
            this.ButtonStart.Enabled = false;
            this.ButtonStop.Enabled = true;
            this.ButtonSaveOutput.Enabled = false;
            this.rtfResults.Text = String.Empty;
            this.TextUUTResult.Text = String.Empty;
            this.TextUUTResult.BackColor = Color.White;
            foreach (KeyValuePair<String, Test> t in this.configTest.Tests) {
                t.Value.Measurement = String.Empty;
                t.Value.Result = EventCodes.UNSET;
            }
            this.configLib.UUT.EventCode = EventCodes.UNSET;
            LogTasks.Start(this.configLib, this.configTest.Group, ref this.rtfResults);

            foreach (KeyValuePair<String, Test> t in this.configTest.Tests) {
                this._currentTestKey = t.Key;
                try {
                    t.Value.Measurement = RunTest(t.Value);
                    t.Value.Result = TestTasks.EvaluateTestResult(t.Value);
                } catch (Exception e) {
                    InstrumentTasks.Reset(this.instruments);
                    if (e.GetType() == typeof(ABTAbortException)) t.Value.Result = EventCodes.ABORT;
                    else {
                        t.Value.Result = EventCodes.ERROR;
                        Log.Error(e.ToString());
                        MessageBox.Show($"Unexpected error.  Details logged for analysis & resolution.{Environment.NewLine}{Environment.NewLine}" +
                            $"If reoccurs, please contact Test Engineering.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    break;
                } finally {
                    LogTasks.LogTest(t.Value);
                }
            }
            PostRun();
        }

        private void PostRun() {
            InstrumentTasks.Reset(this.instruments);
            this.configLib.UUT.EventCode = TestTasks.EvaluateUUTResult(this.configTest);
            this.TextUUTResult.Text = this.configLib.UUT.EventCode;
            this.TextUUTResult.BackColor = EventCodes.GetColor(this.configLib.UUT.EventCode);
            this._currentTestKey = String.Empty;
            LogTasks.Stop(this.configLib, this.configTest.Group);
            if (this.configLib.App.TestEventsEnabled) LogTasks.TestEvents(this.configLib.UUT);
            this.ButtonSelectGroup.Enabled = true;
            this.ButtonStart.Enabled = true;
            this.ButtonStop.Enabled = false;
            if (this.configTest.Group.Required && String.Equals(this.configLib.UUT.EventCode, EventCodes.PASS)) this.ButtonSaveOutput.Enabled = false;
            // Disallow saving output if this was a Required Group & UUT passed, because, why bother?
            // UUT passed & saved test data attesting such; take the win & $hip it.
            else this.ButtonSaveOutput.Enabled = true;
        }
    }
}