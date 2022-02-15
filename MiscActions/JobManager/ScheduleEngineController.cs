extern alias jobentrybo;

using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Erp.Tables;
using Erp.Tablesets;
using Erp.Services.BO;
using Epicor.Data;
using Ice;
using Ice.Tables;
using Ice.Tablesets;

namespace Erp.BO.CRTI_MiscAction
{
    class ScheduleEngineController : BaseService
    {
        private Erp.Tablesets.ScheduleEngineTableset ds;
        private Erp.Contracts.ScheduleEngineSvcContract svc;
        public ScheduleEngineController(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        public bool ScheduleJob(jobentrybo.Erp.Tablesets.JobHeadRow job, out string message)
        {
            message = string.Empty;
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.ScheduleEngineSvcContract>(Db);
            try
            {
                DateTime dateTime = DateTime.Now;
                bool minimizeWIPFlag = false;
                bool schedulingMultiJobActive = false;
                bool allowMoveJobsAcrossPlants = false;
                bool autoLoadParentJobs = false;
                bool autoLoadChildJobs = false;
                int BWSchedStartTime = 0;
                string schedulingDirection = string.Empty;
                this.svc.GetSchedulingFlags(out schedulingMultiJobActive, out minimizeWIPFlag, out allowMoveJobsAcrossPlants, out autoLoadParentJobs, out autoLoadChildJobs, out BWSchedStartTime, out schedulingDirection);
                this.ds = new ScheduleEngineTableset();
                ScheduleEngineRow schedRow = new ScheduleEngineRow()
                {
                    Company = job.Company,
                    JobNum = job.JobNum,
                    AssemblySeq = 0,
                    OprSeq = 0,
                    OpDtlSeq = 0,
                    StartDate = dateTime,
                    StartTime = 0,
                    EndDate = job.ReqDueDate,
                    EndTime = BWSchedStartTime,
                    WhatIf = false,
                    Finite = false,
                    OverrideMtlCon = false,
                    OverRideHistDateSetting = 2,
                    SetupComplete = false,
                    ProductionComplete = false,
                    SchedTypeCode = "JJ",
                    ScheduleDirection = "End",
                    RecalcExpProdYld = false,
                    UseSchedulingMultiJob = schedulingMultiJobActive,
                    SchedulingMultiJobMinimizeWIP = minimizeWIPFlag,
                    SchedulingMultiJobIgnoreLocks = false,
                    SchedulingMultiJobMoveJobsAcrossPlants = allowMoveJobsAcrossPlants,
                    SysRowID = job.SysRowID,
                    RowMod = "A"
                };
                this.ds.ScheduleEngine.Add(schedRow);
                string c_WarnLogTxt = "";
                bool l_finished = false;
                this.svc.MoveJobItem(this.ds, out l_finished, out c_WarnLogTxt);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
            finally
            {
                this.svc.Dispose();
                this.svc = null;
                this.ds = null;
            }
        }
        public bool ScheduleJobs(List<string> jobNums, out string message)
        {
            message = string.Empty;
            var jobs = (from jh in this.Db.JobHead.AsEnumerable()
                        where jh.Company == "01" &&
                              jobNums.Contains(jh.JobNum)
                        select jh);
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.ScheduleEngineSvcContract>(Db);
            try
            {
                foreach(JobHead job in jobs)
                {
                    DateTime dateTime = DateTime.Now;
                    bool minimizeWIPFlag = false;
                    bool schedulingMultiJobActive = false;
                    bool allowMoveJobsAcrossPlants = false;
                    bool autoLoadParentJobs = false;
                    bool autoLoadChildJobs = false;
                    int BWSchedStartTime = 0;
                    string schedulingDirection = string.Empty;
                    this.svc.GetSchedulingFlags(out schedulingMultiJobActive, out minimizeWIPFlag, out allowMoveJobsAcrossPlants, out autoLoadParentJobs, out autoLoadChildJobs, out BWSchedStartTime, out schedulingDirection);
                    this.ds = new ScheduleEngineTableset();
                    ScheduleEngineRow schedRow = new ScheduleEngineRow()
                    {
                        Company = job.Company,
                        JobNum = job.JobNum,
                        AssemblySeq = 0,
                        OprSeq = 0,
                        OpDtlSeq = 0,
                        StartDate = dateTime,
                        StartTime = 0,
                        EndDate = job.ReqDueDate,
                        EndTime = BWSchedStartTime,
                        WhatIf = false,
                        Finite = false,
                        OverrideMtlCon = false,
                        OverRideHistDateSetting = 2,
                        SetupComplete = false,
                        ProductionComplete = false,
                        SchedTypeCode = "JJ",
                        ScheduleDirection = "End",
                        RecalcExpProdYld = false,
                        UseSchedulingMultiJob = schedulingMultiJobActive,
                        SchedulingMultiJobMinimizeWIP = minimizeWIPFlag,
                        SchedulingMultiJobIgnoreLocks = false,
                        SchedulingMultiJobMoveJobsAcrossPlants = allowMoveJobsAcrossPlants,
                        SysRowID = job.SysRowID,
                        RowMod = "A"
                    };
                    this.ds.ScheduleEngine.Add(schedRow);
                    string c_WarnLogTxt = "";
                    bool l_finished = false;
                    this.svc.MoveJobItem(this.ds, out l_finished, out c_WarnLogTxt);
                }
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
            finally
            {
                this.svc.Dispose();
                this.svc = null;
                this.ds = null;
            }
        }
    }
}
