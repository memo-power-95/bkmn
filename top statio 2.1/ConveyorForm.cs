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
//using NPSDK.Enums;
using NPSDK;
using Alpha.Classes;
using System.IO;
using Alpha._0.Classes;
using Alpha.FunctionForms;
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

        public bool smCallboard = false;
        public bool upboardSmema = false;

        cSmartAlarm DynamicAlarm = new cSmartAlarm();
        private Task<ScannerForm.ScannerReadResult> taskGetScannerCode = null;
        private bool bSimulateRobotDone = false;
        private bool bUnitSmemaNG = false;
        private bool bUnitForExpulse = false;
        private string sSerialNumber = "";
        ProcessDataForm pdfProcess = new ProcessDataForm();
        //downConvryor 
        float downInputFlow = 0;
        float downOutputFlow = 0;

        public UC_DownConveyorAutoFlow DownConveyorAutoFlow;
        public UC_UpConveyorAutoFlow.UC_UpConveyorAutoFlow UpConveyorAutoFlow;

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
        }

        /// <summary>
        /// Initialize mod control (初始化模组控制)
        /// </summary>
        /// <param name="motorMoveCtrl">Mod Control(模组控件)</param>
        /// <param name="panel_MoveFlowModule">(Parent panel)插入的父面板</param>
        private void InitMotorControl(ref UC_DownConveyorAutoFlow motorMoveCtrl, Panel panel_MoveFlowModule)
        {
            if (DownConveyorAutoFlow == null)
                DownConveyorAutoFlow = new UC_DownConveyorAutoFlow();
            DownConveyorAutoFlow.Dock = DockStyle.Fill;
            panel_MoveFlowModule.Controls.Add(DownConveyorAutoFlow);
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
            //flowChart7.TaskReset();
            UpConveyorAutoFlow.fc_UpConveryStart.TaskReset();
        }

        public override void Run()
        {
            //flowChart7.TaskRun();
            UpConveyorAutoFlow.fc_UpConveryStart.TaskRun();
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
            //if (isdownInitialOK)//Init Finish flag
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

        #region ConveryInitial
        private FCResultType flowChart1_FlowRun(object sender, EventArgs e)
        {
            bInitialOk = false;
            upInitialStatus = false;
            OB_SM_UpBoardReady.Off();
            OB_SM_UpFaliBoard.Off();
            processData.fnIndicateFinishedUnit();
            SysPara.Que_Product_pallet_Code.Clear();
            MiddleLayer.ConveyorF.UpConveyorAutoFlow.DryRunMode = MiddleLayer.ProcessF.GetSettingValue("PSet", "IsDryCycleMode");
            MiddleLayer.ConveyorF.UpConveyorAutoFlow.ByPassMode = MiddleLayer.ProcessF.GetSettingValue("PSet", "dcEnableCheckProcess");
            MiddleLayer.ConveyorF.UpConveyorAutoFlow.DisbleInOutDoor = true;
            return FCResultType.NEXT;
        }
        //Wait Robot Initial finish
        private FCResultType flowChart2_FlowRun(object sender, EventArgs e)
        {
            lock (SysPara.StatusLock)
            {
                if (SysPara.RIniRet) { ConveyorInitTimeout.Restart(); return FCResultType.NEXT; }
                else { return FCResultType.IDLE; }
            }
        }
        // JackingCy back
        private FCResultType flowChart3_FlowRun(object sender, EventArgs e)
        {
            Cy_ST1UpStreamLiftingUnite.Off();
            if (IB_UpCvyJackingCyOut.Off() || IB_UpCvyJackingCyBack.On())
            {
                ConveyorInitTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(SysPara.TimeOutSec))
                { SysPara.NPShowAlarm("1004"); }     //Board support cylinder go down action overtime

                return FCResultType.IDLE;
            }
        }
        //BlockCy back
        private FCResultType flowChart4_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyBlockCyOut.On();
            if (IB_UpCvyBlockCyOut.On())
            {
                ConveyorInitTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(SysPara.TimeOutSec))
                { SysPara.NPShowAlarm("1006"); }     //Stop cylinder go down action overtime

                return FCResultType.IDLE;
            }
        }
        //UpCvyMtFwd off
        private FCResultType flowChart5_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyMtFwd.Off();
            return FCResultType.NEXT;
        }
        //Initial finish
        private FCResultType flowChart6_FlowRun(object sender, EventArgs e)
        {
            upInitialStatus = true;
            if (downInitialStatus && upInitialStatus)
            {
                bInitialOk = true;
            }
            return FCResultType.IDLE;
        }

        #endregion
        #region Convery AutoRun

        //UpConvery flow start
        private FCResultType flowChart7_FlowRun(object sender, EventArgs e)
        {
            return FCResultType.NEXT;
        }
        //SMEMA Ready
        private FCResultType flowChart8_FlowRun(object sender, EventArgs e)
        {

            if (MiddleLayer.GantryF.GetSettingValue("MSet", "EPlaningCounts"))
            {
                int Planing = MiddleLayer.GantryF.GetSettingValue("MSet", "ProQuantity");
                int finished = MiddleLayer.GantryF.GetRecipeValue("RSet", "finishedCounts");
                if (Planing <= finished)
                {
                    SysPara.NPShowAlarm("0009");
                    return FCResultType.IDLE;
                }
            }

            // OB_SMMachineReady.On();
            OB_UpCvyBlockCyOut.On();
            Conveyor1Timeout.Restart();
            if (IB_SMEMAUpStreamMachineReady.On() || SysPara.bDryCycle || upboardSmema)
            {
                if (IB_SMEMAUPFailAvailable.On())
                {
                    SysPara.bOB_SMForntMachineNg = true;
                }
                else { SysPara.bOB_SMForntMachineNg = false; }
                upboardSmema = false;
                return FCResultType.NEXT;
            }
            else { return FCResultType.IDLE; }

        }
        //Blockcylinder up，IB_UpCvyBlockCyOut=True
        private FCResultType flowChart9_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyBlockCyOut.Off(500) || SysPara.bDryCycle)
            {
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1005");           //Stop cylinder go up action overtime
                }
                return FCResultType.IDLE;
            }
        }
        //Convery forward
        private FCResultType flowChart10_FlowRun(object sender, EventArgs e)
        {

            if (IB_UpCvyFeedSensor.On() || SysPara.bDryCycle)
            //if (true)
            {
                // 10711 data reset
                MiddleLayer.GantryF.LastProduct = false;

                OB_UpCvyMtSpeed.Off();
                OB_UpCvyMtFwd.On();
                MiddleLayer.GantryF.processData.fnIndicateStartingUnit();
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                //if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                //{

                //    SysPara.NPShowAlarm("1001");
                //}//UpStream FeedIn Timeout
                return FCResultType.IDLE;
            }
        }
        //IB_UpCvyFeedSensor=True，Pallet transfer in
        private FCResultType flowChart11_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyFeedSensor.On() || SysPara.bDryCycle)
            {
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            if (Conveyor1Timeout.On(SysPara.TimeOutSec))    
            {

                SysPara.NPShowAlarm("1001");
            }


            return FCResultType.IDLE;
        }
        //IB_UpCvyDecelerationSensor=true
        private FCResultType flowChart12_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle)
            {
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }

            if (IB_UpCvyDecelerationSensor.On())
            {
                OB_SMMachineReady.Off();
                SysPara.bOB_SMMachineReady = false;
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(10000))
                {

                    SysPara.NPShowAlarm("1001");//Slow down upstream Timeout！
                }
                return FCResultType.IDLE;
            }
        }
        //wait product inplace
        private FCResultType flowChart13_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle)
            {
                if (Conveyor1Timeout.On(2000))
                {
                    Cy_ST1UpStreamLiftingUnite.On();
                    //MiddleLayer.MachineMainF.CycleTimeStart(1);
                    //MiddleLayer.MachineMainF.ClearResult();
                    SysPara.R1AssembResult = true;
                    OB_UpCvyMtFwd.Off();
                    OB_UpCvyMtSpeed.Off();
                    Conveyor1Timeout.Restart();
                    SysPara.LeftDoorAlarmEnable = false;
                    return FCResultType.NEXT;
                }
                else { return FCResultType.IDLE; }
            }

            if (IB_UpCvyInPlaceSensor.On())
            {
                // GantryForm.DelayMs(500);
                Cy_ST1UpStreamLiftingUnite.On();
                //MiddleLayer.MachineMainF.CycleTimeStart(1);
                //MiddleLayer.MachineMainF.ClearResult();
                SysPara.R1AssembResult = true;
                OB_UpCvyMtFwd.Off();
                OB_UpCvyMtSpeed.Off();
                Conveyor1Timeout.Restart();
                SysPara.LeftDoorAlarmEnable = false;
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                    SysPara.NPShowAlarm("1001");           //upstream arrive Timeout！
                return FCResultType.IDLE;
            }
        }
        //Convery slow
        private FCResultType flowChart15_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle)
            {
                if (Conveyor1Timeout.On(2000))
                {
                    OB_UpCvyMtSpeed.On();
                    Conveyor1Timeout.Restart();
                    return FCResultType.NEXT;
                }
                else { return FCResultType.IDLE; }
            }

            if (IB_UpCvyDecelerationSensor.On())
            {
                OB_UpCvyMtSpeed.On();
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1001");
                }//Slow down upstream Timeout！
                return FCResultType.IDLE;
            }
        }
        //wait IB_UpCvyJackingCyOut=true
        private FCResultType flowChart14_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle) { processData.fnInitilizeResultTable(); return FCResultType.NEXT; }
            if (IB_UpCvyJackingCyOut.On() && IB_UpCvyJackingCyBack.Off())
            {
                OB_UpCvyBlockCyOut.Off();
                //processData.fnInitilizeResultTable();
                MiddleLayer.GantryF.CurrentRowsIndex = 0;
                //SysPara.Mes_StaraTime = "[" + DateTime.Now.ToString("mm/dd/yyyy hh:mm:ss");
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1003");
                }
                return FCResultType.IDLE;
            }
        }
        //Judge front machine is NG
        private FCResultType flowChart16_FlowRun(object sender, EventArgs e)
        {
            //if (MiddleLayer.GantryF.GetRecipeValue("RSet", "TISEnable"))
            //{
            //    return FCResultType.NEXT;
            //}

            if (SysPara.bOB_SMForntMachineNg)
            {
                return FCResultType.CASE1;
            }
            else { return FCResultType.NEXT; }
        }
        //Front machine result is OK
        private FCResultType flowChart17_FlowRun(object sender, EventArgs e)
        {
            return FCResultType.NEXT;
        }
        //Notice R1 Goto Convery take picture
        private FCResultType flowChart18_FlowRun(object sender, EventArgs e)
        {
            lock (SysPara.StatusLock)
            {
                //MiddleLayer.MachineMainF.ClearPreasureData();
                SysPara.R1AssembStatus = true;
            }
            return FCResultType.NEXT;
        }
        //Wait R1 assemb Compelet
        private FCResultType flowChart19_FlowRun(object sender, EventArgs e)
        {
            bool IsByPass = MiddleLayer.ProcessF.GetSettingValue("PSet", "dcEnableCheckProcess");
            if (IsByPass)
            {
                return FCResultType.NEXT;
            }
            //if (SysPara.bBypassMode) { return FCResultType.NEXT; }
            lock (SysPara.StatusLock)
            {
                if (SysPara.R1AssembCompeletStatus)
                {
                    SysPara.R1AssembStatus = false;
                    SysPara.R1AssembCompeletStatus = false;
                    

                    //MiddleLayer.MachineMainF.GetCycleTime_SingleST();
                    return FCResultType.NEXT;
                }
                else { return FCResultType.IDLE; }
            }
        }
        //Get assemb result
        private FCResultType flowChart20_FlowRun(object sender, EventArgs e)
        {
            bool assemb = SysPara.R1AssembResult;
            if (!MiddleLayer.ProcessF.GetSettingValue("PSet", "dcEnableCheckProcess") || !MiddleLayer.ProcessF.GetSettingValue("PSet", "IsDryCycleMode"))
            {
                //MiddleLayer.cfxHandler.WorkCompleted(SysPara.cfx_PalletCode, SysPara.cfx_FaliList, SysPara.cfx_AbortList);
                //WriteMes(SysPara.cfx_PalletCode, SysPara.Que_Product_pallet_Code, assemb);               
            }
            SysPara.Que_Product_pallet_Code.Clear();
            return FCResultType.NEXT;
        }
        //OB_SMDownFaliBoard=false
        private FCResultType flowChart21_FlowRun(object sender, EventArgs e)
        {
            //OB_SM_UpBoardReady
            OB_SM_UpFaliBoard.Off();
            OB_SM_UpBoardReady.On();
            Conveyor1Timeout.Restart();
            //MiddleLayer.GantryF.processData.fnTimersRunning(false);
            //MiddleLayer.GantryF.processData.IsUnitProcesing = false;
            return FCResultType.NEXT;
        }
        //wait next station call board
        private FCResultType flowChart22_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle)
            {
                return FCResultType.NEXT;
            }
            if (IB_SMEMAUpStreamBoardAvailable.On() || smCallboard)
            {
                smCallboard = false;
                //OB_SM_UpBoardReady.On();
                Conveyor1Timeout.Restart();
                MiddleLayer.GantryF.processData.IsUnitProcesing =true;
                MiddleLayer.GantryF.processData.fnTimersRunning(true);
                return FCResultType.NEXT;
            }
            else
            {
               
                return FCResultType.IDLE;
            }
        }
        //MOD@@ Guillermo Carrillo: mientras se revisa el sensor de descarga, el paso se hace por tiempo
        private const bool IgnorarSensorDescarga = true;      // false = regresa a usar el sensor
        private const int EsperaDescargaMs = 5000;            // 5 s desde que baja el jacking
        private readonly System.Diagnostics.Stopwatch swDescarga = new System.Diagnostics.Stopwatch();

        //OB_UpCvyJackingCyUp down
        private FCResultType flowChart23_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle) { Cy_ST1UpStreamLiftingUnite.Off(); return FCResultType.NEXT; }
            Cy_ST1UpStreamLiftingUnite.Off();

            if (IB_UpCvyJackingCyOut.Off() && IB_UpCvyJackingCyBack.On())
            {
                swDescarga.Restart();                          //MOD@@ jacking abajo: empieza la cuenta
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1004");
                }
                return FCResultType.IDLE;
            }
        }

        private FCResultType flowChart24_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyBlockCyOut.Off();

            if (IB_UpCvyBlockCyOut.On())
            {
                OB_UpCvyMtSpeed.Off(); //Convery Slower off
                OB_UpCvyMtFwd.On();      //Convery forward
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1006");
                }
                return FCResultType.IDLE;
            }
        }
        //Wait product in DischargeSensor
        private FCResultType flowChart25_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle)
            { return FCResultType.NEXT; }

            if (IgnorarSensorDescarga)                         //MOD@@ Guillermo Carrillo: por tiempo, sin el sensor
            {
                if (swDescarga.ElapsedMilliseconds < EsperaDescargaMs)
                    return FCResultType.IDLE;                   // sigue esperando sin frenar la maquina
                swDescarga.Reset();
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }

            if (IB_UpCvyDischargeSensor.On())
            {
                //OB_UpRunnerMtFwd.Off();
                Conveyor1Timeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1002");
                }
                return FCResultType.IDLE;
            }
        }
        //Wait product transfer out
        private FCResultType flowChart26_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle) { Conveyor1Timeout.Restart(); return FCResultType.NEXT; }

            if (IgnorarSensorDescarga)                         //MOD@@ Guillermo Carrillo: sin esperar el sensor
            {
                Conveyor1Timeout.Restart();
                processData.fnIndicateCancelUnit();             // lo mismo que el camino normal
                return FCResultType.NEXT;
            }

            if (IB_UpCvyDischargeSensor.Off())
            {
                Conveyor1Timeout.Restart();
                //OB_UpRunnerMtFwd.Off();
                processData.fnIndicateCancelUnit();
                return FCResultType.NEXT;
            }
            else
            {
                if (Conveyor1Timeout.On(SysPara.TimeOutSec))
                {
                    SysPara.NPShowAlarm("1002");
                }
                return FCResultType.IDLE;
            }
        }
        //Delay 3s,stop convery NPMotor
        private FCResultType flowChart27_FlowRun(object sender, EventArgs e)
        {
            if (SysPara.bDryCycle || IB_SMEMAUpStreamBoardAvailable.Off())
            {
                if (SysPara.bDryCycle)
                {
                    if (!Conveyor1Timeout.On(3000))
                    {
                        return FCResultType.IDLE;
                    }
                }
                OB_UpCvyMtFwd.Off();
                OB_SM_UpBoardReady.Off();
                OB_SM_UpFaliBoard.Off();
                Conveyor1Timeout.Restart();
                if (IB_UpCvyDischargeSensor.On())
                {
                    SysPara.NPShowAlarm("1002");
                }
                SysPara.RightDoorAlarmEnable = false;
                return FCResultType.NEXT;
            }
            return FCResultType.IDLE;
        }
        //Return
        private FCResultType flowChart28_FlowRun(object sender, EventArgs e)
        {
            return FCResultType.NEXT;
        }
        //Front machine result is NG
        private FCResultType flowChart29_FlowRun(object sender, EventArgs e)
        {
            //MiddleLayer.MachineMainF.SetStatistics(false);
            return FCResultType.NEXT;
        }
        //SMEMA OB_SMDownFaliBoard=true
        private FCResultType flowChart30_FlowRun(object sender, EventArgs e)
        {
            OB_SM_UpFaliBoard.On();
            OB_SM_UpBoardReady.On();
            return FCResultType.NEXT;
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
            MiddleLayer.LogF.AddLog(LogType.Production, "Down Convery Autoflow Start", true);
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

            //return FCResultType.IDLE;
            if (downConveyorProductStare != DownConveyorProductStare.None)
            {

                if (downConveyorProductStare == DownConveyorProductStare.Input)
                {
                    //OB_DownCvyBlockCyUp.Off();
                    //RefreshDifferentThreadUI(lalDownCnyState, () =>
                    //{
                    //    lalDownCnyState.Text = "Input";
                    //});

                    if (IB_DownCvyInPlace.On(3) && downInputFlow == 1)
                    {
                        downConveyorProductStare = DownConveyorProductStare.None;
                    }
                    if (IB_DownCvyDischargeSensor.On(3000))
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
                            if (IB_DownCvyBlockCyBack.On(2000))
                            {
                                SysPara.NPShowAlarm("1003");

                            }
                            else
                            {
                                downInputFlow = 2;
                                DownConveyorAutoTimeout.Restart();

                                downConveyorMtRevStare = DownConveyorMtRevStare.Start;
                            }

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
                                SysPara.NPShowAlarm("1010");
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

                            downOutputFlow = 5;
                            DownConveyorAutoTimeout.Restart();
                            downConveyorMtRevStare = DownConveyorMtRevStare.Start;
                        }
                        break;
                    case 2:


                        if (IB_DownCvyDischargeSensor.On())
                        {
                            downConveyorSMMStare = DownConveyorSMMStare.None;
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
                            OB_DownCvyBlockCyUp.Off();
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
                    case 5:
                        OB_DownCvyBlockCyUp.On();
                        if (IB_DownCvyBlockCyBack.On(200))
                        {
                            downOutputFlow = 2;
                            DownConveyorAutoTimeout.Restart();

                        }
                        else
                        {
                            if (DownConveyorAutoTimeout.On(5000))
                            {
                                downConveyorMtRevStare = DownConveyorMtRevStare.stop;
                                SysPara.NPShowAlarm("1014");
                                MiddleLayer.LogF.AddLog(LogType.Production, "DownConvery cylinder up timeout", SysPara.bEnableGeneralSaveLog);
                            }

                        }



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
                    SysPara.NPShowAlarm("1012");
                OB_DownCvyMtFwd.On();
            }
            if (downConveyorMtRevStare == DownConveyorMtRevStare.stop)
            {
                OB_DownCvyMtFwd.Off();
            }
            return FCResultType.NEXT;
        }



        #endregion

        #region down convery init flow
        private FCResultType flowChart36_FlowRun(object sender, EventArgs e)
        {
            if (isdownInitialOK)
            {
                MiddleLayer.LogF.AddLog(LogType.Production, "Down convery do no need initial", true);
                return FCResultType.CASE1;
            }
            else
            {
                OB_DownCvyMtFwd.Off();
                OB_DownCvyMtRev.Off();
                OB_DownCvyMtSpeed.Off();
                downInitialStatus = false;
                MiddleLayer.LogF.AddLog(LogType.Production, "Down convery do  initial", true);
                return FCResultType.NEXT;
            }
        }

        private FCResultType flowChart37_FlowRun(object sender, EventArgs e)
        {
            if (IB_DownCvyDischargeSensor.On(300))
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
            DownConveyorAutoTimeout2.Restart();
            return FCResultType.NEXT;
        }

        private FCResultType flowChart38_FlowRun(object sender, EventArgs e)
        {

            return FCResultType.NEXT;
        }

        private FCResultType flowChart39_FlowRun(object sender, EventArgs e)
        {


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
                MiddleLayer.LogF.AddLog(LogType.Production, "Down Convery initial finish", true);
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
        private void button1_Click(object sender, EventArgs e)
        {
            smCallboard = true;
        }

        private FCResultType flowChart31_FlowRun(object sender, EventArgs e)
        {

            OB_SMMachineReady.On();
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
            //}
            //else { return FCResultType.IDLE; }
        }

        private FCResultType flowChart32_FlowRun(object sender, EventArgs e)
        {
            SysPara.LeftDoorAlarmEnable = true;
            OB_SMMachineReady.Off();
            return FCResultType.NEXT;
        }

        private FCResultType flowChart33_FlowRun(object sender, EventArgs e)
        {


            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
            //}
            //else { return FCResultType.IDLE; }
        }

        private FCResultType flowChart34_FlowRun(object sender, EventArgs e)
        {

           
            SysPara.RightDoorAlarmEnable = true;
            OB_SM_UpBoardReady.Off();
          
            Conveyor1Timeout.Restart();
            return FCResultType.NEXT;
        }

        private void tabPage4_Click(object sender, EventArgs e)
        {

        }

        private FCResultType flowChart35_FlowRun_1(object sender, EventArgs e)
        {
            if (IB_DownCvyInPlace.On())
            {
                OB_DownCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            OB_DownCvyMtFwd.On();
            DownConveyorTimeout.Restart();
            return FCResultType.NEXT;
        }

        private FCResultType flowChart36_FlowRun_1(object sender, EventArgs e)
        {

            OB_DownCvyBlockCyUp.Off();
            if (IB_DownCvyBlockCyBack.Off(1000))
            {
                DownConveyorAutoTimeout2.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (DownConveyorAutoTimeout2.On(5000))
                {
                    DownConveyorAutoTimeout2.Restart();
                    SysPara.NPShowAlarm("1013");
                    MiddleLayer.LogF.AddLog(LogType.Production, "DownConvery cylinder up timeout", SysPara.bEnableGeneralSaveLog);
                }
                return FCResultType.IDLE;
            }
        }







        private void IB_DownCvyDischargeSensor_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            upboardSmema = true;
        }

        private FCResultType flowChart54_FlowRun(object sender, EventArgs e)
        {
            //MiddleLayer.SystemF.Cy_LeftDoorUnite.Off();
            //MiddleLayer.SystemF.Cy_RightDoorUnite.Off();
            //if (MiddleLayer.SystemF.IB_LeftDoorUpSensor.Off() && MiddleLayer.SystemF.IB_LeftDoorDownSensor.On()
            //    && MiddleLayer.SystemF.IB_RightDoorUpSensor.Off() && MiddleLayer.SystemF.IB_RightDoorDownSensor.On())
            //{
            return FCResultType.NEXT;
            //}
            //else
            //{
            //    return FCResultType.IDLE;
            //}
        }

        private async void WriteMes(string sSerialNumber, Queue<Product_Pallet_Code> assembResult, bool bStatusOk)
        {
            SysPara.MESPara = new MESParameter();
            int length = assembResult.Count;
            for (int i = 0; i < length; i++)
            {
                Product_Pallet_Code temp = new Product_Pallet_Code();
                temp = assembResult.Dequeue();
                if (!temp.PressureResult)
                {
                    KeyValuePair<string, string> keyTemp = new KeyValuePair<string, string>("Assemb result" + (i + 1).ToString() + "_FAIL", "");
                    SysPara.MESPara.fails.Add(keyTemp);
                }
                Measurement measureData = new Measurement();
                measureData.measureLabel = "result" + (i + 1).ToString();
                measureData.measureData = temp.PressureResult == true ? "OK" : "NG";
                measureData.measureMessage = temp.PressureResult == true ? "Assemb result" + (i + 1).ToString() + "_PASS" : "Assemb result" + (i + 1).ToString() + "_FAIL";
                SysPara.MESPara.measurements.Add(measureData);
            }
            await MiddleLayer.MesF.WriteUnitMES(sSerialNumber, null, null, true, bStatusOk);
        }

        private FCResultType flowChart55_FlowRun(object sender, EventArgs e)
        {
            if (IB_SMEMAUpStreamBoardAvailable.On())
            {

                return FCResultType.NEXT;
            }
            else
            {
                return FCResultType.IDLE;
            }

        }

        public static bool DelayMs(int delayMilliseconds)
        {
            DateTime now = DateTime.Now;
            Double s;
            do
            {
                TimeSpan spand = DateTime.Now - now;
                s = spand.TotalMilliseconds + spand.Seconds * 1000;
                Application.DoEvents();
            }
            while (s < delayMilliseconds);
            return true;
        }

        private void IB_UpCvyInPlaceSensor_Click(object sender, EventArgs e)
        {

        }

        private void OB_DownCvyMtFwd_Click(object sender, EventArgs e)
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

                string path = @"D:\WorkLog\BMP";
                string path1 = @"D:\WorkLog\JPG";
                string path2 = @"D:\WorkLog\UIDataLog";
                SaveFile.DeleteFile(path1, 3);
                SaveFile.DeleteFile(path2, 3);
                SaveFile.DeleteFile(path, 3);
                string saveImgPath = MiddleLayer.GantryF.GetSettingValue("PSet", "SaveImagePath") + "\\";
                SaveFile.DeleteFile1(saveImgPath, 21);


            }
            if (SaveFile.DriveFreeSpace(20))
                SDKKernal.ShowAlarm("0020");
            timer1.Enabled = true;
        }

        private FCResultType npFlowChart1_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyBlockCyOut.Off();
            if (IB_UpCvyBlockCyOut.Off())
            {
                ConveyorInitTimeout.Restart();
                return FCResultType.NEXT;
            }
            else
            {
                if (ConveyorInitTimeout.On(SysPara.TimeOutSec))
                { SysPara.NPShowAlarm("1006"); }     //Stop cylinder go down action overtime

                return FCResultType.IDLE;
            }
        }

        private FCResultType npFlowChart2_FlowRun(object sender, EventArgs e)
        {
            OB_UpCvyMtFwd.On();
            if (ConveyorInitTimeout.On(3000))
            {
       
                OB_UpCvyMtFwd.Off();
                return FCResultType.NEXT;
            }
            else
            {
                return FCResultType.IDLE;
            }
        }

        private FCResultType npFlowChart3_FlowRun(object sender, EventArgs e)
        {
            if (IB_UpCvyInPlaceSensor.On(200))
            {
                SysPara.NPShowAlarm("1000");
                return FCResultType.IDLE;
            }
            else
            {
                return FCResultType.NEXT;
            }
        }

        private void IB_UpCvyDischargeSensor_Click(object sender, EventArgs e)
        {

        }
    }
}
