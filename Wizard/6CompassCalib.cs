﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MissionPlanner.Controls;

namespace MissionPlanner.Wizard
{
    public partial class _6CompassCalib : MyUserControl, IWizard, IActivate, IDeactivate
    {
        double[] ans;

        public _6CompassCalib()
        {
            InitializeComponent();
        }

        public int WizardValidate()
        {
            return 1;
        }

        public bool WizardBusy()
        {
            return false;
        }
        public void Activate()
        { 
            timer1.Start(); 
        }

        public void Deactivate()
        { 
            timer1.Stop();
        }

        private void pictureBox_Click(object sender, EventArgs e)
        {
            DeselectAll();
            (sender as PictureBoxMouseOver).selected = true;
        }

        void DeselectAll()
        {
            foreach (var ctl in this.panel1.Controls)
            {
                if (ctl.GetType() == typeof(PictureBoxMouseOver))
                {
                    (ctl as PictureBoxMouseOver).selected = false;
                }
            }
        }

        private void BUT_MagCalibration_Click(object sender, EventArgs e)
        {
            MainV2.comPort.MAV.cs.ratesensors = 2;

            MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA3, MainV2.comPort.MAV.cs.ratesensors);
            MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, MainV2.comPort.MAV.cs.ratesensors);

            MainV2.comPort.setParam("MAG_ENABLE", 1);

            CustomMessageBox.Show("Data will be collected for 60 seconds, Please click ok and move the apm around all axises");

            ProgressReporterDialogue prd = new ProgressReporterDialogue();

            Utilities.ThemeManager.ApplyThemeTo(prd);

            prd.DoWork += prd_DoWork;

            prd.RunBackgroundOperationAsync();

