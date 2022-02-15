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
    class JobManagerController : BaseService
    {
        private Erp.Tablesets.JobManagerTableset ds;
        private Erp.Contracts.JobManagerSvcContract svcJobManager;
        public JobManagerController(Erp.ErpContext db, Epicor.Hosting.Session session) : base(db, session) { }
        private void GetTablesetForPartNum(string partNum)
        {
            this.ds = this.svcJobManager.GetMatrix(partNum, string.Empty, string.Empty, string.Empty, string.Empty);
        }
        private void SetJobUnFirm(string jobNum)
        {
            JobHead jh = (from j in this.Db.JobHead
                          where j.Company == this.Session.CompanyID &&
                                j.JobNum == jobNum
                          select j).FirstOrDefault();
            if(jh == null)
            {
                return;
            }
            Erp.Internal.Lib.ValidatingTransactionScope txScope = new Erp.Internal.Lib.ValidatingTransactionScope(this.Db);
            try
            {
                ((IceDataContext)(this.Db)).DisableTriggers("JobHead", (TriggerType)0);
                jh.JobFirm = false;
                jh.JobEngineered = true;
                ((IceDataContext)(this.Db)).Validate();
                ((IceDataContext)(this.Db)).EnableTriggers("JobHead", (TriggerType)0);
                txScope.Complete();
            }
            finally
            {
                ((IDisposable)(object)txScope)?.Dispose();
            }
        }
        public bool CreateJobs(IEnumerable<IJobManagerList> jobManagerLists, out List<string> jobNums, out string message, bool setUnfirm = true)
        {
            message = string.Empty;
            jobNums = new List<string>();
            if (!jobManagerLists.Any())
            {
                return false;
            }
            this.svcJobManager = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobManagerSvcContract>(Db);
            Erp.Contracts.JobEntrySvcContract svcJobEntry = Ice.Assemblies.ServiceRenderer.GetService<Erp.Contracts.JobEntrySvcContract>(Db);
            try
            {
                string currentPartNum = "";
                foreach (IJobManagerList jobManagerList in jobManagerLists)
                {
                    if (currentPartNum != jobManagerList.PartNum)
                    {
                        GetTablesetForPartNum(jobManagerList.PartNum);
                        currentPartNum = jobManagerList.PartNum;
                    }
                    int currentObject = 0;
                    string jobNum = "";
                    foreach (IJobManagerObject jobManagerObject in jobManagerList.Items)
                    {
                        if (currentObject == 0)
                        {
                            svcJobEntry.GetNextJobNum(out jobNum);
                            this.svcJobManager.CreateJob(jobManagerObject.RecKey, jobNum);
                        }
                        else
                        {
                            this.svcJobManager.LinkToJob(jobManagerObject.RecKey, jobNum);
                        }
                        currentObject++;
                    }
                    svcJobEntry.GetDetails(jobNum, 0, "Method", 0, 0, string.Empty, 0, jobManagerList.PartNum, jobManagerList.RevisionNum, string.Empty, true, false, false, false);
                    if (setUnfirm)
                    {
                        SetJobUnFirm(jobNum);
                    }
                    jobNums.Add(jobNum);
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
                svcJobEntry.Dispose();
                svcJobEntry = null;
                this.svcJobManager.Dispose();
                this.svcJobManager = null;
                this.ds = null;
            }
        }
    }
}
