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
    class JobEntryController : BaseService
    {
        private Erp.Tablesets.JobEntryTableset ds;
        private Erp.Contracts.JobEntrySvcContract svc;
        public JobEntryController(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        public bool UpdateJobDates(List<GestionJobUsinage.UpdateDateObject> updateDateObjects, out string message)
        {
            message = string.Empty;
            if(!updateDateObjects.Any())
            {
                throw new Exception("Aucune date à changer.");
            }
            this.svc = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobEntrySvcContract>(Db);
            ScheduleEngineController sec = new ScheduleEngineController(this.Db, this.Session);
            jobentrybo.Erp.Tablesets.JobHeadRow currentJH = null;
            try
            {
                foreach(GestionJobUsinage.UpdateDateObject updateDateObject in updateDateObjects)
                {
                    this.ds = this.svc.GetDatasetForTree(updateDateObject.JobNum, 0, 0, false, "MFG,PRJ,SRV");
                    var jh = this.ds.JobHead.Where(tt => tt.JobNum == updateDateObject.JobNum).FirstOrDefault();
                    if(jh == null)
                    {
                        throw new Exception(string.Format("Le bon de travail #{0} est introuvable.", updateDateObject.JobNum));
                    }
                    currentJH = jh;
                    if(jh.ReqDueDate == updateDateObject.NewDate)
                    {
                        continue;
                    };
                    jh.ReqDueDate = updateDateObject.NewDate;
                    string vMessage;
                    this.svc.CheckToReschedule(jh.Company, jh.JobNum, jh.ReqDueDate, jh.ProdQty, jh.DueDate, jh.StartDate, jh.JobEngineered, out vMessage);
                    //this.svc.Update(ref this.ds);
                    string error;
                    if(!sec.ScheduleJob(jh, out error))
                    {
                        throw new Exception(error);
                    }
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
