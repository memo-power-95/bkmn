using AcuraLibrary.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NPSDK;
using Alpha.Classes;
using System.IO;
using Alpha._0.Classes;
using Alpha.FunctionForms;
using LogSv;
using static Alpha.FunctionForms.LogForm;

namespace Alpha._0.ModuleForms
{
    public partial class ConveyorForm : ModuleBaseForm
    {

        public JTimer ConveyorInitTimeout = new JTimer();
        public JTimer Conveyor1Timeout = new JTimer();
        public bool assambSuccess = true;
        public bool isAssambling = false;
        JTimer DownConveyorTimeout = new JTimer();
        JTimer DownConveyorAutoTimeout = new JTimer();
        JTimer DownConveyorAutoTimeout2 = new JTimer();
        public bool downInitialStatus = false;
        public bool upInitialStatus = false;
        public bool isdownInitialOK = false;

        public bool IsBoardINOUT = false;
        bool bSMEMAUPFailAvailable = false;

        JTimer InitialStart = new JTimer();
        JTimer tmr_Conveyor1 = new JTimer();
        JTimer tmr_Conveyor2 = new JTimer();
        public ProcessDataForm processData = MiddleLayer.ProcessF.fnGetDataForm("Alpha");
        public string StartCT;

        public string OverCT;

        int ConveyorCountOK = 0;

        int ConveyorCountNG = 0;

        public bool UPAnalogBoard = false;
        public bool UPLowerKeyBoard = false;
        public bool DownAnalogBoard = false;
        public bool DownLowerKeyBoard = false;
        public bool LastUnitInProcess = false;
        public bool InitialUnitInExitPosition = false;

        public int ScanNG = 0;
        public bool LocalMachineNG = false;

        public bool ConveyorOK = false;

        public bool Priority = false;

        public bool ZAxisPost = false;


        cSmartAlarm DynamicAlarm = new cSmartAlarm();
        private Task<ScannerForm.ScannerReadResult> taskGetScannerCode = null;
        private bool bSimulateRobotDone = false;
        private bool bUnitSmemaNG = false;
        private bool bUnitForExpulse = false;
        private string sSerialNumber = "";
        bool IsDryCycleMode = false;
        bool IsEnabledInOutDoor = false;
        bool dcEnableCheckProcess = false;//Dry cycle
                                          //downConvryor 
        float downInputFlow = 0;
        float downOutputFlow = 0;

        DownConveyorSMMStare downConveyorSMMStare = new DownConveyorSMMStare();
        DownConveyorMtRevStare downConveyorMtRevStare = new DownConveyorMtRevStare();
        DownConveyorProductStare downConveyorProductStare = new DownConveyorProductStare();
        private enum DownConveyorSMMStare
        {
            Input,
            Output,
            None
        }
        private enum DownConveyorMtRevStare
        {
            Start,
            stop,
            None
        }
        private enum DownConveyorProductStare
        {
            Input,
            Output,
            None
        }

        public UC_DownConveyorAutoFlow DownConveyorAutoFlow;
        public UC_UpConveyorAutoFlow.UC_UpConveyorAutoFlow UpConveyorAutoFlow;
        public ConveyorForm()
        {
            InitializeComponent();
            this.plMachineStatus.Enabled = false;
            this.plRecipeEditor.Enabled = false;
            //DynamicAlarm.AddRange(fc_Initial_Start);
            //DynamicAlarm.AddRange(fc_Auto_Start);
            //pdfProcess = MiddleLayer.ProcessF.fnGetDataForm("");
            InitMotorControl(ref DownConveyorAutoFlow, panel_MoveFlowModule);
            InitUpMotorControl(ref UpConveyorAutoFlow, panel_UpFlowModule);
            CrearConveyorAutomatico();   //MOD@@ Guillermo Carrillo: opcion Conveyor automatico (conveyor inferior)
        }
        /// <summary>
        /// Initialize mod control (初始化模组控制)
        /// </summary>
        /// <param name="motorMoveCtrl">Mod Control(模组控件)</param>
        /// <param name="panel_MoveFlowModule">(Parent panel)插入的父面板</param>
        private void InitMotorControl(ref UC_DownConveyorAutoFlow motorMoveCtrl, Panel panel_MoveFlowModule)
        {
            if (motorMoveCtrl == null)
                motorMoveCtrl = new UC_DownConveyorAutoFlow();
            motorMoveCtrl.Dock = DockStyle.Fill;
            panel_MoveFlowModule.Controls.Add(motorMoveCtrl);
        }

        private void InitUpMotorControl(ref UC_UpConveyorAutoFlow.UC_UpConveyorAutoFlow motorMoveCtrl, Panel panel_UpFlowModule)
        {
            if (UpConveyorAutoFlow == null)
            {
                UpConveyorAutoFlow = new UC_UpConveyorAutoFlow.UC_UpConveyorAutoFlow();
                UpConveyorAutoFlow.FnIndicateStartingUnit = processData.fnIndicateStartingUnit;
                UpConveyorAutoFlow.FnIndicateFinishedUnit = processData.fnIndicateFinishedUnit;
                UpConveyorAutoFlow.SwCycleTimeStart = processData.swCycleTime.Start;
                UpConveyorAutoFlow.SwCycleTimeStop = processData.swCycleTime.Stop;
                UpConveyorAutoFlow.ShowAlarm = MESLib.CommFuns.MES_ShowAlarm;
                UpConveyorAutoFlow.AddLog = MiddleLayer.LogF.AddLog;

                UpConveyorAutoFlow.IB_SMEMAUPFailAvailable = IB_SMEMAUPFailAvailable;
                UpConveyorAutoFlow.IB_SMEMAUpStreamBoardAvailable = IB_SMEMAUpStreamBoardAvailable;
                UpConveyorAutoFlow.IB_SMEMAUpStreamMachineReady = IB_SMEMAUpStreamMachineReady;
                UpConveyorAutoFlow.IB_UpCvyBlockCyOut = IB_UpCvyBlockCyOut;
                UpConveyorAutoFlow.IB_UpCvyDecelerationSensor = IB_UpCvyDecelerationSensor;
                UpConveyorAutoFlow.IB_UpCvyDischargeSensor = IB_UpCvyDischargeSensor;
                UpConveyorAutoFlow.IB_UpCvyFeedSensor = IB_UpCvyFeedSensor;
                UpConveyorAutoFlow.IB_UpCvyInPlaceSensor = IB_UpCvyInPlaceSensor;
                UpConveyorAutoFlow.IB_UpCvyJackingCyBack = IB_UpCvyJackingCyBack;
                UpConveyorAutoFlow.IB_UpCvyJackingCyOut = IB_UpCvyJackingCyOut;
                UpConveyorAutoFlow.OB_SMMachineReady = OB_SMMachineReady;
                UpConveyorAutoFlow.OB_SM_UpBoardReady = OB_SM_UpBoardReady;
                UpConveyorAutoFlow.OB_SM_UpFaliBoard = OB_SM_UpFaliBoard;
                UpConveyorAutoFlow.OB_UpCvyBlockCyUp = OB_UpCvyBlockCyOut;
                UpConveyorAutoFlow.OB_UpCvyMtFwd = OB_UpCvyMtFwd;
                UpConveyorAutoFlow.OB_UpCvyMtSpeed = OB_UpCvyMtSpeed;
                //UpConveyorAutoFlow.Cy_LeftDoorUnite = MiddleLayer.SystemF.Cy_LeftDoorUnite;
                //UpConveyorAutoFlow.Cy_RightDoorUnite = MiddleLayer.SystemF.Cy_RightDoorUnite;
                UpConveyorAutoFlow.Cy_ST1UpStreamLiftingUnite = Cy_ST1UpStreamLiftingUnite;
            }
            UpConveyorAutoFlow.Dock = DockStyle.Fill;
            panel_UpFlowModule.Controls.Add(UpConveyorAutoFlow);
        }
        public override void InitialReset()
        {
            flowChart1.TaskReset();
            BottomConveyorInitFlow.TaskReset();

        }