            if (ans != null)
                MagCalib.SaveOffsets(ans);
        }

        void prd_DoWork(object sender, ProgressWorkerEventArgs e, object passdata = null)
        {
            // list of x,y,z 's
            List<Tuple<float, float, float>> data = new List<Tuple<float, float, float>>();

            // backup current rate and set to 10 hz
            byte backupratesens = MainV2.comPort.MAV.cs.ratesensors;
            MainV2.comPort.MAV.cs.ratesensors = 10;
            MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, MainV2.comPort.MAV.cs.ratesensors); // mag captures at 10 hz

            DateTime deadline = DateTime.Now.AddSeconds(60);

            float oldmx = 0;
            float oldmy = 0;
            float oldmz = 0;

            while (deadline > DateTime.Now)
            {
                double timeremaining = (deadline - DateTime.Now).TotalSeconds;
                ((ProgressReporterDialogue)sender).UpdateProgressAndStatus((int)(((60 - timeremaining) / 60) * 100), timeremaining.ToString("0") + " Seconds - got " + data.Count + " Samples");

                if (e.CancelRequested)
                {
                    // restore old sensor rate
                    MainV2.comPort.MAV.cs.ratesensors = backupratesens;
                    MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, MainV2.comPort.MAV.cs.ratesensors);

                    e.CancelAcknowledged = true;
                    return;
                }

                if (oldmx != MainV2.comPort.MAV.cs.mx &&
                    oldmy != MainV2.comPort.MAV.cs.my &&
                    oldmz != MainV2.comPort.MAV.cs.mz)
                {
                    data.Add(new Tuple<float, float, float>(
                        MainV2.comPort.MAV.cs.mx - (float)MainV2.comPort.MAV.cs.mag_ofs_x,
                        MainV2.comPort.MAV.cs.my - (float)MainV2.comPort.MAV.cs.mag_ofs_y,
                        MainV2.comPort.MAV.cs.mz - (float)MainV2.comPort.MAV.cs.mag_ofs_z));

                    oldmx = MainV2.comPort.MAV.cs.mx;
                    oldmy = MainV2.comPort.MAV.cs.my;
                    oldmz = MainV2.comPort.MAV.cs.mz;
                }
            }

            // restore old sensor rate
            MainV2.comPort.MAV.cs.ratesensors = backupratesens;
            MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, MainV2.comPort.MAV.cs.ratesensors);

            if (data.Count < 10)
            {
                e.ErrorMessage = "Log does not contain enough data";
                ans = null;
                return;
            }

            ans = MagCalib.LeastSq(data);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=DmsueBS0J3E");
        }

        int step = 0;
        HIL.Vector3 north;
        HIL.Vector3 east;
        HIL.Vector3 south;
        HIL.Vector3 west;

        private void BUT_compassorient_Click(object sender, EventArgs e)
        {
            BUT_compassorient.Text = "Continue";

            MainV2.comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, 10);

            switch (step)
            {
                case 0:
                    label5.Text = "Please face the autopilot north";
                    break;
                case 1:
                    north = new HIL.Vector3(MainV2.comPort.MAV.cs.mx, MainV2.comPort.MAV.cs.my, MainV2.comPort.MAV.cs.mz);
                    label5.Text = "Please face the autopilot east";
                    break;
                case 2:
                    east = new HIL.Vector3(MainV2.comPort.MAV.cs.mx, MainV2.comPort.MAV.cs.my, MainV2.comPort.MAV.cs.mz);
                    label5.Text = "Please face the autopilot south";
                    break;
                case 3:
                    south = new HIL.Vector3(MainV2.comPort.MAV.cs.mx, MainV2.comPort.MAV.cs.my, MainV2.comPort.MAV.cs.mz);
                    label5.Text = "Please face the autopilot west";
                    break;
                case 4:
                    west = new HIL.Vector3(MainV2.comPort.MAV.cs.mx, MainV2.comPort.MAV.cs.my, MainV2.comPort.MAV.cs.mz);
                    label5.Text = "Calculating";
                    if (docalc())
                    {

                    }
                    else
                    {
                        label5.Text = "Error, please try again, verify where north is.";
                    }
                    BUT_compassorient.Text = "Start";
                    step = 0;
                    return;
            }
            step++;
        }

        const float rad2deg = (float)(180 / Math.PI);
        const float deg2rad = (float)(1.0 / rad2deg);

        float calcheading(HIL.Vector3 mag)
        {
            HIL.Matrix3 dcm_matrix = new HIL.Matrix3();
            dcm_matrix.from_euler(0, 0, 0);

            // Tilt compensated magnetic field Y component:
            double headY = mag.y * dcm_matrix.c.z - mag.z * dcm_matrix.c.y;

            // Tilt compensated magnetic field X component:
            double headX = mag.x + dcm_matrix.c.x * (headY - mag.x * dcm_matrix.c.x);

            // magnetic heading
            // 6/4/11 - added constrain to keep bad values from ruining DCM Yaw - Jason S.
            double heading = constrain_float((float)Math.Atan2(-headY, headX), -3.15f, 3.15f);

            return (float)((heading * rad2deg) + 360) % 360f;
        }

        float constrain_float(float input, float min, float max)
        {
            if (input > max)
                return max;
            if (input < min)
                return min;
            return input;
        }


        bool docalc()
        {
            try
            {
                //  HIL.Vector3 magoff = new HIL.Vector3((float)MainV2.comPort.MAV.param["COMPASS_OFS_X"], (float)MainV2.comPort.MAV.param["COMPASS_OFS_Y"], (float)MainV2.comPort.MAV.param["COMPASS_OFS_Z"]);
                //north -= magoff;
                //east -= magoff;
                //south -= magoff;
                //west -= magoff;
            }
            catch { }

            foreach (Common.Rotation item in Enum.GetValues(typeof(Common.Rotation)))
            {
                // copy them, as we dont want to change the originals
                HIL.Vector3 northc = new HIL.Vector3(north);
                HIL.Vector3 eastc = new HIL.Vector3(east);
                HIL.Vector3 southc = new HIL.Vector3(south);
                HIL.Vector3 westc = new HIL.Vector3(west);

                northc.rotate(item);
                eastc.rotate(item);
                southc.rotate(item);
                westc.rotate(item);

                // test the copies
                if (withinMargin(calcheading(northc), 35, 0))
                {
                    if (withinMargin(calcheading(eastc), 35, 90))
                    {
                        if (withinMargin(calcheading(southc), 35, 180))
                        {
                            if (withinMargin(calcheading(westc), 35, 270))
                            {
                                Console.WriteLine("Rotation " + item.ToString());
                                label5.Text = "Done Rotation: " + item.ToString();
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        bool withinMargin(float value, float margin, float target)
        {
            // to prevent the near 0 issues
            value += 360;
            target += 360;

            Console.WriteLine("{0} = {1} within +-{2}", value % 360, target % 360, margin);

            if (value >= (target - margin))
            {
                if (value <= (target + margin))
                {
                    return true;
                }
            }
            return false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            float target = (MainV2.comPort.MAV.cs.yaw % 90);

            if (target > 45)
                target -= 90f;


            label6.Text = target.ToString("0");
        }
    }
}