﻿using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using MissionPlanner.Utilities;
using log4net;
using MissionPlanner.Controls;
using System.Collections.Generic;
using System.Net;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigRawParams : UserControl, IActivate
    {
        private static readonly ILog log =
          LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Changes made to the params between writing to the copter
        readonly Hashtable _changes = new Hashtable();

        static Hashtable tooltips = new Hashtable();

        List<GitHubContent.FileInfo> paramfiles;

        // ?
        internal bool startup = true;

        public struct paramsettings // hk's
        {
            public string name;
            public float minvalue;
            public float maxvalue;
            public float normalvalue;
            public float scale;
            public string desc;
        }


        public ConfigRawParams()
        {
            InitializeComponent();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                BUT_writePIDS_Click(null,null);
                return true;
            }

            if (keyData == (Keys.Control | Keys.F))
            {
                BUT_find_Click(null, null);
                return true;
            }

            return false;
        }


        private void BUT_load_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
                          {
                              AddExtension = true,
                              DefaultExt = ".param",
                              RestoreDirectory = true,
                              Filter = "Param List|*.param;*.parm"
                          };
            var dr = ofd.ShowDialog();

            if (dr == DialogResult.OK)
            {
                loadparamsfromfile(ofd.FileName);
            }
        }

        void loadparamsfromfile(string fn)
        {
            Hashtable param2 = Utilities.ParamFile.loadParamFile(fn);

            foreach (string name in param2.Keys)
            {
                string value = param2[name].ToString();
                // set param table as well
                foreach (DataGridViewRow row in Params.Rows)
                {
                    if (name == "SYSID_SW_MREV")
                        continue;
                    if (name == "WP_TOTAL")
                        continue;
                    if (name == "CMD_TOTAL")
                        continue;
                    if (name == "FENCE_TOTAL")
                        continue;
                    if (name == "SYS_NUM_RESETS")
                        continue;
                    if (name == "ARSPD_OFFSET")
                        continue;
                    if (name == "GND_ABS_PRESS")
                        continue;
                    if (name == "GND_TEMP")
                        continue;
                    if (name == "CMD_INDEX")
                        continue;
                    if (name == "LOG_LASTFILE")
                        continue;
                    if (name == "FORMAT_VERSION")
                        continue;
                    if (row.Cells[0].Value.ToString() == name)
                    {
                        if (row.Cells[1].Value.ToString() != value.ToString())
                            row.Cells[1].Value = value;
                        break;
                    }
                }
            }
        }

        private void BUT_save_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog
                          {
                              AddExtension = true,
                              DefaultExt = ".param",
                              RestoreDirectory = true,
                              Filter = "Param List|*.param;*.parm"
                          };

            var dr = sfd.ShowDialog();
            if (dr == DialogResult.OK)
            {
                Hashtable data = new Hashtable();
                foreach (DataGridViewRow row in Params.Rows)
                {
                    float value = float.Parse(row.Cells[1].Value.ToString());

                    data[row.Cells[0].Value.ToString()] = value;
                }

                Utilities.ParamFile.SaveParamFile(sfd.FileName,data);

            }
        }

        private void BUT_writePIDS_Click(object sender, EventArgs e)
        {
            var temp = (Hashtable)_changes.Clone();

            foreach (string value in temp.Keys)
            {
                try
                {
                    MainV2.comPort.setParam(value, (float)_changes[value]);

                    try
                    {
                        // set control as well
                        var textControls = this.Controls.Find(value, true);
                        if (textControls.Length > 0)
                        {
                            ThemeManager.ApplyThemeTo(textControls[0]);
                        }
                    }
                    catch
                    {
                        
                    }

                    try
                    {
                        // set param table as well
                        foreach (DataGridViewRow row in Params.Rows)
                        {
                            if (row.Cells[0].Value.ToString() == value)
                            {
                                row.Cells[1].Style.BackColor = ThemeManager.ControlBGColor;
                                _changes.Remove(value);
                                break;
                            }
                        }
                    }
                    catch { }

                }
                catch
                {
                    CustomMessageBox.Show("Set " + value + " Failed");
                }
            }
        }


        private void BUT_compare_Click(object sender, EventArgs e)
        {
            Hashtable param2 = new Hashtable();

            var ofd = new OpenFileDialog
                          {
                              AddExtension = true,
                              DefaultExt = ".param",
                              RestoreDirectory = true,
                              Filter = "Param List|*.param;*.parm"
                          };

            var dr = ofd.ShowDialog();
            if (dr == DialogResult.OK)
            {
                param2 = Utilities.ParamFile.loadParamFile(ofd.FileName);

                Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2);
                
                ThemeManager.ApplyThemeTo(paramCompareForm);
                paramCompareForm.ShowDialog();
            }
        }


        private void BUT_rerequestparams_Click(object sender, EventArgs e)
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return;

            if (DialogResult.OK == CustomMessageBox.Show("Update Params\nDON'T DO THIS IF YOU ARE IN THE AIR\n", "Error", MessageBoxButtons.OKCancel))
            {
                ((Control)sender).Enabled = false;

                try
                {
                    MainV2.comPort.getParamList();
                }
                catch (Exception ex)
                {
                    log.Error("Exception getting param list", ex);
                    CustomMessageBox.Show("Error: getting param list", "Error");
                }


                ((Control)sender).Enabled = true;

                startup = true;

                processToScreen();

                startup = false;
            }            
        }

        void Params_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1 || startup == true || e.ColumnIndex != 1)
                return;
            try
            {
                if (Params[Command.Index, e.RowIndex].Value.ToString().EndsWith("_REV") && (Params[Command.Index, e.RowIndex].Value.ToString().StartsWith("RC") || Params[Command.Index, e.RowIndex].Value.ToString().StartsWith("HS")))
                {
                    if (Params[e.ColumnIndex, e.RowIndex].Value.ToString() == "0")
                        Params[e.ColumnIndex, e.RowIndex].Value = "-1";
                }

                Params[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.Green;
                _changes[Params[0, e.RowIndex].Value] = float.Parse(((string)Params[e.ColumnIndex, e.RowIndex].Value).ToString());
            }
            catch (Exception)
            {
                Params[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.Red;
            }

        
            Params.Focus();
        }

        void readToolTips()
        {
            return;

            string data = global::MissionPlanner.Properties.Resources.MAVParam;

            string[] tips = data.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var tip in tips)
            {
                if (!tip.StartsWith("||"))
                    continue;

                string[] cols = tip.Split(new string[] { "||" }, 9, StringSplitOptions.None);

                if (cols.Length >= 8)
                {
                    paramsettings param = new paramsettings();
                    try
                    {
                        param.name = cols[1];
                        param.desc = AddNewLinesForTooltip(cols[7]);
                        param.scale = float.Parse(cols[5]);
                        param.minvalue = float.Parse(cols[2]);
                        param.maxvalue = float.Parse(cols[3]);
                        param.normalvalue = float.Parse(cols[4]);
                    }
                    catch { }
                    tooltips[cols[1]] = param;
                }

            }
        }

        // from http://stackoverflow.com/questions/2512781/winforms-big-paragraph-tooltip/2512895#2512895
        private const int maximumSingleLineTooltipLength = 50;

        private static string AddNewLinesForTooltip(string text)
        {
            if (text.Length < maximumSingleLineTooltipLength)
                return text;
            int lineLength = (int)Math.Sqrt((double)text.Length) * 2;
            StringBuilder sb = new StringBuilder();
            int currentLinePosition = 0;
            for (int textIndex = 0; textIndex < text.Length; textIndex++)
            {
                // If we have reached the target line length and the next      
                // character is whitespace then begin a new line.   
                if (currentLinePosition >= lineLength &&
                    char.IsWhiteSpace(text[textIndex]))
                {
                    sb.Append(Environment.NewLine);
                    currentLinePosition = 0;
                }
                // If we have just started a new line, skip all the whitespace.    
                if (currentLinePosition == 0)
                    while (textIndex < text.Length && char.IsWhiteSpace(text[textIndex]))
                        textIndex++;
                // Append the next character.     
                if (textIndex < text.Length) sb.Append(text[textIndex]);
                currentLinePosition++;
            }
            return sb.ToString();
        }

        internal void processToScreen()
        {
            toolTip1.RemoveAll();
            Params.Rows.Clear();


            //Params.Sort(Params.Columns[0], ListSortDirection.Ascending);

            List<string> sorted = new List<string>();
            foreach (string item in MainV2.comPort.MAV.param.Keys)
                sorted.Add(item);

            sorted.Sort();

            // process hashdefines and update display
            foreach (string value in sorted)
            {
                if (value == null || value == "")
                    continue;

                //System.Diagnostics.Debug.WriteLine("Doing: " + value);

                Params.Rows.Add();
                Params.Rows[Params.RowCount - 1].Cells[Command.Index].Value = value;
                Params.Rows[Params.RowCount - 1].Cells[Value.Index].Value = ((float)MainV2.comPort.MAV.param[value]).ToString();
                try
                {
                    string metaDataDescription = ParameterMetaDataRepository.GetParameterMetaData(value, ParameterMetaDataConstants.Description);
                    if (!String.IsNullOrEmpty(metaDataDescription))
                    {
                        Params.Rows[Params.RowCount - 1].Cells[Command.Index].ToolTipText = metaDataDescription;
                        Params.Rows[Params.RowCount - 1].Cells[Value.Index].ToolTipText = metaDataDescription;

                        string range = ParameterMetaDataRepository.GetParameterMetaData(value, ParameterMetaDataConstants.Range);
                        string options = ParameterMetaDataRepository.GetParameterMetaData(value, ParameterMetaDataConstants.Values);
                        string units = ParameterMetaDataRepository.GetParameterMetaData(value, ParameterMetaDataConstants.Units);

                        Params.Rows[Params.RowCount - 1].Cells[Units.Index].Value = units;
                        Params.Rows[Params.RowCount - 1].Cells[Options.Index].Value = range + options;
                        Params.Rows[Params.RowCount - 1].Cells[Desc.Index].Value = metaDataDescription;

                    }
                    else if (tooltips[value] != null)
                    {
                        //Params.Rows[Params.RowCount - 1].Cells[Command.Index].ToolTipText = ((paramsettings)tooltips[value]).desc;
                        //Params.Rows[Params.RowCount - 1].Cells[RawValue.Index].ToolTipText = ((paramsettings)tooltips[value]).desc;
                       // Params.Rows[Params.RowCount - 1].Cells[Value.Index].ToolTipText = ((paramsettings)tooltips[value]).desc;

                        //  Params.Rows[Params.RowCount - 1].Cells[Desc.Index].Value = "Old: "+((paramsettings)tooltips[value]).desc;

                        //Params.Rows[Params.RowCount - 1].Cells[Default.Index].Value = ((paramsettings)tooltips[value]).normalvalue;
                        //Params.Rows[Params.RowCount - 1].Cells[mavScale.Index].Value = ((paramsettings)tooltips[value]).scale;
                        //Params.Rows[Params.RowCount - 1].Cells[Value.Index].Value = float.Parse(Params.Rows[Params.RowCount - 1].Cells[RawValue.Index].Value.ToString()) / float.Parse(Params.Rows[Params.RowCount - 1].Cells[mavScale.Index].Value.ToString());
                    }
                }
                catch { }

            }
            //Params.Sort(Params.Columns[0], ListSortDirection.Ascending);
        }

        public void Activate()
        {
            // read tooltips
            if (tooltips.Count == 0)
                readToolTips();

            startup = true;

            this.SuspendLayout();

            processToScreen();

            this.ResumeLayout();


            CMB_paramfiles.Enabled = false;
            BUT_paramfileload.Enabled = false;


            System.Threading.ThreadPool.QueueUserWorkItem(updatedefaultlist);

            startup = false;
        }

        void updatedefaultlist(object crap)
        {
            try
            {
                if (paramfiles == null)
                {
                    paramfiles = GitHubContent.GetDirContent("diydrones", "ardupilot", "/Tools/Frame_params/",".param");
                }

                this.BeginInvoke((Action)delegate
                {
                    CMB_paramfiles.DataSource = paramfiles.ToArray();
                    CMB_paramfiles.DisplayMember = "name";
                    CMB_paramfiles.Enabled = true;
                    BUT_paramfileload.Enabled = true;
                });
            }
            catch (Exception ex) { log.Error(ex); }
        }

        private void BUT_find_Click(object sender, EventArgs e)
        {
            string searchfor = "";
            InputBox.Show("Search For", "Enter a single word to search for", ref searchfor);

            foreach (DataGridViewRow row in Params.Rows)
            {
                foreach (DataGridViewCell cell in row.Cells)
                {
                    if (cell.Value != null && cell.Value.ToString().ToLower().Contains(searchfor.ToLower()))
                    {
                        row.Visible = true;
                        break;
                    }
                    else
                    {
                        row.Visible = false;
                    }
                }
            }
        }

        private void BUT_paramfileload_Click(object sender, EventArgs e)
        {
            string filepath = Application.StartupPath + Path.DirectorySeparatorChar + CMB_paramfiles.Text;

            byte[] data = GitHubContent.GetFileContent("diydrones", "ardupilot", ((GitHubContent.FileInfo)CMB_paramfiles.SelectedValue).path);

            File.WriteAllBytes(filepath, data);

            Hashtable param2 = Utilities.ParamFile.loadParamFile(filepath);

            Form paramCompareForm = new ParamCompare(Params, MainV2.comPort.MAV.param, param2);

            ThemeManager.ApplyThemeTo(paramCompareForm);
            paramCompareForm.ShowDialog();

            CustomMessageBox.Show("Loaded parameters, please make sure you write them!", "Loaded");

            this.Activate();
        }

        private void CMB_paramfiles_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