        public override void Initial()
        {
            flowChart1.TaskRun();
            BottomConveyorInitFlow.TaskRun();
        }
        public override void RunReset()
        {
            // fc_UpConveryFlowStart.TaskReset();
            UpConveyorAutoFlow.fc_UpConveryStart.TaskReset();
            UpConveyorAutoFlow.subFlowChartStart.TaskReset();
        }

        public override void Run()
        {
            //fc_UpConveryFlowStart.TaskRun();
            UpConveyorAutoFlow.fc_UpConveryStart.TaskRun();
            UpConveyorAutoFlow.subFlowChartStart.TaskRun();
        }


        public override void StartRun()
        {
            //if(fc_Initial_Start.tmrTimeOut !=null)
            //    fc_Initial_Start.tmrTimeOut.Restart();
            //if (fc_Auto_Start.tmrTimeOut != null)
            //    fc_Auto_Start.tmrTimeOut.Restart();
        }
        public override void StopRun()
        {
            //OB_TopCVY_BKW.Off();
            //OB_TopCVY_FWD.Off();
            //OB_BottomCVY_FWD.Off();
            //OB_BottomCVY_BKW.Off();

            //OB_SMEMA_TopCVY_Send.Off();
            //OB_SMEMA_TopCVY_Request.Off();
            //OB_SMEMA_TopCVY_SendNG.Off();
            //OB_SMEMA_TopCVY_SendOK.Off();
        }
        public override void AlwaysRun()
        {
            //if (isdownInitialOK)//Initialization complete flag
            //    BottomConveyorStartFlow.TaskRun();
        }


        public static void RefreshDifferentThreadUI(Control control, Action action)
        {
            if (control.InvokeRequired)
            {
                Action refreshUI = new Action(action);
                control.Invoke(refreshUI);
            }
            else
            {
                action.Invoke();
            }
        }
        /// <summary>
        /// Log  File
        /// </summary>
        /// <param name="describe"></param>
        /// <param name="filename">file name</param>
        /// <param name="flow_Chat_Name">flow_Chat_Name</param>
        /// <param name="boardNumber">Scann</param>
        public void SaveLogCSV(string describe, string filename, string flow_Chat = "", string boardNumber = "")
        {

            string log = DateTime.Now.ToString("yyyy/MM/dd") + "," + DateTime.Now.ToString("HH:mm:ss") + "," + describe + "  " + boardNumber;
            string MesID = "NA";
            MesID = MiddleLayer.MesF2.GetSettingValue("PSet", "EQPID");
            if (MesID == null || MesID == "")
                MesID = "NA";
            string fullpath = @"D:\EVMS\TP\LOG\" + MesID + "_NA_" + DateTime.Now.ToString("yyyyMMddHH") +"_"+ filename + ".CSV";
            FileInfo fi = new FileInfo(fullpath);
            if (!fi.Directory.Exists)
            { fi.Directory.Create(); }

            if (!File.Exists(fullpath))
            {
                using (FileStream fs = new FileStream(fullpath, System.IO.FileMode.Append, System.IO.FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                    {
                        sw.WriteLine(flow_Chat);
                        sw.Close();
                        fs.Close();
                    }

                }
            }
            using (FileStream fs = new FileStream(fullpath, System.IO.FileMode.Append, System.IO.FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine(log);
                    sw.Close();
                    fs.Close();
                }

            }
        }
        #region ConveryInitial
        private FCResultType flowChart1_FlowRun(object sender, EventArgs e)
        {

            //lhj_0512
            //MiddleLayer.ConveyorF.UpConveyorAutoFlow.DisbleInOutDoor = MiddleLayer.ProcessF.GetSettingValue("PSet", "IsEnabledInOutDoor");
            MiddleLayer.ConveyorF.UpConveyorAutoFlow.DryRunMode = MiddleLayer.ProcessF.GetSettingValue("PSet", "IsDryCycleMode");
            MiddleLayer.ConveyorF.UpConveyorAutoFlow.ByPassMode = MiddleLayer.ProcessF.GetSettingValue("PSet", "dcEnableCheckProcess");

            MiddleLayer.ConveyorF.UpConveyorAutoFlow.BEnableFirstGroup = MiddleLayer.ProcessF.GetSettingValue("PSet", "EnableFirstGroup");
            MiddleLayer.ConveyorF.UpConveyorAutoFlow.BEnableSecondGroup = MiddleLayer.ProcessF.GetSettingValue("PSet", "EnableSecondGroup");


            SysPara.RIniRet = false;
            bInitialOk = false;
            upInitialStatus = false;
            OB_SM_UpBoardReady.Off();
            OB_SM_UpFaliBoard.Off();
            OB_UpCvyMtFwd.Off();
            OB_UpCvyMtSpeed.Off();
            OB_UpCvyMtRev.Off();
            processData.fnIndicateFinishedUnit();
            
            MiddleLayer.LogF.AddLog(LogType.Production, "Conveyor starts to initialize", true);
            return FCResultType.NEXT;
        }
        //Wait Robot Initial finish
        private FCResultType flowChart2_FlowRun(object sender, EventArgs e)
        {
            lock (SysPara.StatusLock)
            {
                if (SysPara.RIniRet)
                {
                    OB_UpCvyBlockCyOut.On();
                    ConveyorInitTimeout.Restart();
                    return FCResultType.NEXT;
                }
                else
                {
                    return FCResultType.IDLE;
                }
            }
        }
        private FCResultType flowChart42_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyBlockCyOut.On())
            {
                cy_UnderdraughtCylinderUnite.On();
                ConveyorInitTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(5000))
                {
                    SysPara.NPShowAlarm("1005");
                }
                return FCResultType.IDLE;
            }
        }
        // JackingCy back
        private FCResultType flowChart3_FlowRun(object sender, EventArgs e)
        {

            if (IB_RightUnderPreCylinderRise.On() && IB_LeftUnderPreCylinderRise.On())
            {

                ConveyorInitTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(5000))
                {
                    SysPara.NPShowAlarm("1018");
                }
            }
            return FCResultType.IDLE;
        }

        private FCResultType flowChart4_FlowRun(object sender, EventArgs e)
        {
            Cy_ST1UpStreamLiftingUnite.Off();
            if (IB_UpCvyJackingCyBack.On())
            {
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(5000))
                {
                    SysPara.NPShowAlarm("1004");
                }
            }
            return FCResultType.IDLE;

        }
        //UpCvyMtFwd off
        private FCResultType flowChart5_FlowRun(object sender, EventArgs e)
        {

            if (IB_UpCvyMotorAlarm.On())
            {
                SysPara.NPShowAlarm("1011");
            }
            else
            {
                OB_UpCvyMtSpeed.Off();
                OB_UpCvyMtFwd.On();// Conveyor start
            }
            ConveyorInitTimeout.Restart();
            return FCResultType.NEXT;
        }

        private FCResultType flowChart32_FlowRun(object sender, EventArgs e)
        {

            cy_TransverseCylinderInunite.On();
            if (IB_TransverseCylinderIn.Off() &&
               IB_TransverseCylinderOut.On())
            {
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(5000))
                {
                    SysPara.NPShowAlarm("1015");
                }
                return FCResultType.IDLE;
            }

        }
        private FCResultType flowChart33_FlowRun(object sender, EventArgs e)
        {

            if (ConveyorInitTimeout.On(6000))
            {
                OB_UpCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            return FCResultType.IDLE;
        }
        private FCResultType flowChart31_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyDischargeSensor.On() || IB_UpCvyDecelerationSensor.On() || IB_UpCvyInPlaceSensor.On())
            {
                SysPara.NPShowAlarm("1000");
                return FCResultType.IDLE;
            }
            //if (IB_UpCvyInPlaceSensor.On())
            //{
            //    SysPara.NPShowAlarm("1000");
            //    return FCResultType.IDLE;
            //}
            OB_UpCvyBlockCyOut.On();
            ConveyorInitTimeout.Restart();
            return FCResultType.NEXT;

        }
        //Initial finish
        private FCResultType flowChart6_FlowRun(object sender, EventArgs e)
        {

            if (IB_UpCvyBlockCyOut.On())
            {
                if (MiddleLayer.ProcessF.GetSettingValue("PSet", "IsEnabledInOutDoor"))
                {
                    ConveyorInitTimeout.Restart();
                    return FCResultType.CASE1;


                }
                else
                {
                    if (IB_UpCvyInPlaceSensor.Off(100))
                    {
                        SysPara.LeftDoorAlarmEnable = false;
                        SysPara.RightDoorAlarmEnable = false;
                        ConveyorInitTimeout.Restart();
                        return FCResultType.NEXT;
                    }
                    else
                    {
                        SysPara.NPShowAlarm("1000");
                    }
                }

            }
            else
            {
                if (ConveyorInitTimeout.On(5000))
                {
                    SysPara.NPShowAlarm("1006");
                }

            }
            return FCResultType.IDLE;
        }
        private FCResultType flowChart35_FlowRun_1(object sender, EventArgs e)
        {
            return FCResultType.NEXT;


        }
        private FCResultType flowChart34_FlowRun(object sender, EventArgs e)
        {
            upInitialStatus = true;
            bInitialOk = true;
            OB_SM_UpFaliBoard.Off();
            OB_SM_UpBoardReady.Off();
            OB_SMMachineReady.Off();
            return FCResultType.IDLE;
        }
        #endregion
        #region Convery AutoRun

        //UpConvery flow start
        private FCResultType fc_UpConveryFlowStart_FlowRun(object sender, EventArgs e)
        {
            IsDryCycleMode = MiddleLayer.ProcessF.GetSettingValue("PSet", "IsDryCycleMode");
            if (IsDryCycleMode)
                MiddleLayer.LogF.AddLog(LogType.Production, "The device turns on the dry run mode", true);
            else
                MiddleLayer.LogF.AddLog(LogType.Production, "The device turns off the dry run mode", true);
            IsEnabledInOutDoor = MiddleLayer.ProcessF.GetSettingValue("PSet", "IsEnabledInOutDoor");
            if (IsEnabledInOutDoor)
                MiddleLayer.LogF.AddLog(LogType.Production, "The device is turned on to disable the entry and exit gate mode", true);
            else
                MiddleLayer.LogF.AddLog(LogType.Production, "The device is turned off to disable the entry and exit gate mode", true);
            dcEnableCheckProcess = MiddleLayer.ProcessF.GetSettingValue("PSet", "dcEnableCheckProcess");
            if (dcEnableCheckProcess)
                MiddleLayer.LogF.AddLog(LogType.Production, "Turn on the device in ByPass mode", true);
            else
                MiddleLayer.LogF.AddLog(LogType.Production, "Turn off the device in ByPass mode", true);

            SMMProduct.Reset();
            SMMDisProduct.Reset();
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
        }

        private FCResultType flowChart11_FlowRun(object sender, EventArgs e)
        {
            //if (IsEnabledInOutDoor)
            //    return FCResultType.NEXT;
            if (IB_SMEMAUpStreamMachineReady.On(100) || IsDryCycleMode || SMMProduct.WaitOne(0))
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Front machine There is a signal at the product flow", true);
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            return FCResultType.IDLE;
        }
        private FCResultType flowChart15_FlowRun(object sender, EventArgs e)
        {
            return FCResultType.NEXT;

        }
        //SMEMA Ready
        private FCResultType flowChart8_FlowRun(object sender, EventArgs e)
        {
            OB_SMMachineReady.On();
            OB_UpCvyBlockCyOut.Off();
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
        }

        //Blockcylinder up，IB_UpCvyBlockCyOut=True
        private FCResultType flowChart9_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyBlockCyOut.Off())
            {

                if (MiddleLayer.ProcessF.GetSettingValue("PSet", "EnableFirstGroup"))
                {
                    SysPara.bEnableFirstGroup = true;
                }
                else
                {
                    SysPara.bEnableFirstGroup = false;
                }
                if (MiddleLayer.ProcessF.GetSettingValue("PSet", "EnableSecondGroup"))
                {
                    SysPara.bEnableSecondGroup = true;
                }
                else
                    SysPara.bEnableSecondGroup = false;
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(5000))
                {
                    SysPara.NPShowAlarm("1005");
                }
                return FCResultType.IDLE;
            }
        }
        //Convery forward
        private FCResultType flowChart10_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyFeedSensor.On(200) || IsDryCycleMode)
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Feed sensor On", true);
                if (IB_SMEMAUPFailAvailable.On())
                {
                    bSMEMAUPFailAvailable = true;
                    MiddleLayer.LogF.AddLog(LogType.Production, "Front machine processing OK", true);
                }

                else
                {
                    MiddleLayer.LogF.AddLog(LogType.Production, "Front machine processing NG", true);
                    bSMEMAUPFailAvailable = false;
                }


                OB_UpCvyMtSpeed.Off();
                OB_UpCvyMtFwd.On();
                processData.fnIndicateStartingUnit();
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                //if (Conveyor1Timeout.On(10000))
                //{
                //    SysPara.NPShowAlarm("1001");
                //    Conveyor1Timeout.Restart();
                //    MiddleLayer.MainF.btnPause_Click(null, null);
                //    return FCResultType.IDLE;

                //}
                //OB_UpCvyMtFwd.On();
                return FCResultType.IDLE;
            }
        }


        //IB_UpCvyDecelerationSensor=true
        private FCResultType flowChart12_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyDecelerationSensor.On() || IsDryCycleMode)
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Deceleration sensor On", true);
                if (!IsEnabledInOutDoor)
                {
                    if (IB_UpCvyFeedSensor.Off(100))
                    {
                        SysPara.LeftDoorAlarmEnable = false;
                        Conveyor1Timeout.Restart();
                        OB_UpCvyMtSpeed.On();
                        return FCResultType.NEXT;
                    }
                    else
                    {
                        SysPara.NPShowAlarm("1009");
                        return FCResultType.IDLE;
                    }

                }
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(10000))
                {
                    SysPara.NPShowAlarm("1001");
                    Conveyor1Timeout.Restart();
                    MiddleLayer.MainF.btnPause_Click(null, null);
                    return FCResultType.IDLE;

                }
                OB_UpCvyMtFwd.On();
                return FCResultType.IDLE;
            }
        }
        private FCResultType flowChart29_FlowRun(object sender, EventArgs e)
        {
            SysPara.LeftDoorAlarmEnable = true;
            OB_SMMachineReady.Off();
            OB_UpCvyMtSpeed.On();
            return FCResultType.NEXT;

        }

        //wait product inplace
        private FCResultType flowChart13_FlowRun(object sender, EventArgs e)
        {
            if (IsDryCycleMode)
            {
                if (Conveyor1Timeout.On(5000))
                {
                    if (!dcEnableCheckProcess) { Cy_ST1UpStreamLiftingUnite.On(); }

                    SysPara.R1AssembResult = true;
                    OB_UpCvyMtFwd.Off();
                    OB_UpCvyMtSpeed.Off();
                    Conveyor1Timeout.Restart();
                    return FCResultType.NEXT;
                }
                else { return FCResultType.IDLE; }
            }
            if (IB_UpCvyInPlaceSensor.On(200))
            {
                Cy_ST1UpStreamLiftingUnite.On();
                SysPara.R1AssembResult = true;
                OB_UpCvyMtFwd.Off();
                OB_UpCvyMtSpeed.Off();
                Conveyor1Timeout.Restart();
                MiddleLayer.LogF.AddLog(LogType.Production, "In-position sensor On", true);
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(10000))
                {
                    SysPara.NPShowAlarm("1001");
                    Conveyor1Timeout.Restart();
                    MiddleLayer.MainF.btnPause_Click(null, null);
                    return FCResultType.IDLE;

                }
                OB_UpCvyMtFwd.On();
                return FCResultType.IDLE;
            }
        }

        //wait IB_UpCvyJackingCyOut=true
        private FCResultType flowChart14_FlowRun(object sender, EventArgs e)
        {





            if (IB_UpCvyJackingCyOut.On() && IB_UpCvyJackingCyBack.Off())
            {
                if (SysPara.bEnableFirstGroup)
                {
                    SysPara.bFirstOrSecond = true;
                }
                if (!SysPara.bEnableFirstGroup && SysPara.bEnableSecondGroup)
                {
                    SysPara.bFirstOrSecond = false;
                }


                if (!dcEnableCheckProcess)
                    MiddleLayer.ConveyorF.UpConveyorAutoFlow.NotifyAssembly = true;
                SysPara.R1AssembResult = true;
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(5000))
                {

                    SysPara.NPShowAlarm("1003");
                }
                return FCResultType.IDLE;
            }
        }
        //Judge front machine is NG or OK

        private FCResultType flowChart16_FlowRun(object sender, EventArgs e)
        {

            if (bSMEMAUPFailAvailable)
            {
                return FCResultType.NEXT;
            }
            else
            { return FCResultType.NEXT; }
        }





        //Wait R1 assemb Compelet
        private FCResultType flowChart19_FlowRun(object sender, EventArgs e)
        {
            if (dcEnableCheckProcess)
            {
                return FCResultType.NEXT;
            }
            if (SysPara.R1AssembCompeletStatus)
            {
                if (SysPara.bFirstOrSecond)
                    SysPara.bEnableFirstGroup = false;
                else
                    SysPara.bEnableSecondGroup = false;

                SysPara.R1AssembStatus = false;
                SysPara.R1AssembCompeletStatus = false;
                MiddleLayer.LogF.AddLog(LogType.Production, "Wait for the mod job to finish", true);
                return FCResultType.NEXT;
            }
            else { return FCResultType.IDLE; }

        }
        //Get assemb result
        private FCResultType flowChart20_FlowRun(object sender, EventArgs e)
        {
            if (!SysPara.bEnableFirstGroup && SysPara.bEnableSecondGroup)
            {
              
                Conveyor1Timeout.Restart();
                return FCResultType.CASE1;
            }



            if (dcEnableCheckProcess)
            {
                OB_UpCvyBlockCyOut.On();
                Cy_ST1UpStreamLiftingUnite.Off();
            }
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
        }
        //OB_SMDownFaliBoard=false
        private FCResultType flowChart21_FlowRun(object sender, EventArgs e)
        {
            if (bSMEMAUPFailAvailable)
            {
                OB_SM_UpFaliBoard.On();
                OB_SM_UpBoardReady.On();
            }
            else
            {
                OB_SM_UpFaliBoard.Off();
                OB_SM_UpBoardReady.On();
            }


            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
        }
        //wait next station call board
        private FCResultType flowChart22_FlowRun(object sender, EventArgs e)
        {
            //if (MiddleLayer.SystemF.GetSettingValue("PSet", "IsDryCycleMode")) { return FCResultType.NEXT; }
            if (IB_SMEMAUpStreamBoardAvailable.On(100) || IsDryCycleMode || SMMDisProduct.WaitOne(0))
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Back machine request On", true);
                Conveyor1Timeout.Restart();
                MiddleLayer.GantryF.processData.fnTimersRunning(true);
                return FCResultType.NEXT;
            }
            else { return FCResultType.IDLE; }
        }
        //Front machine result is OK
        private FCResultType flowChart17_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyBlockCyOut.On();
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;

        }
        //OB_UpCvyJackingCyUp down
        private FCResultType flowChart23_FlowRun(object sender, EventArgs e)
        {
            Cy_ST1UpStreamLiftingUnite.Off();
            if (IB_UpCvyJackingCyOut.Off() && IB_UpCvyJackingCyBack.On())
            {

                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(5000))
                {

                    SysPara.NPShowAlarm("1004");
                }
                return FCResultType.IDLE;
            }
        }

        private FCResultType flowChart24_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyBlockCyOut.On();
            if (IB_UpCvyBlockCyOut.On())
            {
                OB_UpCvyMtSpeed.Off(); //Convery Slower off
                OB_UpCvyMtFwd.On();      //Convery forward
                DownConveyorTimeout.Restart();
                //  GantryForm.DelayMs(5000);
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(5000))
                {

                    SysPara.NPShowAlarm("1006");
                }
                return FCResultType.IDLE;
            }
        }
        //Wait product in DischargeSensor
        private FCResultType flowChart25_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyDischargeSensor.On() || IsDryCycleMode)
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Discharge sensor On", true);
                DownConveyorTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (DownConveyorTimeout.On(10000))
                {
                    SysPara.NPShowAlarm("1002");
                    DownConveyorTimeout.Restart();
                    MiddleLayer.MainF.btnPause_Click(null, null);
                    return FCResultType.IDLE;

                }
                OB_UpCvyMtFwd.On();



                return FCResultType.IDLE;
            }
        }
        //Wait product transfer out
        private FCResultType flowChart26_FlowRun(object sender, EventArgs e)
        {

            if (IB_UpCvyDischargeSensor.Off(1000) || IsDryCycleMode)
            {
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (DownConveyorTimeout.On(10000))
                {
                    SysPara.NPShowAlarm("1002");
                    DownConveyorTimeout.Restart();
                    MiddleLayer.MainF.btnPause_Click(null, null);
                    return FCResultType.IDLE;

                }
                OB_UpCvyMtFwd.On();

                return FCResultType.IDLE;
            }
        }
        //Delay 3s,stop convery NPMotor
        private FCResultType flowChart27_FlowRun(object sender, EventArgs e)
        {
            if (IB_SMEMAUpStreamBoardAvailable.Off() || IsDryCycleMode)
            {
                OB_SM_UpBoardReady.Off();
                OB_UpCvyMtFwd.Off();
                OB_SM_UpFaliBoard.Off();
                if (!IsEnabledInOutDoor)
                {
                    if (IB_UpCvyDischargeSensor.Off(100))
                    {
                        SysPara.RightDoorAlarmEnable = false;
                        Conveyor1Timeout.Restart();
                        return FCResultType.NEXT;
                    }
                    else
                    {

                        if (DownConveyorTimeout.On(10000))
                        {
                            SysPara.NPShowAlarm("1002");
                            DownConveyorTimeout.Restart();
                            MiddleLayer.MainF.btnPause_Click(null, null);
                            return FCResultType.IDLE;

                        }
                        OB_UpCvyMtFwd.On();


                        return FCResultType.IDLE;
                    }
                }
                Conveyor1Timeout.Restart();
                processData.fnIndicateCancelUnit();
                return FCResultType.NEXT;
            }
            else if (Conveyor1Timeout.On(15000))
            {
                SDKKernal.ShowAlarm(IB_SMEMAUpStreamBoardAvailable.Port.ToString());
                return FCResultType.IDLE;
            }
            else { return FCResultType.IDLE; }

        }
        private FCResultType flowChart18_FlowRun(object sender, EventArgs e)
        {
            SysPara.RightDoorAlarmEnable = true;
            OB_SM_UpBoardReady.Off();
            return FCResultType.NEXT;

        }
        //Return
        private FCResultType flowChart28_FlowRun(object sender, EventArgs e)
        {
            if (IsDryCycleMode)
            {
                if (!Conveyor1Timeout.On(1000))
                    return FCResultType.IDLE;
            }
            MiddleLayer.LogF.AddLog(LogType.Production, "The upper conveyor cycle ends, waiting for the front machine to be released", true);
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
        }


        //SMEMA OB_SMDownFaliBoard=true
        private FCResultType flowChart30_FlowRun(object sender, EventArgs e)
        {
            OB_SM_UpFaliBoard.On();
            return FCResultType.NEXT;
        }
        #endregion

        private void OB_UpCvyJackingCyDown_Click(object sender, EventArgs e)
        {

        }
        #region down convery init flow
        private FCResultType flowChart36_FlowRun(object sender, EventArgs e)
        {
            if (isdownInitialOK)
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "The down conveyor  has been initialized, skip the initialization this time", true);
                return FCResultType.CASE1;
            }
            else
            {
                OB_DownCvyMtFwd.Off();
                OB_DownCvyMtRev.Off();
                OB_DownCvyMtSpeed.Off();
                downInitialStatus = false;
                MiddleLayer.LogF.AddLog(LogType.Production, "Down conveyor starts to initialize", true);
                return FCResultType.NEXT;
            }
        }

        private FCResultType flowChart37_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyDischargeSensor.On())
            {
                SysPara.NPShowAlarm("1010");
                OB_DownCvyMtFwd.Off();
                return FCResultType.IDLE;
            }
            if (IB_DownCvyMotorAlarm.On())
            {
                OB_DownCvyMtRev.Off();
                OB_DownCvyMtSpeed.Off();
                OB_DownCvyMtFwd.Off();
                SysPara.NPShowAlarm("1012");
                return FCResultType.IDLE;
            }
            return FCResultType.NEXT;
        }

        private FCResultType flowChart38_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyInPlace.On() || IB_DownCvyDischargeSensor.On())
            {
                OB_DownCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            OB_DownCvyMtFwd.On();
            DownConveyorTimeout.Restart();
            return FCResultType.NEXT;
        }

        private FCResultType flowChart39_FlowRun(object sender, EventArgs e)
        {
            OB_DownCvyMtFwd.On();
            if (IB_DownCvyInPlace.On() || IB_DownCvyDischargeSensor.On())
            {
                OB_DownCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            else
            {
                if (DownConveyorTimeout.On(5000))
                {
                    if (IB_DownCvyFeedSensor.On())
                    {
                        SysPara.NPShowAlarm("1010");
                        OB_DownCvyMtFwd.Off();
                        return FCResultType.IDLE;
                    }
                    else
                    {
                        OB_DownCvyMtFwd.Off();
                        return FCResultType.NEXT;
                    }

                }
            }
            return FCResultType.IDLE;
        }

        private FCResultType flowChart40_FlowRun(object sender, EventArgs e)
        {
            if (!isdownInitialOK)
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Down conveyor initialization completed", true);
                //BottomConveyorStartFlow.TaskReset();
                DownConveyorAutoFlow.ResetDownConveyorFlow();
                DownConveyorAutoFlow.StartDownConveyorFlow();
            }
            downInitialStatus = true;
            isdownInitialOK = true;
            if (downInitialStatus && upInitialStatus)
            {
                bInitialOk = true;
            }
            return FCResultType.IDLE;
        }
        #endregion
        private void panel10_Paint(object sender, PaintEventArgs e)
        {

        }

        #region MOD@@ Guillermo Carrillo - Conveyor automatico (conveyor inferior)
        // Con la casilla "Conveyor automatico" activada y la maquina corriendo en AUTO, cada 30 s prende
        // DownCvyMtFwd hasta 5 s, o hasta que un carrier llegue a InPlace o a Discharge.
        // Protecciones: no arranca si hay un carrier en InPlace o en Discharge (no lo empuja a la
        // siguiente maquina), ni con alarma del motor, ni con la maquina detenida, ni mientras el
        // flujo del conveyor inferior lo esta moviendo. La casilla arranca apagada al abrir el programa.
        private const int CvyAutoIntervaloMs = 30000;   // cada cuanto se prende (ms)
        private const int CvyAutoPulsoMs = 5000;        // tiempo maximo encendido (ms)
        private CheckBox chkCvyAutomatico;
        private System.Windows.Forms.Timer tmrCvyAutomatico;
        private readonly System.Diagnostics.Stopwatch swCvyAuto = new System.Diagnostics.Stopwatch();
        private bool cvyAutoPulsoActivo = false;

        private void CrearConveyorAutomatico()
        {
            chkCvyAutomatico = new CheckBox();
            chkCvyAutomatico.Name = "chkCvyAutomatico";
            chkCvyAutomatico.Appearance = System.Windows.Forms.Appearance.Button;
            chkCvyAutomatico.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            chkCvyAutomatico.Location = new Point(110, 463);   // debajo de DownCvyMotorAlarm
            chkCvyAutomatico.Size = new Size(250, 40);
            chkCvyAutomatico.TextAlign = ContentAlignment.MiddleLeft;
            chkCvyAutomatico.CheckedChanged += ChkCvyAutomatico_CheckedChanged;
            panel10.Controls.Add(chkCvyAutomatico);
            PintarCvyAutomatico();

            tmrCvyAutomatico = new System.Windows.Forms.Timer();   // corre en la pantalla, no frena el flujo
            tmrCvyAutomatico.Interval = 200;
            tmrCvyAutomatico.Tick += TmrCvyAutomatico_Tick;
            tmrCvyAutomatico.Start();
        }

        private void PintarCvyAutomatico()
        {
            chkCvyAutomatico.Text = "Conveyor automatico: " + (chkCvyAutomatico.Checked ? "ON" : "OFF");
            chkCvyAutomatico.BackColor = chkCvyAutomatico.Checked ? Color.LightGreen : Color.White;
        }

        private void ChkCvyAutomatico_CheckedChanged(object sender, EventArgs e)
        {
            PintarCvyAutomatico();
            if (!chkCvyAutomatico.Checked && cvyAutoPulsoActivo)
                TerminarPulsoCvyAuto("se apago la casilla");
            swCvyAuto.Restart();   // el primer encendido es 30 s despues de activarla
            MiddleLayer.LogF.AddLog(LogType.Production, "Conveyor automatico: " + (chkCvyAutomatico.Checked ? "activado" : "desactivado"), true);
        }

        private void TmrCvyAutomatico_Tick(object sender, EventArgs e)
        {
            try
            {
                bool permitido = chkCvyAutomatico.Checked && SysPara.SystemRun && SysPara.SystemMode == RunMode.AUTO
                                 && !IB_DownCvyMotorAlarm.On();   // solo en automatico (no en HOME/inicializacion)
                bool hayCarrier = IB_DownCvyInPlace.On() || IB_DownCvyDischargeSensor.On();
                bool flujoMoviendo = downConveyorMtRevStare == DownConveyorMtRevStare.Start;

                if (cvyAutoPulsoActivo)
                {
                    if (!permitido)
                        TerminarPulsoCvyAuto("maquina detenida, casilla apagada o alarma del motor");
                    else if (hayCarrier)
                        TerminarPulsoCvyAuto("llego un carrier a InPlace o Discharge");
                    else if (swCvyAuto.ElapsedMilliseconds >= CvyAutoPulsoMs)
                        TerminarPulsoCvyAuto("tiempo cumplido");
                    return;
                }

                if (!permitido || hayCarrier || flujoMoviendo)
                {
                    swCvyAuto.Restart();   // la cuenta de 30 s empieza cuando se puede mover
                    return;
                }
                if (swCvyAuto.ElapsedMilliseconds < CvyAutoIntervaloMs)
                    return;

                OB_DownCvyMtFwd.On();
                cvyAutoPulsoActivo = true;
                swCvyAuto.Restart();
                MiddleLayer.LogF.AddLog(LogType.Production, "Conveyor automatico: forward encendido", true);
            }
            catch (Exception ex)
            {
                try { OB_DownCvyMtFwd.Off(); } catch { }
                cvyAutoPulsoActivo = false;
                chkCvyAutomatico.Checked = false;   // se apaga sola si algo falla
                MiddleLayer.LogF.AddLog(LogType.Production, "Conveyor automatico: error, se desactivo: " + ex.Message, true);
            }
        }

        private void TerminarPulsoCvyAuto(string motivo)
        {
            // si el flujo del conveyor inferior lo esta usando, no se le apaga
            if (downConveyorMtRevStare != DownConveyorMtRevStare.Start)
                OB_DownCvyMtFwd.Off();
            cvyAutoPulsoActivo = false;
            swCvyAuto.Restart();
            MiddleLayer.LogF.AddLog(LogType.Production, "Conveyor automatico: forward apagado (" + motivo + ")", true);
        }
        #endregion

        #region bottom conveyor auto flow

        private FCResultType flowChart35_FlowRun(object sender, EventArgs e)
        {
            downConveyorProductStare = DownConveyorProductStare.None;
            downConveyorSMMStare = DownConveyorSMMStare.None;
            downConveyorMtRevStare = DownConveyorMtRevStare.None;
            downInputFlow = 0;
            downOutputFlow = 0;
            MiddleLayer.LogF.AddLog(LogType.Production, "Down conveyor automatic flow on", true);
            return FCResultType.NEXT;
        }

        private FCResultType flowChart41_FlowRun(object sender, EventArgs e)
        {


            if (IB_DownCvyFeedSensor.On(2000) && IB_DownCvyDischargeSensor.On(2000))
            {

                DownConveyorAutoTimeout.Restart();
                SysPara.NPShowAlarm("1010");
                MiddleLayer.ConveyorF.OB_DownCvyMtFwd.Off();
                MiddleLayer.ConveyorF.OB_DownCvyMtRev.Off();
                OB_SM_DownBoardReady.Off();
                OB_SM_DownCallBoard.Off();
                return FCResultType.IDLE;
            }
            if (IB_DownCvyFeedSensor.On(3000) && downConveyorProductStare == DownConveyorProductStare.None)
            {
                downInputFlow = 2;
                DownConveyorAutoTimeout.Restart();
                downConveyorProductStare = DownConveyorProductStare.Input;
                downConveyorMtRevStare = DownConveyorMtRevStare.Start;
            }

            //return FCResultType.IDLE;
            if (downConveyorProductStare != DownConveyorProductStare.None)
            {

                if (downConveyorProductStare == DownConveyorProductStare.Input)
                {
                    //RefreshDifferentThreadUI(lalDownCnyState, () =>
                    //{
                    //    lalDownCnyState.Text = "Input";
                    //});
                    if (IB_DownCvyInPlace.On(3) && downInputFlow == 1)
                    {
                        downConveyorProductStare = DownConveyorProductStare.None;
                    }
                    if (IB_DownCvyDischargeSensor.On(5000))
                    {

                        SysPara.NPShowAlarm("1008");
                        MiddleLayer.ConveyorF.OB_DownCvyMtFwd.Off();
                        MiddleLayer.ConveyorF.OB_DownCvyMtRev.Off();
                        OB_SM_DownBoardReady.Off();
                        OB_SM_DownCallBoard.Off();
                        return FCResultType.IDLE;
                    }

                }
                if (downConveyorProductStare == DownConveyorProductStare.Output)
                {
                    //RefreshDifferentThreadUI(lalDownCnyState, () =>
                    //{
                    //    lalDownCnyState.Text = "Output";
                    //});

                    if (IB_DownCvyInPlace.Off(10000) && IB_DownCvyFeedSensor.Off(10000) && IB_DownCvyDischargeSensor.Off(10000))
                    {
                        downConveyorProductStare = DownConveyorProductStare.None;
                    }
                    if (IB_DownCvyFeedSensor.On(3000))
                    {


                        SysPara.NPShowAlarm("1007");
                        MiddleLayer.ConveyorF.OB_DownCvyMtFwd.Off();
                        MiddleLayer.ConveyorF.OB_DownCvyMtRev.Off();
                        OB_SM_DownBoardReady.Off();
                        OB_SM_DownCallBoard.Off();
                        return FCResultType.IDLE;
                    }

                }
                return FCResultType.NEXT;
            }
            //RefreshDifferentThreadUI(lalDownCnyState, () =>
            //{
            //    lalDownCnyState.Text = "None";
            //});

            if (IB_DownCvyInPlace.On(100) || IB_DownCvyDischargeSensor.On(100))
            {

                downOutputFlow = 1;
                downConveyorProductStare = DownConveyorProductStare.Output;
                downConveyorSMMStare = DownConveyorSMMStare.Output;

            }
            else
            {
                downInputFlow = 1;
                downConveyorProductStare = DownConveyorProductStare.Input;
                downConveyorSMMStare = DownConveyorSMMStare.Input;
            }
            return FCResultType.NEXT;
        }

        private FCResultType flowChart43_FlowRun(object sender, EventArgs e)
        {



            if (downConveyorProductStare == DownConveyorProductStare.Input)
            {

                switch (downInputFlow)
                {
                    case 1:
                        if (IB_DownCvyFeedSensor.On())
                        {
                            downInputFlow = 2;
                            DownConveyorAutoTimeout.Restart();

                            downConveyorMtRevStare = DownConveyorMtRevStare.Start;
                        }
                        break;
                    case 2:
                        if (IB_DownCvyInPlace.On())
                        {
                            downConveyorSMMStare = DownConveyorSMMStare.None;
                            if (IB_DownCvyFeedSensor.On())
                            {
                                downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                                DownConveyorAutoTimeout.Restart();
                                SysPara.NPShowAlarm("1007");
                            }
                            else
                            {

                                downInputFlow = 0;
                                DownConveyorAutoTimeout.Restart();
                                downConveyorSMMStare = DownConveyorSMMStare.Output;
                                downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                                downConveyorProductStare = DownConveyorProductStare.None;
                            }

                        }
                        else
                        {
                            if (DownConveyorAutoTimeout.On(10000))
                            {
                                if (IB_DownCvyFeedSensor.On())
                                {
                                    downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                                    DownConveyorAutoTimeout.Restart();
                                    SysPara.NPShowAlarm("1007");
                                }
                                else
                                {

                                    downInputFlow = 0;
                                    downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                                    downConveyorSMMStare = DownConveyorSMMStare.None;
                                    downConveyorProductStare = DownConveyorProductStare.None;
                                }
                            }
                        }
                        break;

                }
            }
            if (downConveyorProductStare == DownConveyorProductStare.Output)
            {
                switch (downOutputFlow)
                {
                    case 1:
                        if (IB_SMEMADownStreamBoardAvailable.On())
                        {

                            downOutputFlow = 2;
                            DownConveyorAutoTimeout.Restart();
                            downConveyorMtRevStare = DownConveyorMtRevStare.Start;
                        }
                        break;
                    case 2:
                        if (IB_DownCvyDischargeSensor.On())
                        {
                            downOutputFlow = 3;
                            DownConveyorAutoTimeout.Restart();
                        }
                        else
                        {
                            if (DownConveyorAutoTimeout.On(10000))
                            {

                                downInputFlow = 0;
                                downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                                downConveyorSMMStare = DownConveyorSMMStare.None;
                                downConveyorProductStare = DownConveyorProductStare.None;
                            }
                        }
                        break;
                    case 3:
                        if (IB_DownCvyDischargeSensor.Off())
                        {
                            downConveyorSMMStare = DownConveyorSMMStare.None;
                            downOutputFlow = 4;
                            DownConveyorAutoTimeout.Restart();
                        }
                        else
                        {
                            if (DownConveyorAutoTimeout.On(10000))
                            {
                                SysPara.NPShowAlarm("1008");
                                downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                            }
                        }
                        break;
                    case 4:

                        if (DownConveyorAutoTimeout.On(3000))
                        {
                            downOutputFlow = 0;
                            downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                            downConveyorSMMStare = DownConveyorSMMStare.None;
                            downConveyorProductStare = DownConveyorProductStare.None;
                        }
                        break;
                }
            }
            return FCResultType.NEXT;
        }

        private FCResultType flowChart44_FlowRun(object sender, EventArgs e)
        {
            return FCResultType.NEXT;
        }

        private FCResultType flowChart46_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyDischargeSensor.On())
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Down Conveyor: The material reaches the discharge port", true);
                DownConveyorAutoTimeout.Restart();
                return FCResultType.NEXT;
            }





            return FCResultType.IDLE;
        }

        private FCResultType flowChart47_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyDischargeSensor.Off())
            {
                DownConveyorAutoTimeout.Restart();
                return FCResultType.NEXT;
            }
            if (DownConveyorAutoTimeout.On(5000))
            {

                SysPara.NPShowAlarm("1008");
                DownConveyorAutoTimeout.Restart();
                MiddleLayer.MainF.btnPause_Click(null, null);
                return FCResultType.IDLE;
            }
            OB_DownCvyMtFwd.On();
            return FCResultType.IDLE;
        }

        private FCResultType flowChart48_FlowRun(object sender, EventArgs e)
        {
            if (IB_SMEMADownStreamBoardAvailable.Off())
            {
                OB_DownCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            if (DownConveyorAutoTimeout.On(5000))
            {

                SysPara.NPShowAlarm("1008");
                DownConveyorAutoTimeout.Restart();
                MiddleLayer.MainF.btnPause_Click(null, null);
                return FCResultType.IDLE;
            }
            OB_DownCvyMtFwd.On();
            return FCResultType.IDLE;
        }

        private FCResultType flowChart49_FlowRun(object sender, EventArgs e)
        {
            if (downConveyorSMMStare == DownConveyorSMMStare.Input)
            {
                OB_SM_DownBoardReady.Off();
                OB_SM_DownCallBoard.On();
            }
            if (downConveyorSMMStare == DownConveyorSMMStare.Output)
            {
                OB_SM_DownBoardReady.On();
                OB_SM_DownCallBoard.Off();
            }
            if (downConveyorSMMStare == DownConveyorSMMStare.None)
            {
                OB_SM_DownBoardReady.Off();
                OB_SM_DownCallBoard.Off();
            }
            return FCResultType.NEXT;
        }

        private FCResultType flowChart50_FlowRun(object sender, EventArgs e)
        {
            if (downConveyorMtRevStare == DownConveyorMtRevStare.Start)
            {
                OB_DownCvyMtRev.Off();
                OB_DownCvyMtSpeed.Off();
                if (IB_DownCvyMotorAlarm.On())
                {
                    SysPara.NPShowAlarm("0012");
                }

                OB_DownCvyMtFwd.On();
            }
            if (downConveyorMtRevStare == DownConveyorMtRevStare.stop)
            {
                OB_DownCvyMtFwd.Off();
            }
            return FCResultType.NEXT;
        }

        private FCResultType flowChart52_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyInPlace.Off() && IB_DownCvyDischargeSensor.Off())
            {
                DownConveyorAutoTimeout.Restart();
            }
            if (DownConveyorAutoTimeout.On(3000))
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Lower conveyor: The feed flow block station detects that there is material, and jumps to the state of waiting for the material to be released", true);
                return FCResultType.CASE1;
            }
            if (IB_DownCvyFeedSensor.On())
            {
                DownConveyorAutoTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {

            }
            return FCResultType.IDLE;
        }

        private FCResultType flowChart51_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyInPlace.On())
            {
                OB_DownCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            else
            {
                if (DownConveyorAutoTimeout.On(10000))
                {
                    if (IB_DownCvyFeedSensor.On())
                        SysPara.NPShowAlarm("1007");
                    OB_DownCvyMtFwd.Off();
                    return FCResultType.CASE1;
                }
            }
            return FCResultType.IDLE;
        }

        #endregion

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private FCResultType flowChart36_FlowRun_1(object sender, EventArgs e)
        {
            return FCResultType.NEXT;

        }
        AutoResetEvent SMMProduct = new AutoResetEvent(false);
        AutoResetEvent SMMDisProduct = new AutoResetEvent(false);
        private void button1_Click(object sender, EventArgs e)
        {
            SMMProduct.Set();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SMMDisProduct.Set();
        }

        private FCResultType flowChart45_FlowRun(object sender, EventArgs e)
        {
            OB_SM_UpBoardReady.On();
            if (SysPara.R1AssembResult)
            {
                OB_SM_UpFaliBoard.Off();
            }
            else
            {
                OB_SM_UpFaliBoard.On();
            }
            MiddleLayer.GantryF.processData.fnTimersRunning(false);
            MiddleLayer.GantryF.processData.IsUnitProcesing = false;
            return FCResultType.NEXT;
        }

        private void ConveyorForm_Load(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            downConveyorProductStare = DownConveyorProductStare.None;
            downConveyorSMMStare = DownConveyorSMMStare.None;
            downConveyorMtRevStare = DownConveyorMtRevStare.None;
            downInputFlow = 0;
            downOutputFlow = 0;
            MiddleLayer.ConveyorF.OB_DownCvyMtFwd.Off();
            MiddleLayer.ConveyorF.OB_DownCvyMtRev.Off();
            OB_SM_DownBoardReady.Off();
            OB_SM_DownCallBoard.Off();
            DownConveyorAutoTimeout.Restart();
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            string dateTimeStr = DateTime.Now.ToString("yyyyMMdd");
            if (GetSettingValue("PSet", "dateTime") != dateTimeStr)
            {
                DataTable dt = SettingData.Tables["PSet"];
                dt.Rows[0]["dateTime"] = dateTimeStr;
                dt.AcceptChanges();
                MiddleLayer.SystemF.WriteSettingData();

                string path = @"D:\WorkLog\Image\BMP";
                string path1 = @"D:\WorkLog\Image\JPG";
                string path2 = @"D:\WorkLog\Image\ScrewData";
                string path3 = @"D:\WorkLog\MesLog";
                string path4 = @"D:\WorkLog\UIDataLog";
                SaveFile.DeleteFile(path, 10);
                SaveFile.DeleteFile(path1, 10);
                SaveFile.DeleteFile(path2, 10);
                SaveFile.DeleteFile(path3, 10);
                SaveFile.DeleteFile(path4, 10);
               



                string path5 = @"D:\EVMS\TP\LOG";
                SaveFile.DeleteFile1(path5, 21);

            }
            if (SaveFile.DriveFreeSpace(20))
                SDKKernal.ShowAlarm("0020");
            timer1.Enabled = true;
        }

        private void panel_UpFlowModule_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
